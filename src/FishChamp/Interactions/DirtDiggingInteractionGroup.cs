using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Tracker;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Interactions;

public class DirtDiggingInteractionGroup(
    IDiscordRestInteractionAPI interactionAPI,
    IInteractionContext context, 
    IServiceProvider services,
    IInstanceTracker<DirtDiggingInstance> diggingTracker,
    IInventoryRepository inventoryRepository) : InteractionGroup
{
    public const string Start = "dig_dirt_start";
    public const string MoveUp = "dig_dirt_move_up";
    public const string MoveDown = "dig_dirt_move_down";
    public const string MoveLeft = "dig_dirt_move_left";
    public const string MoveRight = "dig_dirt_move_right";
    public const string Dig = "dig_dirt_dig";
    public const string Stop = "dig_dirt_stop";

    [Button(Start)]
    public async Task<IResult> StartAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot reel in on someone else's fishing spot!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        diggingTracker.TryRemove(user.ID, out _);

        DirtDiggingInstance dirtDiggingInstances = ActivatorUtilities.CreateInstance<DirtDiggingInstance>(services);
        dirtDiggingInstances.Context = context;
        diggingTracker.Add(user.ID, dirtDiggingInstances);

        return await dirtDiggingInstances.UpdateInteractionAsync();
    }

    [Button(MoveUp)]
    public async Task<IResult> MoveUpAsync() => await MoveAsync(-Vector2.UnitY);  

    [Button(MoveDown)]
    public async Task<IResult> MoveDownAsync() => await MoveAsync(Vector2.UnitY);

    [Button(MoveLeft)]
    public async Task<IResult> MoveLeftAsync() => await MoveAsync(-Vector2.UnitX);

    [Button(MoveRight)]
    public async Task<IResult> MoveRightAsync() => await MoveAsync(Vector2.UnitX);

    private async Task<IResult> MoveAsync(Vector2 direction)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot use someone's interaction.",
                    flags: MessageFlags.Ephemeral);
            }
        }

        if (!diggingTracker.TryGetValue(user.ID, out var diggingInstance))
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "Digging interaction not found.",
                flags: MessageFlags.Ephemeral);
        }

        return await diggingInstance.MoveAsync(direction);
    }

    [Button(Dig)]
    public async Task<IResult> DigAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot use someone's interaction.",
                    flags: MessageFlags.Ephemeral);
            }
        }

        if (!diggingTracker.TryGetValue(user.ID, out var diggingInstance))
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "Digging interaction not found.",
                flags: MessageFlags.Ephemeral);
        }

        return await diggingInstance.DigAsync();
    }

    [Button(Stop)]
    public async Task<IResult> StopAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot use someone's interaction.",
                    flags: MessageFlags.Ephemeral);
            }
        }

        int foundWorms = 0;
        if (diggingTracker.TryRemove(user.ID, out var diggingInstance))
        {
            foundWorms = diggingInstance.WormCount;
        }

        var digResults = new List<InventoryItem>();

        for (int i = 0; i < foundWorms; i++)
        {
            // Determine what was found
            var findRoll = Random.Shared.NextDouble();

            if (findRoll < 0.4) // 40% - common worms
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "worm_bait",
                    ItemType = "Bait",
                    Name = "Worm Bait",
                    Quantity = 1,
                    Properties = new() { ["attraction"] = 1.1 }
                });
            }
            else if (findRoll < 0.7) // 30% - quality worms
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "quality_worms",
                    ItemType = "Bait",
                    Name = "Quality Worms",
                    Quantity = 1,
                    Properties = new() { ["attraction"] = 1.3 }
                });
            }
            else if (findRoll < 0.9) // 20% - grubs
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "grub_bait",
                    ItemType = "Bait",
                    Name = "Grub Bait",
                    Quantity = 1,
                    Properties = new() { ["attraction"] = 1.4 }
                });
            }
            else // 10% - rare find
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "magical_larvae",
                    ItemType = "Bait",
                    Name = "Magical Larvae",
                    Quantity = 1,
                    Properties = new() 
                    { 
                        ["attraction"] = 1.8, 
                        ["rare_bonus"] = true 
                    }
                });
            }
        }

        if (digResults.Count > 0)
        {
            foreach (var item in digResults)
            {
                await inventoryRepository.AddItemAsync(user.ID.Value, item);
            }

            var embed = new Embed
            {
                Title = "🪱 Digging Success!",
                Description = $"You found something while digging!",
                Colour = Color.Brown,
                Fields = digResults.Select(item => new EmbedField("Found", $"{item.Quantity}x {item.Name}", true)).ToList(),
                Timestamp = DateTimeOffset.UtcNow
            };

            return await interactionAPI.EditOriginalInteractionResponseAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                embeds: new[] { embed },
                components: new Remora.Rest.Core.Optional<IReadOnlyList<IMessageComponent>?>([]));
        }
        else
        {
            var embed = new Embed
            {
                Title = "🪱 No Luck",
                Description = $"You didn't find anything useful this time.",
                Colour = Color.Gray,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await interactionAPI.EditOriginalInteractionResponseAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                embeds: new[] { embed },
                components: new Remora.Rest.Core.Optional<IReadOnlyList<IMessageComponent>?>([]));
        }
    }
}
