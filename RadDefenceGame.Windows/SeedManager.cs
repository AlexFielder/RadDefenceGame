namespace RadDefenceGame.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class SeedManager
{
    private const string FavouritesFile = "favourites.json";
    private static readonly string FavouritesPath;

    public int CurrentSeed { get; private set; }
    public List<FavouriteSeed> Favourites { get; private set; } = new();

    static SeedManager()
    {
        // store next to the executable
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        FavouritesPath = Path.Combine(dir, FavouritesFile);
    }

    public SeedManager()
    {
        LoadFavourites();
    }

    public int NewRandomSeed()
    {
        CurrentSeed = new Random().Next(1, 999_999);
        return CurrentSeed;
    }

    public void SetSeed(int seed)
    {
        CurrentSeed = seed;
    }

    public bool IsFavourite(int seed)
    {
        foreach (var f in Favourites)
            if (f.Seed == seed) return true;
        return false;
    }

    public void ToggleFavourite(int seed, int score, int wave)
    {
        for (int i = 0; i < Favourites.Count; i++)
        {
            if (Favourites[i].Seed == seed)
            {
                Favourites.RemoveAt(i);
                SaveFavourites();
                return;
            }
        }

        Favourites.Add(new FavouriteSeed
        {
            Seed = seed,
            BestScore = score,
            BestWave = wave,
            SavedAt = DateTime.UtcNow
        });
        SaveFavourites();
    }

    public void UpdateBest(int seed, int score, int wave)
    {
        foreach (var f in Favourites)
        {
            if (f.Seed == seed)
            {
                if (score > f.BestScore)
                {
                    f.BestScore = score;
                    f.BestWave = wave;
                    SaveFavourites();
                }
                return;
            }
        }
    }

    private void SaveFavourites()
    {
        try
        {
            var json = JsonSerializer.Serialize(Favourites, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FavouritesPath, json);
        }
        catch { /* silently fail — not critical */ }
    }

    private void LoadFavourites()
    {
        try
        {
            if (File.Exists(FavouritesPath))
            {
                var json = File.ReadAllText(FavouritesPath);
                Favourites = JsonSerializer.Deserialize<List<FavouriteSeed>>(json) ?? new();
            }
        }
        catch { Favourites = new(); }
    }
}

public class FavouriteSeed
{
    public int Seed { get; set; }
    public int BestScore { get; set; }
    public int BestWave { get; set; }
    public DateTime SavedAt { get; set; }
}
