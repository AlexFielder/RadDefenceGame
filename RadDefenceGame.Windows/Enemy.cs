namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public class Enemy
{
    public Vector2 Position { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; }
    public float BaseSpeed { get; }
    public bool IsAlive { get; set; } = true;
    public bool ReachedEnd { get; set; }
    public int Reward { get; }
    public Texture2D Sprite { get; }
    public EnemyType Type { get; }
    public TowerType? LastDamageSource { get; private set; }

    /// <summary>If true, this enemy is a child spawn (spreader/centipede) and should not spawn more on death.</summary>
    public bool IsChild { get; set; }

    public float Speed => BaseSpeed * SlowMultiplier;

    // -- burn DOT --
    public bool IsBurning => _burnTimer > 0f;
    private float _burnTimer;
    private float _burnDps;
    private TowerType _burnSource;

    // -- slow --
    public bool IsSlowed => _slowTimer > 0f;
    public float SlowMultiplier => IsSlowed ? _slowFactor : 1f;
    private float _slowTimer;
    private float _slowFactor = 1f;

    // -- vulnerability --
    public bool IsVulnerable => _vulnTimer > 0f;
    public float VulnerabilityMultiplier => IsVulnerable ? (1f + _vulnBonus) : 1f;
    private float _vulnTimer;
    private float _vulnBonus;

    // -- ability timers (used by Game1 for cross-object abilities) --
    public float AbilityCooldown { get; set; }

    // -- teleporter state --
    private float _teleportTimer;

    // -- path following --
    private int _currentWaypoint = 1;
    private List<Vector2> _path;
    public int CurrentWaypoint => _currentWaypoint;
    public List<Vector2> Path => _path;

    public Enemy(List<Vector2> path, float health, float speed, int reward,
        Texture2D sprite, EnemyType type)
    {
        _path = new List<Vector2>(path);
        Position = _path.Count > 0 ? _path[0] : Vector2.Zero;
        Health = health;
        MaxHealth = health;
        BaseSpeed = speed;
        Reward = reward;
        Sprite = sprite;
        Type = type;
        AbilityCooldown = 0f;
        _teleportTimer = GameSettings.TeleportCooldown;
    }

    public void UpdatePath(List<Vector2> newPath)
    {
        if (newPath.Count == 0) return;
        float bestDist = float.MaxValue; int bestIdx = 0;
        for (int i = 0; i < newPath.Count; i++)
        {
            float d = Vector2.DistanceSquared(Position, newPath[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        _path = new List<Vector2>(newPath);
        _currentWaypoint = Math.Min(bestIdx + 1, _path.Count);
    }

    public void TakeDamage(float damage, TowerType source)
    {
        float actual = damage * VulnerabilityMultiplier;
        Health -= actual;
        LastDamageSource = source;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    public void TakeRawDamage(float damage, TowerType source)
    {
        Health -= damage;
        LastDamageSource = source;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    /// <summary>Heal this enemy (used by Medic). Cannot exceed max health.</summary>
    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Health = MathF.Min(Health + amount, MaxHealth);
    }

    public void ApplyBurn(float dps, float duration, TowerType source)
    {
        _burnDps = MathF.Max(_burnDps, dps);
        _burnTimer = MathF.Max(_burnTimer, duration);
        _burnSource = source;
    }

    public void ApplySlow(float factor, float duration)
    {
        if (factor < _slowFactor || !IsSlowed) _slowFactor = factor;
        _slowTimer = MathF.Max(_slowTimer, duration);
    }

    public void ApplyVulnerability(float bonus, float duration)
    {
        _vulnBonus = MathF.Max(_vulnBonus, bonus);
        _vulnTimer = MathF.Max(_vulnTimer, duration);
    }

    public void Update(GameTime gameTime)
    {
        if (!IsAlive || ReachedEnd) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // tick debuffs
        if (_burnTimer > 0f)
        {
            _burnTimer -= dt;
            TakeRawDamage(_burnDps * dt, _burnSource);
            if (!IsAlive) return;
            if (_burnTimer <= 0f) { _burnTimer = 0f; _burnDps = 0f; }
        }
        if (_slowTimer > 0f)
        {
            _slowTimer -= dt;
            if (_slowTimer <= 0f) { _slowTimer = 0f; _slowFactor = 1f; }
        }
        if (_vulnTimer > 0f)
        {
            _vulnTimer -= dt;
            if (_vulnTimer <= 0f) { _vulnTimer = 0f; _vulnBonus = 0f; }
        }

        // tick ability cooldown (used by Game1 for medic/hacker/blaster)
        if (AbilityCooldown > 0f) AbilityCooldown -= dt;

        // teleporter: blink forward along path
        if (Type == EnemyType.Teleporter)
        {
            _teleportTimer -= dt;
            if (_teleportTimer <= 0f)
            {
                _teleportTimer = GameSettings.TeleportCooldown;
                int skip = Math.Min(GameSettings.TeleportWaypoints, _path.Count - _currentWaypoint);
                if (skip > 0)
                {
                    _currentWaypoint += skip;
                    if (_currentWaypoint < _path.Count)
                        Position = _path[_currentWaypoint];
                }
            }
        }

        // movement
        if (_currentWaypoint >= _path.Count)
        {
            ReachedEnd = true;
            IsAlive = false;
            return;
        }

        var target = _path[_currentWaypoint];
        var diff = target - Position;
        float dist = diff.Length();

        if (dist < 2f) { _currentWaypoint++; return; }

        diff.Normalize();
        Position += diff * MathF.Min(Speed * dt, dist);
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsAlive) return;

        int size = IsChild ? 18 : 24;

        Color tint = Color.White;
        if (IsBurning)
            tint = Color.Lerp(Color.OrangeRed, Color.Yellow, (_burnTimer * 3f) % 1f);
        else if (IsSlowed)
            tint = Color.Lerp(Color.White, Color.MediumPurple, 0.5f);
        else if (IsVulnerable)
            tint = Color.Lerp(Color.White, Color.Cyan, 0.4f);

        sb.Draw(Sprite,
            new Rectangle((int)(Position.X - size / 2f), (int)(Position.Y - size / 2f), size, size),
            tint);

        // health bar
        float hp = Health / MaxHealth;
        int barW = IsChild ? 20 : 28;
        const int barH = 4;
        int bx = (int)(Position.X - barW / 2f);
        int by = (int)(Position.Y - size / 2f - 8);
        sb.Draw(pixel, new Rectangle(bx, by, barW, barH), new Color(60, 0, 0));
        sb.Draw(pixel, new Rectangle(bx, by, (int)(barW * hp), barH), Color.Lime);

        // debuff indicators
        int indicatorY = by + barH + 1;
        if (IsBurning)
        {
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * (_burnTimer / GameSettings.BurnDuration)), 2), Color.Orange);
            indicatorY += 3;
        }
        if (IsSlowed)
        {
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * (_slowTimer / GameSettings.TachyonSlowDuration)), 2), Color.MediumPurple);
            indicatorY += 3;
        }
        if (IsVulnerable)
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * (_vulnTimer / GameSettings.TeslaVulnerabilityDuration)), 2), Color.Cyan);

        // ability icon indicators
        if (Type == EnemyType.Medic)
        {
            // small green + above head
            int cx = (int)Position.X, cy = by - 6;
            sb.Draw(pixel, new Rectangle(cx - 3, cy, 6, 2), new Color(60, 240, 200));
            sb.Draw(pixel, new Rectangle(cx - 1, cy - 2, 2, 6), new Color(60, 240, 200));
        }
        else if (Type == EnemyType.Hacker)
        {
            // small purple diamond
            int cx = (int)Position.X, cy = by - 4;
            sb.Draw(pixel, new Rectangle(cx - 1, cy - 2, 2, 2), new Color(180, 60, 240));
            sb.Draw(pixel, new Rectangle(cx - 2, cy, 4, 1), new Color(180, 60, 240));
            sb.Draw(pixel, new Rectangle(cx - 1, cy + 1, 2, 2), new Color(180, 60, 240));
        }
    }
}
