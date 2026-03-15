namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;

public class AudioManager
{
    public static AudioManager Instance { get; private set; } = new();

    private readonly Dictionary<string, SoundEffect> _sounds = new();
    private readonly Dictionary<string, float> _cooldowns = new();

    // separate volume channels
    public float MasterVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.5f;
    public bool Muted { get; set; }

    // music loop
    private SoundEffectInstance? _musicInstance;
    private string? _currentMusicName;

    public static void Init() => Instance = new AudioManager();

    public void LoadFromDirectory(string path)
    {
        if (!Directory.Exists(path)) { System.Diagnostics.Debug.WriteLine($"AudioManager: directory not found: {path}"); return; }
        foreach (var file in Directory.GetFiles(path, "*.wav"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try { using var stream = File.OpenRead(file); _sounds[name] = SoundEffect.FromStream(stream); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AudioManager: failed to load {name}: {ex.Message}"); }
        }
        System.Diagnostics.Debug.WriteLine($"AudioManager: loaded {_sounds.Count} sounds from {path}");
    }

    public void Play(string name, float volume = 1.0f, float cooldownSeconds = 0.03f)
    {
        if (Muted || !_sounds.TryGetValue(name, out var sfx)) return;
        if (_cooldowns.TryGetValue(name, out float remaining) && remaining > 0) return;
        sfx.Play(Math.Clamp(MasterVolume * SfxVolume * volume, 0f, 1f), 0f, 0f);
        _cooldowns[name] = cooldownSeconds;
    }

    public void PlayVaried(string name, float volume = 1.0f, float pitchRange = 0.1f, float cooldownSeconds = 0.03f)
    {
        if (Muted || !_sounds.TryGetValue(name, out var sfx)) return;
        if (_cooldowns.TryGetValue(name, out float remaining) && remaining > 0) return;
        float pitch = (Random.Shared.NextSingle() * 2f - 1f) * pitchRange;
        sfx.Play(Math.Clamp(MasterVolume * SfxVolume * volume, 0f, 1f), pitch, 0f);
        _cooldowns[name] = cooldownSeconds;
    }

    /// <summary>Start looping a music track. If already playing this track, just updates volume.</summary>
    public void PlayMusic(string name)
    {
        if (_currentMusicName == name && _musicInstance != null && _musicInstance.State == SoundState.Playing)
        { UpdateMusicVolume(); return; }
        StopMusic();
        if (!_sounds.TryGetValue(name, out var sfx)) return;
        _musicInstance = sfx.CreateInstance();
        _musicInstance.IsLooped = true;
        _musicInstance.Volume = Muted ? 0f : Math.Clamp(MasterVolume * MusicVolume, 0f, 1f);
        _musicInstance.Play();
        _currentMusicName = name;
    }

    public void StopMusic()
    {
        if (_musicInstance != null) { _musicInstance.Stop(); _musicInstance.Dispose(); _musicInstance = null; }
        _currentMusicName = null;
    }

    public void UpdateMusicVolume()
    {
        if (_musicInstance != null && _musicInstance.State == SoundState.Playing)
            _musicInstance.Volume = Muted ? 0f : Math.Clamp(MasterVolume * MusicVolume, 0f, 1f);
    }

    public void Update(float deltaSeconds)
    {
        var keys = new List<string>(_cooldowns.Keys);
        foreach (var key in keys) { _cooldowns[key] -= deltaSeconds; if (_cooldowns[key] <= 0) _cooldowns.Remove(key); }
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        UpdateMusicVolume();
    }
}
