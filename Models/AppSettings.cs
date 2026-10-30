using CommunityToolkit.Mvvm.ComponentModel;

namespace SysPulse.Models;

public partial class AppSettings : ObservableObject
{
    // Appearance & Themes
    [ObservableProperty]
    private string _selectedTheme = "Midnight Dark"; // "Midnight Dark", "Cyber Blue", "OLED Black", "Light Clean"

    [ObservableProperty]
    private string _accentColor = "#58A6FF";

    // Refresh Rate
    [ObservableProperty]
    private int _refreshIntervalSeconds = 1; // 1, 2, 5, 10

    // Metric Visibility
    [ObservableProperty]
    private bool _showCpuMetrics = true;

    [ObservableProperty]
    private bool _showRamMetrics = true;

    [ObservableProperty]
    private bool _showDiskMetrics = true;

    [ObservableProperty]
    private bool _showNetworkMetrics = true;

    [ObservableProperty]
    private bool _showPowerMetrics = true;

    [ObservableProperty]
    private bool _showHardwareSpecs = true;

    // Dashboard Customization
    [ObservableProperty]
    private bool _showHistoryCharts = true;

    [ObservableProperty]
    private bool _showStorageCard = true;

    [ObservableProperty]
    private bool _showNetworkCard = true;

    // Startup & System Tray
    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private bool _startMinimized = false;

    // Notifications & Alert Thresholds
    [ObservableProperty]
    private bool _enableAlerts = true;

    [ObservableProperty]
    private double _cpuAlertThreshold = 85.0; // %

    [ObservableProperty]
    private double _ramAlertThreshold = 90.0; // %

    // Data & Privacy
    [ObservableProperty]
    private bool _enableScreenTimeTracking = true;

    [ObservableProperty]
    private int _historyRetentionSeconds = 60; // 30, 60, 120
}
