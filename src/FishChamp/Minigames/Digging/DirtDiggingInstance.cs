using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Interactivity;
using Remora.Results;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Minigames.Digging;

public class DirtDiggingInstance(IDiscordRestInteractionAPI interactionAPI)
{
    public const int MapWidth = 11;
    public const int MapHeight = 11;

    public IInteractionContext Context { get; set; }

    public Vector2 BasketLocation { get; private set; } = new Vector2(5, 5);
    public int WormCount { get; private set; }
    public int DigAttemptsLeft { get; private set; } = 5;

    private List<Vector2> SearchedTiles = [];
    private List<Vector2> wormPositions = [];

    public string GenerateDirtMap()
    {
        StringBuilder mapBuilder = new StringBuilder();
        for (int j = 0; j < MapHeight; j++)
        {
            StringBuilder lineBuilder = new StringBuilder();

            for (int i = 0; i < MapWidth; i++)
            {
                if (BasketLocation.X == i && BasketLocation.Y == j)
                {
                    lineBuilder.Append(WormCount switch
                    {
                        0 => "<:basket:1378444839053557921>",
                        1 => "<:basket1:1378444859622297621>",
                        2 => "<:basket2:1378444874046640135>",
                        3 => "<:basket3:1378444888458268825>",
                    });
                }
                else if (SearchedTiles.Any(tile => tile == new Vector2(i, j)))
                {
                    lineBuilder.Append(":black_large_square:");
                }
                else
                {
                    lineBuilder.Append(":brown_square:");
                }
            }

            mapBuilder.AppendLine(lineBuilder.ToString());
        }

        return mapBuilder.ToString();
    }

    public async Task<IResult> UpdateInteractionAsync(string extraMessage = "")
    {
        var fields = new List<EmbedField>
            {
                new EmbedField($"Worms Found: {WormCount}", ""),
                new EmbedField($"Dig Attempts Left: {DigAttemptsLeft}", "")
            };

        if (!string.IsNullOrEmpty(extraMessage))
        {
            fields.Add(new EmbedField(extraMessage, ""));
        }

        var diggingEmbed = new Embed
        {
            Title = "🎯 Find the worms!",
            Description = GenerateDirtMap(),
            Fields = fields,
            Colour = Color.SandyBrown,
            Timestamp = DateTimeOffset.UtcNow
        };

        var upButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "⬆️"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.MoveUp));
        var downButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "⬇️"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.MoveDown));
        var leftButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "⬅️"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.MoveLeft));
        var rightButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "➡️"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.MoveRight));

        var digButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "🧺"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.Dig));
        var stopButton = new ButtonComponent(ButtonComponentStyle.Primary, "", new PartialEmoji(Name: "🛑"), CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.Stop));

        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([upButton, downButton]),
            new ActionRowComponent([leftButton, rightButton]),
            new ActionRowComponent([digButton, stopButton]),
        };

        return await interactionAPI.EditOriginalInteractionResponseAsync(
            Context.Interaction.ApplicationID,
            Context.Interaction.Token,
            embeds: new[] { diggingEmbed },
            components: components);
    }

    public async Task<IResult> MoveAsync(Vector2 direction)
    {
        bool canMove = false;

        if (direction == Vector2.UnitX && BasketLocation.X < MapWidth - 1)
        {
            canMove = true;
        }
        else if (direction == -Vector2.UnitX && BasketLocation.X > 0)
        {
            canMove = true;
        }
        else if (direction == Vector2.UnitY && BasketLocation.Y < MapHeight - 1)
        {
            canMove = true;
        }
        else if (direction == -Vector2.UnitY && BasketLocation.Y > 0)
        {
            canMove = true;
        }

        if (canMove)
        {
            BasketLocation += direction;
        }

        return await UpdateInteractionAsync();
    }

    public async Task<IResult> DigAsync()
    {
        if (SearchedTiles.Any(SearchedTiles => SearchedTiles == BasketLocation))
        {
            return await UpdateInteractionAsync("❗ You already searched this spot!");
        }

        if (WormCount >= 3)
        {
            return await UpdateInteractionAsync("❗ Already have 3 worms!");
        }

        if (DigAttemptsLeft <= 0)
        {
            return await UpdateInteractionAsync("❗ No dig attempts left! Click Stop to end digging.");
        }

        bool foundWorm = Random.Shared.NextDouble() > .5;

        if (foundWorm)
        {
            WormCount++;
        }

        DigAttemptsLeft--;
        SearchedTiles.Add(BasketLocation);

        return await UpdateInteractionAsync(foundWorm ? "You found a worm!" : "You dug but found nothing.");
    }
}
