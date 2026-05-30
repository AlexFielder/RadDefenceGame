namespace RadDefenceGame.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Persistent toolbar layout preferences. Stored alongside leaderboard data so
/// the choice survives across runs. Custom button order is opt-in: only used when
/// <see cref="Style"/> == <see cref="ToolbarStyle.Custom"/>.</summary>
public class ToolbarPrefs
{
    public ToolbarStyle Style { get; set; } = ToolbarStyle.Compact;

    /// <summary>Ordered list of tower-type ids (cast from <see cref="TowerType"/>).
    /// Empty until the user makes their first drag in Custom mode, at which point
    /// the default order is captured and then mutated by subsequent drags.</summary>
    public List<int> CustomOrder { get; set; } = new();

    // --- persistence ---

    private static readonly string PrefsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadDefence");
    private static readonly string PrefsFile = Path.Combine(PrefsDir, "toolbar.json");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ToolbarPrefs Load()
    {
        if (!File.Exists(PrefsFile)) return new ToolbarPrefs();
        try { return JsonSerializer.Deserialize<ToolbarPrefs>(File.ReadAllText(PrefsFile), JsonOpts) ?? new ToolbarPrefs(); }
        catch { return new ToolbarPrefs(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PrefsDir);
            File.WriteAllText(PrefsFile, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* swallow — toolbar prefs are best-effort, never block gameplay */ }
    }
}
