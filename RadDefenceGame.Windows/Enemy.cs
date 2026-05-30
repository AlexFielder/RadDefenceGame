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

    // -- shield (applied as a damage buffer in front of HP; primarily used for the
    // Shielded enemy type and as the target of Electric Fence damage)
    public float Shield { get; set; }
    public float MaxShield { get; private set; }

    // -- block-by-fence (set by ElectricFence each frame; when > 0 the enemy stops moving)
    public float BlockTimer { get; private set; }
    public bool IsBlocked => BlockTimer > 0f;
    public void Block(float duration) { if (duration > BlockTimer) BlockTimer = duration; }

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
        if (type == EnemyType.Shielded)
        {
            MaxShield = health * GameSettings.ShieldedEnemyShieldRatio;
            Shield = MaxShield;
        }
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
        LastDamageSource = source;
        // shield absorbs damage first; spill-over hits health.
        if (Shield > 0f)
        {
            float pre = Shield;
            if (actual <= Shield) { Shield -= actual; MaybeShieldHitSound(pre, actual); return; }
            actual -= Shield; MaybeShieldHitSound(pre, pre); Shield = 0f;
        }
        Health -= actual;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    public void TakeRawDamage(float damage, TowerType source)
    {
        LastDamageSource = source;
        if (Shield > 0f)
        {
            float pre = Shield;
            if (damage <= Shield) { Shield -= damage; MaybeShieldHitSound(pre, damage); return; }
            damage -= Shield; MaybeShieldHitSound(pre, pre); Shield = 0f;
        }
        Health -= damage;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    /// <summary>Damage applied specifically to the shield layer, used by the Electric Fence.
    /// Spills over into HP only after the shield is fully depleted. Bypasses Tesla
    /// vulnerability since the fence is a contact-DOT effect.</summary>
    public void TakeShieldDamage(float damage, TowerType source)
    {
        LastDamageSource = source;
        if (Shield > 0f)
        {
            float pre = Shield;
            if (damage <= Shield) { Shield -= damage; MaybeShieldHitSound(pre, damage); return; }
            damage -= Shield; MaybeShieldHitSound(pre, pre); Shield = 0f;
        }
        Health -= damage;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    /// <summary>Audio cue when an attack lands on the shield (rather than HP). Throttled
    /// by AudioManager's per-sound cooldown so swarms don't machine-gun the effect, and
    /// gated by a minimum damage threshold so DOT ticks don't spam.</summary>
    private void MaybeShieldHitSound(float shieldBefore, float absorbed)
    {
        if (absorbed < 2f) return;
        AudioManager.Instance.PlayVaried("shield_hit", 0.4f, 0.12f, 0.05f);
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

        // tick block-timer (set by Electric Fence each frame the enemy is held against it).
        // If held, skip movement so the enemy stays pinned at the fence until it breaks.
        if (BlockTimer > 0f)
        {
            BlockTimer -= dt;
            if (BlockTimer > 0f) return;
            BlockTimer = 0f;
        }

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

        // shield bar (sits above the health bar when the enemy has shield capacity)
        if (MaxShield > 0f)
        {
            int sby = by - (barH + 1);
            sb.Draw(pixel, new Rectangle(bx, sby, barW, barH), new Color(0, 20, 60));
            float sf = MathF.Max(0f, Shield / MaxShield);
            sb.Draw(pixel, new Rectangle(bx, sby, (int)(barW * sf), barH), new Color(120, 200, 255));
        }

        // block indicator (enemy held by an Electric Fence)
        if (IsBlocked)
        {
            int s = 6;
            sb.Draw(pixel, new Rectangle((int)Position.X - s / 2, by - 14, s, s),
                new Color(255, 240, 80));
        }

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
