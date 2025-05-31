using FishChamp.Data.Models;
using FishChamp.Data.Repositories;

namespace FishChamp.Services;

public interface IAreaUnlockService
{
    Task<List<string>> CheckAndUnlockAreasAsync(PlayerProfile player);
}

public class AreaUnlockService(IAreaRepository areaRepository, IPlayerRepository playerRepository) : IAreaUnlockService
{
    public async Task<List<string>> CheckAndUnlockAreasAsync(PlayerProfile player)
    {
        var unlockedAreas = new List<string>();
        var allAreas = await areaRepository.GetAllAreasAsync();

        foreach (var area in allAreas)
        {
            // Skip if already unlocked
            if (player.UnlockedAreas.Contains(area.AreaId))
                continue;

            // Check unlock requirements
            bool shouldUnlock = area.UnlockRequirement switch
            {
                "Catch 5 different fish species" => player.BiggestCatch.Count >= 5,
                _ => false // Default to locked for unknown requirements
            };

            if (shouldUnlock)
            {
                player.UnlockedAreas.Add(area.AreaId);
                unlockedAreas.Add(area.Name);
            }
        }

        if (unlockedAreas.Count > 0)
        {
            await playerRepository.UpdatePlayerAsync(player);
        }

        return unlockedAreas;
    }
}