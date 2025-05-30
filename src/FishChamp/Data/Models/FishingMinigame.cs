using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using System.Drawing;

namespace FishChamp.Data.Models;

/// <summary>
/// Simple model to track fishing minigame state
/// </summary>
public class FishingMinigame
{
    public ulong UserId { get; set; }
    public string FishType { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public int Size { get; set; }
    public double Weight { get; set; }
    public FishTrait Traits { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsComplete { get; set; }
    public bool IsSuccess { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    
    public static IReadOnlyList<IMessageComponent> CreateButtons(FishTrait traits)
    {
        // Create row of buttons for fishing interaction
        var buttonStyle = ButtonComponentStyle.Primary;
        var catchButton = new ButtonComponent(buttonStyle, "Catch!", new PartialEmoji(Name: "🎣"), "fishing_catch");
        
        return new List<IMessageComponent>
        {
            new ActionRowComponent(new[] { (IMessageComponent)catchButton })
        };
    }
    
    public static Embed CreateMinigameEmbed(string fishSpot)
    {
        return new Embed
        {
            Title = "🎣 Fishing in Progress...",
            Description = $"You cast your line at {fishSpot.ToUpperInvariant()}...\n\n" +
                         "Click the 🎣 **Catch!** button when you feel a bite!",
            Colour = Color.Blue,
            Footer = new EmbedFooter("If you wait too long, the fish might get away!"),
            Timestamp = DateTimeOffset.UtcNow
        };
    }
    
    public static Embed CreateTimeoutEmbed()
    {
        return new Embed
        {
            Title = "🎣 Too Slow!",
            Description = "You missed your chance to catch the fish!",
            Colour = Color.Red,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}