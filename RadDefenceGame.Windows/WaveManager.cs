namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    public event Action? OnWaveCompleted;

    private readonly Map _map;
    private readonly Random _rng = new();
    private SpriteSet? _sprites;
    private float _spawnTimer;
    private readonly Queue<SpawnEntry> _spawnQueue = new();
    private float _baseHealth;
    private float _baseSpeed;
    private const float AutoStartDelay = 10f;
    private record struct SpawnEntry(EnemyType Type, float Health, float Speed, int Reward);

    public WaveManager(Map map) => _map = map;
    public void SetSprites(SpriteSet sprites) => _sprites = sprites;
    public void RequestStart() { if (!WaveActive) StartNextWave(); }

    private void StartNextWave()
    {
        OnWaveStarting?.Invoke();
        CurrentWave++;
        _baseHealth = 50 + CurrentWave * 25; _baseSpeed = 60f + CurrentWave * 5f;
        int baseCount = 5 + CurrentWave * 2;
        int baseReward = GameSettings.KillBaseReward + CurrentWave;
        _spawnQueue.Clear();
        var (_, primaryType) = _sprites!.GetEnemyInfo(CurrentWave);
        foreach (var entry in BuildComposition(baseCount, primaryType, baseReward))
            _spawnQueue.Enqueue(entry);
        _spawnTimer = 0; IsSpawning = true; WaveActive = true;
        WaitingForPlayer = false; BreakTimer = 0; LastWallGrant = 0;
    }

    private List<SpawnEntry> BuildComposition(int count, EnemyType primary, int baseReward)
    {
        var list = new List<SpawnEntry>();
        for (int i = 0; i < count; i++) list.Add(new SpawnEntry(primary, _baseHealth, _baseSpeed, baseReward));

        if (CurrentWave >= 3)
        { int n = (int)(count * GameSettings.SwarmCountMultiplier * 0.3f);
          for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Swarm,
              _baseHealth * GameSettings.SwarmHealthMultiplier, _baseSpeed * GameSettings.SwarmSpeedMultiplier,
              Math.Max(1, (int)(baseReward * GameSettings.SwarmRewardMultiplier)))); }
        if (CurrentWave >= 5)
        { int n = 1 + (CurrentWave - 5) / 3; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Blaster, _baseHealth * 0.9f, _baseSpeed * 0.85f, baseReward + 3)); }
        if (CurrentWave >= 6)
        { int n = 1 + (CurrentWave - 6) / 4; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Centipede, _baseHealth * 0.8f, _baseSpeed * 1.1f, baseReward + 2)); }
        if (CurrentWave >= 7)
        { int n = 1 + (CurrentWave - 7) / 3; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Teleporter, _baseHealth * 0.7f, _baseSpeed * 0.9f, baseReward + 4)); }
        if (CurrentWave >= 9)
        { int n = 1 + (CurrentWave - 9) / 4; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Medic, _baseHealth * 0.6f, _baseSpeed * 0.8f, baseReward + 5)); }
        if (CurrentWave >= 10)
        { int n = 1 + (CurrentWave - 10) / 4; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Hacker, _baseHealth * 0.5f, _baseSpeed * 0.85f, baseReward + 5)); }
        if (CurrentWave >= 11)
        { int n = 1 + (CurrentWave - 11) / 3; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Spreader, _baseHealth * 0.9f, _baseSpeed * 0.75f, baseReward + 3)); }
        if (CurrentWave >= 13)
        { int n = 1 + (CurrentWave - 13) / 4; for (int i = 0; i < n; i++) list.Add(new SpawnEntry(EnemyType.Kamikaze, _baseHealth * 0.6f, _baseSpeed * 1.2f, baseReward + 4)); }

        for (int i = list.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        return list;
    }

    public void Update(GameTime gameTime, List<Enemy> enemies, GameState state)
    {
        if (WaitingForPlayer && CurrentWave > 0)
        { BreakTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
          if (state.AutoStartWaves && BreakTimer >= AutoStartDelay) StartNextWave(); return; }
        if (!WaveActive) return;

        if (IsSpawning)
        {
            _spawnTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            float interval = MathF.Max(0.3f, 1.2f - CurrentWave * 0.05f);
            if (_spawnTimer <= 0 && _spawnQueue.Count > 0)
            {
                var entry = _spawnQueue.Dequeue();
                enemies.Add(new Enemy(_map.CurrentPath, entry.Health, entry.Speed,
                    entry.Reward, _sprites!.GetSprite(entry.Type), entry.Type));
                _spawnTimer = interval;
                if (_spawnQueue.Count == 0) IsSpawning = false;
            }
        }

        if (!IsSpawning)
        {
            bool anyAlive = false;
            foreach (var e in enemies) if (e.IsAlive) { anyAlive = true; break; }
            if (!anyAlive)
            {
                WaveActive = false; WaitingForPlayer = true; BreakTimer = 0;
                LastWallGrant = _rng.Next(GameSettings.WallGrantMin, GameSettings.WallGrantMax + 1);
                state.GrantWalls(LastWallGrant);
                OnWaveCompleted?.Invoke();
            }
        }
    }
}
