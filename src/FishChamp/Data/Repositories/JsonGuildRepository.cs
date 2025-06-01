using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonGuildRepository : IGuildRepository
{
    private readonly string _guildsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "guilds.json");
    private readonly string _invitationsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "guild_invitations.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Guild?> GetGuildAsync(string guildId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            return guilds.FirstOrDefault(g => g.GuildId == guildId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Guild?> GetUserGuildAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            return guilds.FirstOrDefault(g => g.Members.Any(m => m.UserId == userId));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Guild>> GetPublicGuildsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            return guilds.Where(g => g.IsPublic && g.Members.Count < g.MaxMembers).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Guild>> GetAllGuildsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadGuildsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Guild> CreateGuildAsync(Guild guild)
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            guilds.Add(guild);
            await SaveGuildsAsync(guilds);
            return guild;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateGuildAsync(Guild guild)
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            var existingGuild = guilds.FirstOrDefault(g => g.GuildId == guild.GuildId);
            if (existingGuild != null)
            {
                guilds.Remove(existingGuild);
                guilds.Add(guild);
                await SaveGuildsAsync(guilds);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteGuildAsync(string guildId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var guilds = await LoadGuildsAsync();
            var guild = guilds.FirstOrDefault(g => g.GuildId == guildId);
            if (guild != null)
            {
                guilds.Remove(guild);
                await SaveGuildsAsync(guilds);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<GuildInvitation>> GetUserInvitationsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var invitations = await LoadInvitationsAsync();
            return invitations.Where(i => i.TargetUserId == userId && 
                                        i.Status == InvitationStatus.Pending && 
                                        i.ExpiresAt > DateTime.UtcNow).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<GuildInvitation> CreateInvitationAsync(GuildInvitation invitation)
    {
        await _semaphore.WaitAsync();
        try
        {
            var invitations = await LoadInvitationsAsync();
            invitations.Add(invitation);
            await SaveInvitationsAsync(invitations);
            return invitation;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateInvitationAsync(GuildInvitation invitation)
    {
        await _semaphore.WaitAsync();
        try
        {
            var invitations = await LoadInvitationsAsync();
            var existingInvitation = invitations.FirstOrDefault(i => i.InvitationId == invitation.InvitationId);
            if (existingInvitation != null)
            {
                invitations.Remove(existingInvitation);
                invitations.Add(invitation);
                await SaveInvitationsAsync(invitations);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteInvitationAsync(string invitationId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var invitations = await LoadInvitationsAsync();
            var invitation = invitations.FirstOrDefault(i => i.InvitationId == invitationId);
            if (invitation != null)
            {
                invitations.Remove(invitation);
                await SaveInvitationsAsync(invitations);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Guild>> LoadGuildsAsync()
    {
        if (!File.Exists(_guildsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_guildsDataPath)!);
            return new List<Guild>();
        }

        var json = await File.ReadAllTextAsync(_guildsDataPath);
        return JsonSerializer.Deserialize<List<Guild>>(json) ?? new List<Guild>();
    }

    private async Task SaveGuildsAsync(List<Guild> guilds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_guildsDataPath)!);
        var json = JsonSerializer.Serialize(guilds, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_guildsDataPath, json);
    }

    private async Task<List<GuildInvitation>> LoadInvitationsAsync()
    {
        if (!File.Exists(_invitationsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_invitationsDataPath)!);
            return new List<GuildInvitation>();
        }

        var json = await File.ReadAllTextAsync(_invitationsDataPath);
        return JsonSerializer.Deserialize<List<GuildInvitation>>(json) ?? new List<GuildInvitation>();
    }

    private async Task SaveInvitationsAsync(List<GuildInvitation> invitations)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_invitationsDataPath)!);
        var json = JsonSerializer.Serialize(invitations, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_invitationsDataPath, json);
    }
}