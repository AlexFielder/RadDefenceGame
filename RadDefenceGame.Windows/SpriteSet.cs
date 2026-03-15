namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

public class SpriteSet
{
    public Texture2D EnemyScout { get; }
    public Texture2D EnemyGrunt { get; }
    public Texture2D EnemyTank { get; }
    public Texture2D EnemySpeeder { get; }
    public Texture2D EnemyShielded { get; }
    public Texture2D EnemyBoss { get; }

    public Dictionary<TowerType, Texture2D> Towers { get; } = new();
    public Dictionary<TowerType, Texture2D> Projectiles { get; } = new();

    public Texture2D TileWall { get; }
    public Texture2D TilePath { get; }
    public Texture2D Pixel { get; }
    public Texture2D Ring { get; }

    public SpriteSet(ContentManager content, Texture2D pixel, Texture2D ring)
    {
        Pixel = pixel;
        Ring = ring;

        EnemyScout = content.Load<Texture2D>("Images/enemy_scout");
        EnemyGrunt = content.Load<Texture2D>("Images/enemy_grunt");
        EnemyTank = content.Load<Texture2D>("Images/enemy_tank");
        EnemySpeeder = content.Load<Texture2D>("Images/enemy_speeder");
        EnemyShielded = content.Load<Texture2D>("Images/enemy_shielded");
        EnemyBoss = content.Load<Texture2D>("Images/enemy_boss");

        Towers[TowerType.Basic] = content.Load<Texture2D>("Images/tower_gun");
        Towers[TowerType.Sniper] = content.Load<Texture2D>("Images/tower_sniper");
        Towers[TowerType.Rapid] = content.Load<Texture2D>("Images/tower_rapid");
        Towers[TowerType.Rocket] = content.Load<Texture2D>("Images/tower_rocket");
        Towers[TowerType.Flame] = content.Load<Texture2D>("Images/tower_flame");
        Towers[TowerType.Tesla] = content.Load<Texture2D>("Images/tower_tesla_array");
        Towers[TowerType.Tachyon] = content.Load<Texture2D>("Images/tower_tachyon_warp");
        Towers[TowerType.Grinder] = content.Load<Texture2D>("Images/tower_parts_grinder");

        Projectiles[TowerType.Basic] = content.Load<Texture2D>("Images/proj_bullet");
        Projectiles[TowerType.Sniper] = content.Load<Texture2D>("Images/proj_sniper");
        Projectiles[TowerType.Rapid] = content.Load<Texture2D>("Images/proj_rapid");
        Projectiles[TowerType.Rocket] = content.Load<Texture2D>("Images/proj_rocket");
        Projectiles[TowerType.Flame] = content.Load<Texture2D>("Images/proj_flame");

        TileWall = content.Load<Texture2D>("Images/tile_wall");
        TilePath = content.Load<Texture2D>("Images/tile_path");
    }

    public (Texture2D sprite, EnemyType type) GetEnemyInfo(int waveNumber)
    {
        return waveNumber switch
        {
            <= 2 => (EnemyScout, EnemyType.Scout),
            <= 5 => (EnemyGrunt, EnemyType.Grunt),
            <= 8 => (EnemySpeeder, EnemyType.Speeder),
            <= 12 => (EnemyTank, EnemyType.Tank),
            <= 16 => (EnemyShielded, EnemyType.Shielded),
            _ => (EnemyBoss, EnemyType.Boss)
        };
    }
}
