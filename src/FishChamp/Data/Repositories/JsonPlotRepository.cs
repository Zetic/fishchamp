using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonPlotRepository : IPlotRepository
{
    private readonly IAreaRepository _areaRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonPlotRepository(IAreaRepository areaRepository, IPlayerRepository playerRepository)
    {
        _areaRepository = areaRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Plot?> GetPlotAsync(string areaId, string plotId)
    {
        var area = await _areaRepository.GetAreaAsync(areaId);
        return area?.AvailablePlots.FirstOrDefault(p => p.PlotId == plotId);
    }

    public async Task<List<Plot>> GetAreaPlotsAsync(string areaId)
    {
        var area = await _areaRepository.GetAreaAsync(areaId);
        return area?.AvailablePlots ?? new List<Plot>();
    }

    public async Task<List<OwnedPlot>> GetUserPlotsAsync(ulong userId)
    {
        var player = await _playerRepository.GetPlayerAsync(userId);
        return player?.OwnedPlots ?? new List<OwnedPlot>();
    }

    public async Task<bool> IsPlotAvailableAsync(string areaId, string plotId)
    {
        var plot = await GetPlotAsync(areaId, plotId);
        return plot != null && plot.OwnerId == null;
    }

    public async Task<bool> PurchasePlotAsync(ulong userId, string areaId, string plotId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var area = await _areaRepository.GetAreaAsync(areaId);
            if (area == null) return false;

            var plot = area.AvailablePlots.FirstOrDefault(p => p.PlotId == plotId);
            if (plot == null || plot.OwnerId != null) return false;

            var player = await _playerRepository.GetPlayerAsync(userId);
            if (player == null || player.FishCoins < plot.Price) return false;

            // Deduct coins from player
            player.FishCoins -= plot.Price;

            // Mark plot as owned in area
            plot.OwnerId = userId;
            plot.PurchasedAt = DateTime.UtcNow;

            // Add to player's owned plots
            var ownedPlot = new OwnedPlot
            {
                PlotId = plot.PlotId,
                AreaId = area.AreaId,
                Name = plot.Name,
                PurchasedAt = DateTime.UtcNow,
                Size = plot.Size,
                Properties = new Dictionary<string, object>(plot.Properties)
            };
            player.OwnedPlots.Add(ownedPlot);

            // Save changes
            await _areaRepository.UpdateAreaAsync(area);
            await _playerRepository.UpdatePlayerAsync(player);

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}