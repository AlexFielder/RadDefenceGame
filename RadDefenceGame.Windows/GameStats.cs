namespace RadDefenceGame.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Tracks statistics for a single game run.</summary>
public class GameStats
{
    public int Seed { get; set; }
    public int FinalScore { get; set; }
    public int WavesCompleted { get; set; }
    public int TotalKills { get; set; }
    public int TotalMoneyEarned { get; set; }
    public int TowersBuilt { get; set; }
    public int LivesLost { get; set; }
    public float PlayTimeSeconds { get; set; }

    public Dictionary<EnemyType, int> KillsByEnemyType { get; set; } = new();
    public Dictionary<TowerType, int> KillsByTowerType { get; set; } = new();

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public void RecordKill(EnemyType enemyType)
    {
        TotalKills++;
        KillsByEnemyType.TryGetValue(enemyType, out int count);
        KillsByEnemyType[enemyType] = count + 1;
    }

    public void RecordTowerKill(TowerType towerType)
    {
        KillsByTowerType.TryGetValue(towerType, out int count);
        KillsByTowerType[towerType] = count + 1;
    }

    public void RecordMoney(int amount) => TotalMoneyEarned += amount;
    public void RecordTowerBuilt() => TowersBuilt++;
    public void RecordLifeLost() => LivesLost++;
}

/// <summary>Persistent leaderboard stored as JSON. Tracks top runs and career totals.</summary>
public class Leaderboard
{
    private const string FileName = "leaderboard.json";
    private static readonly string FilePath;

    public List<LeaderboardEntry> TopRuns { get; set; } = new();
    public CareerStats Career { get; set; } = new();

    private const int MaxEntries = 20;

    static Leaderboard()
    {
        FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
    }

    public int Submit(GameStats stats)
    {
        Career.TotalGamesPlayed++;
        Career.TotalKills += stats.TotalKills;
        Career.TotalMoneyEarned += stats.TotalMoneyEarned;
        Career.TotalPlayTimeSeconds += stats.PlayTimeSeconds;
        Career.TotalWavesCompleted += stats.WavesCompleted;
        Career.HighestWave = Math.Max(Career.HighestWave, stats.WavesCompleted);
        Career.HighestScore = Math.Max(Career.HighestScore, stats.FinalScore);

        foreach (var (type, count) in stats.KillsByEnemyType)
        {
            Career.TotalKillsByEnemyType.TryGetValue(type, out int existing);
            Career.TotalKillsByEnemyType[type] = existing + count;
        }

        foreach (var (type, count) in stats.KillsByTowerType)
        {
            Career.TotalKillsByTowerType.TryGetValue(type, out int existing);
            Career.TotalKillsByTowerType[type] = existing + count;
        }

        var entry = new LeaderboardEntry
        {
            Score = stats.FinalScore,
            Wave = stats.WavesCompleted,
            Seed = stats.Seed,
            Kills = stats.TotalKills,
            PlayedAt = stats.PlayedAt,
            PlayTimeSeconds = stats.PlayTimeSeconds
        };

        int rank = 0;
        for (int i = 0; i < TopRuns.Count; i++)
        {
            if (stats.FinalScore > TopRuns[i].Score)
            {
                TopRuns.Insert(i, entry);
                rank = i + 1;
                break;
            }
        }

        if (rank == 0 && TopRuns.Count < MaxEntries)
        {
            TopRuns.Add(entry);
            rank = TopRuns.Count;
        }

        while (TopRuns.Count > MaxEntries)
            TopRuns.RemoveAt(TopRuns.Count - 1);

        Save();
        return rank;
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions)); }
        catch { }
    }

    public static Leaderboard Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Leaderboard>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch { }
        return new Leaderboard();
    }
}

public class LeaderboardEntry
{
    public int Score { get; set; }
    public int Wave { get; set; }
    public int Seed { get; set; }
    public int Kills { get; set; }
    public float PlayTimeSeconds { get; set; }
    public DateTime PlayedAt { get; set; }
}

public class CareerStats
{
    public int TotalGamesPlayed { get; set; }
    public int TotalKills { get; set; }
    public int TotalMoneyEarned { get; set; }
    public float TotalPlayTimeSeconds { get; set; }
    public int TotalWavesCompleted { get; set; }
    public int HighestWave { get; set; }
    public int HighestScore { get; set; }

    public Dictionary<EnemyType, int> TotalKillsByEnemyType { get; set; } = new();
    public Dictionary<TowerType, int> TotalKillsByTowerType { get; set; } = new();
}
