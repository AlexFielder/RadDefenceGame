namespace RadDefenceGame.Windows;

/// <summary>How the in-game toolbar lays out its tower-selection buttons. The user picks
/// this from the Settings screen — see <see cref="ToolbarPrefs"/> for persistence.</summary>
public enum ToolbarStyle
{
    /// <summary>Single row of compact buttons (original layout). Labels can wrap or
    /// truncate when many towers are present.</summary>
    Compact,
    /// <summary>Category buttons (Projectile / Field / Utility / Path) that open
    /// vertical dropdowns over the play area.</summary>
    Grouped,
    /// <summary>Two horizontal rows — projectiles on top, utility/field on bottom.</summary>
    TwoRow,
    /// <summary>User-defined order via press-drag. Saved on every change.</summary>
    Custom
}
