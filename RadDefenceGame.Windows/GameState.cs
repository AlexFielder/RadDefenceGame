namespace RadDefenceGame.Windows;

public class GameState
{
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    public int Lives { get; set; } = GameSettings.StartingLives;
    public int Money { get; set; } = GameSettings.StartingMoney;
    public int Walls { get; set; } = GameSettings.StartingWalls;
    public int Score { get; set; }

    public PlacementMode Mode { get; set; } = PlacementMode.Tower;
    public TowerType SelectedTower { get; set; } = TowerType.Basic;
    public bool AutoStartWaves { get; set; } = false;

    // -- speed system --
    private int _speedIndex = GameSettings.DefaultSpeedIndex;
    public float SpeedMultiplier => GameSettings.SpeedSteps[_speedIndex];
    public string SpeedLabel => $"{SpeedMultiplier}x";
    public bool IsNormalSpeed => _speedIndex == GameSettings.DefaultSpeedIndex;
    public void SpeedUp() { if (_speedIndex < GameSettings.SpeedSteps.Length - 1) _speedIndex++; }
    public void SlowDown() { if (_speedIndex > 0) _speedIndex--; }
    public void CycleSpeed() { _speedIndex = (_speedIndex + 1) % GameSettings.SpeedSteps.Length; }
    public void ResetSpeed() => _speedIndex = GameSettings.DefaultSpeedIndex;

    public bool IsGameOver => Lives <= 0;
    public bool CanAffordTower() => Money >= Tower.GetCost(SelectedTower);
    public bool HasWalls() => Walls > 0;
    public void SpendTower() => Money -= Tower.GetCost(SelectedTower);
    public void SpendWall() => Walls--;
    public void GrantWalls(int count) => Walls += count;
    public void EarnReward(int reward) { Money += reward; Score += reward; }
    public void LoseLife() => Lives = System.Math.Max(0, Lives - 1);

    public void ApplyDifficulty(Difficulty d)
    {
        Difficulty = d;
        Lives = GameSettings.GetStartingLives(d);
        Money = GameSettings.GetStartingMoney(d);
    }

    public void Reset()
    {
        Lives = GameSettings.GetStartingLives(Difficulty);
        Money = GameSettings.GetStartingMoney(Difficulty);
        Walls = GameSettings.StartingWalls;
        Score = 0;
        Mode = PlacementMode.Tower;
        SelectedTower = TowerType.Basic;
        _speedIndex = GameSettings.DefaultSpeedIndex;
    }
}
