using System;
using System.Collections.Generic;
using SysPulse.Models;

namespace SysPulse.Services;

public interface IScreenTimeTrackerService
{
    void TrackTick(double elapsedSeconds);
    TimeSpan TotalScreenTime { get; }
    IReadOnlyList<AppScreenTimeMetric> GetAppScreenTimes();
    void Reset();
    bool IsTrackingEnabled { get; set; }
}
