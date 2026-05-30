namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

/// <summary>An indirect-fire shell lobbed by Mortar/Artillery towers. Travels in an arc
/// toward a fixed ground position (snapshot of target enemy at firing time) and applies
/// splash damage on impact. Unlike <see cref="Projectile"/>, it does not home — by design
/// it can miss a moving target, which is the cost of these towers' raw damage output.</summary>
public class MortarShell
{
    public Vector2 Position { get; private set; }
    public bool IsActive { get; private set; } = true;
    public TowerType Source { get; }

    private readonly Vector2 _origin;
    private readonly Vector2 _impactPoint;
    private readonly float _flightTime;
    private float _elapsed;
    private readonly float _damage;
    private readonly float _splashRadius;
    private readonly List<Enemy> _allEnemies;

    public MortarShell(Vector2 origin, Vector2 impactPoint, float damage, float splashRadius,
        float shellSpeed, TowerType source, List<Enemy> allEnemies)
    {
        _origin = origin;
        Position = origin;
        _impactPoint = impactPoint;
        _damage = damage;
        _splashRadius = splashRadius;
        Source = source;
        _allEnemies = allEnemies;
        float groundDist = Vector2.Distance(origin, impactPoint);
        _flightTime = MathF.Max(0.15f, groundDist / shellSpeed);
    }

    /// <summary>Visual arc offset (negative Y = upward). Peaks at t=0.5 with height
    /// proportional to flight distance.</summary>
    public float ArcOffset
    {
        get
        {
            float t = _elapsed / _flightTime;
            float arcHeight = MathF.Min(120f, Vector2.Distance(_origin, _impactPoint) * 0.45f);
            return -4f * arcHeight * t * (1f - t);
        }
    }

    public void Update(GameTime gt)
    {
        if (!IsActive) return;
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        _elapsed += dt;
        float t = MathF.Min(1f, _elapsed / _flightTime);
        Position = Vector2.Lerp(_origin, _impactPoint, t);
        if (t >= 1f) { OnImpact(); IsActive = false; }
    }

    private void OnImpact()
    {
        foreach (var e in _allEnemies)
        {
            if (!e.IsAlive) continue;
            float d = Vector2.Distance(_impactPoint, e.Position);
            if (d <= _splashRadius)
            {
                float falloff = 1f - (d / _splashRadius) * 0.5f;
                e.TakeDamage(_damage * falloff, Source);
            }
        }
        // Each shell type has its own dedicated impact sound.
        string sfx = Source == TowerType.Artillery ? "tower_artillery_impact" : "tower_mortar_impact";
        AudioManager.Instance.PlayVaried(sfx, 0.6f, 0.1f, 0.06f);
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites)
    {
        if (!IsActive) return;
        float arc = ArcOffset;
        var drawPos = new Vector2(Position.X, Position.Y + arc);

        // shadow on the ground (impact point trajectory)
        int sh = 6;
        sb.Draw(sprites.Pixel,
            new Rectangle((int)(Position.X - sh / 2f), (int)(Position.Y - sh / 4f), sh, sh / 2),
            new Color(0, 0, 0) * 0.35f);

        // shell — reuse rocket projectile sprite if present
        var tex = sprites.Projectiles.GetValueOrDefault(TowerType.Rocket);
        int size = Source == TowerType.Artillery ? 14 : 11;
        var rect = new Rectangle((int)(drawPos.X - size / 2f), (int)(drawPos.Y - size / 2f), size, size);
        if (tex != null) sb.Draw(tex, rect, Color.White);
        else sb.Draw(sprites.Pixel, rect, Color.LightGray);
    }
}
