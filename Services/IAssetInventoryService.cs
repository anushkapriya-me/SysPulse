using System.Collections.Generic;
using System.Threading.Tasks;
using SysPulse.Models;

namespace SysPulse.Services;

public interface IAssetInventoryService
{
    Task<List<IdeAsset>> DiscoverInstalledIdesAsync();
}
