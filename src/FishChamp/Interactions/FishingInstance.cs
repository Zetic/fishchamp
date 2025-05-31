using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Contexts;

namespace FishChamp.Interactions;

public class FishingInstance
{
    public const int MaxWater = 10;
    
    public IInteractionContext? Context { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    public async Task UpdateAsync()
    {
        // Placeholder for fishing instance updates
        // This could handle timeout logic, bait degradation, etc.
        await Task.CompletedTask;
    }
    
    public float GetFishPositionPercent()
    {
        // Simple timing-based calculation for fishing success
        var elapsed = DateTime.UtcNow - StartTime;
        var random = new Random();
        return (float)(random.NextDouble() * 0.8 + 0.1); // Return value between 0.1 and 0.9
    }
}