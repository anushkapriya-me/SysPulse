using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using SysPulse.Services;
using SysPulse.ViewModels;
using SysPulse.Views;

namespace SysPulse;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<ISystemTelemetryService, SystemTelemetryService>();
        collection.AddSingleton<IScreenTimeTrackerService, ScreenTimeTrackerService>();
        collection.AddSingleton<IAssetInventoryService, AssetInventoryService>();
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddTransient<MainWindowViewModel>();

        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var win = new MainWindow
            {
                DataContext = vm
            };

            // Set window icon if file exists on disk
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SysPulse.ico");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "SysPulse.ico");
                }
                if (File.Exists(iconPath))
                {
                    win.Icon = new WindowIcon(iconPath);
                }
            }
            catch {}

            desktop.MainWindow = win;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
