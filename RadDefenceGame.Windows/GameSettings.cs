namespace RadDefenceGame.Windows;

public static class GameSettings
{
    // Display
    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 720;

    // Grid
    public const int CellSize = 40;
    public const int GridCols = 32;
    public const int GridRows = 15;
    public const int UIHeight = 80;

    // Player starting values
    public const int StartingLives = 20;
    public const int StartingMoney = 100;
    public const int StartingWalls = 3;
    public const int KillBaseReward = 10;

    // Tower costs
    public const int BasicTowerCost = 50;
    public const int SniperTowerCost = 100;
    public const int RapidTowerCost = 75;
    public const int RocketTowerCost = 150;
    public const int FlameTowerCost = 125;
    public const int TeslaTowerCost = 120;
    public const int TachyonTowerCost = 100;
    public const int GrinderTowerCost = 200;

    // Selling
    public const float SellRefundRatio = 0.5f;

    // Upgrades per level
    public const float UpgradeCostMultiplier = 0.75f;
    public const float UpgradeDamageMultiplier = 1.4f;
    public const float UpgradeRangeMultiplier = 1.15f;
    public const float UpgradeFireRateMultiplier = 1.2f;
    public const int MaxTowerLevel = 3;

    // Flame burn DOT
    public const float BurnDamagePerSecond = 8f;
    public const float BurnDuration = 3f;

    // Rocket splash
    public const float RocketSplashRadius = 50f;

    // Tesla Array - vulnerability debuff
    public const float TeslaVulnerabilityBonus = 0.25f; // +25% damage from all sources
    public const float TeslaVulnerabilityDuration = 3f;

    // Tachyon Warp - slow debuff
    public const float TachyonSlowFactor = 0.5f; // 50% speed
    public const float TachyonSlowDuration = 2.5f;

    // Parts Grinder - bonus credits
    public const float GrinderBonusCreditRatio = 0.5f; // +50% credits from nearby kills
    public const float GrinderRange = 100f;

    // Wall grants per wave (min, max)
    public const int WallGrantMin = 1;
    public const int WallGrantMax = 3;

    // Map generation
    public const int InitialWallCount = 60;
}
