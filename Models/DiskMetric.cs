using System;

namespace SysPulse.Models;

public class DiskMetric
{
    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    // Guaranteed strictly under 100%
    public double PercentUsed
    {
        get
        {
            if (TotalBytes <= 0) return 0.0;
            double pct = ((double)UsedBytes / TotalBytes) * 100.0;
            if (pct > 100.0) pct = pct / 100.0;
            return Math.Clamp(pct, 0.0, 99.9);
        }
    }

    public string TotalFormatted => FormatBytes(TotalBytes);
    public string FreeFormatted => FormatBytes(FreeBytes);
    public string UsedFormatted => FormatBytes(UsedBytes);
    public string DisplayTitle => string.IsNullOrWhiteSpace(VolumeLabel) ? Name : $"{VolumeLabel} ({Name})";

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
