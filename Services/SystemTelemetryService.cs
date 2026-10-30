using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using SysPulse.Models;

namespace SysPulse.Services;

public class SystemTelemetryService : ISystemTelemetryService
{
    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;
    private bool _hasPreviousCpuTimes;

    private long _prevBytesReceived;
    private long _prevBytesSent;
    private DateTime _prevNetworkTime = DateTime.UtcNow;

    private readonly Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _processCpuHistory = new();
    private SystemSpecsMetric? _cachedSpecs;

    public SystemTelemetryService()
    {
        InitializeMetrics();
    }

    private void InitializeMetrics()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                _prevIdleTime = ToUInt64(idle);
                _prevKernelTime = ToUInt64(kernel);
                _prevUserTime = ToUInt64(user);
                _hasPreviousCpuTimes = true;
            }
        }

        var (rx, tx) = GetTotalNetworkBytes();
        _prevBytesReceived = rx;
        _prevBytesSent = tx;
        _prevNetworkTime = DateTime.UtcNow;
    }

    public Task<HardwareSnapshot> CaptureSnapshotAsync()
    {
        return Task.Run(() =>
        {
            var snapshot = new HardwareSnapshot
            {
                LogicalCoreCount = Environment.ProcessorCount,
                SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                CpuUsagePercent = CalculateCpuUsage(),
                Disks = GetDiskMetrics(),
                Power = GetPowerMetrics(),
                Specs = GetSystemSpecs()
            };

            var (totalRam, availRam) = GetMemoryMetrics();
            snapshot.RamTotalBytes = totalRam;
            snapshot.RamUsedBytes = Math.Max(0, totalRam - availRam);

            var (downSpeed, upSpeed) = CalculateNetworkSpeeds();
            snapshot.DownloadSpeedBytesPerSec = downSpeed;
            snapshot.UploadSpeedBytesPerSec = upSpeed;

            return snapshot;
        });
    }

    private double CalculateCpuUsage()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return 0.0;
        }

        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0.0;
        }

        var currentIdle = ToUInt64(idle);
        var currentKernel = ToUInt64(kernel);
        var currentUser = ToUInt64(user);

        if (!_hasPreviousCpuTimes)
        {
            _prevIdleTime = currentIdle;
            _prevKernelTime = currentKernel;
            _prevUserTime = currentUser;
            _hasPreviousCpuTimes = true;
            return 0.0;
        }

        // Detect unsigned wrap-around due to clock jitter
        if (currentIdle < _prevIdleTime || currentKernel < _prevKernelTime || currentUser < _prevUserTime)
        {
            _prevIdleTime = currentIdle;
            _prevKernelTime = currentKernel;
            _prevUserTime = currentUser;
            return 0.0;
        }

        var idleDiff = currentIdle - _prevIdleTime;
        var kernelDiff = currentKernel - _prevKernelTime;
        var userDiff = currentUser - _prevUserTime;

        _prevIdleTime = currentIdle;
        _prevKernelTime = currentKernel;
        _prevUserTime = currentUser;

        var totalDiff = kernelDiff + userDiff;
        if (totalDiff == 0) return 0.0;

        double cpu = (1.0 - ((double)idleDiff / totalDiff)) * 100.0;
        if (cpu > 100.0) cpu = cpu / 100.0;
        return Math.Clamp(cpu, 0.0, 99.9);
    }

    private static (long Total, long Available) GetMemoryMetrics()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return ((long)memStatus.ullTotalPhys, (long)memStatus.ullAvailPhys);
            }
        }

        var total = 16L * 1024 * 1024 * 1024;
        var used = GC.GetTotalMemory(false) * 20;
        return (total, Math.Max(0, total - used));
    }

    private PowerMetric GetPowerMetrics()
    {
        var metric = new PowerMetric();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (GetSystemPowerStatus(out var status))
            {
                metric.IsAcConnected = status.ACLineStatus == 1;
                // BatteryLifePercent == 255 means no system battery
                metric.HasBattery = status.BatteryLifePercent != 255 && (status.BatteryFlag & 128) == 0;
                metric.IsCharging = (status.BatteryFlag & 8) != 0;
                double pct = status.BatteryLifePercent;
                if (pct > 100.0) pct = pct / 100.0;
                metric.BatteryPercent = Math.Clamp(pct, 0.0, 99.9);
                metric.EstimatedRemainingMinutes = status.BatteryLifeTime > 0 ? status.BatteryLifeTime / 60 : -1;
            }
        }
        return metric;
    }

    private SystemSpecsMetric GetSystemSpecs()
    {
        if (_cachedSpecs != null) return _cachedSpecs;

        var specs = new SystemSpecsMetric
        {
            LogicalCores = Environment.ProcessorCount,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            WindowsEdition = RuntimeInformation.OSDescription
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (cpuKey != null)
                {
                    var name = cpuKey.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        specs.ProcessorName = name.Trim();
                    }
                }

                using var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (biosKey != null)
                {
                    specs.MotherboardManufacturer = (biosKey.GetValue("SystemManufacturer") as string) ?? "Unknown";
                    specs.MotherboardModel = (biosKey.GetValue("SystemProductName") as string) ?? 
                                            (biosKey.GetValue("BaseBoardProduct") as string) ?? "Generic";
                    specs.BiosVersion = (biosKey.GetValue("BIOSVersion") as string) ?? "Unknown";
                }
            }
            catch
            {
                // Non-critical fallback
            }
        }

        _cachedSpecs = specs;
        return specs;
    }

    private (double DownSpeed, double UpSpeed) CalculateNetworkSpeeds()
    {
        var now = DateTime.UtcNow;
        var elapsedSec = (now - _prevNetworkTime).TotalSeconds;
        if (elapsedSec <= 0.001) elapsedSec = 1.0;

        var (currRx, currTx) = GetTotalNetworkBytes();

        var rxDiff = Math.Max(0, currRx - _prevBytesReceived);
        var txDiff = Math.Max(0, currTx - _prevBytesSent);

        _prevBytesReceived = currRx;
        _prevBytesSent = currTx;
        _prevNetworkTime = now;

        return (rxDiff / elapsedSec, txDiff / elapsedSec);
    }

    private static (long Rx, long Tx) GetTotalNetworkBytes()
    {
        long rx = 0;
        long tx = 0;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var stats = ni.GetIPv4Statistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }
        }
        catch
        {
            // Ignore network interface access errors
        }

        return (rx, tx);
    }

    private static List<DiskMetric> GetDiskMetrics()
    {
        var list = new List<DiskMetric>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                list.Add(new DiskMetric
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    FileSystem = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace
                });
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }
        return list;
    }

    public Task<List<ProcessMetric>> GetProcessesAsync()
    {
        return Task.Run(() =>
        {
            var result = new List<ProcessMetric>();
            var now = DateTime.UtcNow;
            var coreCount = Math.Max(1, Environment.ProcessorCount);

            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    var id = proc.Id;
                    var name = proc.ProcessName;
                    var mem = proc.WorkingSet64;
                    var responding = proc.Responding;
                    double cpuPercent = 0.0;

                    try
                    {
                        var totalCpu = proc.TotalProcessorTime;
                        if (_processCpuHistory.TryGetValue(id, out var prev))
                        {
                            var timeDelta = (now - prev.Timestamp).TotalSeconds;
                            var cpuDelta = (totalCpu - prev.CpuTime).TotalSeconds;
                            if (timeDelta > 0.05)
                            {
                                cpuPercent = (cpuDelta / (timeDelta * coreCount)) * 100.0;
                                if (cpuPercent > 100.0) cpuPercent = cpuPercent / 100.0;
                                cpuPercent = Math.Clamp(cpuPercent, 0.0, 99.9);
                            }
                        }
                        _processCpuHistory[id] = (totalCpu, now);
                    }
                    catch
                    {
                        // Some system processes deny access to CPU time
                    }

                    result.Add(new ProcessMetric
                    {
                        Id = id,
                        Name = name,
                        WorkingSetBytes = mem,
                        CpuPercent = cpuPercent,
                        Responding = responding
                    });
                }
                catch
                {
                    // Process may have exited between listing and accessing
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Cleanup history for dead processes
            var activeIds = result.Select(p => p.Id).ToHashSet();
            var deadIds = _processCpuHistory.Keys.Where(k => !activeIds.Contains(k)).ToList();
            foreach (var dead in deadIds)
            {
                _processCpuHistory.Remove(dead);
            }

            return result.OrderByDescending(p => p.WorkingSetBytes).ToList();
        });
    }

    public bool KillProcess(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            proc.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    private static ulong ToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((ulong)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    #endregion
}
