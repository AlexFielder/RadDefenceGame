namespace RadDefenceGame.Windows;

public enum PlacementMode
{
    Tower,
    Wall
}

/// <summary>
/// Controls how guns get positioned on the map.
/// <list type="bullet">
/// <item><b>Block</b> — classic mode, towers snap to wall cell centres (one tower per cell).</item>
/// <item><b>Zone</b> — towers can be placed at any pixel position over a wall cell, with a
/// minimum spacing between towers. Lets the player customise layouts within a wall area.</item>
/// </list>
/// </summary>
public enum PlacementSystem
{
    Block,
    Zone
}
