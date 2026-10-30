using System;
using System.Collections.Generic;

namespace SysPulse.Models;

public class HardwareSnapshot
{
    private double _cpuUsagePercent;
    public double CpuUsagePercent
    {
        get => _cpuUsagePercent;
        set
        {
            // Divide by 100 if incoming value was > 100, and clamp strictly under 100%
            double val = value;
            if (val > 100.0) val = val / 100.0;
            _cpuUsagePercent = Math.Clamp(val, 0.0, 99.9);
        }
    }

    public long RamTotalBytes { get; set; }
    public long RamUsedBytes { get; set; }
    public long RamAvailableBytes => Math.Max(0, RamTotalBytes - RamUsedBytes);

    // Guaranteed strictly under 100%
    public double RamUsagePercent
    {
        get
        {
            if (RamTotalBytes <= 0) return 0.0;
            double pct = ((double)RamUsedBytes / RamTotalBytes) * 100.0;
            if (pct > 100.0) pct = pct / 100.0;
            return Math.Clamp(pct, 0.0, 99.9);
        }
    }

    public TimeSpan SystemUptime { get; set; }
    public int LogicalCoreCount { get; set; }
    public string MachineName { get; set; } = Environment.MachineName;
    public string OsDescription { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    public double DownloadSpeedBytesPerSec { get; set; }
    public double UploadSpeedBytesPerSec { get; set; }

    public List<DiskMetric> Disks { get; set; } = [];
    public List<ProcessMetric> TopProcesses { get; set; } = [];

    public PowerMetric Power { get; set; } = new();
    public SystemSpecsMetric Specs { get; set; } = new();

    public DiskMetric? PrimaryDisk => Disks.Count > 0 ? Disks[0] : null;
    public string PrimaryDiskUsedFormatted => PrimaryDisk?.UsedFormatted ?? "--";
    public string PrimaryDiskTotalFormatted => PrimaryDisk != null ? $"Total: {PrimaryDisk.TotalFormatted}" : "--";
    public double PrimaryDiskPercentUsed => PrimaryDisk?.PercentUsed ?? 0.0;

    public string CpuFormatted => $"{CpuUsagePercent:0.1}%";
    public string RamUsedFormatted => FormatBytes(RamUsedBytes);
    public string RamTotalFormatted => FormatBytes(RamTotalBytes);
    public string RamAvailableFormatted => FormatBytes(RamAvailableBytes);
    public string RamPercentageFormatted => $"{RamUsagePercent:0.1}%";
    public string UptimeFormatted => $"{(int)SystemUptime.TotalDays}d {SystemUptime.Hours:D2}h {SystemUptime.Minutes:D2}m {SystemUptime.Seconds:D2}s";
    public string DownloadSpeedFormatted => $"{FormatSpeed(DownloadSpeedBytesPerSec)}/s";
    public string UploadSpeedFormatted => $"{FormatSpeed(UploadSpeedBytesPerSec)}/s";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec < 1024)
            return $"{bytesPerSec:F0} B";
        if (bytesPerSec < 1024 * 1024)
            return $"{bytesPerSec / 1024.0:F1} KB";
        return $"{bytesPerSec / (1024.0 * 1024.0):F2} MB";
    }
}
