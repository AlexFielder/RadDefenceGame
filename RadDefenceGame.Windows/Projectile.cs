namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

public class Projectile
{
    public Vector2 Position { get; set; }
    public Enemy Target { get; }
    public float Damage { get; }
    public float Speed { get; }
    public bool IsActive { get; set; } = true;
    public Color Color { get; }
    public TowerType Source { get; }

    // burn stats (only for Flame projectiles)
    public float BurnDps { get; }
    public float BurnDuration { get; }

    // splash radius (only for Rocket projectiles)
    public float SplashRadius { get; }

    // reference to all enemies for splash damage
    private List<Enemy>? _allEnemies;

    public Projectile(Vector2 start, Enemy target, float damage, TowerType source,
        float burnDps = 0f, float burnDuration = 0f, float splashRadius = 0f)
    {
        Position = start;
        Target = target;
        Damage = damage;
        Source = source;
        BurnDps = burnDps;
        BurnDuration = burnDuration;
        SplashRadius = splashRadius;

        (Speed, Color) = source switch
        {
            TowerType.Basic  => (400f, new Color(100, 200, 255)),
            TowerType.Sniper => (600f, new Color(255, 200, 50)),
            TowerType.Rapid  => (500f, new Color(100, 255, 150)),
            TowerType.Rocket => (250f, new Color(255, 80, 30)),
            TowerType.Flame  => (350f, new Color(255, 150, 0)),
            _ => (400f, Color.White)
        };
    }

    public void SetEnemyList(List<Enemy> enemies) => _allEnemies = enemies;

    public void Update(GameTime gameTime)
    {
        if (!IsActive) return;

        if (!Target.IsAlive)
        {
            IsActive = false;
            return;
        }

        var diff = Target.Position - Position;
        float dist = diff.Length();

        if (dist < 8f)
        {
            OnHit();
            IsActive = false;
            return;
        }

        diff.Normalize();
        Position += diff * Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    private void OnHit()
    {
        if (Source == TowerType.Rocket && _allEnemies != null)
        {
            // splash damage to all enemies in radius
            foreach (var e in _allEnemies)
            {
                if (!e.IsAlive) continue;
                float d = Vector2.Distance(Target.Position, e.Position);
                if (d <= SplashRadius)
                {
                    // full damage at centre, half at edge
                    float falloff = 1f - (d / SplashRadius) * 0.5f;
                    e.TakeDamage(Damage * falloff);
                }
            }
        }
        else if (Source == TowerType.Flame)
        {
            Target.TakeDamage(Damage);
            Target.ApplyBurn(BurnDps, BurnDuration);
        }
        else
        {
            Target.TakeDamage(Damage);
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsActive) return;

        // rockets are bigger
        int s = Source == TowerType.Rocket ? 8 : (Source == TowerType.Flame ? 6 : 5);
        sb.Draw(pixel,
            new Rectangle((int)(Position.X - s / 2f), (int)(Position.Y - s / 2f), s, s),
            Color);
    }
}
