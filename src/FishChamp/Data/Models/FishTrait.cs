namespace FishChamp.Data.Models;

[Flags]
public enum FishTrait
{
    None = 0,
    Evasive = 1,      // Makes fish harder to catch, reducing success rate
    Slippery = 2,     // Fish might escape after being caught
    Magnetic = 4,     // Attracts other fish, possibly giving bonus catches
    Camouflage = 8    // Makes fish harder to detect, reducing success rate
}

public enum RodAbility
{
    None = 0,
    SharpHook = 1,    // Counter to Slippery
    Precision = 2,    // Counter to Evasive
    FishFinder = 4,   // Counter to Camouflage
    Lure = 8          // Enhances Magnetic effect
}