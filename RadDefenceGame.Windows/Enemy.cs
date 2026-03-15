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
    public float Speed { get; }
    public bool IsAlive { get; set; } = true;
    public bool ReachedEnd { get; set; }
    public int Reward { get; }
    public Texture2D Sprite { get; }

    // -- burn DOT --
    public bool IsBurning => _burnTimer > 0f;
    private float _burnTimer;
    private float _burnDps;

    private int _currentWaypoint = 1;
    private List<Vector2> _path;

    public Enemy(List<Vector2> path, float health, float speed, int reward, Texture2D sprite)
    {
        _path = new List<Vector2>(path);
        Position = _path.Count > 0 ? _path[0] : Vector2.Zero;
        Health = health;
        MaxHealth = health;
        Speed = speed;
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

    public void TakeDamage(float damage)
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

    public void Update(GameTime gameTime)
    {
        if (!IsAlive || ReachedEnd) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_burnTimer > 0f)
        {
            _burnTimer -= dt;
            TakeDamage(_burnDps * dt);
            if (!IsAlive) return;

            if (_burnTimer <= 0f)
            {
                _burnTimer = 0f;
                _burnDps = 0f;
            }
        }

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

        // tint: burning enemies glow orange, otherwise white (show sprite's original colours)
        Color tint = IsBurning
            ? Color.Lerp(Color.OrangeRed, Color.Yellow, (_burnTimer * 3f) % 1f)
            : Color.White;

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

        if (IsBurning)
        {
            float burnPct = _burnTimer / GameSettings.BurnDuration;
            sb.Draw(pixel, new Rectangle(bx, by + barH + 1, (int)(barW * burnPct), 2), Color.Orange);
        }
    }
}
