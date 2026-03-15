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

    public record TowerRecord(
        int Type, int Col, int Row, int Level, int TotalInvested,
        float TowerHealth, bool AutoRebuildEnabled);

    public record SaveData(
        int Seed, int Wave, int Lives, int Money, int Walls, int Score,
        int Difficulty, bool AutoStartWaves, float PlayTimeSeconds,
        List<TowerRecord> Towers, List<int[]> PlayerWalls);

    public static bool HasSave() => File.Exists(SaveFile);

    public static void Save(int seed, int wave, GameState state, List<Tower> towers,
        Map map, float playTime)
    {
        var towerRecords = new List<TowerRecord>();
        foreach (var t in towers)
            towerRecords.Add(new TowerRecord(
                (int)t.Type, t.GridPos.X, t.GridPos.Y, t.Level,
                t.TotalInvested, t.TowerHealth, t.AutoRebuildEnabled));

        var playerWalls = new List<int[]>();
        foreach (var pt in map.PlayerPlacedWalls)
            playerWalls.Add(new[] { pt.X, pt.Y });

        var data = new SaveData(seed, wave, state.Lives, state.Money, state.Walls,
            state.Score, (int)state.Difficulty, state.AutoStartWaves, playTime,
            towerRecords, playerWalls);

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
