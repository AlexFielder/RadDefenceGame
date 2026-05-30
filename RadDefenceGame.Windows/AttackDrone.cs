namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public enum AttackDroneState { Orbiting, Diving }

/// <summary>Combat drone deployed by a Drone Controller tower. Orbits the controller
/// while scanning for enemies within the controller's range, then dives to attack.</summary>
public class AttackDrone
{
    public Vector2 Position { get; private set; }
    public AttackDroneState State { get; private set; } = AttackDroneState.Orbiting;
    public Enemy? Target { get; private set; }

    private readonly Tower _home;
    private readonly int _slotIndex;
    private float _orbitPhase;
    private float _attackCooldown;
    private float _bobPhase;
    private static readonly Random Rng = new();

    public Tower GetHomeTower() => _home;

    public AttackDrone(Tower home, int slotIndex, int totalSlots)
    {
        _home = home;
        _slotIndex = slotIndex;
        _orbitPhase = MathF.Tau * slotIndex / MathF.Max(1, totalSlots);
        _bobPhase = (float)(Rng.NextDouble() * MathF.Tau);
        Position = home.WorldPos + OrbitOffset(_orbitPhase);
    }

    private static Vector2 OrbitOffset(float phase) =>
        new(MathF.Cos(phase) * GameSettings.AttackDroneOrbitRadius,
            MathF.Sin(phase) * GameSettings.AttackDroneOrbitRadius);

    public void Update(GameTime gt, List<Enemy> enemies)
    {
        if (_home.IsDestroyed) return;
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        _bobPhase += dt * 3f;
        _orbitPhase += dt * 1.4f;
        if (_attackCooldown > 0f) _attackCooldown -= dt;

        switch (State)
        {
            case AttackDroneState.Orbiting:
                var orbitPos = _home.WorldPos + OrbitOffset(_orbitPhase);
                Position = Vector2.Lerp(Position, orbitPos, MathF.Min(1f, dt * 6f));
                if (_attackCooldown <= 0f)
                {
                    Target = FindNearestInRange(enemies);
                    if (Target != null) State = AttackDroneState.Diving;
                }
                break;

            case AttackDroneState.Diving:
                if (Target == null || !Target.IsAlive ||
                    Vector2.Distance(_home.WorldPos, Target.Position) > _home.Range)
                { State = AttackDroneState.Orbiting; Target = null; break; }

                var toT = Target.Position - Position;
                float d = toT.Length();
                if (d < 14f)
                {
                    Target.TakeDamage(GameSettings.AttackDroneDamage * (1f + 0.15f * (_home.Level - 1)),
                        TowerType.DroneController);
                    _attackCooldown = 1f / GameSettings.AttackDroneFireRate;
                    State = AttackDroneState.Orbiting;
                    Target = null;
                    AudioManager.Instance.PlayVaried("drone_attack_laser", 0.35f, 0.1f, 0.06f);
                }
                else
                {
                    toT.Normalize();
                    Position += toT * GameSettings.AttackDroneSpeed * dt;
                }
                break;
        }
    }

    private Enemy? FindNearestInRange(List<Enemy> enemies)
    {
        Enemy? best = null; float bd = float.MaxValue;
        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            float dh = Vector2.Distance(_home.WorldPos, e.Position);
            if (dh > _home.Range) continue;
            float d = Vector2.Distance(Position, e.Position);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites)
    {
        var tex = sprites.DroneAttack;
        int size = 18;
        Color tint = State == AttackDroneState.Diving ? new Color(255, 180, 80) : Color.White;
        var rect = new Rectangle((int)(Position.X - size / 2f), (int)(Position.Y - size / 2f), size, size);
        sb.Draw(tex, rect, tint);

        // attack flash
        if (State == AttackDroneState.Diving && Target != null && Vector2.Distance(Position, Target.Position) < 24f)
        {
            var mid = (Position + Target.Position) * 0.5f;
            int s = 6;
            sb.Draw(sprites.Pixel, new Rectangle((int)(mid.X - s / 2f), (int)(mid.Y - s / 2f), s, s),
                new Color(255, 220, 100) * 0.8f);
        }
    }
}
