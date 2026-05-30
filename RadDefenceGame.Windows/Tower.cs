namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public class Tower
{
    public Point GridPos { get; }
    public Vector2 WorldPos { get; }
    public TowerType Type { get; }
    public int Level { get; private set; } = 1;

    /// <summary>True when the tower was placed via Zone mode (free pixel position
    /// over a wall cell) rather than snapped to a block centre. Affects save
    /// serialization and grid-cell ownership semantics.</summary>
    public bool IsZonePlaced { get; }

    public float Range { get; private set; }
    public float Damage { get; private set; }
    public float FireRate { get; private set; }
    public Color Color { get; private set; }

    public float BurnDps { get; private set; }
    public float BurnDuration { get; private set; }
    public float SplashRadius { get; private set; }
    public float VulnBonus { get; private set; }
    public float VulnDuration { get; private set; }
    public float SlowFactor { get; private set; }
    public float SlowDuration { get; private set; }
    public float GrinderBonusRatio { get; private set; }

    // -- cone-of-fire (Mortar / Artillery)
    public float ConeFacing { get; private set; }      // world-space angle the cone points along
    public float ConeHalfAngle { get; private set; }   // half-width of the cone, radians
    public float ShellSpeed { get; private set; }

    // -- Drone Controller upgrade pathing (Range OR Count, mutually exclusive once chosen)
    public UpgradePath ChosenPath { get; private set; } = UpgradePath.None;
    public int RangeUpgradesApplied { get; private set; }
    public int CountUpgradesApplied { get; private set; }

    public int TotalInvested { get; private set; }
    public bool PlacedDuringPrep { get; set; } = true;

    public bool IsFieldTower => Type == TowerType.Tesla || Type == TowerType.Tachyon;
    /// <summary>Towers that visually rotate to track their target.</summary>
    public bool ShouldRotate => !IsFieldTower
        && Type != TowerType.Grinder && Type != TowerType.Repair
        && Type != TowerType.Mortar && Type != TowerType.Artillery
        && Type != TowerType.DroneController && Type != TowerType.ElectricFence;
    public bool Is2x2 => Type == TowerType.Repair;
    public bool IsConeTower => Type == TowerType.Mortar || Type == TowerType.Artillery;
    public bool IsPathPlaced => Type == TowerType.ElectricFence;

    public float DisabledTimer { get; set; }
    public bool IsDisabled => DisabledTimer > 0f;

    public float TowerHealth { get; set; } = GameSettings.MaxTowerHealth;
    public bool IsDestroyed => TowerHealth <= 0f;
    public void TakeTowerDamage(float amount) { TowerHealth -= amount; if (TowerHealth < 0f) TowerHealth = 0f; }

    public int DroneCount =>
        Type == TowerType.Repair      ? Level :
        Type == TowerType.DroneController ? GameSettings.DroneControllerStartDrones + CountUpgradesApplied :
                                            0;
    public bool AutoRebuildEnabled { get; set; } = false;

    private float _aimAngle;
    private float _targetAngle;
    private bool _hasTarget;
    private const float AimSpeed = 8f;

    private static float GetSpriteRestAngle(TowerType t) => t switch
    {
        TowerType.Sniper => -MathF.PI * 3 / 4, TowerType.Flame => -MathF.PI, _ => -MathF.PI / 2f
    };

    private float _pulseTimer;
    private const float PulseDuration = 0.4f;
    private float _flameSoundCooldown;
    private float _cooldown;

    private static readonly Dictionary<TowerType, string> FireSounds = new()
    {
        { TowerType.Basic, "tower_gun" }, { TowerType.Sniper, "tower_sniper" },
        { TowerType.Rapid, "tower_rapid" }, { TowerType.Rocket, "tower_rocket" },
        { TowerType.Flame, "tower_flame" }, { TowerType.Tesla, "tower_tesla" },
        { TowerType.Tachyon, "tower_tachyon" }, { TowerType.Grinder, "tower_grinder" },
        { TowerType.Mortar, "tower_mortar_fire" }, { TowerType.Artillery, "tower_artillery_fire" },
    };

    public Tower(int col, int row, TowerType type)
    {
        GridPos = new Point(col, row);
        if (type == TowerType.Repair)
            WorldPos = new Vector2(col * GameSettings.CellSize + GameSettings.CellSize,
                row * GameSettings.CellSize + GameSettings.CellSize + GameSettings.UIHeight);
        else WorldPos = Map.GridToWorld(col, row);
        Type = type;
        IsZonePlaced = false;
        InitFromStats(GetBaseStats(type));
        TotalInvested = GetCost(type);
    }

    /// <summary>Zone-mode constructor: uses an explicit world position (must still be
    /// anchored to a wall cell via <paramref name="col"/>/<paramref name="row"/> for
    /// save/load, but WorldPos is free within that cell). Not valid for Repair (2x2).</summary>
    public Tower(int col, int row, TowerType type, Vector2 worldPos)
    {
        GridPos = new Point(col, row);
        WorldPos = worldPos;
        Type = type;
        IsZonePlaced = true;
        InitFromStats(GetBaseStats(type));
        TotalInvested = GetCost(type);
    }

    private void InitFromStats(TowerStats s)
    {
        Range = s.range; Damage = s.damage; FireRate = s.fireRate; Color = s.color;
        BurnDps = s.burnDps; BurnDuration = s.burnDuration; SplashRadius = s.splashRadius;
        VulnBonus = s.vulnBonus; VulnDuration = s.vulnDuration;
        SlowFactor = s.slowFactor; SlowDuration = s.slowDuration; GrinderBonusRatio = s.grinderBonus;
        ConeHalfAngle = s.coneHalfAngle; ShellSpeed = s.shellSpeed;
        ConeFacing = 0f;
        if (Type == TowerType.ElectricFence) TowerHealth = GameSettings.ElectricFenceHealth;
    }

    /// <summary>Used at placement time by Mortar/Artillery and ElectricFence to lock their
    /// orientation toward the nearest path waypoint. For ElectricFence this is informational
    /// only; for cone towers it determines what they can hit.</summary>
    public void SetFacing(float angle) { ConeFacing = angle; }

    public static int GetCost(TowerType t) => t switch
    {
        TowerType.Basic => GameSettings.BasicTowerCost, TowerType.Sniper => GameSettings.SniperTowerCost,
        TowerType.Rapid => GameSettings.RapidTowerCost, TowerType.Rocket => GameSettings.RocketTowerCost,
        TowerType.Flame => GameSettings.FlameTowerCost, TowerType.Tesla => GameSettings.TeslaTowerCost,
        TowerType.Tachyon => GameSettings.TachyonTowerCost, TowerType.Grinder => GameSettings.GrinderTowerCost,
        TowerType.Repair => GameSettings.RepairTowerCost,
        TowerType.Mortar => GameSettings.MortarTowerCost,
        TowerType.Artillery => GameSettings.ArtilleryTowerCost,
        TowerType.DroneController => GameSettings.DroneControllerCost,
        TowerType.ElectricFence => GameSettings.ElectricFenceCost,
        _ => 999
    };

    public static string GetName(TowerType t) => t switch
    {
        TowerType.Basic => "Gun", TowerType.Sniper => "Sniper", TowerType.Rapid => "Rapid",
        TowerType.Rocket => "Rocket", TowerType.Flame => "Flame", TowerType.Tesla => "Tesla",
        TowerType.Tachyon => "Tachyon", TowerType.Grinder => "Grinder", TowerType.Repair => "Repair",
        TowerType.Mortar => "Mortar", TowerType.Artillery => "Artillery",
        TowerType.DroneController => "Drone Ctrl", TowerType.ElectricFence => "E-Fence", _ => "???"
    };

    private record struct TowerStats(float range, float damage, float fireRate, Color color,
        float burnDps, float burnDuration, float splashRadius,
        float vulnBonus, float vulnDuration, float slowFactor, float slowDuration, float grinderBonus,
        float coneHalfAngle, float shellSpeed);

    private static TowerStats GetBaseStats(TowerType t) => t switch
    {
        TowerType.Basic   => new(120f,25f,1.5f, new Color(0,150,255), 0,0,0, 0,0, 0,0, 0, 0,0),
        TowerType.Sniper  => new(220f,80f,0.5f, new Color(255,100,0), 0,0,0, 0,0, 0,0, 0, 0,0),
        TowerType.Rapid   => new(90f,10f,5.0f, new Color(0,255,100), 0,0,0, 0,0, 0,0, 0, 0,0),
        TowerType.Rocket  => new(280f,60f,0.3f, new Color(200,50,30), 0,0,GameSettings.RocketSplashRadius, 0,0, 0,0, 0, 0,0),
        TowerType.Flame   => new(80f,3f,8.0f, new Color(255,140,0), GameSettings.BurnDamagePerSecond,GameSettings.BurnDuration,0, 0,0, 0,0, 0, 0,0),
        TowerType.Tesla   => new(110f,15f,0.8f, new Color(100,220,255), 0,0,0, GameSettings.TeslaVulnerabilityBonus,GameSettings.TeslaVulnerabilityDuration, 0,0, 0, 0,0),
        TowerType.Tachyon => new(100f,5f,0.6f, new Color(220,200,50), 0,0,0, 0,0, GameSettings.TachyonSlowFactor,GameSettings.TachyonSlowDuration, 0, 0,0),
        TowerType.Grinder => new(GameSettings.GrinderRange,0f,0f, new Color(200,80,80), 0,0,0, 0,0, 0,0, GameSettings.GrinderBonusCreditRatio, 0,0),
        TowerType.Repair  => new(GameSettings.RepairTowerRange,0f,0f, new Color(80,220,80), 0,0,0, 0,0, 0,0, 0, 0,0),
        TowerType.Mortar  => new(GameSettings.MortarRange, GameSettings.MortarDamage, GameSettings.MortarFireRate,
                                 new Color(150,120,90), 0,0, GameSettings.MortarSplashRadius, 0,0, 0,0, 0,
                                 GameSettings.MortarConeHalfAngle, GameSettings.MortarShellSpeed),
        TowerType.Artillery => new(GameSettings.ArtilleryRange, GameSettings.ArtilleryDamage, GameSettings.ArtilleryFireRate,
                                 new Color(180,140,60), 0,0, GameSettings.ArtillerySplashRadius, 0,0, 0,0, 0,
                                 GameSettings.ArtilleryConeHalfAngle, GameSettings.ArtilleryShellSpeed),
        TowerType.DroneController => new(GameSettings.DroneControllerRange, 0f, 0f,
                                 new Color(120,180,255), 0,0,0, 0,0, 0,0, 0, 0,0),
        TowerType.ElectricFence => new(GameSettings.ElectricFenceBlockRadius, 0f, 0f,
                                 new Color(255,220,80), 0,0,0, 0,0, 0,0, 0, 0,0),
        _ => new(100f,20f,1f, Color.White, 0,0,0, 0,0, 0,0, 0, 0,0)
    };

    // -- upgrade rules --

    // Repair: uncapped, 3x cost. Grinder: none. Fence: capped at MaxLevel,
    // each upgrade adds back tower health. DroneController has split paths.
    public bool CanUpgrade
    {
        get
        {
            if (Type == TowerType.Grinder) return false;
            if (Type == TowerType.Repair) return true;
            if (Type == TowerType.DroneController)
                return CanUpgradeRange || CanUpgradeCount;
            return Level < GameSettings.MaxTowerLevel;
        }
    }

    public bool CanUpgradeRange => Type == TowerType.DroneController
        && (ChosenPath == UpgradePath.None || ChosenPath == UpgradePath.Range)
        && RangeUpgradesApplied < GameSettings.DroneControllerMaxRangeUpgrades;

    public bool CanUpgradeCount => Type == TowerType.DroneController
        && (ChosenPath == UpgradePath.None || ChosenPath == UpgradePath.Count)
        && DroneCount < GameSettings.DroneControllerMaxDrones;

    public int UpgradeCost
    {
        get
        {
            if (Type == TowerType.Repair)
            {
                // 300 * 3^(level-1): Lv1->2=$900, 2->3=$2700, 3->4=$8100, 4->5=$24300 ...
                int cost = GameSettings.RepairTowerCost;
                for (int i = 1; i < Level; i++) cost *= (int)GameSettings.RepairUpgradeCostMultiplier;
                return cost;
            }
            return (int)(GetCost(Type) * GameSettings.UpgradeCostMultiplier);
        }
    }

    public void Upgrade()
    {
        if (!CanUpgrade) return;
        int cost = UpgradeCost;
        Level++;
        if (Type == TowerType.Repair) { Range *= 1.25f; }
        else if (Type == TowerType.DroneController)
        {
            // Default Upgrade() picks whichever single track is still legal — if both are
            // available it favours Range (callers who care use UpgradeRange/UpgradeCount).
            if (CanUpgradeRange) UpgradeRangeInternal();
            else if (CanUpgradeCount) UpgradeCountInternal();
        }
        else if (Type == TowerType.ElectricFence)
        {
            // Fence upgrades restore + extend health and increase shield-damage radius.
            TowerHealth = GameSettings.ElectricFenceHealth * (1f + 0.4f * (Level - 1));
            Range *= 1.15f;
        }
        else
        {
            Damage *= GameSettings.UpgradeDamageMultiplier; Range *= GameSettings.UpgradeRangeMultiplier;
            FireRate *= GameSettings.UpgradeFireRateMultiplier;
            if (Type == TowerType.Flame) { BurnDps *= GameSettings.UpgradeDamageMultiplier; BurnDuration += 0.5f; }
            if (Type == TowerType.Rocket) SplashRadius *= 1.15f;
            if (Type == TowerType.Mortar || Type == TowerType.Artillery) SplashRadius *= 1.12f;
            if (Type == TowerType.Tesla) { VulnBonus += 0.1f; VulnDuration += 0.5f; }
            if (Type == TowerType.Tachyon) { SlowFactor *= 0.8f; SlowDuration += 0.5f; }
        }
        TotalInvested += cost;
    }

    /// <summary>Drone Controller: extend per-drone attack range. Locks the upgrade path
    /// to Range — future upgrades cannot add drones.</summary>
    public void UpgradeRange()
    {
        if (!CanUpgradeRange) return;
        int cost = UpgradeCost;
        Level++;
        UpgradeRangeInternal();
        TotalInvested += cost;
    }

    /// <summary>Drone Controller: add another orbiting drone. Locks the upgrade path
    /// to Count — future upgrades cannot extend range.</summary>
    public void UpgradeCount()
    {
        if (!CanUpgradeCount) return;
        int cost = UpgradeCost;
        Level++;
        UpgradeCountInternal();
        TotalInvested += cost;
    }

    private void UpgradeRangeInternal()
    {
        ChosenPath = UpgradePath.Range;
        RangeUpgradesApplied++;
        Range *= GameSettings.UpgradeRangeMultiplier;
    }

    private void UpgradeCountInternal()
    {
        ChosenPath = UpgradePath.Count;
        CountUpgradesApplied++;
        // No range change — additional drones are the upgrade.
    }

    public int FullRefundValue => TotalInvested;
    public int SellValue => (int)(TotalInvested * GameSettings.SellRefundRatio);

    public void UpdatePassiveHeal(float dt, List<Tower> towers)
    {
        if (Type != TowerType.Repair) return;
        foreach (var t in towers)
        {
            if (t == this || t.IsDestroyed || t.TowerHealth >= GameSettings.MaxTowerHealth) continue;
            // Repair Tower never heals an Electric Fence — fences are meant to decay.
            if (t.Type == TowerType.ElectricFence) continue;
            if (Vector2.Distance(WorldPos, t.WorldPos) <= Range)
                t.TowerHealth = MathF.Min(t.TowerHealth + GameSettings.RepairPassiveHealPerSec * dt, GameSettings.MaxTowerHealth);
        }
    }

    private Enemy? FindNearest(List<Enemy> enemies, out float dist)
    {
        Enemy? best = null; dist = float.MaxValue;
        foreach (var e in enemies)
        { if (!e.IsAlive) continue; float d = Vector2.Distance(WorldPos, e.Position); if (d <= Range && d < dist) { dist = d; best = e; } }
        return best;
    }

    /// <summary>Cone-tower target finder: requires the enemy to be inside the firing arc.
    /// Returns the nearest qualifying enemy.</summary>
    private Enemy? FindInCone(List<Enemy> enemies, out float dist)
    {
        Enemy? best = null; dist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            var diff = e.Position - WorldPos;
            float d = diff.Length();
            if (d > Range) continue;
            if (d < 1f) continue;
            float ang = MathF.Atan2(diff.Y, diff.X);
            float delta = WrapAngle(ang - ConeFacing);
            if (MathF.Abs(delta) > ConeHalfAngle) continue;
            if (d < dist) { dist = d; best = e; }
        }
        return best;
    }

    private void UpdateAim(float dt, Vector2? targetPos)
    {
        if (!ShouldRotate) return;
        if (targetPos.HasValue) { var dir = targetPos.Value - WorldPos; _targetAngle = MathF.Atan2(dir.Y, dir.X) - GetSpriteRestAngle(Type); _hasTarget = true; }
        else _hasTarget = false;
        if (_hasTarget) { float diff = WrapAngle(_targetAngle - _aimAngle); float ms = AimSpeed * dt;
            if (MathF.Abs(diff) <= ms) _aimAngle = _targetAngle; else _aimAngle += MathF.Sign(diff) * ms; _aimAngle = WrapAngle(_aimAngle); }
    }

    private static float WrapAngle(float a) { while (a > MathF.PI) a -= MathF.Tau; while (a < -MathF.PI) a += MathF.Tau; return a; }

    /// <summary>Standard Update used by every tower that isn't an Electric Fence or
    /// a Mortar/Artillery (which need access to the shells list).</summary>
    public void Update(GameTime gameTime, List<Enemy> enemies, List<Projectile> projectiles, List<FlameParticle> flames)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_pulseTimer > 0f) _pulseTimer -= dt;
        if (_flameSoundCooldown > 0f) _flameSoundCooldown -= dt;
        if (DisabledTimer > 0f) DisabledTimer -= dt;
        if (Type == TowerType.Grinder || Type == TowerType.Repair) return;
        // Cone/fence/drone-controller all run in specialised update paths from Game1.
        if (IsConeTower || Type == TowerType.ElectricFence || Type == TowerType.DroneController) return;
        var nearest = FindNearest(enemies, out _); UpdateAim(dt, nearest?.Position);
        if (IsDisabled) return;
        _cooldown -= dt; if (_cooldown > 0) return;

        if (IsFieldTower)
        {
            bool hitAny = false;
            foreach (var e in enemies) { if (!e.IsAlive || Vector2.Distance(WorldPos, e.Position) > Range) continue; hitAny = true;
                if (Damage > 0) e.TakeDamage(Damage, Type);
                if (Type == TowerType.Tesla) e.ApplyVulnerability(VulnBonus, VulnDuration);
                else if (Type == TowerType.Tachyon) e.ApplySlow(SlowFactor, SlowDuration); }
            if (hitAny) { _cooldown = 1f / FireRate; _pulseTimer = PulseDuration;
                if (FireSounds.TryGetValue(Type, out var snd)) AudioManager.Instance.PlayVaried(snd, 0.5f, 0.08f); }
        }
        else if (Type == TowerType.Flame)
        {
            if (nearest != null) { var dir = nearest.Position - WorldPos; if (dir.LengthSquared() > 0) dir.Normalize();
                int count = 2 + (Level > 2 ? 1 : 0); float ps = 140f + Level * 20f; float pl = Range / ps * 1.2f;
                for (int i = 0; i < count; i++) flames.Add(new FlameParticle(WorldPos, dir, ps, pl, Damage, BurnDps, BurnDuration));
                _cooldown = 1f / FireRate;
                if (_flameSoundCooldown <= 0f) { AudioManager.Instance.PlayVaried("tower_flame", 0.4f, 0.1f, 0.15f); _flameSoundCooldown = 0.2f; } }
        }
        else
        {
            if (nearest != null) { var proj = new Projectile(WorldPos, nearest, Damage, Type, BurnDps, BurnDuration, SplashRadius);
                if (Type == TowerType.Rocket) proj.SetEnemyList(enemies); projectiles.Add(proj); _cooldown = 1f / FireRate;
                if (FireSounds.TryGetValue(Type, out var snd)) AudioManager.Instance.PlayVaried(snd, 0.5f, 0.08f); }
        }
    }

    /// <summary>Mortar / Artillery update — fires lobbed shells at fixed-cone targets.</summary>
    public void UpdateCone(GameTime gameTime, List<Enemy> enemies, List<MortarShell> shells)
    {
        if (!IsConeTower) return;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (DisabledTimer > 0f) DisabledTimer -= dt;
        if (IsDisabled) return;
        _cooldown -= dt; if (_cooldown > 0f) return;
        var target = FindInCone(enemies, out _);
        if (target == null) return;
        shells.Add(new MortarShell(WorldPos, target.Position, Damage, SplashRadius, ShellSpeed, Type, enemies));
        _cooldown = 1f / FireRate;
        if (FireSounds.TryGetValue(Type, out var snd)) AudioManager.Instance.PlayVaried(snd, 0.45f, 0.1f);
    }

    /// <summary>Electric Fence update — sits on a path tile and damages any enemy in contact
    /// while holding them in place. The fence only loses health while an enemy is touching
    /// it; an undisturbed fence will sit there indefinitely.</summary>
    public void UpdateFence(GameTime gameTime, List<Enemy> enemies)
    {
        if (Type != TowerType.ElectricFence || IsDestroyed) return;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        int contacts = 0;
        bool damagedShield = false;
        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            if (Vector2.Distance(WorldPos, e.Position) > Range) continue;
            contacts++;
            // hold the enemy in place — set a block-timer slightly longer than one frame
            e.Block(0.08f);
            // pour electric damage into the shield (spills into HP once the shield is gone)
            float before = e.Shield + e.Health;
            e.TakeShieldDamage(GameSettings.ElectricFenceShieldDps * dt, TowerType.ElectricFence);
            if (e.Shield + e.Health < before) damagedShield = true;
        }
        // Only burns down when enemies are pushing against it.
        if (contacts > 0)
        {
            TakeTowerDamage(GameSettings.ElectricFenceContactDecayDps * contacts * dt);
            if (damagedShield) AudioManager.Instance.PlayVaried("tower_electric_fence_zap", 0.35f, 0.1f, 0.18f);
        }
        if (IsDestroyed) AudioManager.Instance.Play("tower_electric_fence_break", 0.6f);
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites, bool selected)
    {
        var tex = sprites.Towers[Type];
        Color tint = IsDisabled ? new Color(100, 50, 50) : Color.White;
        if (TowerHealth < 150f && !IsDisabled && Type != TowerType.ElectricFence)
            tint = Color.Lerp(Color.White, new Color(255, 100, 100), 1f - (TowerHealth / 150f));

        if (Type == TowerType.Repair)
        {
            int size = GameSettings.CellSize * 2 - 4;
            var rect = new Rectangle((int)(WorldPos.X - size / 2f), (int)(WorldPos.Y - size / 2f), size, size);
            sb.Draw(tex, rect, tint);
            int d = (int)(Range * 2);
            sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), new Color(80, 220, 80) * 0.08f);

            // auto-rebuild indicator
            if (AutoRebuildEnabled)
            {
                int ax = (int)(WorldPos.X + size / 2f) - 12, ay = (int)(WorldPos.Y - size / 2f) + 2;
                sb.Draw(sprites.Pixel, new Rectangle(ax, ay, 10, 10), new Color(40, 180, 40) * 0.7f);
                sb.Draw(sprites.Pixel, new Rectangle(ax + 2, ay + 2, 6, 6), new Color(80, 255, 80));
            }

            // level stars (max 5 gold dots, then red overflow dot)
            if (Level > 1)
            {
                int stars = Math.Min(Level - 1, 5);
                int sx = (int)(WorldPos.X - size / 2f) + 5, sy = (int)(WorldPos.Y + size / 2f) - 9;
                for (int i = 0; i < stars; i++) sb.Draw(sprites.Pixel, new Rectangle(sx + i * 9, sy, 7, 7), Color.Gold);
                if (Level > 6) sb.Draw(sprites.Pixel, new Rectangle(sx + stars * 9 + 2, sy + 1, 5, 5), Color.OrangeRed);
            }
        }
        else
        {
            int pad = 4; int size = GameSettings.CellSize - pad;
            if (ShouldRotate)
            {
                var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                var scale = new Vector2(size / (float)tex.Width, size / (float)tex.Height);
                sb.Draw(tex, WorldPos, null, tint, _aimAngle, origin, scale, SpriteEffects.None, 0f);
            }
            else if (IsConeTower)
            {
                // Cone towers face their fixed direction. Draw rotated to that angle (with sprite rest offset).
                var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                var scale = new Vector2(size / (float)tex.Width, size / (float)tex.Height);
                float rot = ConeFacing - GetSpriteRestAngle(Type);
                sb.Draw(tex, WorldPos, null, tint, rot, origin, scale, SpriteEffects.None, 0f);
            }
            else
            {
                var rect = new Rectangle((int)(WorldPos.X - size / 2f), (int)(WorldPos.Y - size / 2f), size, size);
                Color drawTint = tint;
                if (Type == TowerType.ElectricFence)
                {
                    float frac = TowerHealth / GameSettings.ElectricFenceHealth;
                    drawTint = Color.Lerp(new Color(120, 60, 0), Color.White, MathF.Max(0.15f, frac));
                }
                sb.Draw(tex, rect, drawTint);
            }
            if (Level > 1) { int sx = (int)(WorldPos.X - size / 2f) + 3; int sy = (int)(WorldPos.Y + size / 2f) - 7;
                for (int i = 0; i < Level - 1; i++) sb.Draw(sprites.Pixel, new Rectangle(sx + i * 7, sy, 5, 5), Color.Gold); }
            if (IsFieldTower) { Color ac = Type == TowerType.Tesla ? new Color(100, 220, 255) : new Color(220, 200, 50);
                int d = (int)(Range * 2); sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), ac * 0.08f);
                if (_pulseTimer > 0f) { float p = 1f - (_pulseTimer / PulseDuration); float pr = Range * p; int pd = (int)(pr * 2);
                    sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - pr), (int)(WorldPos.Y - pr), pd, pd), ac * (0.35f * (1f - p))); } }
            if (Type == TowerType.Grinder) { int d = (int)(Range * 2);
                sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), new Color(200, 80, 80) * 0.1f); }
            if (Type == TowerType.DroneController) { int d = (int)(Range * 2);
                sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), new Color(120, 180, 255) * 0.08f); }
            if (Type == TowerType.ElectricFence)
            {
                // arc indicator above the fence
                float frac = TowerHealth / GameSettings.ElectricFenceHealth;
                int barW = GameSettings.CellSize - 8;
                int bx = (int)(WorldPos.X - barW / 2f);
                int by = (int)(WorldPos.Y - GameSettings.CellSize / 2f);
                sb.Draw(sprites.Pixel, new Rectangle(bx, by, barW, 3), new Color(40, 30, 0));
                sb.Draw(sprites.Pixel, new Rectangle(bx, by, (int)(barW * MathF.Max(0f, frac)), 3),
                    new Color(255, 220, 80));
            }
        }
    }

    public void DrawRange(SpriteBatch sb, Texture2D ring)
    {
        int d = (int)(Range * 2);
        Color rc = Type switch
        {
            TowerType.Tesla => new Color(100, 220, 255),
            TowerType.Tachyon => new Color(220, 200, 50),
            TowerType.Grinder => new Color(200, 80, 80),
            TowerType.Flame => new Color(255, 140, 0),
            TowerType.Repair => new Color(80, 220, 80),
            TowerType.Mortar => new Color(150, 120, 90),
            TowerType.Artillery => new Color(180, 140, 60),
            TowerType.DroneController => new Color(120, 180, 255),
            TowerType.ElectricFence => new Color(255, 220, 80),
            _ => Color.White
        };
        sb.Draw(ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), rc * 0.15f);
    }

    /// <summary>Cone-of-fire shading. Called by Game1 for Mortar/Artillery towers since the
    /// effect needs a pixel texture rather than the ring used by DrawRange.</summary>
    public void DrawConeOverlay(SpriteBatch sb, Texture2D pixel, Color rc, float alpha)
    {
        if (!IsConeTower) return;
        int steps = 14;
        float angStart = ConeFacing - ConeHalfAngle;
        float angEnd = ConeFacing + ConeHalfAngle;
        for (int i = 0; i <= steps; i++)
        {
            float a = MathHelper.Lerp(angStart, angEnd, i / (float)steps);
            for (float r = 8f; r <= Range; r += 8f)
            {
                int x = (int)(WorldPos.X + MathF.Cos(a) * r);
                int y = (int)(WorldPos.Y + MathF.Sin(a) * r);
                float a2 = alpha * (1f - r / Range);
                if (a2 < 0.02f) continue;
                sb.Draw(pixel, new Rectangle(x - 1, y - 1, 3, 3), rc * a2);
            }
        }
    }
}
      