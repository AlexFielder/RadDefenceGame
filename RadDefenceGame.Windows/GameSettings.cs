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
    // Hard mode tuning (playtester feedback: "fine tune the harder mode so it's actually playable"):
    // Softened from 10 lives / $75 / 1.4x HP / 1.1x speed / 0.75x rewards so Hard is punishing
    // but still beatable. Rewards bump in particular stops the early economy from stalling out.
    public static int GetStartingLives(Difficulty d) => d switch { Difficulty.Easy => 30, Difficulty.Hard => 12, _ => StartingLives };
    public static int GetStartingMoney(Difficulty d) => d switch { Difficulty.Easy => 200, Difficulty.Hard => 85, _ => StartingMoney };
    public static float GetEnemyHealthMultiplier(Difficulty d) => d switch { Difficulty.Easy => 0.7f, Difficulty.Hard => 1.25f, _ => 1f };
    public static float GetEnemySpeedMultiplier(Difficulty d) => d switch { Difficulty.Easy => 0.9f, Difficulty.Hard => 1.05f, _ => 1f };
    public static float GetRewardMultiplier(Difficulty d) => d switch { Difficulty.Easy => 1.3f, Difficulty.Hard => 0.85f, _ => 1f };

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
    public const int MortarTowerCost = 175;
    public const int ArtilleryTowerCost = 250;
    public const int DroneControllerCost = 220;
    public const int ElectricFenceCost = 100;

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

    // Mortar / Artillery (cone-of-fire, fixed facing, arcing shells)
    // Cone half-angle is radians on either side of the facing direction.
    public const float MortarRange = 200f;
    public const float MortarDamage = 40f;
    public const float MortarFireRate = 0.7f;
    public const float MortarSplashRadius = 60f;
    public const float MortarConeHalfAngle = 0.7f;        // ~40°
    public const float ArtilleryRange = 350f;
    public const float ArtilleryDamage = 100f;
    public const float ArtilleryFireRate = 0.25f;
    public const float ArtillerySplashRadius = 80f;
    public const float ArtilleryConeHalfAngle = 0.5f;     // ~28°
    public const float MortarShellSpeed = 260f;
    public const float ArtilleryShellSpeed = 300f;

    // Drone Controller — spawns attack drones that orbit the controller and dive on enemies
    public const float DroneControllerRange = 180f;
    public const int DroneControllerStartDrones = 1;
    public const int DroneControllerMaxDrones = 5;
    public const int DroneControllerMaxRangeUpgrades = 4;
    public const float AttackDroneSpeed = 220f;
    public const float AttackDroneDamage = 18f;
    public const float AttackDroneFireRate = 1.4f;
    public const float AttackDroneOrbitRadius = 28f;

    // Electric Fence — path-placed, takes DOT, blocks enemies, damages shields
    public const float ElectricFenceHealth = 160f;
    public const float ElectricFenceSelfDecayDps = 6f;     // dissipation when no enemies on it
    public const float ElectricFenceContactDecayDps = 18f; // additional decay per enemy in contact
    public const float ElectricFenceShieldDps = 25f;       // damage applied to enemy shields/HP while held
    public const float ElectricFenceBlockRadius = 18f;
    public const float ElectricFenceMaxLevel = 3;

    // Enemy shields — used by Shielded enemies and Electric Fence interaction
    public const float ShieldedEnemyShieldRatio = 0.6f;    // 60% of MaxHealth as shield

    // Wall grants per wave
    public const int WallGrantMin = 1;
    public const int WallGrantMax = 3;

    // Map generation
    public const int InitialWallCount = 60;

    // Speed system
    public static readonly float[] SpeedSteps = { 0.25f, 0.5f, 1f, 2f, 3f, 5f, 10f, 15f, 20f };
    public const int DefaultSpeedIndex = 2;
    // Above this multiplier we substep the simulation so projectiles don't skip
    // past enemies and waypoint distances stay sane.
    public const float SubstepSpeedThreshold = 10f;

    // Zone placement (free placement anywhere on wall cells instead of snapped to block centres)
    public const float ZoneMinTowerSpacing = 30f;

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
