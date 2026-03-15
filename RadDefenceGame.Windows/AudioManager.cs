namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>Manages all game sound effects. Access globally via Audio.Instance.</summary>
public class AudioManager
{
    public static AudioManager Instance { get; private set; } = new();

    private readonly Dictionary<string, SoundEffect> _sounds = new();
    private readonly Dictionary<string, float> _cooldowns = new();

    public float MasterVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool Muted { get; set; }

    public static void Init() => Instance = new AudioManager();

    /// <summary>Load all WAV files from a directory.</summary>
    public void LoadFromDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            System.Diagnostics.Debug.WriteLine($"AudioManager: directory not found: {path}");
            return;
        }

        foreach (var file in Directory.GetFiles(path, "*.wav"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                using var stream = File.OpenRead(file);
                var sfx = SoundEffect.FromStream(stream);
                _sounds[name] = sfx;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioManager: failed to load {name}: {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"AudioManager: loaded {_sounds.Count} sounds from {path}");
    }

    /// <summary>Play a sound by name.</summary>
    public void Play(string name, float volume = 1.0f, float cooldownSeconds = 0.03f)
    {
        if (Muted) return;
        if (!_sounds.TryGetValue(name, out var sfx)) return;

        if (_cooldowns.TryGetValue(name, out float remaining) && remaining > 0)
            return;

        float finalVol = Math.Clamp(MasterVolume * SfxVolume * volume, 0f, 1f);
        sfx.Play(finalVol, 0f, 0f);
        _cooldowns[name] = cooldownSeconds;
    }

    /// <summary>Play with random pitch variation for variety.</summary>
    public void PlayVaried(string name, float volume = 1.0f, float pitchRange = 0.1f, float cooldownSeconds = 0.03f)
    {
        if (Muted) return;
        if (!_sounds.TryGetValue(name, out var sfx)) return;

        if (_cooldowns.TryGetValue(name, out float remaining) && remaining > 0)
            return;

        float finalVol = Math.Clamp(MasterVolume * SfxVolume * volume, 0f, 1f);
        float pitch = (Random.Shared.NextSingle() * 2f - 1f) * pitchRange;
        sfx.Play(finalVol, pitch, 0f);
        _cooldowns[name] = cooldownSeconds;
    }

    /// <summary>Call each frame to tick cooldowns.</summary>
    public void Update(float deltaSeconds)
    {
        var keys = new List<string>(_cooldowns.Keys);
        foreach (var key in keys)
        {
            _cooldowns[key] -= deltaSeconds;
            if (_cooldowns[key] <= 0)
                _cooldowns.Remove(key);
        }
    }

    public void ToggleMute() => Muted = !Muted;
}
