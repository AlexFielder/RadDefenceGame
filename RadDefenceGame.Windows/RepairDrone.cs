namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public enum DroneState { Idle, FlyingToTarget, Repairing, Returning }

public class RepairDrone
{
    public Vector2 Position { get; set; }
    public DroneState State { get; private set; } = DroneState.Idle;
    public Tower? Target { get; private set; }

    private readonly Tower _home;
    private float _repairTimer;
    private float _idleTimer;
    private float _bobPhase;
    private static readonly Random Rng = new();

    public RepairDrone(Tower home)
    {
        _home = home;
        Position = home.WorldPos;
        _bobPhase = (float)(Rng.NextDouble() * MathF.Tau);
    }

    public void Update(GameTime gameTime, List<Tower> towers, GameState state)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _bobPhase += dt * 3f;

        switch (State)
        {
            case DroneState.Idle:
                // hover near home
                Position = _home.WorldPos + new Vector2(0, MathF.Sin(_bobPhase) * 3f);
                _idleTimer -= dt;
                if (_idleTimer <= 0f)
                {
                    _idleTimer = 0.5f; // scan every 0.5s
                    Target = FindBestTarget(towers, state);
                    if (Target != null) State = DroneState.FlyingToTarget;
                }
                break;

            case DroneState.FlyingToTarget:
                if (Target == null || Target.IsDestroyed)
                { State = DroneState.Returning; Target = null; break; }

                var toTarget = Target.WorldPos - Position;
                float distT = toTarget.Length();
                if (distT < 8f)
                {
                    State = DroneState.Repairing;
                    _repairTimer = 0f;
                }
                else
                {
                    toTarget.Normalize();
                    Position += toTarget * GameSettings.RepairDroneSpeed * dt;
                }
                break;

            case DroneState.Repairing:
                if (Target == null || Target.IsDestroyed)
                { State = DroneState.Returning; Target = null; break; }

                Position = Target.WorldPos + new Vector2(0, -14 + MathF.Sin(_bobPhase) * 2f);

                if (Target.TowerHealth < GameSettings.MaxTowerHealth)
                {
                    // repair
                    Target.TowerHealth = MathF.Min(
                        Target.TowerHealth + GameSettings.RepairDroneRepairRate * dt,
                        GameSettings.MaxTowerHealth);
                    _repairTimer += dt;
                }
                else if (Target.CanUpgrade && state.Money >= Target.UpgradeCost)
                {
                    // auto-upgrade
                    state.Money -= Target.UpgradeCost;
                    Target.Upgrade();
                    State = DroneState.Returning;
                    Target = null;
                }
                else
                {
                    // nothing to do, head home
                    State = DroneState.Returning;
                    Target = null;
                }
                break;

            case DroneState.Returning:
                var toHome = _home.WorldPos - Position;
                float distH = toHome.Length();
                if (distH < 8f)
                {
                    State = DroneState.Idle;
                    Position = _home.WorldPos;
                    _idleTimer = 1f; // wait a moment before scanning
                }
                else
                {
                    toHome.Normalize();
                    Position += toHome * GameSettings.RepairDroneSpeed * dt;
                }
                break;
        }
    }

    private Tower? FindBestTarget(List<Tower> towers, GameState state)
    {
        Tower? best = null;
        float bestScore = 0f;

        foreach (var t in towers)
        {
            if (t == _home || t.IsDestroyed) continue;
            float dist = Vector2.Distance(_home.WorldPos, t.WorldPos);
            if (dist > GameSettings.RepairDroneRange) continue;

            // score: prioritise most damaged, then upgradeable
            float dmg = GameSettings.MaxTowerHealth - t.TowerHealth;
            float score = dmg; // raw damage is primary priority

            // small bonus for upgradeable towers that player can afford
            if (t.TowerHealth >= GameSettings.MaxTowerHealth && t.CanUpgrade && state.Money >= t.UpgradeCost)
                score = 10f; // low priority upgrade task

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return bestScore > 0f ? best : null;
    }

    public void Draw(SpriteBatch sb, SpriteSet sprites)
    {
        var tex = sprites.DroneRepair;
        int size = 16;
        var rect = new Rectangle(
            (int)(Position.X - size / 2f),
            (int)(Position.Y - size / 2f),
            size, size);

        // green tint when repairing
        Color tint = State == DroneState.Repairing ? new Color(100, 255, 100) : Color.White;
        sb.Draw(tex, rect, tint);

        // repair spark effect
        if (State == DroneState.Repairing && Target != null)
        {
            float flash = (MathF.Sin(_bobPhase * 8f) + 1f) * 0.5f;
            int sx = (int)(Position.X - 2), sy = (int)(Position.Y + 6);
            sb.Draw(sprites.Pixel, new Rectangle(sx, sy, 4, 2), new Color(80, 255, 80) * flash);
        }
    }
}
