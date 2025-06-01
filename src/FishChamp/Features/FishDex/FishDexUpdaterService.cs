using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Events;
using FishChamp.Helpers;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Features.FishDex;

public class FishDexUpdaterService(IEventBus eventBus, 
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository) : IHostedService, IEventHandler<OnFishCatchEvent>
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<OnFishCatchEvent>(this);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        eventBus.Unsubscribe<OnFishCatchEvent>(this);

        await Task.CompletedTask;
    }

    public async Task HandleAsync(OnFishCatchEvent eventData, CancellationToken cancellationToken = default)
    {
        var player = await GetOrCreatePlayerAsync(eventData.UserId, eventData.Username);

        int fishSize = eventData.FishItem.Properties.GetInt("size");
        string fishRarity = eventData.FishItem.Properties.GetString("rarity", "common");

        // Update fish dex with new discovery
        var fishTraits = (FishTrait)eventData.FishItem.Properties.GetValueOrDefault("traits", 0);
        if (player.FishDex.TryGetValue(eventData.FishItem.ItemId, out var existingDiscovery))
        {
            // Update existing discovery record
            existingDiscovery.TimesDiscovered++;
            existingDiscovery.LastDiscovered = DateTime.UtcNow;
            if (eventData.FishWeight > existingDiscovery.HeaviestWeight)
            {
                existingDiscovery.HeaviestWeight = eventData.FishWeight;
            }
            if (fishSize > existingDiscovery.LargestSize)
            {
                existingDiscovery.LargestSize = fishSize;
            }
            existingDiscovery.ObservedTraits |= fishTraits; // Add any new traits observed
        }
        else
        {
            // First time catching this fish species
            player.FishDex[eventData.FishItem.ItemId] = new FishDiscoveryRecord
            {
                FishName = eventData.FishItem.Name,
                Rarity = fishRarity,
                TimesDiscovered = 1,
                FirstDiscovered = DateTime.UtcNow,
                LastDiscovered = DateTime.UtcNow,
                HeaviestWeight = eventData.FishWeight,
                LargestSize = fishSize,
                ObservedTraits = fishTraits
            };
        }

        await Task.CompletedTask;
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await playerRepository.CreatePlayerAsync(userId, username);
            await inventoryRepository.CreateInventoryAsync(userId);
        }
        return player;
    }
}
