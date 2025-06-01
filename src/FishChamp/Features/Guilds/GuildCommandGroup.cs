using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;
using GuildModel = FishChamp.Data.Models.Guild;
using GuildMemberModel = FishChamp.Data.Models.GuildMember;

namespace FishChamp.Features.Guilds;

[Group("guild")]
[Description("Guild management commands")]
public class GuildCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IGuildRepository guildRepository,
    DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("create")]
    [Description("Create a new guild")]
    public async Task<IResult> CreateGuildAsync(
        [Description("Guild name")] string name,
        [Description("Guild description")] string description,
        [Description("Guild tag (optional)")] string? tag = null,
        [Description("Is public guild")] bool isPublic = true)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        // Check if user is already in a guild
        var existingGuild = await guildRepository.GetUserGuildAsync(user.ID.Value);
        if (existingGuild != null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are already a member of a guild! Leave your current guild first.", Color.Red);
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        // Check if guild name is taken
        var allGuilds = await guildRepository.GetAllGuildsAsync();
        if (allGuilds.Any(g => g.Name.ToLower() == name.ToLower()))
        {
            return await feedbackService.SendContextualContentAsync("🚫 A guild with that name already exists!", Color.Red);
        }

        var guild = new GuildModel
        {
            Name = name,
            Description = description,
            OwnerId = user.ID.Value,
            Tag = tag,
            IsPublic = isPublic,
            Members = [new GuildMemberModel
            {
                UserId = user.ID.Value,
                Username = user.Username,
                Role = GuildRole.Owner,
                JoinedAt = DateTime.UtcNow
            }]
        };

        await guildRepository.CreateGuildAsync(guild);

        // Update player profile
        player.GuildId = guild.GuildId;
        await playerRepository.UpdatePlayerAsync(player);

        var embed = new Embed
        {
            Title = "🏛️ Guild Created!",
            Description = $"Successfully created **{guild.Name}**!\n\n" +
                         $"**Description:** {guild.Description}\n" +
                         $"**Tag:** {guild.Tag ?? "None"}\n" +
                         $"**Type:** {(guild.IsPublic ? "Public" : "Private")}\n" +
                         $"**Guild ID:** `{guild.GuildId}`\n\n" +
                         $"You are now the guild owner! Use `/guild invite` to add members.",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("info")]
    [Description("View guild information")]
    public async Task<IResult> ViewGuildInfoAsync([Description("Guild ID (optional - shows your guild if not specified)")] string? guildId = null)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        GuildModel? guild;
        if (string.IsNullOrEmpty(guildId))
        {
            guild = await guildRepository.GetUserGuildAsync(user.ID.Value);
            if (guild == null)
            {
                return await feedbackService.SendContextualContentAsync("🚫 You are not a member of any guild!", Color.Red);
            }
        }
        else
        {
            guild = await guildRepository.GetGuildAsync(guildId);
            if (guild == null)
            {
                return await feedbackService.SendContextualContentAsync("🚫 Guild not found!", Color.Red);
            }
        }

        var owner = guild.Members.FirstOrDefault(m => m.Role == GuildRole.Owner);
        var memberList = string.Join("\n", guild.Members.Take(10).Select(m =>
            $"• {m.Username} ({m.Role}) - {m.ContributionPoints} points"));

        if (guild.Members.Count > 10)
        {
            memberList += $"\n... and {guild.Members.Count - 10} more members";
        }

        var goalsText = guild.Goals.Any()
            ? string.Join("\n", guild.Goals.Take(3).Select(g =>
                $"• {g.Name}: {g.CurrentAmount}/{g.TargetAmount} {(g.IsCompleted ? "✅" : "⏳")}"))
            : "No active goals";

        var embed = new Embed
        {
            Title = $"🏛️ {guild.Name} {guild.Tag ?? ""}",
            Description = guild.Description,
            Fields = new List<EmbedField>
            {
                new("📊 Guild Stats",
                    $"**Level:** {guild.Level}\n" +
                    $"**Members:** {guild.Members.Count}/{guild.MaxMembers}\n" +
                    $"**Created:** {guild.CreatedAt:yyyy-MM-dd}\n" +
                    $"**Owner:** {owner?.Username ?? "Unknown"}", true),
                new("👥 Members", memberList, true),
                new("🎯 Goals", goalsText, false)
            },
            Colour = Color.Blue,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("invite")]
    [Description("Invite a player to your guild")]
    public async Task<IResult> InvitePlayerAsync([Description("User to invite")] IUser targetUser)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (user.ID.Value == targetUser.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You cannot invite yourself!", Color.Red);
        }

        var guild = await guildRepository.GetUserGuildAsync(user.ID.Value);
        if (guild == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not a member of any guild!", Color.Red);
        }

        var memberRole = guild.Members.FirstOrDefault(m => m.UserId == user.ID.Value)?.Role;
        if (memberRole == null || memberRole != GuildRole.Owner && memberRole != GuildRole.Leader && memberRole != GuildRole.Officer)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You don't have permission to invite members!", Color.Red);
        }

        // Check if target is already in a guild
        var targetGuild = await guildRepository.GetUserGuildAsync(targetUser.ID.Value);
        if (targetGuild != null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 That player is already in a guild!", Color.Red);
        }

        // Check if guild is full
        if (guild.Members.Count >= guild.MaxMembers)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Your guild is full!", Color.Red);
        }

        // Check for existing invitation
        var existingInvitations = await guildRepository.GetUserInvitationsAsync(targetUser.ID.Value);
        if (existingInvitations.Any(i => i.GuildId == guild.GuildId))
        {
            return await feedbackService.SendContextualContentAsync("🚫 You have already invited this player!", Color.Red);
        }

        var invitation = new GuildInvitation
        {
            GuildId = guild.GuildId,
            GuildName = guild.Name,
            InviterId = user.ID.Value,
            InviterUsername = user.Username,
            TargetUserId = targetUser.ID.Value,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await guildRepository.CreateInvitationAsync(invitation);

        var embed = new Embed
        {
            Title = "📨 Guild Invitation Sent!",
            Description = $"Invitation sent to {targetUser.Username} to join **{guild.Name}**!\n\n" +
                         $"The invitation will expire in 7 days.",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("invitations")]
    [Description("View your pending guild invitations")]
    public async Task<IResult> ViewInvitationsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var invitations = await guildRepository.GetUserInvitationsAsync(user.ID.Value);

        if (!invitations.Any())
        {
            return await feedbackService.SendContextualContentAsync("📭 You have no pending guild invitations!", Color.Yellow);
        }

        var description = "**Pending Invitations:**\n\n";
        foreach (var invitation in invitations.Take(5))
        {
            var timeLeft = invitation.ExpiresAt - DateTime.UtcNow;
            description += $"• **{invitation.GuildName}**\n" +
                          $"  From: {invitation.InviterUsername}\n" +
                          $"  Expires in: {timeLeft.Days}d {timeLeft.Hours}h\n" +
                          $"  ID: `{invitation.InvitationId}`\n\n";
        }

        var embed = new Embed
        {
            Title = "📨 Guild Invitations",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /guild accept or /guild decline to respond"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("accept")]
    [Description("Accept a guild invitation")]
    public async Task<IResult> AcceptInvitationAsync([Description("Invitation ID")] string invitationId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var invitations = await guildRepository.GetUserInvitationsAsync(user.ID.Value);
        var invitation = invitations.FirstOrDefault(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Invitation not found or expired!", Color.Red);
        }

        var guild = await guildRepository.GetGuildAsync(invitation.GuildId);
        if (guild == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Guild no longer exists!", Color.Red);
        }

        if (guild.Members.Count >= guild.MaxMembers)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Guild is now full!", Color.Red);
        }

        // Add member to guild
        var newMember = new GuildMemberModel
        {
            UserId = user.ID.Value,
            Username = user.Username,
            Role = GuildRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        guild.Members.Add(newMember);
        await guildRepository.UpdateGuildAsync(guild);

        // Update player profile
        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        player.GuildId = guild.GuildId;
        await playerRepository.UpdatePlayerAsync(player);

        // Update invitation status
        invitation.Status = InvitationStatus.Accepted;
        await guildRepository.UpdateInvitationAsync(invitation);

        var embed = new Embed
        {
            Title = "🎉 Welcome to the Guild!",
            Description = $"You have joined **{guild.Name}**!\n\n" +
                         $"**Members:** {guild.Members.Count}/{guild.MaxMembers}\n" +
                         $"**Your Role:** Member\n\n" +
                         $"Use `/guild info` to learn more about your new guild!",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("leave")]
    [Description("Leave your current guild")]
    public async Task<IResult> LeaveGuildAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var guild = await guildRepository.GetUserGuildAsync(user.ID.Value);
        if (guild == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not a member of any guild!", Color.Red);
        }

        var member_ = guild.Members.FirstOrDefault(m => m.UserId == user.ID.Value);
        if (member_ == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not a member of this guild!", Color.Red);
        }

        // Guild owners cannot leave unless they transfer ownership
        if (member_.Role == GuildRole.Owner)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Guild owners cannot leave! Transfer ownership first or disband the guild.", Color.Red);
        }

        // Remove member from guild
        guild.Members.Remove(member_);
        await guildRepository.UpdateGuildAsync(guild);

        // Update player profile
        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player != null)
        {
            player.GuildId = null;
            await playerRepository.UpdatePlayerAsync(player);
        }

        return await feedbackService.SendContextualContentAsync($"👋 You have left **{guild.Name}**.", Color.Orange);
    }

    [Command("list")]
    [Description("List public guilds")]
    public async Task<IResult> ListPublicGuildsAsync()
    {
        var guilds = await guildRepository.GetPublicGuildsAsync();

        if (!guilds.Any())
        {
            return await feedbackService.SendContextualContentAsync("🏛️ No public guilds are currently recruiting!", Color.Yellow);
        }

        var description = "**Public Guilds Recruiting:**\n\n";
        foreach (var guild in guilds.Take(10))
        {
            description += $"• **{guild.Name}** {guild.Tag ?? ""}\n" +
                          $"  {guild.Description}\n" +
                          $"  Members: {guild.Members.Count}/{guild.MaxMembers} | Level: {guild.Level}\n" +
                          $"  ID: `{guild.GuildId}`\n\n";
        }

        var embed = new Embed
        {
            Title = "🏛️ Public Guilds",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /guild info <guild_id> to learn more"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await playerRepository.CreatePlayerAsync(userId, username);
        }
        return player;
    }
}