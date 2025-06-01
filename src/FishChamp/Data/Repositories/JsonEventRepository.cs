using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonEventRepository : IEventRepository
{
    private readonly string _eventsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "events.json");
    private readonly string _participationsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "event_participations.json");
    private readonly string _worldBossesDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "world_bosses.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<SeasonalEvent?> GetEventAsync(string eventId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            return events.FirstOrDefault(e => e.EventId == eventId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<SeasonalEvent>> GetActiveEventsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            var now = DateTime.UtcNow;
            return events.Where(e => e.Status == EventStatus.Active && 
                                   e.StartDate <= now && 
                                   e.EndDate > now).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<SeasonalEvent>> GetUpcomingEventsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            var now = DateTime.UtcNow;
            return events.Where(e => e.Status == EventStatus.Upcoming && 
                                   e.StartDate > now).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<SeasonalEvent>> GetAllEventsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadEventsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SeasonalEvent> CreateEventAsync(SeasonalEvent seasonalEvent)
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            events.Add(seasonalEvent);
            await SaveEventsAsync(events);
            return seasonalEvent;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateEventAsync(SeasonalEvent seasonalEvent)
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            var existingEvent = events.FirstOrDefault(e => e.EventId == seasonalEvent.EventId);
            if (existingEvent != null)
            {
                events.Remove(existingEvent);
                events.Add(seasonalEvent);
                await SaveEventsAsync(events);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteEventAsync(string eventId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var events = await LoadEventsAsync();
            var seasonalEvent = events.FirstOrDefault(e => e.EventId == eventId);
            if (seasonalEvent != null)
            {
                events.Remove(seasonalEvent);
                await SaveEventsAsync(events);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<EventParticipation?> GetEventParticipationAsync(string eventId, ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participations = await LoadParticipationsAsync();
            return participations.FirstOrDefault(p => p.EventId == eventId && p.UserId == userId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<EventParticipation>> GetUserEventParticipationsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participations = await LoadParticipationsAsync();
            return participations.Where(p => p.UserId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<EventParticipation> CreateEventParticipationAsync(EventParticipation participation)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participations = await LoadParticipationsAsync();
            participations.Add(participation);
            await SaveParticipationsAsync(participations);
            return participation;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateEventParticipationAsync(EventParticipation participation)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participations = await LoadParticipationsAsync();
            var existingParticipation = participations.FirstOrDefault(p => p.ParticipationId == participation.ParticipationId);
            if (existingParticipation != null)
            {
                participations.Remove(existingParticipation);
                participations.Add(participation);
                await SaveParticipationsAsync(participations);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<SeasonalEvent>> LoadEventsAsync()
    {
        if (!File.Exists(_eventsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_eventsDataPath)!);
            return new List<SeasonalEvent>();
        }

        var json = await File.ReadAllTextAsync(_eventsDataPath);
        return JsonSerializer.Deserialize<List<SeasonalEvent>>(json) ?? new List<SeasonalEvent>();
    }

    private async Task SaveEventsAsync(List<SeasonalEvent> events)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_eventsDataPath)!);
        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_eventsDataPath, json);
    }

    private async Task<List<EventParticipation>> LoadParticipationsAsync()
    {
        if (!File.Exists(_participationsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_participationsDataPath)!);
            return new List<EventParticipation>();
        }

        var json = await File.ReadAllTextAsync(_participationsDataPath);
        return JsonSerializer.Deserialize<List<EventParticipation>>(json) ?? new List<EventParticipation>();
    }

    private async Task SaveParticipationsAsync(List<EventParticipation> participations)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_participationsDataPath)!);
        var json = JsonSerializer.Serialize(participations, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_participationsDataPath, json);
    }

    // Phase 11 additions - World Boss functionality
    public async Task<WorldBossEvent?> GetWorldBossAsync(string bossId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var bosses = await LoadWorldBossesAsync();
            return bosses.FirstOrDefault(b => b.BossId == bossId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<WorldBossEvent>> GetActiveWorldBossesAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var bosses = await LoadWorldBossesAsync();
            return bosses.Where(b => b.Status == BossStatus.Active || b.Status == BossStatus.Waiting).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<WorldBossEvent> CreateWorldBossAsync(WorldBossEvent bossEvent)
    {
        await _semaphore.WaitAsync();
        try
        {
            var bosses = await LoadWorldBossesAsync();
            bosses.Add(bossEvent);
            await SaveWorldBossesAsync(bosses);
            return bossEvent;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateWorldBossAsync(WorldBossEvent bossEvent)
    {
        await _semaphore.WaitAsync();
        try
        {
            var bosses = await LoadWorldBossesAsync();
            var existingBoss = bosses.FirstOrDefault(b => b.BossId == bossEvent.BossId);
            if (existingBoss != null)
            {
                bosses.Remove(existingBoss);
                bosses.Add(bossEvent);
                await SaveWorldBossesAsync(bosses);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteWorldBossAsync(string bossId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var bosses = await LoadWorldBossesAsync();
            var boss = bosses.FirstOrDefault(b => b.BossId == bossId);
            if (boss != null)
            {
                bosses.Remove(boss);
                await SaveWorldBossesAsync(bosses);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<WorldBossEvent>> LoadWorldBossesAsync()
    {
        if (!File.Exists(_worldBossesDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_worldBossesDataPath)!);
            return new List<WorldBossEvent>();
        }

        var json = await File.ReadAllTextAsync(_worldBossesDataPath);
        return JsonSerializer.Deserialize<List<WorldBossEvent>>(json) ?? new List<WorldBossEvent>();
    }

    private async Task SaveWorldBossesAsync(List<WorldBossEvent> bosses)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_worldBossesDataPath)!);
        var json = JsonSerializer.Serialize(bosses, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_worldBossesDataPath, json);
    }
}