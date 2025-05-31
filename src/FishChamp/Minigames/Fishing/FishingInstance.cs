using Polly;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Interactivity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Minigames.Fishing;

public class FishingInstance(IDiscordRestInteractionAPI interactionAPI)
{
    public const int MaxWater = 10;

    public IInteractionContext Context { get; set; }
    private int fishPosition;

    public async Task UpdateAsync()
    {
        fishPosition++;

        if (fishPosition > MaxWater)
        {
            var failEmbed = new Embed
            {
                Title = "🐟 Fish Escaped!",
                Description = "The fish got away!\n\nBetter luck next time!",
                Colour = Color.Red,
                Timestamp = DateTimeOffset.UtcNow
            };

            var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), CustomIDHelpers.CreateButtonID(FishingInteractionGroup.CastLine));
            var failedComponents = new List<IMessageComponent>
            {
                new ActionRowComponent(new[] { (IMessageComponent)tryAgainButton })
            };

            await interactionAPI.EditOriginalInteractionResponseAsync(
                Context.Interaction.ApplicationID,
                Context.Interaction.Token,
                embeds: new[] { failEmbed },
                components: failedComponents);

            return;

        }

        StringBuilder builder = new StringBuilder();

        for (int i = MaxWater; i >= 0; i--)
        {
            if (i == fishPosition)
            {
                builder.Append("<:newfish2:1378160844864487504>");
            }
            else
            {
                builder.Append(":blue_square:");
            }
        }

        // Start the timing mini-game
        var timingEmbed = new Embed
        {
            Title = "🎯 Timing Challenge!",
            Description = $"Click **Stop Reel** when the :fish: is in the center!\n\n{builder}",
            Colour = Color.Yellow,
            Footer = new EmbedFooter("Quick! Don't let the fish escape!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        var stopButton = new ButtonComponent(ButtonComponentStyle.Danger, "Stop Reel", new PartialEmoji(Name: "🛑"), CustomIDHelpers.CreateButtonID(FishingInteractionGroup.StopReel));
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([stopButton])
        };

        await interactionAPI.EditOriginalInteractionResponseAsync(
            Context.Interaction.ApplicationID,
            Context.Interaction.Token,
            embeds: new[] { timingEmbed },
            components: components);
    }

    private float GetCenterClosenessPercent(float value, float min, float max)
    {
        float center = (min + max) / 2f;
        float maxDistance = center - min;
        float distance = float.Abs(value - center);
        float closeness = 1f - distance / maxDistance;
        return float.Clamp(closeness, 0f, 1f);
    }

    public float GetFishPositionPercent()
    {
        // Assuming fishPosition is between 0 and 11
        return GetCenterClosenessPercent(fishPosition, 0, MaxWater);
    }
}
