using System;

namespace SysPulse.Models;

public class PowerMetric
{
    public bool HasBattery { get; set; }
    public bool IsCharging { get; set; }
    public bool IsAcConnected { get; set; }
    public double BatteryPercent { get; set; } // 0 to 100
    public int EstimatedRemainingMinutes { get; set; } // -1 if unknown

    public double SafeBatteryPercent => Math.Clamp(BatteryPercent > 100.0 ? BatteryPercent / 100.0 : BatteryPercent, 0.0, 99.9);
    public string BatteryPercentFormatted => HasBattery ? $"{SafeBatteryPercent:0.0}%" : "No Battery (Desktop)";
    public string PowerStatusText => IsAcConnected
        ? (IsCharging ? "Charging (AC Connected)" : "Plugged in (Full/Maintaining)")
        : "On Battery Power";

    public string EstimatedTimeFormatted
    {
        get
        {
            if (!HasBattery) return "Continuous (AC)";
            if (IsAcConnected) return "Plugged in";
            if (EstimatedRemainingMinutes <= 0) return "Calculating...";
            int hours = EstimatedRemainingMinutes / 60;
            int mins = EstimatedRemainingMinutes % 60;
            return $"{hours}h {mins:D2}m remaining";
        }
    }

    public string BatteryIcon => !HasBattery ? "🔌" : (IsCharging ? "⚡" : (SafeBatteryPercent > 50 ? "🔋" : "🪫"));
}
