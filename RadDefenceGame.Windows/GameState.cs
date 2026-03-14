namespace RadDefenceGame.Windows;

public enum GameSpeed
{
    Normal = 1,
    Fast = 2,
    Fastest = 3
}

public class GameState
{
    public int Lives { get; set; } = GameSettings.StartingLives;
    public int Money { get; set; } = GameSettings.StartingMoney;
    public int Walls { get; set; } = GameSettings.StartingWalls;
    public int Score { get; set; }

    public PlacementMode Mode { get; set; } = PlacementMode.Tower;
    public TowerType SelectedTower { get; set; } = TowerType.Basic;
    public GameSpeed Speed { get; set; } = GameSpeed.Normal;

    /// <summary>When false, waves only start on SPACE. When true, auto-starts after countdown.</summary>
    public bool AutoStartWaves { get; set; } = false;

    public float SpeedMultiplier => (int)Speed;

    public bool IsGameOver => Lives <= 0;

    public bool CanAffordTower() => Money >= Tower.GetCost(SelectedTower);
    public bool HasWalls() => Walls > 0;

    public void SpendTower() => Money -= Tower.GetCost(SelectedTower);
    public void SpendWall() => Walls--;
    public void GrantWalls(int count) => Walls += count;

    public void CycleSpeed()
    {
        Speed = Speed switch
        {
            GameSpeed.Normal => GameSpeed.Fast,
            GameSpeed.Fast => GameSpeed.Fastest,
            _ => GameSpeed.Normal
        };
    }

    public string SpeedLabel => Speed switch
    {
        GameSpeed.Fast => "2x",
        GameSpeed.Fastest => "3x",
        _ => "1x"
    };

    public void EarnReward(int reward)
    {
        Money += reward;
        Score += reward;
    }

    public void LoseLife() => Lives = System.Math.Max(0, Lives - 1);

    public void Reset()
    {
        Lives = GameSettings.StartingLives;
        Money = GameSettings.StartingMoney;
        Walls = GameSettings.StartingWalls;
        Score = 0;
        Mode = PlacementMode.Tower;
        SelectedTower = TowerType.Basic;
        Speed = GameSpeed.Normal;
        // note: AutoStartWaves is intentionally NOT reset — it's a player preference
    }
}
