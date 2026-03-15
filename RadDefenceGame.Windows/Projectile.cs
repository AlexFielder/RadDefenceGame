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
    public TowerType Source { get; }

    public float BurnDps { get; }
    public float BurnDuration { get; }
    public float SplashRadius { get; }

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

        Speed = source switch
        {
            TowerType.Basic  => 400f,
            TowerType.Sniper => 600f,
            TowerType.Rapid  => 500f,
            TowerType.Rocket => 250f,
            TowerType.Flame  => 350f,
            _ => 400f
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
            foreach (var e in _allEnemies)
            {
                if (!e.IsAlive) continue;
                float d = Vector2.Distance(Target.Position, e.Position);
                if (d <= SplashRadius)
                {
                    float falloff = 1f - (d / SplashRadius) * 0.5f;
                    e.TakeDamage(Damage * falloff);
                }
            }
            AudioManager.Instance.PlayVaried("rocket_explode", 0.6f, 0.15f, 0.05f);
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

    public void Draw(SpriteBatch sb, SpriteSet sprites)
    {
        if (!IsActive) return;

        var tex = sprites.Projectiles[Source];
        int s = Source == TowerType.Rocket ? 12 : (Source == TowerType.Flame ? 10 : 8);
        sb.Draw(tex,
            new Rectangle((int)(Position.X - s / 2f), (int)(Position.Y - s / 2f), s, s),
            Color.White);
    }
}
