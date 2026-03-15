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

    public float Range { get; private set; }
    public float Damage { get; private set; }
    public float FireRate { get; private set; }
    public Color Color { get; }

    public float BurnDps { get; private set; }
    public float BurnDuration { get; private set; }
    public float SplashRadius { get; private set; }

    public float VulnBonus { get; private set; }
    public float VulnDuration { get; private set; }

    public float SlowFactor { get; private set; }
    public float SlowDuration { get; private set; }

    public float GrinderBonusRatio { get; private set; }

    public int TotalInvested { get; private set; }
    public bool PlacedDuringPrep { get; set; } = true;

    public bool IsFieldTower => Type == TowerType.Tesla || Type == TowerType.Tachyon;
    public bool ShouldRotate => !IsFieldTower && Type != TowerType.Grinder;

    // -- aiming --
    private float _aimAngle;
    private float _targetAngle;
    private bool _hasTarget;
    private const float AimSpeed = 8f;

    /// <summary>The direction the sprite faces at rest (radians from +X axis).
    /// 0 = points right, -PI/2 = points up. Used to calculate correct rotation.</summary>
    private static float GetSpriteRestAngle(TowerType t) => t switch
    {
        // sprites whose barrel/nozzle points RIGHT at rest
        TowerType.Sniper => 0f,
        TowerType.Flame  => 0f,
        // all others point UP at rest
        _ => -MathF.PI / 2f
    };

    // -- pulse animation for field towers --
    private float _pulseTimer;
    private const float PulseDuration = 0.4f;

    private float _flameSoundCooldown;
    private float _cooldown;

    private static readonly Dictionary<TowerType, string> FireSounds = new()
    {
        { TowerType.Basic, "tower_gun" },
        { TowerType.Sniper, "tower_sniper" },
        { TowerType.Rapid, "tower_rapid" },
        { TowerType.Rocket, "tower_rocket" },
        { TowerType.Flame, "tower_flame" },
        { TowerType.Tesla, "tower_tesla" },
        { TowerType.Tachyon, "tower_tachyon" },
        { TowerType.Grinder, "tower_grinder" },
    };

    public Tower(int col, int row, TowerType type)
    {
        GridPos = new Point(col, row);
        WorldPos = Map.GridToWorld(col, row);
        Type = type;

        var stats = GetBaseStats(type);
        Range = stats.range;
        Damage = stats.damage;
        FireRate = stats.fireRate;
        Color = stats.color;
        BurnDps = stats.burnDps;
        BurnDuration = stats.burnDuration;
        SplashRadius = stats.splashRadius;
        VulnBonus = stats.vulnBonus;
        VulnDuration = stats.vulnDuration;
        SlowFactor = stats.slowFactor;
        SlowDuration = stats.slowDuration;
        GrinderBonusRatio = stats.grinderBonus;
        TotalInvested = GetCost(type);
    }

    public static int GetCost(TowerType t) => t switch
    {
        TowerType.Basic   => GameSettings.BasicTowerCost,
        TowerType.Sniper  => GameSettings.SniperTowerCost,
        TowerType.Rapid   => GameSettings.RapidTowerCost,
        TowerType.Rocket  => GameSettings.RocketTowerCost,
        TowerType.Flame   => GameSettings.FlameTowerCost,
        TowerType.Tesla   => GameSettings.TeslaTowerCost,
        TowerType.Tachyon => GameSettings.TachyonTowerCost,
        TowerType.Grinder => GameSettings.GrinderTowerCost,
        _ => 999
    };

    public static string GetName(TowerType t) => t switch
    {
        TowerType.Basic   => "Gun",
        TowerType.Sniper  => "Sniper",
        TowerType.Rapid   => "Rapid",
        TowerType.Rocket  => "Rocket",
        TowerType.Flame   => "Flame",
        TowerType.Tesla   => "Tesla",
        TowerType.Tachyon => "Tachyon",
        TowerType.Grinder => "Grinder",
        _ => "???"
    };

    private record struct TowerStats(
        float range, float damage, float fireRate, Color color,
        float burnDps, float burnDuration, float splashRadius,
        float vulnBonus, float vulnDuration,
        float slowFactor, float slowDuration,
        float grinderBonus);

    private static TowerStats GetBaseStats(TowerType t) => t switch
    {
        TowerType.Basic   => new(120f, 25f,  1.5f, new Color(0, 150, 255),
            0, 0, 0, 0, 0, 0, 0, 0),
        TowerType.Sniper  => new(220f, 80f,  0.5f, new Color(255, 100, 0),
            0, 0, 0, 0, 0, 0, 0, 0),
        TowerType.Rapid   => new(90f,  10f,  5.0f, new Color(0, 255, 100),
            0, 0, 0, 0, 0, 0, 0, 0),
        TowerType.Rocket  => new(280f, 60f,  0.3f, new Color(200, 50, 30),
            0, 0, GameSettings.RocketSplashRadius, 0, 0, 0, 0, 0),
        TowerType.Flame   => new(80f,  3f,   8.0f, new Color(255, 140, 0),
            GameSettings.BurnDamagePerSecond, GameSettings.BurnDuration, 0, 0, 0, 0, 0, 0),
        TowerType.Tesla   => new(110f, 15f,  0.8f, new Color(100, 220, 255),
            0, 0, 0, GameSettings.TeslaVulnerabilityBonus, GameSettings.TeslaVulnerabilityDuration, 0, 0, 0),
        TowerType.Tachyon => new(100f, 5f,   0.6f, new Color(220, 200, 50),
            0, 0, 0, 0, 0, GameSettings.TachyonSlowFactor, GameSettings.TachyonSlowDuration, 0),
        TowerType.Grinder => new(GameSettings.GrinderRange, 0f, 0f, new Color(200, 80, 80),
            0, 0, 0, 0, 0, 0, 0, GameSettings.GrinderBonusCreditRatio),
        _ => new(100f, 20f, 1f, Color.White, 0, 0, 0, 0, 0, 0, 0, 0)
    };

    public bool CanUpgrade => Level < GameSettings.MaxTowerLevel && Type != TowerType.Grinder;
    public int UpgradeCost => (int)(GetCost(Type) * GameSettings.UpgradeCostMultiplier);

    public void Upgrade()
    {
        if (!CanUpgrade) return;
        Level++;
        Damage *= GameSettings.UpgradeDamageMultiplier;
        Range *= GameSettings.UpgradeRangeMultiplier;
        FireRate *= GameSettings.UpgradeFireRateMultiplier;

        if (Type == TowerType.Flame) { BurnDps *= GameSettings.UpgradeDamageMultiplier; BurnDuration += 0.5f; }
        if (Type == TowerType.Rocket) SplashRadius *= 1.15f;
        if (Type == TowerType.Tesla) { VulnBonus += 0.1f; VulnDuration += 0.5f; }
        if (Type == TowerType.Tachyon) { SlowFactor *= 0.8f; SlowDuration += 0.5f; }

        TotalInvested += UpgradeCost;
    }

    public int FullRefundValue => TotalInvested;
    public int SellValue => (int)(TotalInvested * GameSettings.SellRefundRatio);

    private Enemy? FindNearest(List<Enemy> enemies, out float dist)
    {
        Enemy? best = null; dist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            float d = Vector2.Distance(WorldPos, e.Position);
            if (d <= Range && d < dist) { dist = d; best = e; }
        }
        return best;
    }

    private void UpdateAim(float dt, Vector2? targetPos)
    {
        if (!ShouldRotate) return;

        if (targetPos.HasValue)
        {
            var dir = targetPos.Value - WorldPos;
            // atan2 gives angle from +X axis to the target direction
            // subtract the sprite's rest angle so rotation=0 means "facing default direction"
            _targetAngle = MathF.Atan2(dir.Y, dir.X) - GetSpriteRestAngle(Type);
            _hasTarget = true;
        }
        else
        {
            _hasTarget = false;
        }

        if (_hasTarget)
        {
            float diff = WrapAngle(_targetAngle - _aimAngle);
            float maxStep = AimSpeed * dt;
            if (MathF.Abs(diff) <= maxStep) _aimAngle = _targetAngle;
            else _aimAngle += MathF.Sign(diff) * maxStep;
            _aimAngle = WrapAngle(_aimAngle);
        }
    }

    private static float WrapAngle(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    public void Update(GameTime gameTime, List<Enemy> enemies,
        List<Projectile> projectiles, List<FlameParticle> flames)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_pulseTimer > 0f) _pulseTimer -= dt;
        if (_flameSoundCooldown > 0f) _flameSoundCooldown -= dt;

        if (Type == TowerType.Grinder) return;

        var nearest = FindNearest(enemies, out _);
        UpdateAim(dt, nearest?.Position);

        _cooldown -= dt;
        if (_cooldown > 0) return;

        if (IsFieldTower)
        {
            bool hitAny = false;
            foreach (var e in enemies)
            {
                if (!e.IsAlive) continue;
                if (Vector2.Distance(WorldPos, e.Position) > Range) continue;
                hitAny = true;
                if (Damage > 0) e.TakeDamage(Damage, Type);
                if (Type == TowerType.Tesla) e.ApplyVulnerability(VulnBonus, VulnDuration);
                else if (Type == TowerType.Tachyon) e.ApplySlow(SlowFactor, SlowDuration);
            }
            if (hitAny)
            {
                _cooldown = 1f / FireRate; _pulseTimer = PulseDuration;
                if (FireSounds.TryGetValue(Type, out var s)) AudioManager.Instance.PlayVaried(s, 0.5f, 0.08f);
            }
        }
        else if (Type == TowerType.Flame)
        {
            if (nearest != null)
            {
                var dir = nearest.Position - WorldPos;
                if (dir.LengthSquared() > 0) dir.Normalize();
                int count = 2 + (Level > 2 ? 1 : 0);
                float ps = 140f + Level * 20f;
                float pl = Range / ps * 1.2f;
                for (int i = 0; i < count; i++)
                    flames.Add(new FlameParticle(WorldPos, dir, ps, pl, Damage, BurnDps, BurnDuration));
                _cooldown = 1f / FireRate;
                if (_flameSoundCooldown <= 0f)
                { AudioManager.Instance.PlayVaried("tower_flame", 0.4f, 0.1f, 0.15f); _flameSoundCooldown = 0.2f; }
            }
        }
        else
        {
            if (nearest != null)
            {
                var proj = new Projectile(WorldPos, nearest, Damage, Type, BurnDps, BurnDuration, SplashRadius);
                if (Type == TowerType.Rocket) proj.SetEnemyList(enemies);
                projectiles.Add(proj);
                _cooldown = 1f / FireRate;
                if (FireSounds.TryGetValue(Type, out var s)) AudioManager.Instance.PlayVaried(s, 0.5f, 0.08f);
            }
        }
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites, bool selected)
    {
        int pad = 4;
        int size = GameSettings.CellSize - pad;
        var tex = sprites.Towers[Type];

        if (ShouldRotate)
        {
            var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            var scale = new Vector2(size / (float)tex.Width, size / (float)tex.Height);
            sb.Draw(tex, WorldPos, null, Color.White, _aimAngle, origin, scale, SpriteEffects.None, 0f);
        }
        else
        {
            var rect = new Rectangle((int)(WorldPos.X - size / 2f), (int)(WorldPos.Y - size / 2f), size, size);
            sb.Draw(tex, rect, Color.White);
        }

        if (Level > 1)
        {
            int sx = (int)(WorldPos.X - size / 2f) + 3;
            int sy = (int)(WorldPos.Y + size / 2f) - 7;
            for (int i = 0; i < Level - 1; i++)
                sb.Draw(sprites.Pixel, new Rectangle(sx + i * 7, sy, 5, 5), Color.Gold);
        }

        if (IsFieldTower)
        {
            Color ac = Type == TowerType.Tesla ? new Color(100, 220, 255) : new Color(220, 200, 50);
            int d = (int)(Range * 2);
            sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), ac * 0.08f);
            if (_pulseTimer > 0f)
            {
                float p = 1f - (_pulseTimer / PulseDuration);
                float pr = Range * p; int pd = (int)(pr * 2);
                sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - pr), (int)(WorldPos.Y - pr), pd, pd), ac * (0.35f * (1f - p)));
            }
        }

        if (Type == TowerType.Grinder)
        {
            int d = (int)(Range * 2);
            sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), new Color(200, 80, 80) * 0.1f);
        }
    }

    public void DrawRange(SpriteBatch sb, Texture2D ring)
    {
        int d = (int)(Range * 2);
        Color rc = Type switch
        {
            TowerType.Tesla => new Color(100, 220, 255), TowerType.Tachyon => new Color(220, 200, 50),
            TowerType.Grinder => new Color(200, 80, 80), TowerType.Flame => new Color(255, 140, 0),
            _ => Color.White
        };
        sb.Draw(ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), rc * 0.15f);
    }
}
