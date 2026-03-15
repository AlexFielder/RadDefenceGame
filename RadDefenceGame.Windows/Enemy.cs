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

    // -- effective speed (after debuffs) --
    public float Speed => BaseSpeed * SlowMultiplier;

    // -- burn DOT --
    public bool IsBurning => _burnTimer > 0f;
    private float _burnTimer;
    private float _burnDps;

    // -- slow (Tachyon Warp) --
    public bool IsSlowed => _slowTimer > 0f;
    public float SlowMultiplier => IsSlowed ? _slowFactor : 1f;
    private float _slowTimer;
    private float _slowFactor = 1f;

    // -- vulnerability (Tesla Array) --
    public bool IsVulnerable => _vulnTimer > 0f;
    public float VulnerabilityMultiplier => IsVulnerable ? (1f + _vulnBonus) : 1f;
    private float _vulnTimer;
    private float _vulnBonus;

    private int _currentWaypoint = 1;
    private List<Vector2> _path;

    public Enemy(List<Vector2> path, float health, float speed, int reward, Texture2D sprite)
    {
        _path = new List<Vector2>(path);
        Position = _path.Count > 0 ? _path[0] : Vector2.Zero;
        Health = health;
        MaxHealth = health;
        BaseSpeed = speed;
        Reward = reward;
        Sprite = sprite;
    }

    public void UpdatePath(List<Vector2> newPath)
    {
        if (newPath.Count == 0) return;

        float bestDist = float.MaxValue;
        int bestIdx = 0;
        for (int i = 0; i < newPath.Count; i++)
        {
            float d = Vector2.DistanceSquared(Position, newPath[i]);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        _path = new List<Vector2>(newPath);
        _currentWaypoint = Math.Min(bestIdx + 1, _path.Count);
    }

    /// <summary>Apply damage, amplified by vulnerability if active.</summary>
    public void TakeDamage(float damage)
    {
        float actual = damage * VulnerabilityMultiplier;
        Health -= actual;
        if (Health <= 0f)
        {
            Health = 0f;
            IsAlive = false;
        }
    }

    /// <summary>Apply raw damage (no vulnerability multiplier) - used for burn ticks.</summary>
    public void TakeRawDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0f)
        {
            Health = 0f;
            IsAlive = false;
        }
    }

    public void ApplyBurn(float dps, float duration)
    {
        _burnDps = MathF.Max(_burnDps, dps);
        _burnTimer = MathF.Max(_burnTimer, duration);
    }

    public void ApplySlow(float factor, float duration)
    {
        // take the stronger slow, refresh duration
        if (factor < _slowFactor || !IsSlowed)
            _slowFactor = factor;
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
            TakeRawDamage(_burnDps * dt);
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

        if (dist < 2f)
        {
            _currentWaypoint++;
            return;
        }

        diff.Normalize();
        float step = Speed * dt;
        Position += diff * MathF.Min(step, dist);
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsAlive) return;

        const int size = 24;

        // tint based on active debuffs
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
        const int barW = 28, barH = 4;
        int bx = (int)(Position.X - barW / 2f);
        int by = (int)(Position.Y - size / 2f - 8);
        sb.Draw(pixel, new Rectangle(bx, by, barW, barH), new Color(60, 0, 0));
        sb.Draw(pixel, new Rectangle(bx, by, (int)(barW * hp), barH), Color.Lime);

        // debuff indicators under health bar
        int indicatorY = by + barH + 1;
        if (IsBurning)
        {
            float pct = _burnTimer / GameSettings.BurnDuration;
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * pct), 2), Color.Orange);
            indicatorY += 3;
        }
        if (IsSlowed)
        {
            float pct = _slowTimer / GameSettings.TachyonSlowDuration;
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * pct), 2), Color.MediumPurple);
            indicatorY += 3;
        }
        if (IsVulnerable)
        {
            float pct = _vulnTimer / GameSettings.TeslaVulnerabilityDuration;
            sb.Draw(pixel, new Rectangle(bx, indicatorY, (int)(barW * pct), 2), Color.Cyan);
        }
    }
}
