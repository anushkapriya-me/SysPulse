using System;

namespace SysPulse.Models;

public class SystemSpecsMetric
{
    public string ProcessorName { get; set; } = "Generic Processor";
    public string Architecture { get; set; } = Environment.Is64BitOperatingSystem ? "64-bit (x64)" : "32-bit (x86)";
    public int PhysicalCores { get; set; } = Environment.ProcessorCount / 2;
    public int LogicalCores { get; set; } = Environment.ProcessorCount;
    public string MotherboardManufacturer { get; set; } = "Unknown";
    public string MotherboardModel { get; set; } = "Unknown";
    public string BiosVersion { get; set; } = "Unknown";
    public string WindowsEdition { get; set; } = "Windows";
    public string WindowsBuild { get; set; } = Environment.OSVersion.Version.ToString();
    public string MachineName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
}
