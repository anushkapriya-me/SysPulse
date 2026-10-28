using System.Collections.Generic;
using System.Threading.Tasks;
using SysPulse.Models;

namespace SysPulse.Services;

public interface ISystemTelemetryService
{
    Task<HardwareSnapshot> CaptureSnapshotAsync();
    Task<List<ProcessMetric>> GetProcessesAsync();
    bool KillProcess(int processId);
}
