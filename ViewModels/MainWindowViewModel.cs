using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    private readonly DispatcherTimer _timer;

    private readonly List<double> _cpuBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _ramBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _downloadBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));
    private readonly List<double> _uploadBuffer = new(Enumerable.Repeat(0.0, HistoryCapacity));

    private List<ProcessMetric> _allProcesses = [];

    [ObservableProperty]
    private HardwareSnapshot _snapshot = new();

    [ObservableProperty]
    private int _selectedNavIndex = 0;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProcessMetric? _selectedProcess;

    [ObservableProperty]
    private string _statusMessage = "Monitoring active";

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

    public ObservableCollection<ProcessMetric> FilteredProcesses { get; } = [];

    public MainWindowViewModel(ISystemTelemetryService telemetryService)
    {
        _telemetryService = telemetryService;

        _cpuHistory = _cpuBuffer.ToList();
        _ramHistory = _ramBuffer.ToList();
        _downloadHistory = _downloadBuffer.ToList();
        _uploadHistory = _uploadBuffer.ToList();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += async (_, _) => await OnTickAsync();
        _timer.Start();

        // Initial fetch
        _ = OnTickAsync();
    }

    // Default parameterless constructor for design-time preview
    public MainWindowViewModel() : this(new SystemTelemetryService())
    {
    }

    public bool HasSelectedProcess => SelectedProcess != null;

    partial void OnSelectedProcessChanged(ProcessMetric? value)
    {
        OnPropertyChanged(nameof(HasSelectedProcess));
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyProcessFilter();
    }

    [RelayCommand]
    private void Navigate(string indexString)
    {
        if (int.TryParse(indexString, out var index))
        {
            SelectedNavIndex = index;
        }
    }

    [RelayCommand]
    private async Task KillSelectedProcessAsync()
    {
        if (SelectedProcess == null)
            return;

        var proc = SelectedProcess;
        var success = _telemetryService.KillProcess(proc.Id);
        if (success)
        {
            StatusMessage = $"Terminated process '{proc.Name}' (PID {proc.Id})";
            await RefreshProcessesAsync();
        }
        else
        {
            StatusMessage = $"Failed to terminate process '{proc.Name}' (Access Denied)";
        }
    }

    private async Task OnTickAsync()
    {
        try
        {
            var newSnapshot = await _telemetryService.CaptureSnapshotAsync();
            Snapshot = newSnapshot;

            PushToBuffer(_cpuBuffer, newSnapshot.CpuUsagePercent);
            PushToBuffer(_ramBuffer, newSnapshot.RamUsagePercent);
            PushToBuffer(_downloadBuffer, newSnapshot.DownloadSpeedBytesPerSec);
            PushToBuffer(_uploadBuffer, newSnapshot.UploadSpeedBytesPerSec);

            var peakNet = Math.Max(
                _downloadBuffer.Max(),
                _uploadBuffer.Max());
            if (peakNet > MaxNetworkSpeedRecorded)
            {
                MaxNetworkSpeedRecorded = peakNet * 1.2;
            }

            CpuHistory = _cpuBuffer.ToList();
            RamHistory = _ramBuffer.ToList();
            DownloadHistory = _downloadBuffer.ToList();
            UploadHistory = _uploadBuffer.ToList();

            // Only refresh full process list if Processes tab is active or every 5 seconds
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

    [RelayCommand]
    public async Task RefreshProcessesAsync()
    {
        _allProcesses = await _telemetryService.GetProcessesAsync();
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        var filter = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Id.ToString().Contains(filter));

        FilteredProcesses.Clear();
        foreach (var item in filtered)
        {
            FilteredProcesses.Add(item);
        }
    }

    private static void PushToBuffer(List<double> buffer, double value)
    {
        if (buffer.Count >= HistoryCapacity)
        {
            buffer.RemoveAt(0);
        }
        buffer.Add(value);
    }
}
