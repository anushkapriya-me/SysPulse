using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using SysPulse.Models;

namespace SysPulse.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "SysPulse");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch
        {
            // Fallback to default settings
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch
        {
            // Non-critical write failure
        }
    }

    public void ApplyStartupPreference(bool startWithWindows)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(appPath)) return;

            if (startWithWindows)
            {
                key.SetValue("SysPulse", $"\"{appPath}\"");
            }
            else
            {
                key.DeleteValue("SysPulse", false);
            }
        }
        catch {}
    }
}
