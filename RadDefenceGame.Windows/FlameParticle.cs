namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

/// <summary>A single flame particle emitted by Flame towers.</summary>
public class FlameParticle
{
    public Vector2 Position { get; private set; }
    public bool IsAlive { get; private set; } = true;

    private Vector2 _velocity;
    private float _life;
    private readonly float _maxLife;
    private readonly float _damage;
    private readonly float _burnDps;
    private readonly float _burnDuration;
    private readonly float _size;
    private bool _hasDamaged;

    private static readonly Random Rng = new();

    public FlameParticle(Vector2 origin, Vector2 direction, float speed, float lifetime,
        float damage, float burnDps, float burnDuration)
    {
        Position = origin;
        _maxLife = lifetime;
        _life = lifetime;
        _damage = damage;
        _burnDps = burnDps;
        _burnDuration = burnDuration;

        float angle = MathF.Atan2(direction.Y, direction.X);
        float spread = (float)(Rng.NextDouble() * 0.6 - 0.3);
        angle += spread;

        float speedVar = speed * (0.8f + (float)Rng.NextDouble() * 0.4f);
        _velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speedVar;
        _size = 3f + (float)Rng.NextDouble() * 3f;
    }

    public void Update(GameTime gameTime, List<Enemy> enemies)
    {
        if (!IsAlive) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _life -= dt;
        if (_life <= 0) { IsAlive = false; return; }

        Position += _velocity * dt;
        _velocity *= (1f - 1.5f * dt);

        if (!_hasDamaged)
        {
            foreach (var e in enemies)
            {
                if (!e.IsAlive) continue;
                if (Vector2.Distance(Position, e.Position) < 14f)
                {
                    e.TakeDamage(_damage, TowerType.Flame);
                    e.ApplyBurn(_burnDps, _burnDuration, TowerType.Flame);
                    _hasDamaged = true;
                    break;
                }
            }
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsAlive) return;

        float t = 1f - (_life / _maxLife);

        Color col;
        if (t < 0.3f) col = Color.Lerp(Color.Yellow, Color.Orange, t / 0.3f);
        else if (t < 0.7f) col = Color.Lerp(Color.Orange, Color.OrangeRed, (t - 0.3f) / 0.4f);
        else col = Color.Lerp(Color.OrangeRed, new Color(80, 20, 0), (t - 0.7f) / 0.3f);

        float alpha = t < 0.6f ? 0.9f : 0.9f * (1f - (t - 0.6f) / 0.4f);
        float sizeMul = t < 0.3f ? (0.6f + t / 0.3f * 0.4f) : (1f - (t - 0.3f) * 0.5f);
        int s = Math.Max(2, (int)(_size * sizeMul));

        sb.Draw(pixel, new Rectangle((int)(Position.X - s / 2f), (int)(Position.Y - s / 2f), s, s), col * alpha);
    }
}
