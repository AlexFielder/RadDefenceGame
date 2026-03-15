namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using System.Collections.Generic;

/// <summary>Lore descriptions and tactical info for all enemy types.</summary>
public static class EnemyGlossary
{
    public record GlossaryEntry(
        string Name,
        string Lore,
        string Ability,
        Color AccentColor);

    public static readonly Dictionary<EnemyType, GlossaryEntry> Entries = new()
    {
        [EnemyType.Scout] = new(
            "Scout Drone",
            "Lightweight recon units deployed in the first waves. Mass-produced and expendable, "
            + "they map defensive positions for the swarms that follow. What they lack in armour "
            + "they make up for in sheer numbers.",
            "None - basic enemy",
            new Color(0, 220, 200)),

        [EnemyType.Grunt] = new(
            "Grunt Mech",
            "The backbone of the invasion force. These armoured walkers are built in orbital foundries "
            + "and dropped in pods. Reliable, predictable, and just tough enough to soak up a few rounds "
            + "before they fall.",
            "None - standard enemy with moderate health",
            new Color(60, 120, 220)),

        [EnemyType.Speeder] = new(
            "Speeder Craft",
            "Stripped of all non-essential systems, these dart-shaped interceptors trade durability "
            + "for raw velocity. They slip through kill zones before towers can acquire a lock. "
            + "Best countered with rapid-fire or area denial.",
            "High speed, low health",
            new Color(240, 220, 40)),

        [EnemyType.Tank] = new(
            "Siege Tank",
            "Heavily plated war machines that absorb punishment like sponges. Siege Tanks are slow "
            + "but their layered reactive armour can shrug off everything short of a direct rocket hit. "
            + "Focus fire is essential.",
            "Very high health, slow speed",
            new Color(140, 40, 40)),

        [EnemyType.Shielded] = new(
            "Shielded Orb",
            "Protected by a regenerating energy barrier, these orbs are nearly impervious to chip damage. "
            + "The shield must be overwhelmed with sustained fire before the fragile core is exposed. "
            + "Tesla Arrays are particularly effective at cracking them open.",
            "High health with damage resistance",
            new Color(180, 120, 255)),

        [EnemyType.Boss] = new(
            "Hivemind Colossus",
            "A massive bio-mechanical horror that commands lesser units through psionic links. "
            + "Each Colossus is unique, grown in the deepest vaults of the enemy homeworld. "
            + "Destroying one sends shockwaves through the entire invasion network. Worth a fortune in salvage.",
            "Extreme health, high reward",
            new Color(220, 50, 220)),

        [EnemyType.Centipede] = new(
            "Centipede Wyrm",
            "A segmented bio-construct that slithers through defences with unnerving speed. "
            + "Each segment operates semi-independently -- severing the head merely promotes the next "
            + "segment to leader. You have to destroy every last piece to stop it.",
            "On death: spawns 3 fast segment enemies",
            new Color(40, 255, 80)),

        [EnemyType.Blaster] = new(
            "Blaster Assault",
            "Unlike passive targets, Blasters fight back. Armed with a forward-mounted plasma cannon, "
            + "they strafe your towers as they pass. Left unchecked, a column of Blasters can "
            + "systematically dismantle your front line. Prioritise them before they open fire.",
            "Periodically damages the nearest tower",
            new Color(60, 160, 220)),

        [EnemyType.Swarm] = new(
            "Swarm Cluster",
            "Thousands of micro-drones compressed into a roiling mass. Individually fragile, "
            + "they overwhelm through sheer volume. Each cluster that reaches your core splits into "
            + "dozens of infiltrators. Area-of-effect weapons are your best friend here.",
            "Spawns in 3x numbers, low health, fast, low reward each",
            new Color(200, 50, 180)),

        [EnemyType.Teleporter] = new(
            "Phase Shifter",
            "Equipped with experimental fold-space drives, these units periodically blink forward "
            + "along their path, bypassing entire sections of your kill zone. The displacement field "
            + "leaves a brief afterimage at the origin point. Towers with long range fare better "
            + "against these unpredictable targets.",
            "Every 4s blinks forward, skipping part of the path",
            new Color(0, 240, 255)),

        [EnemyType.Kamikaze] = new(
            "Kamikaze Bomber",
            "Volatile munitions packed into a thruster frame. These units have no intention of "
            + "surviving -- they exist to reach your towers and detonate. The blast radius is "
            + "devastating. Snipe them from range before they get close, or watch your defences "
            + "crumble in a chain of explosions.",
            "On death: explodes, dealing heavy damage to nearby towers",
            new Color(255, 120, 30)),

        [EnemyType.Medic] = new(
            "Medic Drone",
            "A support unit broadcasting a nanite repair field that knits damaged allies back together. "
            + "The Medic itself is moderately tough, but its real threat is keeping Tanks and Bosses "
            + "alive far longer than they should be. Always kill the Medic first.",
            "Heals nearby enemies for 15 HP/s within 80px range",
            new Color(60, 240, 200)),

        [EnemyType.Spreader] = new(
            "Spreader Husk",
            "A biological carrier designed to multiply under fire. When the outer shell is breached, "
            + "two smaller but faster copies emerge from the wreckage. Flame towers are ideal -- "
            + "the burn damage can finish off the children before they scatter.",
            "On death: splits into 2 smaller, faster copies",
            new Color(240, 180, 40)),

        [EnemyType.Hacker] = new(
            "Hacker Node",
            "A cyber-warfare platform that broadcasts disruptor signals, scrambling tower targeting "
            + "systems. Any tower within range goes dark for several seconds -- weapons lock up, "
            + "tracking fails, ammunition feeds jam. Build redundant coverage or lose entire sectors.",
            "Every 5s disables the nearest tower for 3s",
            new Color(180, 60, 240)),
    };

    public static GlossaryEntry Get(EnemyType type)
        => Entries.TryGetValue(type, out var e) ? e : new("Unknown", "No data.", "None", Color.Gray);
}
