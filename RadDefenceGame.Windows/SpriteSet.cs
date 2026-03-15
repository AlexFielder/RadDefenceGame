namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public class SpriteSet
{
    public Dictionary<EnemyType, Texture2D> Enemies { get; } = new();
    public Texture2D EnemyScout => Enemies[EnemyType.Scout];

    public Dictionary<TowerType, Texture2D> Towers { get; } = new();
    public Dictionary<TowerType, Texture2D> Projectiles { get; } = new();

    public Texture2D DroneRepair { get; }
    public Texture2D TileWall { get; }
    public Texture2D TilePath { get; }
    public Texture2D Pixel { get; }
    public Texture2D Ring { get; }

    public SpriteSet(ContentManager content, Texture2D pixel, Texture2D ring)
    {
        Pixel = pixel;
        Ring = ring;

        Enemies[EnemyType.Scout]      = content.Load<Texture2D>("Images/enemy_scout");
        Enemies[EnemyType.Grunt]      = content.Load<Texture2D>("Images/enemy_grunt");
        Enemies[EnemyType.Tank]       = content.Load<Texture2D>("Images/enemy_tank");
        Enemies[EnemyType.Speeder]    = content.Load<Texture2D>("Images/enemy_speeder");
        Enemies[EnemyType.Shielded]   = content.Load<Texture2D>("Images/enemy_shielded");
        Enemies[EnemyType.Boss]       = content.Load<Texture2D>("Images/enemy_boss");
        Enemies[EnemyType.Centipede]  = content.Load<Texture2D>("Images/enemy_centipede");
        Enemies[EnemyType.Blaster]    = content.Load<Texture2D>("Images/enemy_blaster");
        Enemies[EnemyType.Swarm]      = content.Load<Texture2D>("Images/enemy_swarm");
        Enemies[EnemyType.Teleporter] = content.Load<Texture2D>("Images/enemy_teleporter");
        Enemies[EnemyType.Kamikaze]   = content.Load<Texture2D>("Images/enemy_kamikaze");
        Enemies[EnemyType.Medic]      = content.Load<Texture2D>("Images/enemy_medic");
        Enemies[EnemyType.Spreader]   = content.Load<Texture2D>("Images/enemy_spreader");
        Enemies[EnemyType.Hacker]     = content.Load<Texture2D>("Images/enemy_hacker");

        Towers[TowerType.Basic]   = content.Load<Texture2D>("Images/tower_gun");
        Towers[TowerType.Sniper]  = content.Load<Texture2D>("Images/tower_sniper");
        Towers[TowerType.Rapid]   = content.Load<Texture2D>("Images/tower_rapid");
        Towers[TowerType.Rocket]  = content.Load<Texture2D>("Images/tower_rocket");
        Towers[TowerType.Flame]   = content.Load<Texture2D>("Images/tower_flame");
        Towers[TowerType.Tesla]   = content.Load<Texture2D>("Images/tower_tesla_array");
        Towers[TowerType.Tachyon] = content.Load<Texture2D>("Images/tower_tachyon_warp");
        Towers[TowerType.Grinder] = content.Load<Texture2D>("Images/tower_parts_grinder");
        Towers[TowerType.Repair]  = content.Load<Texture2D>("Images/tower_repair");

        DroneRepair = content.Load<Texture2D>("Images/drone_repair");

        Projectiles[TowerType.Basic]  = content.Load<Texture2D>("Images/proj_bullet");
        Projectiles[TowerType.Sniper] = content.Load<Texture2D>("Images/proj_sniper");
        Projectiles[TowerType.Rapid]  = content.Load<Texture2D>("Images/proj_rapid");
        Projectiles[TowerType.Rocket] = content.Load<Texture2D>("Images/proj_rocket");
        Projectiles[TowerType.Flame]  = content.Load<Texture2D>("Images/proj_flame");

        TileWall = content.Load<Texture2D>("Images/tile_wall");
        TilePath = content.Load<Texture2D>("Images/tile_path");
    }

    public (Texture2D sprite, EnemyType type) GetEnemyInfo(int waveNumber)
    {
        var type = waveNumber switch
        {
            <= 2  => EnemyType.Scout, <= 5 => EnemyType.Grunt, <= 8 => EnemyType.Speeder,
            <= 12 => EnemyType.Tank, <= 16 => EnemyType.Shielded, _ => EnemyType.Boss
        };
        return (Enemies[type], type);
    }

    public Texture2D GetSprite(EnemyType type) => Enemies[type];
}
