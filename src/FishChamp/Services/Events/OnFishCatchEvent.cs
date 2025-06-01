using FishChamp.Data.Models;

namespace FishChamp.Services.Events;

/// <summary>
/// Event fired when a fish is successfully caught
/// </summary>
public class OnFishCatchEvent : IEvent
{
    public DateTime CreatedAt { get; }
    public string EventId { get; }
    
    /// <summary>
    /// The player who caught the fish
    /// </summary>
    public ulong UserId { get; }
    
    /// <summary>
    /// The username of the player
    /// </summary>
    public string Username { get; }
    
    /// <summary>
    /// The fish that was caught
    /// </summary>
    public InventoryItem FishItem { get; }
    
    /// <summary>
    /// The weight of the caught fish
    /// </summary>
    public double FishWeight { get; }
    
    /// <summary>
    /// The area where the fish was caught
    /// </summary>
    public string AreaId { get; }
    
    /// <summary>
    /// The fishing spot where the fish was caught
    /// </summary>
    public string FishingSpotId { get; }
    
    /// <summary>
    /// The timing percentage when the fish was caught (affects quality)
    /// </summary>
    public float TimingPercent { get; }

    public OnFishCatchEvent(ulong userId, string username, InventoryItem fishItem, double fishWeight, 
        string areaId, string fishingSpotId, float timingPercent)
    {
        CreatedAt = DateTime.UtcNow;
        EventId = Guid.NewGuid().ToString();
        UserId = userId;
        Username = username;
        FishItem = fishItem;
        FishWeight = fishWeight;
        AreaId = areaId;
        FishingSpotId = fishingSpotId;
        TimingPercent = timingPercent;
    }
}