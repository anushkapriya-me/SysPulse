using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysPulse.Models;
using SysPulse.Services;

namespace SysPulse.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int HistoryCapacity = 60;
    private readonly ISystemTelemetryService _telemetryService;
    private readonly IScreenTimeTrackerService _screenTimeService;
    private readonly IAssetInventoryService _assetService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _timer;

    private readonly List<double> _cpuBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _ramBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _downloadBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _uploadBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));

    private List<ProcessMetric> _allProcesses = [];

    [ObservableProperty]
    private HardwareSnapshot _snapshot = new();

    [ObservableProperty]
    private AppSettings _settings;

    [ObservableProperty]
    private int _selectedNavIndex = 0;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProcessMetric? _selectedProcess;

    [ObservableProperty]
    private string _statusMessage = "Monitoring active";

    // Refresh Intervals: 1s, 2s, 5s, 10s
    public ObservableCollection<RefreshIntervalOption> RefreshIntervals { get; } = [
        new(1, "🟢 1s (Live)"),
        new(2, "🟡 2s (Normal)"),
        new(5, "🟠 5s (Balanced)"),
        new(10, "🔵 10s (Eco Mode)")
    ];

    [ObservableProperty]
    private RefreshIntervalOption _selectedRefreshInterval;

    // Charts history
    [ObservableProperty]
    private IReadOnlyList<double> _cpuHistory = Array.Empty<double>();

    [ObservableProperty]
    private IReadOnlyList<double> _ramHistory = Array.Empty<double>();

    [ObservableProperty]
    private IReadOnlyList<double> _downloadHistory = Array.Empty<double>();

    [ObservableProperty]
    private IReadOnlyList<double> _uploadHistory = Array.Empty<double>();

    [ObservableProperty]
    private double _maxNetworkSpeedRecorded = 1024 * 1024; // 1 MB/s scale baseline

    // Screen Time
    [ObservableProperty]
    private string _totalScreenTimeFormatted = "0s";

    [ObservableProperty]
    private string _topScreenTimeApp = "None";

    [ObservableProperty]
    private bool _isScreenTimeTrackingActive = true;

    // Asset Inventory
    [ObservableProperty]
    private int _installedIdesCount = 0;

    [ObservableProperty]
    private int _runningIdesCount = 0;

    [ObservableProperty]
    private int _openProjectsCount = 0;

    // Alerts
    [ObservableProperty]
    private bool _isAlertVisible = false;

    [ObservableProperty]
    private string _alertMessage = string.Empty;

    public ObservableCollection<ProcessMetric> FilteredProcesses { get; } = [];
    public ObservableCollection<AppScreenTimeMetric> ScreenTimes { get; } = [];
    public ObservableCollection<IdeAsset> DetectedIdes { get; } = [];

    public bool HasSelectedProcess => SelectedProcess != null;

    public MainWindowViewModel(
        ISystemTelemetryService telemetryService,
        IScreenTimeTrackerService screenTimeService,
        IAssetInventoryService assetService,
        ISettingsService settingsService)
    {
        _telemetryService = telemetryService;
        _screenTimeService = screenTimeService;
        _assetService = assetService;
        _settingsService = settingsService;

        Settings = _settingsService.Settings;
        SelectedRefreshInterval = RefreshIntervals.FirstOrDefault(r => r.Seconds == Settings.RefreshIntervalSeconds) ?? RefreshIntervals[0];

        _cpuHistory = _cpuBuffer.ToList();
        _ramHistory = _ramBuffer.ToList();
        _downloadHistory = _downloadBuffer.ToList();
        _uploadHistory = _uploadBuffer.ToList();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_selectedRefreshInterval.Seconds)
        };
        _timer.Tick += async (_, _) => await OnTickAsync();
        _timer.Start();

        // Initial fetches
        _ = OnTickAsync();
        _ = RefreshAssetInventoryAsync();
    }

    // Default constructor for design-time and fallback
    public MainWindowViewModel() : this(
        new SystemTelemetryService(),
        new ScreenTimeTrackerService(),
        new AssetInventoryService(),
        new SettingsService())
    {
    }

    partial void OnSelectedProcessChanged(ProcessMetric? value)
    {
        OnPropertyChanged(nameof(HasSelectedProcess));
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyProcessFilter();
    }

    partial void OnSelectedRefreshIntervalChanged(RefreshIntervalOption value)
    {
        if (value != null && _timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(value.Seconds);
            Settings.RefreshIntervalSeconds = value.Seconds;
            _ = _settingsService.SaveSettingsAsync();
            StatusMessage = $"Refresh interval updated to {value.Label}";
        }
    }

    [RelayCommand]
    private void Navigate(string indexString)
    {
        if (int.TryParse(indexString, out var index))
        {
            SelectedNavIndex = index;

            if (index == 4)
            {
                _ = RefreshProcessesAsync();
            }
            else if (index == 5)
            {
                RefreshScreenTimeDisplay();
            }
            else if (index == 6)
            {
                _ = RefreshAssetInventoryAsync();
            }
        }
    }

    [RelayCommand]
    private async Task KillSelectedProcessAsync()
    {
        if (SelectedProcess == null)
            return;

        var proc = SelectedProcess;
        int pid = proc.Id;
        string name = proc.Name;

        StatusMessage = $"Terminating '{name}' (PID {pid})...";
        var success = _telemetryService.KillProcess(pid);
        if (success)
        {
            StatusMessage = $"Terminated process '{name}' (PID {pid})";
            
            // Remove from list immediately
            var existing = FilteredProcesses.FirstOrDefault(p => p.Id == pid);
            if (existing != null)
            {
                FilteredProcesses.Remove(existing);
            }
            SelectedProcess = null;

            await RefreshProcessesAsync();
        }
        else
        {
            StatusMessage = $"Failed to terminate '{name}' (PID {pid}) - Access Denied or System Protected";
        }
    }

    [RelayCommand]
    public async Task RefreshProcessesAsync()
    {
        _allProcesses = await _telemetryService.GetProcessesAsync();
        ApplyProcessFilter();
    }

    [RelayCommand]
    public async Task RefreshAssetInventoryAsync()
    {
        try
        {
            var ides = await _assetService.DiscoverInstalledIdesAsync();
            DetectedIdes.Clear();
            int totalProjects = 0;
            int runningCount = 0;
            int installedCount = 0;

            foreach (var ide in ides)
            {
                DetectedIdes.Add(ide);
                if (ide.IsInstalled) installedCount++;
                if (ide.IsRunning) runningCount++;
                totalProjects += ide.ProjectCount;
            }

            InstalledIdesCount = installedCount;
            RunningIdesCount = runningCount;
            OpenProjectsCount = totalProjects;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Asset scan error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LaunchIde(string? exePath)
    {
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
                StatusMessage = $"Launched {System.IO.Path.GetFileName(exePath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not launch IDE: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ResetScreenTime()
    {
        _screenTimeService.Reset();
        RefreshScreenTimeDisplay();
        StatusMessage = "Screen time statistics reset";
    }

    [RelayCommand]
    private void ToggleScreenTimeTracking()
    {
        _screenTimeService.IsTrackingEnabled = !_screenTimeService.IsTrackingEnabled;
        IsScreenTimeTrackingActive = _screenTimeService.IsTrackingEnabled;
        Settings.EnableScreenTimeTracking = IsScreenTimeTrackingActive;
        _ = _settingsService.SaveSettingsAsync();
        StatusMessage = IsScreenTimeTrackingActive ? "Screen time tracking resumed" : "Screen time tracking paused";
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _settingsService.ApplyStartupPreference(Settings.StartWithWindows);
        await _settingsService.SaveSettingsAsync();
        StatusMessage = "Settings saved successfully";
    }

    [RelayCommand]
    private void DismissAlert()
    {
        IsAlertVisible = false;
    }

    private async Task OnTickAsync()
    {
        try
        {
            double elapsed = SelectedRefreshInterval?.Seconds ?? 1.0;

            // 1. Track screen time tick
            _screenTimeService.TrackTick(elapsed);

            // 2. Fetch hardware telemetry
            var newSnapshot = await _telemetryService.CaptureSnapshotAsync();
            Snapshot = newSnapshot;

            // 3. Push to history buffers (strictly under 100%)
            PushToBuffer(_cpuBuffer, newSnapshot.CpuUsagePercent);
            PushToBuffer(_ramBuffer, newSnapshot.RamUsagePercent);
            PushToBuffer(_downloadBuffer, newSnapshot.DownloadSpeedBytesPerSec);
            PushToBuffer(_uploadBuffer, newSnapshot.UploadSpeedBytesPerSec);

            var peakNet = Math.Max(_downloadBuffer.Max(), _uploadBuffer.Max());
            if (peakNet > MaxNetworkSpeedRecorded)
            {
                MaxNetworkSpeedRecorded = peakNet * 1.2;
            }

            CpuHistory = _cpuBuffer.ToList();
            RamHistory = _ramBuffer.ToList();
            DownloadHistory = _downloadBuffer.ToList();
            UploadHistory = _uploadBuffer.ToList();

            // 4. Check Alerts
            CheckThresholdAlerts(newSnapshot);

            // 5. Update Screen Time tab if active
            if (SelectedNavIndex == 5)
            {
                RefreshScreenTimeDisplay();
            }

            // 6. Refresh processes if Processes tab is active or initially empty
            if (SelectedNavIndex == 4 || _allProcesses.Count == 0)
            {
                await RefreshProcessesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Telemetry error: {ex.Message}";
        }
    }

    private void CheckThresholdAlerts(HardwareSnapshot snap)
    {
        if (!Settings.EnableAlerts)
        {
            IsAlertVisible = false;
            return;
        }

        if (snap.CpuUsagePercent >= Settings.CpuAlertThreshold)
        {
            AlertMessage = $"⚠️ High CPU Warning: Processor usage is at {snap.CpuFormatted} (exceeds {Settings.CpuAlertThreshold:0}% threshold)";
            IsAlertVisible = true;
        }
        else if (snap.RamUsagePercent >= Settings.RamAlertThreshold)
        {
            AlertMessage = $"⚠️ High Memory Warning: RAM allocation is at {snap.RamPercentageFormatted} (exceeds {Settings.RamAlertThreshold:0}% threshold)";
            IsAlertVisible = true;
        }
        else
        {
            IsAlertVisible = false;
        }
    }

    private void RefreshScreenTimeDisplay()
    {
        var total = _screenTimeService.TotalScreenTime;
        if (total.TotalHours >= 1)
            TotalScreenTimeFormatted = $"{(int)total.TotalHours}h {total.Minutes:D2}m {total.Seconds:D2}s";
        else if (total.TotalMinutes >= 1)
            TotalScreenTimeFormatted = $"{total.Minutes}m {total.Seconds:D2}s";
        else
            TotalScreenTimeFormatted = $"{total.Seconds}s";

        var times = _screenTimeService.GetAppScreenTimes();
        ScreenTimes.Clear();
        foreach (var item in times)
        {
            ScreenTimes.Add(item);
        }

        TopScreenTimeApp = times.Count > 0 ? $"{times[0].IconEmoji} {times[0].AppName} ({times[0].PercentageFormatted})" : "None";
        IsScreenTimeTrackingActive = _screenTimeService.IsTrackingEnabled;
    }

    private void ApplyProcessFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Id.ToString().Contains(filter)).ToList();

        // BUG FIX: Reconcile in-place so selection is preserved!
        int? previousSelectedPid = SelectedProcess?.Id;
        var newDict = filtered.ToDictionary(p => p.Id);

        // Update existing items in FilteredProcesses
        for (int i = FilteredProcesses.Count - 1; i >= 0; i--)
        {
            var item = FilteredProcesses[i];
            if (newDict.TryGetValue(item.Id, out var updated))
            {
                item.WorkingSetBytes = updated.WorkingSetBytes;
                item.CpuPercent = updated.CpuPercent;
                item.Responding = updated.Responding;
                newDict.Remove(item.Id);
            }
            else
            {
                FilteredProcesses.RemoveAt(i);
            }
        }

        // Add newly appeared processes
        foreach (var remaining in newDict.Values)
        {
            FilteredProcesses.Add(remaining);
        }

        // Re-affirm selected process so it never gets cleared!
        if (previousSelectedPid.HasValue)
        {
            var matching = FilteredProcesses.FirstOrDefault(p => p.Id == previousSelectedPid.Value);
            if (matching != null && SelectedProcess != matching)
            {
                SelectedProcess = matching;
            }
        }
    }

    private static void PushToBuffer(List<double> buffer, double value)
    {
        if (buffer.Count >= HistoryCapacity)
        {
            buffer.RemoveAt(0);
        }
        buffer.Add(Math.Clamp(value > 100.0 ? value / 100.0 : value, 0.0, 99.9));
    }
}

public record RefreshIntervalOption(int Seconds, string Label)
{
    public override string ToString() => Label;
}
