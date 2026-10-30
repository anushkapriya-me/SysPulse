using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SysPulse.Models;

public partial class ProcessMetric : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    private long _workingSetBytes;

    [ObservableProperty]
    private double _cpuPercent;

    [ObservableProperty]
    private bool _responding = true;

    // Ensure CPU is strictly under 100%
    public double SafeCpuPercent => Math.Clamp(CpuPercent > 100.0 ? CpuPercent / 100.0 : CpuPercent, 0.0, 99.9);

    public string MemoryFormatted => FormatBytes(WorkingSetBytes);
    public string CpuFormatted => $"{SafeCpuPercent:0.1}%";
    public string StatusText => Responding ? "Running" : "Not Responding";

    partial void OnWorkingSetBytesChanged(long value)
    {
        OnPropertyChanged(nameof(MemoryFormatted));
    }

    partial void OnCpuPercentChanged(double value)
    {
        OnPropertyChanged(nameof(SafeCpuPercent));
        OnPropertyChanged(nameof(CpuFormatted));
    }

    partial void OnRespondingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
