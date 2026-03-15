namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

public class WaveManager
{
    public int CurrentWave { get; private set; }
    public bool IsSpawning { get; private set; }
    public bool WaveActive { get; private set; }
    public bool WaitingForPlayer { get; private set; } = true;
    public float BreakTimer { get; private set; }
    public int LastWallGrant { get; private set; }

    public event Action? OnWaveStarting;

    private readonly Map _map;
    private readonly Random _rng = new();
    private SpriteSet? _sprites;
    private float _spawnTimer;
    private int _enemiesToSpawn;
    private float _spawnInterval;
    private float _enemyHealth;
    private float _enemySpeed;

    private const float AutoStartDelay = 10f;

    public WaveManager(Map map)
    {
        _map = map;
    }

    public void SetSprites(SpriteSet sprites) => _sprites = sprites;

    public void RequestStart()
    {
        if (!WaveActive)
            StartNextWave();
    }

    private void StartNextWave()
    {
        OnWaveStarting?.Invoke();

        CurrentWave++;
        _enemiesToSpawn = 5 + CurrentWave * 2;
        _spawnInterval = MathF.Max(0.3f, 1.2f - CurrentWave * 0.05f);
        _enemyHealth = 50 + CurrentWave * 25;
        _enemySpeed = 60f + CurrentWave * 5f;
        _spawnTimer = 0;
        IsSpawning = true;
        WaveActive = true;
        WaitingForPlayer = false;
        BreakTimer = 0;
        LastWallGrant = 0;
    }

    private int RollWallGrant()
    {
        return _rng.Next(GameSettings.WallGrantMin, GameSettings.WallGrantMax + 1);
    }

    public void Update(GameTime gameTime, List<Enemy> enemies, GameState state)
    {
        if (WaitingForPlayer && CurrentWave > 0)
        {
            BreakTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (state.AutoStartWaves && BreakTimer >= AutoStartDelay)
                StartNextWave();

            return;
        }

        if (!WaveActive) return;

        if (IsSpawning)
        {
            _spawnTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_spawnTimer <= 0 && _enemiesToSpawn > 0)
            {
                int reward = GameSettings.KillBaseReward + CurrentWave;
                var sprite = _sprites?.GetEnemySprite(CurrentWave) ?? _sprites?.EnemyScout;
                enemies.Add(new Enemy(_map.CurrentPath, _enemyHealth, _enemySpeed, reward, sprite!));
                _enemiesToSpawn--;
                _spawnTimer = _spawnInterval;

                if (_enemiesToSpawn <= 0)
                    IsSpawning = false;
            }
        }

        if (!IsSpawning)
        {
            bool anyAlive = false;
            foreach (var e in enemies)
                if (e.IsAlive) { anyAlive = true; break; }

            if (!anyAlive)
            {
                WaveActive = false;
                WaitingForPlayer = true;
                BreakTimer = 0;

                LastWallGrant = RollWallGrant();
                state.GrantWalls(LastWallGrant);
            }
        }
    }
}
