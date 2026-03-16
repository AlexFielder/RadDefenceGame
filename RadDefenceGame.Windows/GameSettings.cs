namespace RadDefenceGame.Windows;

public enum Difficulty { Easy, Normal, Hard }

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

    // Player starting values (Normal)
    public const int StartingLives = 20;
    public const int StartingMoney = 100;
    public const int StartingWalls = 3;
    public const int KillBaseReward = 10;

    // Difficulty multipliers
    public static int GetStartingLives(Difficulty d) => d switch { Difficulty.Easy => 30, Difficulty.Hard => 10, _ => StartingLives };
    public static int GetStartingMoney(Difficulty d) => d switch { Difficulty.Easy => 200, Difficulty.Hard => 75, _ => StartingMoney };
    public static float GetEnemyHealthMultiplier(Difficulty d) => d switch { Difficulty.Easy => 0.7f, Difficulty.Hard => 1.4f, _ => 1f };
    public static float GetEnemySpeedMultiplier(Difficulty d) => d switch { Difficulty.Easy => 0.9f, Difficulty.Hard => 1.1f, _ => 1f };
    public static float GetRewardMultiplier(Difficulty d) => d switch { Difficulty.Easy => 1.3f, Difficulty.Hard => 0.75f, _ => 1f };

    // Tower costs
    public const int BasicTowerCost = 50;
    public const int SniperTowerCost = 100;
    public const int RapidTowerCost = 75;
    public const int RocketTowerCost = 150;
    public const int FlameTowerCost = 125;
    public const int TeslaTowerCost = 120;
    public const int TachyonTowerCost = 100;
    public const int GrinderTowerCost = 200;
    public const int RepairTowerCost = 300;

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

    // Tesla Array
    public const float TeslaVulnerabilityBonus = 0.25f;
    public const float TeslaVulnerabilityDuration = 3f;

    // Tachyon Warp
    public const float TachyonSlowFactor = 0.5f;
    public const float TachyonSlowDuration = 2.5f;

    // Parts Grinder
    public const float GrinderBonusCreditRatio = 0.5f;
    public const float GrinderRange = 100f;

    // Repair Tower
    public const float RepairTowerRange = 140f;
    public const float RepairPassiveHealPerSec = 5f;
    public const float RepairDroneSpeed = 200f;
    public const float RepairDroneRepairRate = 40f;
    public const float RepairDroneRange = 400f;
    public const float MaxTowerHealth = 200f;
    public const float RepairUpgradeCostMultiplier = 3f;
    public const float AutoRebuildCostMultiplier = 5f;

    // Wall grants per wave
    public const int WallGrantMin = 1;
    public const int WallGrantMax = 3;

    // Map generation
    public const int InitialWallCount = 60;

    // Speed system
    public static readonly float[] SpeedSteps = { 0.25f, 0.5f, 1f, 2f, 3f, 5f, 10f };
    public const int DefaultSpeedIndex = 2;

    // --- Enemy ability settings ---
    public const float TeleportCooldown = 4f;
    public const int TeleportWaypoints = 6;
    public const float MedicHealCooldown = 1f;
    public const float MedicHealAmount = 15f;
    public const float MedicHealRange = 80f;
    public const float HackerDisableCooldown = 5f;
    public const float HackerDisableDuration = 3f;
    public const float HackerDisableRange = 120f;
    public const float BlasterAttackCooldown = 3f;
    public const float BlasterDamageToTower = 20f;
    public const float BlasterAttackRange = 100f;
    public const float KamikazeExplosionRadius = 60f;
    public const float KamikazeTowerDamage = 80f;
    public const int SpreaderChildCount = 2;
    public const float SpreaderChildHealthRatio = 0.4f;
    public const float SpreaderChildSpeedBonus = 1.2f;
    public const int CentipedeSegmentCount = 3;
    public const float CentipedeSegmentHealthRatio = 0.25f;
    public const float CentipedeSegmentSpeedBonus = 1.3f;
    public const float SwarmHealthMultiplier = 0.3f;
    public const float SwarmSpeedMultiplier = 1.4f;
    public const float SwarmCountMultiplier = 3f;
    public const float SwarmRewardMultiplier = 0.4f;
}
