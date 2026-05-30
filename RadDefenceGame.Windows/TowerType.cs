namespace RadDefenceGame.Windows;

public enum TowerType
{
    Basic,
    Sniper,
    Rapid,
    Rocket,
    Flame,
    Tesla,
    Tachyon,
    Grinder,
    Repair,
    Mortar,
    Artillery,
    DroneController,
    ElectricFence
}

/// <summary>For towers (like Drone Controller) where the player must pick between two
/// mutually-exclusive upgrade tracks. Once a track is selected, the other is locked out.</summary>
public enum UpgradePath
{
    None,
    Range,
    Count
}
