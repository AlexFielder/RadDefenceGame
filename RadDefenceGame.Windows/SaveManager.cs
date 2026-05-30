namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class SaveManager
{
    private static readonly string SaveDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadDefence");
    private static readonly string SaveFile = Path.Combine(SaveDir, "savegame.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Zone fields default to 0/false so older saves deserialize cleanly as block-mode towers.
    // ChosenPath/RangeUpgrades persist Drone Controller upgrade-track state; default values
    // (None / 0) load cleanly for all other tower types.
    public record TowerRecord(
        int Type, int Col, int Row, int Level, int TotalInvested,
        float TowerHealth, bool AutoRebuildEnabled,
        bool IsZonePlaced = false, float WorldPosX = 0f, float WorldPosY = 0f,
        int ChosenPath = 0, int RangeUpgrades = 0, float ConeFacing = 0f);

    // PlacementSystem defaults to 0 (Block) so older saves load correctly.
    public record SaveData(
        int Seed, int Wave, int Lives, int Money, int Walls, int Score,
        int Difficulty, bool AutoStartWaves, float PlayTimeSeconds,
        List<TowerRecord> Towers, List<int[]> PlayerWalls,
        int PlacementSystem = 0);

    public static bool HasSave() => File.Exists(SaveFile);

    public static void Save(int seed, int wave, GameState state, List<Tower> towers,
        Map map, float playTime)
    {
        var towerRecords = new List<TowerRecord>();
        foreach (var t in towers)
            towerRecords.Add(new TowerRecord(
                (int)t.Type, t.GridPos.X, t.GridPos.Y, t.Level,
                t.TotalInvested, t.TowerHealth, t.AutoRebuildEnabled,
                t.IsZonePlaced, t.WorldPos.X, t.WorldPos.Y,
                (int)t.ChosenPath, t.RangeUpgradesApplied, t.ConeFacing));

        var playerWalls = new List<int[]>();
        foreach (var pt in map.PlayerPlacedWalls)
            playerWalls.Add(new[] { pt.X, pt.Y });

        var data = new SaveData(seed, wave, state.Lives, state.Money, state.Walls,
            state.Score, (int)state.Difficulty, state.AutoStartWaves, playTime,
            towerRecords, playerWalls, (int)state.PlacementSystem);

        Directory.CreateDirectory(SaveDir);
        File.WriteAllText(SaveFile, JsonSerializer.Serialize(data, JsonOpts));
    }

    public static SaveData? Load()
    {
        if (!File.Exists(SaveFile)) return null;
        try { return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SaveFile), JsonOpts); }
        catch { return null; }
    }

    public static void DeleteSave()
    {
        if (File.Exists(SaveFile)) File.Delete(SaveFile);
    }
}
