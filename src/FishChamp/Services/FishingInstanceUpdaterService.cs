using FishChamp.Data.Models;
using FishChamp.Interactions;
using FishChamp.Tracker;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Services;


public class FishingInstanceUpdaterService(IInstanceTracker<FishingInstance> fishingTracker) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000);

            foreach (var fishingInstance in fishingTracker.GetAll())
            {
                await fishingInstance.UpdateAsync();
            }
        }
    }
}
