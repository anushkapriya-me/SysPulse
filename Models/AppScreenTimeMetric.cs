using System;

namespace SysPulse.Models;

public class AppScreenTimeMetric
{
    public string AppName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Development, Web Browsing, Productivity, Media, System
    public TimeSpan Duration { get; set; }
    public double PercentageOfTotal { get; set; } // 0 to 100
    public DateTime LastActiveTime { get; set; } = DateTime.Now;
    public string IconEmoji { get; set; } = "💻";

    // Strictly under 100%
    public double SafePercentage => Math.Clamp(PercentageOfTotal > 100.0 ? PercentageOfTotal / 100.0 : PercentageOfTotal, 0.0, 99.9);
    public string PercentageFormatted => $"{SafePercentage:0.1}%";

    public string FormattedDuration => FormatDuration(Duration);

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }
}
