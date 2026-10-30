using System.Collections.Generic;

namespace SysPulse.Models;

public class IdeAsset
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public int? ProcessId { get; set; }
    public string StatusText => IsRunning ? $"Running (PID {ProcessId})" : (IsInstalled ? "Installed (Idle)" : "Not Detected");
    public string StatusBadgeColor => IsRunning ? "#3FB950" : (IsInstalled ? "#58A6FF" : "#6E7681");
    public string IconEmoji { get; set; } = "💻";
    public List<IdeWorkspaceProject> OpenProjects { get; set; } = [];

    public int ProjectCount => OpenProjects.Count;
    public string ProjectCountText => $"{ProjectCount} Project{(ProjectCount == 1 ? "" : "s")}";
}

public class IdeWorkspaceProject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string LastOpenedText { get; set; } = "Active";
    public bool IsActiveNow { get; set; }
    public string StatusBadgeColor => IsActiveNow ? "#3FB950" : "#8B949E";
}
