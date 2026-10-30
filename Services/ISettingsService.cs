using System.Threading.Tasks;
using SysPulse.Models;

namespace SysPulse.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    void ApplyStartupPreference(bool startWithWindows);
}
