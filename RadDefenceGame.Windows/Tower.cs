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

    public int TotalInvested { get; private set; }
    public bool PlacedDuringPrep { get; set; } = true;

    private float _cooldown;

    private static readonly Dictionary<TowerType, string> FireSounds = new()
    {
        { TowerType.Basic, "tower_gun" },
        { TowerType.Sniper, "tower_sniper" },
        { TowerType.Rapid, "tower_rapid" },
        { TowerType.Rocket, "tower_rocket" },
        { TowerType.Flame, "tower_flame" },
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
        TotalInvested = GetCost(type);
    }

    public static int GetCost(TowerType t) => t switch
    {
        TowerType.Basic  => GameSettings.BasicTowerCost,
        TowerType.Sniper => GameSettings.SniperTowerCost,
        TowerType.Rapid  => GameSettings.RapidTowerCost,
        TowerType.Rocket => GameSettings.RocketTowerCost,
        TowerType.Flame  => GameSettings.FlameTowerCost,
        _ => 999
    };

    public static string GetName(TowerType t) => t switch
    {
        TowerType.Basic  => "Gun",
        TowerType.Sniper => "Sniper",
        TowerType.Rapid  => "Rapid",
        TowerType.Rocket => "Rocket",
        TowerType.Flame  => "Flame",
        _ => "???"
    };

    private record struct TowerStats(float range, float damage, float fireRate, Color color,
        float burnDps, float burnDuration, float splashRadius);

    private static TowerStats GetBaseStats(TowerType t) => t switch
    {
        TowerType.Basic  => new(120f, 25f,  1.5f, new Color(0, 150, 255), 0, 0, 0),
        TowerType.Sniper => new(220f, 80f,  0.5f, new Color(255, 100, 0), 0, 0, 0),
        TowerType.Rapid  => new(90f,  10f,  5.0f, new Color(0, 255, 100), 0, 0, 0),
        TowerType.Rocket => new(280f, 60f,  0.3f, new Color(200, 50, 30),
            0, 0, GameSettings.RocketSplashRadius),
        TowerType.Flame  => new(80f,  8f,   3.0f, new Color(255, 140, 0),
            GameSettings.BurnDamagePerSecond, GameSettings.BurnDuration, 0),
        _ => new(100f, 20f, 1f, Color.White, 0, 0, 0)
    };

    public bool CanUpgrade => Level < GameSettings.MaxTowerLevel;
    public int UpgradeCost => (int)(GetCost(Type) * GameSettings.UpgradeCostMultiplier);

    public void Upgrade()
    {
        if (!CanUpgrade) return;
        Level++;
        Damage *= GameSettings.UpgradeDamageMultiplier;
        Range *= GameSettings.UpgradeRangeMultiplier;
        FireRate *= GameSettings.UpgradeFireRateMultiplier;

        if (Type == TowerType.Flame)
        {
            BurnDps *= GameSettings.UpgradeDamageMultiplier;
            BurnDuration += 0.5f;
        }

        if (Type == TowerType.Rocket)
            SplashRadius *= 1.15f;

        TotalInvested += UpgradeCost;
    }

    public int FullRefundValue => TotalInvested;
    public int SellValue => (int)(TotalInvested * GameSettings.SellRefundRatio);

    public void Update(GameTime gameTime, List<Enemy> enemies, List<Projectile> projectiles)
    {
        _cooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_cooldown > 0) return;

        Enemy? best = null;
        float bestDist = float.MaxValue;

        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            float d = Vector2.Distance(WorldPos, e.Position);
            if (d <= Range && d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        if (best != null)
        {
            var proj = new Projectile(WorldPos, best, Damage, Type,
                BurnDps, BurnDuration, SplashRadius);

            if (Type == TowerType.Rocket)
                proj.SetEnemyList(enemies);

            projectiles.Add(proj);
            _cooldown = 1f / FireRate;

            // play firing sound
            if (FireSounds.TryGetValue(Type, out var soundName))
                AudioManager.Instance.PlayVaried(soundName, 0.5f, 0.08f);
        }
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites, bool selected)
    {
        int pad = 4;
        int size = GameSettings.CellSize - pad;
        var rect = new Rectangle(
            (int)(WorldPos.X - size / 2f),
            (int)(WorldPos.Y - size / 2f),
            size, size);

        var tex = sprites.Towers[Type];
        sb.Draw(tex, rect, Color.White);

        if (Level > 1)
        {
            for (int i = 0; i < Level - 1; i++)
            {
                int sx = rect.X + 3 + i * 7;
                int sy = rect.Bottom - 7;
                sb.Draw(sprites.Pixel, new Rectangle(sx, sy, 5, 5), Color.Gold);
            }
        }
    }

    public void DrawRange(SpriteBatch sb, Texture2D ring)
    {
        int d = (int)(Range * 2);
        sb.Draw(ring,
            new Rectangle((int)(WorldPos.X - Range), (int)(WorldPos.Y - Range), d, d),
            Color.White * 0.15f);
    }
}
