namespace SysPulse.Models;

public class ProcessMetric
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long WorkingSetBytes { get; set; }
    public double CpuPercent { get; set; }
    public bool Responding { get; set; } = true;

    public string MemoryFormatted => FormatBytes(WorkingSetBytes);
    public string CpuFormatted => $"{CpuPercent:0.1}%";
    public string StatusText => Responding ? "Running" : "Not Responding";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
