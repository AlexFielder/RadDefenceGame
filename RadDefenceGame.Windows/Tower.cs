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
    public bool ShouldRotate => !IsFieldTower && Type != TowerType.Grinder && Type != TowerType.Repair;
    public bool Is2x2 => Type == TowerType.Repair;

    public float DisabledTimer { get; set; }
    public bool IsDisabled => DisabledTimer > 0f;

    public float TowerHealth { get; set; } = GameSettings.MaxTowerHealth;
    public bool IsDestroyed => TowerHealth <= 0f;
    public void TakeTowerDamage(float amount) { TowerHealth -= amount; if (TowerHealth < 0f) TowerHealth = 0f; }

    public int DroneCount => Type == TowerType.Repair ? Level : 0;
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
    };

    public Tower(int col, int row, TowerType type)
    {
        GridPos = new Point(col, row);
        if (type == TowerType.Repair)
            WorldPos = new Vector2(col * GameSettings.CellSize + GameSettings.CellSize,
                row * GameSettings.CellSize + GameSettings.CellSize + GameSettings.UIHeight);
        else WorldPos = Map.GridToWorld(col, row);
        Type = type;
        var s = GetBaseStats(type);
        Range = s.range; Damage = s.damage; FireRate = s.fireRate; Color = s.color;
        BurnDps = s.burnDps; BurnDuration = s.burnDuration; SplashRadius = s.splashRadius;
        VulnBonus = s.vulnBonus; VulnDuration = s.vulnDuration;
        SlowFactor = s.slowFactor; SlowDuration = s.slowDuration; GrinderBonusRatio = s.grinderBonus;
        TotalInvested = GetCost(type);
    }

    public static int GetCost(TowerType t) => t switch
    {
        TowerType.Basic => GameSettings.BasicTowerCost, TowerType.Sniper => GameSettings.SniperTowerCost,
        TowerType.Rapid => GameSettings.RapidTowerCost, TowerType.Rocket => GameSettings.RocketTowerCost,
        TowerType.Flame => GameSettings.FlameTowerCost, TowerType.Tesla => GameSettings.TeslaTowerCost,
        TowerType.Tachyon => GameSettings.TachyonTowerCost, TowerType.Grinder => GameSettings.GrinderTowerCost,
        TowerType.Repair => GameSettings.RepairTowerCost, _ => 999
    };

    public static string GetName(TowerType t) => t switch
    {
        TowerType.Basic => "Gun", TowerType.Sniper => "Sniper", TowerType.Rapid => "Rapid",
        TowerType.Rocket => "Rocket", TowerType.Flame => "Flame", TowerType.Tesla => "Tesla",
        TowerType.Tachyon => "Tachyon", TowerType.Grinder => "Grinder", TowerType.Repair => "Repair", _ => "???"
    };

    private record struct TowerStats(float range, float damage, float fireRate, Color color,
        float burnDps, float burnDuration, float splashRadius,
        float vulnBonus, float vulnDuration, float slowFactor, float slowDuration, float grinderBonus);

    private static TowerStats GetBaseStats(TowerType t) => t switch
    {
        TowerType.Basic   => new(120f,25f,1.5f, new Color(0,150,255), 0,0,0, 0,0, 0,0, 0),
        TowerType.Sniper  => new(220f,80f,0.5f, new Color(255,100,0), 0,0,0, 0,0, 0,0, 0),
        TowerType.Rapid   => new(90f,10f,5.0f, new Color(0,255,100), 0,0,0, 0,0, 0,0, 0),
        TowerType.Rocket  => new(280f,60f,0.3f, new Color(200,50,30), 0,0,GameSettings.RocketSplashRadius, 0,0, 0,0, 0),
        TowerType.Flame   => new(80f,3f,8.0f, new Color(255,140,0), GameSettings.BurnDamagePerSecond,GameSettings.BurnDuration,0, 0,0, 0,0, 0),
        TowerType.Tesla   => new(110f,15f,0.8f, new Color(100,220,255), 0,0,0, GameSettings.TeslaVulnerabilityBonus,GameSettings.TeslaVulnerabilityDuration, 0,0, 0),
        TowerType.Tachyon => new(100f,5f,0.6f, new Color(220,200,50), 0,0,0, 0,0, GameSettings.TachyonSlowFactor,GameSettings.TachyonSlowDuration, 0),
        TowerType.Grinder => new(GameSettings.GrinderRange,0f,0f, new Color(200,80,80), 0,0,0, 0,0, 0,0, GameSettings.GrinderBonusCreditRatio),
        TowerType.Repair  => new(GameSettings.RepairTowerRange,0f,0f, new Color(80,220,80), 0,0,0, 0,0, 0,0, 0),
        _ => new(100f,20f,1f, Color.White, 0,0,0, 0,0, 0,0, 0)
    };

    // Repair: uncapped, 3x cost. Others: capped at MaxTowerLevel. Grinder: none.
    public bool CanUpgrade => Type == TowerType.Repair || (Level < GameSettings.MaxTowerLevel && Type != TowerType.Grinder);

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
        else
        {
            Damage *= GameSettings.UpgradeDamageMultiplier; Range *= GameSettings.UpgradeRangeMultiplier;
            FireRate *= GameSettings.UpgradeFireRateMultiplier;
            if (Type == TowerType.Flame) { BurnDps *= GameSettings.UpgradeDamageMultiplier; BurnDuration += 0.5f; }
            if (Type == TowerType.Rocket) SplashRadius *= 1.15f;
            if (Type == TowerType.Tesla) { VulnBonus += 0.1f; VulnDuration += 0.5f; }
            if (Type == TowerType.Tachyon) { SlowFactor *= 0.8f; SlowDuration += 0.5f; }
        }
        TotalInvested += cost;
    }

    public int FullRefundValue => TotalInvested;
    public int SellValue => (int)(TotalInvested * GameSettings.SellRefundRatio);

    public void UpdatePassiveHeal(float dt, List<Tower> towers)
    {
        if (Type != TowerType.Repair) return;
        foreach (var t in towers)
        {
            if (t == this || t.IsDestroyed || t.TowerHealth >= GameSettings.MaxTowerHealth) continue;
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

    private void UpdateAim(float dt, Vector2? targetPos)
    {
        if (!ShouldRotate) return;
        if (targetPos.HasValue) { var dir = targetPos.Value - WorldPos; _targetAngle = MathF.Atan2(dir.Y, dir.X) - GetSpriteRestAngle(Type); _hasTarget = true; }
        else _hasTarget = false;
        if (_hasTarget) { float diff = WrapAngle(_targetAngle - _aimAngle); float ms = AimSpeed * dt;
            if (MathF.Abs(diff) <= ms) _aimAngle = _targetAngle; else _aimAngle += MathF.Sign(diff) * ms; _aimAngle = WrapAngle(_aimAngle); }
    }

    private static float WrapAngle(float a) { while (a > MathF.PI) a -= MathF.Tau; while (a < -MathF.PI) a += MathF.Tau; return a; }

    public void Update(GameTime gameTime, List<Enemy> enemies, List<Projectile> projectiles, List<FlameParticle> flames)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_pulseTimer > 0f) _pulseTimer -= dt;
        if (_flameSoundCooldown > 0f) _flameSoundCooldown -= dt;
        if (DisabledTimer > 0f) DisabledTimer -= dt;
        if (Type == TowerType.Grinder || Type == TowerType.Repair) return;
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

    public void Draw(SpriteBatch sb, SpriteSet sprites, bool selected)
    {
        var tex = sprites.Towers[Type];
        Color tint = IsDisabled ? new Color(100, 50, 50) : Color.White;
        if (TowerHealth < 150f && !IsDisabled) tint = Color.Lerp(Color.White, new Color(255, 100, 100), 1f - (TowerHealth / 150f));

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
            if (ShouldRotate) { var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                var scale = new Vector2(size / (float)tex.Width, size / (float)tex.Height);
                sb.Draw(tex, WorldPos, null, tint, _aimAngle, origin, scale, SpriteEffects.None, 0f); }
            else { var rect = new Rectangle((int)(WorldPos.X - size / 2f), (int)(WorldPos.Y - size / 2f), size, size); sb.Draw(tex, rect, tint); }
            if (Level > 1) { int sx = (int)(WorldPos.X - size / 2f) + 3; int sy = (int)(WorldPos.Y + size / 2f) - 7;
                for (int i = 0; i < Level - 1; i++) sb.Draw(sprites.Pixel, new Rectangle(sx + i * 7, sy, 5, 5), Color.Gold); }
            if (IsFieldTower) { Color ac = Type == TowerType.Tesla ? new Color(100, 220, 255) : new Color(220, 200, 50);
                int d = (int)(Range * 2); sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), ac * 0.08f);
                if (_pulseTimer > 0f) { float p = 1f - (_pulseTimer / PulseDuration); float pr = Range * p; int pd = (int)(pr * 2);
                    sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - pr), (int)(WorldPos.Y - pr), pd, pd), ac * (0.35f * (1f - p))); } }
            if (Type == TowerType.Grinder) { int d = (int)(Range * 2);
                sb.Draw(sprites.Ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), new Color(200, 80, 80) * 0.1f); }
        }
    }

    public void DrawRange(SpriteBatch sb, Texture2D ring)
    {
        int d = (int)(Range * 2);
        Color rc = Type switch { TowerType.Tesla => new Color(100, 220, 255), TowerType.Tachyon => new Color(220, 200, 50),
            TowerType.Grinder => new Color(200, 80, 80), TowerType.Flame => new Color(255, 140, 0), TowerType.Repair => new Color(80, 220, 80), _ => Color.White };
        sb.Draw(ring, new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d), rc * 0.15f);
    }
}
