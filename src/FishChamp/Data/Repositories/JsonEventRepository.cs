using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonEventRepository : IEventRepository
{
    private readonly string _eventsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "events.json");
    private readonly string _participationsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "event_participations.json");
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
}