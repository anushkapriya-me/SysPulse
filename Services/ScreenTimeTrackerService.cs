using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SysPulse.Models;

namespace SysPulse.Services;

public class ScreenTimeTrackerService : IScreenTimeTrackerService
{
    private readonly ConcurrentDictionary<string, (string DisplayName, string Category, string Emoji, double Seconds, DateTime LastActive)> _appTimes = new(StringComparer.OrdinalIgnoreCase);
    private double _totalSecondsTracked = 0;

    public bool IsTrackingEnabled { get; set; } = true;

    public TimeSpan TotalScreenTime => TimeSpan.FromSeconds(_totalSecondsTracked);

    public void TrackTick(double elapsedSeconds)
    {
        if (!IsTrackingEnabled || elapsedSeconds <= 0) return;

        // Check if user is active (not away/idle for > 5 mins)
        if (IsUserIdle(300)) return;

        var activeProc = GetActiveForegroundProcess();
        if (string.IsNullOrWhiteSpace(activeProc)) return;

        _totalSecondsTracked += elapsedSeconds;

        _appTimes.AddOrUpdate(activeProc,
            // Add new
            key =>
            {
                var (disp, cat, emoji) = CategorizeProcess(key);
                return (disp, cat, emoji, elapsedSeconds, DateTime.Now);
            },
            // Update existing
            (key, prev) => (prev.DisplayName, prev.Category, prev.Emoji, prev.Seconds + elapsedSeconds, DateTime.Now)
        );
    }

    public IReadOnlyList<AppScreenTimeMetric> GetAppScreenTimes()
    {
        var list = new List<AppScreenTimeMetric>();
        double total = Math.Max(1.0, _totalSecondsTracked);

        foreach (var (proc, info) in _appTimes)
        {
            double pct = (info.Seconds / total) * 100.0;
            if (pct > 100.0) pct = pct / 100.0;

            list.Add(new AppScreenTimeMetric
            {
                ProcessName = proc,
                AppName = info.DisplayName,
                Category = info.Category,
                IconEmoji = info.Emoji,
                Duration = TimeSpan.FromSeconds(info.Seconds),
                PercentageOfTotal = Math.Clamp(pct, 0.0, 99.9),
                LastActiveTime = info.LastActive
            });
        }

        return list.OrderByDescending(a => a.Duration).ToList();
    }

    public void Reset()
    {
        _appTimes.Clear();
        _totalSecondsTracked = 0;
    }

    private static (string DisplayName, string Category, string Emoji) CategorizeProcess(string processName)
    {
        var lower = processName.ToLowerInvariant();

        return lower switch
        {
            "code" => ("Visual Studio Code", "Development", "💻"),
            "cursor" => ("Cursor AI Editor", "Development", "⚡"),
            "devenv" => ("Visual Studio IDE", "Development", "🟣"),
            "zed" => ("Zed Editor", "Development", "⚡"),
            "idea64" or "idea" => ("IntelliJ IDEA", "Development", "☕"),
            "pycharm64" or "pycharm" => ("PyCharm", "Development", "🐍"),
            "rider64" or "rider" => ("JetBrains Rider", "Development", "🔷"),
            "webstorm64" or "webstorm" => ("WebStorm", "Development", "🌐"),
            "sublime_text" => ("Sublime Text", "Development", "📄"),
            "notepad++" => ("Notepad++", "Development", "📝"),
            "windowsterminal" or "wt" => ("Windows Terminal", "Development", "🖥️"),
            "powershell" or "pwsh" => ("PowerShell", "Development", "🐚"),
            "cmd" => ("Command Prompt", "Development", "⌨️"),

            "chrome" => ("Google Chrome", "Web Browsing", "🌐"),
            "msedge" => ("Microsoft Edge", "Web Browsing", "🌊"),
            "firefox" => ("Mozilla Firefox", "Web Browsing", "🦊"),
            "brave" => ("Brave Browser", "Web Browsing", "🦁"),
            "opera" => ("Opera", "Web Browsing", "🔴"),

            "slack" => ("Slack", "Communication", "💬"),
            "discord" => ("Discord", "Communication", "🎮"),
            "teams" or "ms-teams" => ("Microsoft Teams", "Communication", "👥"),
            "telegram" => ("Telegram", "Communication", "✈️"),
            "whatsapp" => ("WhatsApp", "Communication", "📱"),

            "spotify" => ("Spotify", "Media & Music", "🎵"),
            "vlc" => ("VLC Media Player", "Media & Video", "🎬"),
            "mpv" => ("MPV Player", "Media & Video", "🎥"),

            "explorer" => ("Windows Explorer", "File Management", "📁"),
            "taskmgr" => ("Task Manager", "System", "📊"),
            "syspulse" => ("SysPulse", "Telemetry", "⚡"),

            _ => (char.ToUpper(processName[0]) + (processName.Length > 1 ? processName[1..] : ""), "Productivity", "📌")
        };
    }

    private static string GetActiveForegroundProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return string.Empty;

        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return string.Empty;

            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0) return string.Empty;

            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsUserIdle(int maxIdleSeconds)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (GetLastInputInfo(ref lii))
            {
                uint idleTicks = (uint)Environment.TickCount - lii.dwTime;
                return (idleTicks / 1000) > maxIdleSeconds;
            }
        }
        catch {}
        return false;
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    #endregion
}
