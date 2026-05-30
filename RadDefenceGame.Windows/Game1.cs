namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

public enum GameScreen { Title, Playing, GameOver, Glossary, Settings }

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteSet _sprites = null!;
    private SpriteFont _font = null!;

    private Map _map = null!;
    private GameState _state = null!;
    private WaveManager _waves = null!;
    private SeedManager _seeds = null!;
    private readonly List<Enemy> _enemies = new();
    private readonly List<Tower> _towers = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<FlameParticle> _flames = new();
    private readonly List<Enemy> _pendingSpawns = new();
    private readonly List<RepairDrone> _drones = new();
    private readonly List<AttackDrone> _attackDrones = new();
    private readonly List<MortarShell> _shells = new();

    private record DestroyedTowerRecord(TowerType Type, Point GridPos, int Level, int TotalInvested,
        bool IsZonePlaced, Vector2 WorldPos);
    private readonly List<DestroyedTowerRecord> _destroyedThisWave = new();

    private GameStats _runStats = null!;
    private Leaderboard _leaderboard = null!;
    private int _lastRank;

    private MouseState _prevMouse;
    private KeyboardState _prevKb;
    private Point _hoverCell;
    private Vector2 _mouseWorld;
    private Tower? _hoveredTower;

    private readonly List<ToolbarButton> _toolbar = new();
    private ToolbarButton? _speedButton;
    private ToolbarButton? _autoStartButton;
    private ToolbarButton? _muteButton;
    private readonly ContextMenu _contextMenu = new();

    // Toolbar layout / persistence
    private ToolbarPrefs _toolbarPrefs = new();
    // Dropdowns (Grouped style): when a category button is clicked, _openDropdown holds
    // the list of tower-button entries to render as a vertical popup. Closes on outside click.
    private List<ToolbarButton>? _openDropdown;
    private Rectangle _openDropdownBounds;
    private ToolbarButton? _openDropdownAnchor;
    // Drag (Custom style): tracks press-drag-release to detect rearrange vs click.
    private ToolbarButton? _dragCandidate;
    private Point _dragStart;
    private bool _dragActive;

    // Cone-aim offset applied to the next Mortar/Artillery placement. Adjusted by the
    // mouse wheel — one notch (120 wheel units) = 90°. Reset to 0 after each placement.
    private float _pendingConeAimOffset;

    private bool _autoStartPref = false;
    // Zone-placement preference is sticky across runs once toggled, like _autoStartPref.
    private PlacementSystem _placementSystemPref = PlacementSystem.Block;
    private ToolbarButton? _zoneButton;
    private Difficulty _selectedDifficulty = Difficulty.Normal;
    private GameScreen _screen = GameScreen.Title;
    private GameScreen _settingsReturnScreen;
    private string _seedInput = "";
    private bool _seedInputActive;
    private bool _gameOverSoundPlayed;
    private bool _paused;

    private int _glossaryIndex;
    private GameScreen _glossaryReturnScreen;
    private static readonly EnemyType[] GlossaryOrder = (EnemyType[])Enum.GetValues(typeof(EnemyType));

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Rad Defence";
        Window.TextInput += OnTextInput;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = GameSettings.ScreenWidth;
        _graphics.PreferredBackBufferHeight = GameSettings.ScreenHeight;
        _graphics.ApplyChanges();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Score");
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        _sprites = new SpriteSet(Content, pixel, CreateRing(64, 2));
        AudioManager.Init();
        AudioManager.Instance.LoadFromDirectory(Path.Combine(Content.RootDirectory, "Audio"));
        _leaderboard = Leaderboard.Load();
        _toolbarPrefs = ToolbarPrefs.Load();
        _seeds = new SeedManager();
        _seeds.NewRandomSeed();
    }

    private void StartGame(int seed, Difficulty difficulty)
    {
        _seeds.SetSeed(seed); _map = new Map(seed); _state = new GameState();
        _state.ApplyDifficulty(difficulty); _state.AutoStartWaves = _autoStartPref;
        _state.PlacementSystem = _placementSystemPref;
        _waves = new WaveManager(_map); _waves.SetSprites(_sprites);
        _waves.OnWaveStarting += OnWaveStarting; _waves.OnWaveCompleted += OnWaveCompleted;
        _enemies.Clear(); _towers.Clear(); _projectiles.Clear(); _flames.Clear();
        _pendingSpawns.Clear(); _drones.Clear(); _attackDrones.Clear(); _shells.Clear();
        _destroyedThisWave.Clear();
        _contextMenu.Close(); _screen = GameScreen.Playing;
        _seedInput = ""; _seedInputActive = false;
        _gameOverSoundPlayed = false; _paused = false; _lastRank = 0;
        _runStats = new GameStats { Seed = seed }; BuildToolbar();
    }

    private void LoadGame()
    {
        var save = SaveManager.Load(); if (save == null) return;
        StartGame(save.Seed, (Difficulty)save.Difficulty);
        _state.Lives = save.Lives; _state.Money = save.Money;
        _state.Walls = save.Walls; _state.Score = save.Score;
        _state.AutoStartWaves = save.AutoStartWaves;
        _state.PlacementSystem = (PlacementSystem)save.PlacementSystem;
        _placementSystemPref = _state.PlacementSystem;
        _runStats.PlayTimeSeconds = save.PlayTimeSeconds;
        foreach (var w in save.PlayerWalls)
        { var pt = new Point(w[0], w[1]);
          if (_map.Grid[w[0], w[1]] == CellType.Empty) _map.Grid[w[0], w[1]] = CellType.Wall;
          if (!_map.PlayerPlacedWalls.Contains(pt)) _map.PlayerPlacedWalls.Add(pt); }
        _map.RecalculatePath();
        foreach (var tr in save.Towers)
        { var type = (TowerType)tr.Type;
          // Zone-placed towers don't mark the cell (multiple zone towers may share one wall cell).
          // Block-placed towers keep the classic cell-ownership semantics.
          // Electric Fence sits on a path tile and never marks the grid as Tower/Wall.
          if (type == TowerType.Repair) _map.Place2x2Tower(tr.Col, tr.Row);
          else if (type == TowerType.ElectricFence) { /* nothing — fence lives on a path cell */ }
          else if (!tr.IsZonePlaced) _map.PlaceTower(tr.Col, tr.Row);
          var tower = tr.IsZonePlaced && type != TowerType.Repair
              ? new Tower(tr.Col, tr.Row, type, new Vector2(tr.WorldPosX, tr.WorldPosY))
              : new Tower(tr.Col, tr.Row, type);
          if (type == TowerType.DroneController)
          {
              // Replay the saved upgrade-track sequence so ChosenPath / RangeUpgradesApplied
              // exactly match the saved tower.
              int ranged = tr.RangeUpgrades;
              int countOnly = (tr.Level - 1) - ranged;
              for (int i = 0; i < ranged; i++) tower.UpgradeRange();
              for (int i = 0; i < countOnly; i++) tower.UpgradeCount();
          }
          else for (int lvl = 1; lvl < tr.Level; lvl++) tower.Upgrade();
          tower.TowerHealth = tr.TowerHealth; tower.AutoRebuildEnabled = tr.AutoRebuildEnabled;
          tower.PlacedDuringPrep = false;
          if (type == TowerType.Mortar || type == TowerType.Artillery || type == TowerType.ElectricFence)
          {
              // Prefer the saved facing; if absent (older save), reconstruct from current map state.
              if (tr.ConeFacing != 0f) tower.SetFacing(tr.ConeFacing);
              else if (type == TowerType.ElectricFence)
              { var pd = _map.GetPathDirectionAt(tr.Col, tr.Row); tower.SetFacing(MathF.Atan2(pd.Y, pd.X)); }
              else
              { Vector2 nearest = Vector2.Zero; float bd = float.MaxValue;
                foreach (var wp in _map.CurrentPath)
                { float d = Vector2.Distance(tower.WorldPos, wp); if (d < bd) { bd = d; nearest = wp; } }
                var diff = nearest - tower.WorldPos;
                if (diff.LengthSquared() > 0.001f) tower.SetFacing(MathF.Atan2(diff.Y, diff.X));
              }
          }
          _towers.Add(tower);
          if (type == TowerType.Repair) for (int d = 0; d < tower.DroneCount; d++) _drones.Add(new RepairDrone(tower));
          if (type == TowerType.DroneController) SyncAttackDronesFor(tower); }
        _waves.SetWave(save.Wave); SaveManager.DeleteSave();
    }

    private void SaveGame()
    {
        if (_waves.WaveActive) return;
        SaveManager.Save(_seeds.CurrentSeed, _waves.CurrentWave, _state, _towers, _map, _runStats.PlayTimeSeconds);
        AudioManager.Instance.Play("ui_click", 0.6f);
    }

    private void QuitToMenu() { _paused = false; _contextMenu.Close(); _seeds.NewRandomSeed(); _screen = GameScreen.Title; }

    private void OnWaveStarting()
    { foreach (var t in _towers) t.PlacedDuringPrep = false; _destroyedThisWave.Clear(); AudioManager.Instance.Play("wave_start", 0.8f); }

    private void OnWaveCompleted() { ProcessAutoRebuild(); }

    private void ProcessAutoRebuild()
    {
        if (_destroyedThisWave.Count == 0) return;
        var rts = new List<Tower>();
        foreach (var t in _towers) if (t.Type == TowerType.Repair && t.AutoRebuildEnabled && !t.IsDestroyed) rts.Add(t);
        if (rts.Count == 0) return;
        for (int i = _destroyedThisWave.Count - 1; i >= 0; i--)
        { var rec = _destroyedThisWave[i]; if (rec.Type == TowerType.Repair) continue;
          Tower? nearest = null; float best = float.MaxValue; var wp = Map.GridToWorld(rec.GridPos.X, rec.GridPos.Y);
          foreach (var rt in rts) { float d = Vector2.Distance(rt.WorldPos, wp); if (d < best) { best = d; nearest = rt; } }
          if (nearest == null) continue;
          int cost = (int)(rec.TotalInvested * GameSettings.AutoRebuildCostMultiplier);
          if (_state.Money < cost) continue;
          // Rebuild using the same placement system the tower originally used.
          Tower? nt;
          if (rec.IsZonePlaced)
          { if (!CanZonePlaceAt(rec.GridPos, rec.WorldPos)) continue;
            _state.Money -= cost;
            nt = new Tower(rec.GridPos.X, rec.GridPos.Y, rec.Type, rec.WorldPos); }
          else
          { if (!_map.CanPlaceTower(rec.GridPos.X, rec.GridPos.Y)) continue;
            _state.Money -= cost; _map.PlaceTower(rec.GridPos.X, rec.GridPos.Y);
            nt = new Tower(rec.GridPos.X, rec.GridPos.Y, rec.Type); }
          for (int lvl = 1; lvl < rec.Level; lvl++) nt.Upgrade();
          _towers.Add(nt); _destroyedThisWave.RemoveAt(i); AudioManager.Instance.Play("ui_upgrade", 0.5f); }
    }

    private void OpenGlossary() { _glossaryReturnScreen = _screen; _screen = GameScreen.Glossary; _glossaryIndex = 0; AudioManager.Instance.Play("ui_click", 0.4f); }
    private void OpenSettings(GameScreen returnTo) { _settingsReturnScreen = returnTo; _screen = GameScreen.Settings; AudioManager.Instance.Play("ui_click", 0.4f); }

    // ---------- TOWER CATALOGUE (the single source of truth for button labels/colors/hotkeys) ----------

    private record TowerCatalogEntry(TowerType Type, string Short, string Name, int Cost, Color Accent, Keys Hotkey, ToolbarGroup Group);
    private enum ToolbarGroup { Guns, Heavy, Special, Utility }

    private static readonly TowerCatalogEntry[] TowerCatalog =
    {
        new(TowerType.Basic,            "Gun",  "Gun",          GameSettings.BasicTowerCost,           new Color(0, 150, 255),   Keys.D1, ToolbarGroup.Guns),
        new(TowerType.Sniper,           "Snp",  "Sniper",       GameSettings.SniperTowerCost,          new Color(255, 100, 0),   Keys.D2, ToolbarGroup.Guns),
        new(TowerType.Rapid,            "Rpd",  "Rapid",        GameSettings.RapidTowerCost,           new Color(0, 255, 100),   Keys.D3, ToolbarGroup.Guns),
        new(TowerType.Rocket,           "Rkt",  "Rocket",       GameSettings.RocketTowerCost,          new Color(200, 50, 30),   Keys.D4, ToolbarGroup.Heavy),
        new(TowerType.Flame,            "Flm",  "Flame",        GameSettings.FlameTowerCost,           new Color(255, 140, 0),   Keys.D5, ToolbarGroup.Special),
        new(TowerType.Tesla,            "Tsl",  "Tesla",        GameSettings.TeslaTowerCost,           new Color(100, 220, 255), Keys.D6, ToolbarGroup.Special),
        new(TowerType.Tachyon,          "Tch",  "Tachyon",      GameSettings.TachyonTowerCost,         new Color(220, 200, 50),  Keys.D7, ToolbarGroup.Special),
        new(TowerType.Grinder,          "Grd",  "Grinder",      GameSettings.GrinderTowerCost,         new Color(200, 80, 80),   Keys.D8, ToolbarGroup.Utility),
        new(TowerType.Repair,           "Rpr",  "Repair",       GameSettings.RepairTowerCost,          new Color(80, 220, 80),   Keys.D9, ToolbarGroup.Utility),
        new(TowerType.Mortar,           "Mtr",  "Mortar",       GameSettings.MortarTowerCost,          new Color(150, 120, 90),  Keys.D0, ToolbarGroup.Heavy),
        new(TowerType.Artillery,        "Art",  "Artillery",    GameSettings.ArtilleryTowerCost,       new Color(180, 140, 60),  Keys.A,  ToolbarGroup.Heavy),
        new(TowerType.DroneController,  "Drn",  "Drone Ctrl",   GameSettings.DroneControllerCost,      new Color(120, 180, 255), Keys.D,  ToolbarGroup.Utility),
        new(TowerType.ElectricFence,    "Fnc",  "E-Fence",      GameSettings.ElectricFenceCost,        new Color(255, 220, 80),  Keys.E,  ToolbarGroup.Utility),
    };

    private static TowerCatalogEntry GetCatalog(TowerType t)
    {
        foreach (var c in TowerCatalog) if (c.Type == t) return c;
        throw new System.ArgumentException($"No catalog entry for {t}");
    }

    private static string KeyShort(Keys k) => k switch
    {
        Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
        Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
        _ => k.ToString()
    };

    // ---------- TOOLBAR BUILD (dispatches by current style) ----------

    private void BuildToolbar()
    {
        _toolbar.Clear();
        CloseDropdown();
        // Cancel any in-progress drag — its target reference would be stale after rebuild.
        _dragCandidate = null;
        _dragActive = false;
        try
        {
            switch (_toolbarPrefs.Style)
            {
                case ToolbarStyle.Compact:  BuildToolbarCompact(); break;
                case ToolbarStyle.Grouped:  BuildToolbarGrouped(); break;
                case ToolbarStyle.TwoRow:   BuildToolbarTwoRow(); break;
                case ToolbarStyle.Custom:   BuildToolbarCustom(); break;
            }
        }
        catch (System.Exception ex)
        {
            // If a layout throws for any reason, fall back to Compact so the player isn't
            // locked out of the game with a broken toolbar. Surface the error to the debug log.
            System.Diagnostics.Debug.WriteLine($"BuildToolbar({_toolbarPrefs.Style}) threw: {ex}");
            _toolbar.Clear();
            _toolbarPrefs.Style = ToolbarStyle.Compact;
            BuildToolbarCompact();
        }
        BuildRightSideButtons();
    }

    private void BuildToolbarCompact()
    {
        int y = 44, x = 10, gap = 3, bw = 72;
        foreach (var c in TowerCatalog)
            AddTowerButton(ref x, y, gap, bw, $"{KeyShort(c.Hotkey)}:{c.Short} ${c.Cost}", c.Hotkey, c.Type, c.Cost, c.Accent);
        x += 4;
        _toolbar.Add(MakeWallButton(x, y, 60, 28));
    }

    private void BuildToolbarTwoRow()
    {
        // Two horizontal rows; row 1 = guns + heavy + special, row 2 = utility/path + wall.
        // Buttons are 17 tall to fit cleanly inside the existing 80-px UI bar.
        int y1 = 42, y2 = 61, h = 17, gap = 3, bw = 78;
        int x = 10;
        foreach (var c in TowerCatalog)
            if (c.Group == ToolbarGroup.Guns || c.Group == ToolbarGroup.Heavy || c.Group == ToolbarGroup.Special)
                AddTowerButton(ref x, y1, gap, bw, $"{KeyShort(c.Hotkey)}:{c.Short} ${c.Cost}", c.Hotkey, c.Type, c.Cost, c.Accent, h);
        x = 10;
        foreach (var c in TowerCatalog)
            if (c.Group == ToolbarGroup.Utility)
                AddTowerButton(ref x, y2, gap, bw, $"{KeyShort(c.Hotkey)}:{c.Short} ${c.Cost}", c.Hotkey, c.Type, c.Cost, c.Accent, h);
        x += 4;
        _toolbar.Add(MakeWallButton(x, y2, 60, h));
    }

    private void BuildToolbarGrouped()
    {
        // Category buttons. Clicking opens a vertical dropdown of the towers in that group.
        // Hotkeys still pick towers directly so keyboard users aren't slowed down.
        _categoryAnchors.Clear();
        int y = 44, x = 10, gap = 6, bw = 110;
        var groupSpecs = new (ToolbarGroup g, string label, Color accent)[]
        {
            (ToolbarGroup.Guns,    "Guns",    new Color(0, 200, 255)),
            (ToolbarGroup.Heavy,   "Heavy",   new Color(220, 100, 60)),
            (ToolbarGroup.Special, "Special", new Color(220, 200, 80)),
            (ToolbarGroup.Utility, "Utility", new Color(160, 220, 160)),
        };
        foreach (var spec in groupSpecs)
        {
            var bounds = new Rectangle(x, y, bw, 28);
            var grp = spec.g; var label = spec.label; var ac = spec.accent;
            ToolbarButton btn = null!;
            btn = new ToolbarButton(bounds, label + "  v", null,
                () => ToggleDropdown(btn, grp),
                () => _openDropdown != null && _openDropdownAnchor != null && ReferenceEquals(_openDropdownAnchor, btn),
                () => true, ac);
            _toolbar.Add(btn);
            _categoryAnchors[btn] = grp;
            x += bw + gap;
        }
        _toolbar.Add(MakeWallButton(x, y, 60, 28));
        // Register hidden hotkey buttons so number keys still pick towers even when no
        // dropdown is open.
        BuildHiddenHotkeyButtons();
    }

    private void BuildToolbarCustom()
    {
        // Same layout as Compact, but the order comes from _toolbarPrefs.CustomOrder and
        // the user can drag-rearrange. If CustomOrder is empty, fall back to catalogue order.
        int y = 44, x = 10, gap = 3, bw = 72;
        var order = new List<TowerType>();
        if (_toolbarPrefs.CustomOrder.Count > 0)
        {
            foreach (var v in _toolbarPrefs.CustomOrder)
                if (System.Enum.IsDefined(typeof(TowerType), v)) order.Add((TowerType)v);
            // Append any catalogue towers that aren't yet in the saved order (handles new
            // tower types added after the user last saved).
            foreach (var c in TowerCatalog) if (!order.Contains(c.Type)) order.Add(c.Type);
        }
        else foreach (var c in TowerCatalog) order.Add(c.Type);

        foreach (var tt in order)
        {
            var c = GetCatalog(tt);
            AddTowerButton(ref x, y, gap, bw, $"{KeyShort(c.Hotkey)}:{c.Short} ${c.Cost}", c.Hotkey, c.Type, c.Cost, c.Accent);
        }
        x += 4;
        _toolbar.Add(MakeWallButton(x, y, 60, 28));
    }

    /// <summary>For Grouped mode: hotkey-only tower buttons that aren't drawn in the bar
    /// but still respond to keyboard input. Bounds are zero-sized so mouse clicks ignore them.</summary>
    private void BuildHiddenHotkeyButtons()
    {
        foreach (var c in TowerCatalog)
        {
            var captured = c;
            _toolbar.Add(new ToolbarButton(Rectangle.Empty,
                "", captured.Hotkey,
                () => SelectTower(captured.Type),
                () => false, () => false, captured.Accent, captured.Type));
        }
    }

    private ToolbarButton MakeWallButton(int x, int y, int w, int h) =>
        new(new Rectangle(x, y, w, h), "W:Wall", Keys.W,
            () => { _state.Mode = PlacementMode.Wall; AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Mode == PlacementMode.Wall, () => _state.HasWalls(), new Color(160, 120, 200));

    private void BuildRightSideButtons()
    {
        int y = 44;
        int rx = GameSettings.ScreenWidth - 10;
        _speedButton = new ToolbarButton(new Rectangle(rx - 90, y, 90, 28), $"Spd {_state.SpeedLabel}", null,
            () => { _state.CycleSpeed(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.4f); },
            () => !_state.IsNormalSpeed, () => true, Color.Yellow);
        _toolbar.Add(_speedButton); rx -= 93;
        _autoStartButton = new ToolbarButton(new Rectangle(rx - 80, y, 80, 28), _state.AutoStartWaves ? "Auto ON" : "Auto OFF", null,
            () => { _state.AutoStartWaves = !_state.AutoStartWaves; _autoStartPref = _state.AutoStartWaves;
                _autoStartButton!.SetLabel(_state.AutoStartWaves ? "Auto ON" : "Auto OFF"); AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.AutoStartWaves, () => true, new Color(100, 180, 220));
        _toolbar.Add(_autoStartButton); rx -= 83;
        _zoneButton = new ToolbarButton(new Rectangle(rx - 80, y, 80, 28),
            _state.UsesZonePlacement ? "Z:Zone" : "Z:Block", Keys.Z,
            () => {
                _state.PlacementSystem = _state.UsesZonePlacement ? PlacementSystem.Block : PlacementSystem.Zone;
                _placementSystemPref = _state.PlacementSystem;
                _zoneButton!.SetLabel(_state.UsesZonePlacement ? "Z:Zone" : "Z:Block");
                AudioManager.Instance.Play("ui_click", 0.4f);
            },
            () => _state.UsesZonePlacement, () => true, new Color(255, 140, 80));
        _toolbar.Add(_zoneButton); rx -= 83;
        _muteButton = new ToolbarButton(new Rectangle(rx - 50, y, 50, 28), "M:Snd", Keys.M,
            () => { AudioManager.Instance.ToggleMute(); _muteButton!.SetLabel(AudioManager.Instance.Muted ? "M:OFF" : "M:Snd"); },
            () => AudioManager.Instance.Muted, () => true, new Color(180, 180, 180));
        _toolbar.Add(_muteButton);
    }

    private void UpdateSpeedLabel() => _speedButton?.SetLabel($"Spd {_state.SpeedLabel}");

    private void AddTowerButton(ref int x, int y, int gap, int w, string label, Keys hotkey, TowerType type, int cost, Color accent, int h = 28)
    { _toolbar.Add(new ToolbarButton(new Rectangle(x, y, w, h), label, hotkey,
        () => SelectTower(type),
        () => _state.Mode == PlacementMode.Tower && _state.SelectedTower == type, () => _state.Money >= cost, accent, type));
      x += w + gap; }

    private void SelectTower(TowerType type)
    {
        _state.Mode = PlacementMode.Tower;
        _state.SelectedTower = type;
        // Reset the wheel-applied cone aim whenever the player changes selection so a
        // half-rotated offset from a previous tower doesn't carry over silently.
        _pendingConeAimOffset = 0f;
        AudioManager.Instance.Play("ui_click", 0.4f);
        CloseDropdown();
    }

    // ---------- DROPDOWN (Grouped style) ----------

    /// <summary>Map from category button → its toolbar group, populated during BuildToolbarGrouped.
    /// Lets us identify which group an anchor button represents without parsing labels.</summary>
    private readonly Dictionary<ToolbarButton, ToolbarGroup> _categoryAnchors = new();

    private void ToggleDropdown(ToolbarButton anchor, ToolbarGroup g)
    {
        AudioManager.Instance.Play("ui_click", 0.4f);
        if (_openDropdown != null && ReferenceEquals(_openDropdownAnchor, anchor)) { CloseDropdown(); return; }
        OpenDropdownFor(anchor, g);
    }

    private void OpenDropdownFor(ToolbarButton anchor, ToolbarGroup g)
    {
        var items = new List<ToolbarButton>();
        int itemH = 28, itemW = 150;
        int ix = anchor.Bounds.X;
        int iy = anchor.Bounds.Bottom + 4;
        int row = 0;
        foreach (var c in TowerCatalog)
        {
            if (c.Group != g) continue;
            var bounds = new Rectangle(ix, iy + row * (itemH + 2), itemW, itemH);
            var cc = c;
            items.Add(new ToolbarButton(bounds, $"{KeyShort(cc.Hotkey)}:{cc.Name} ${cc.Cost}", cc.Hotkey,
                () => SelectTower(cc.Type),
                () => _state.Mode == PlacementMode.Tower && _state.SelectedTower == cc.Type,
                () => _state.Money >= cc.Cost, cc.Accent, cc.Type));
            row++;
        }
        _openDropdown = items;
        _openDropdownAnchor = anchor;
        _openDropdownBounds = new Rectangle(ix - 4, iy - 4, itemW + 8, row * (itemH + 2) + 6);
    }

    private void CloseDropdown()
    {
        _openDropdown = null;
        _openDropdownAnchor = null;
        _openDropdownBounds = Rectangle.Empty;
    }

    // ---------- DRAG (Custom style) ----------

    /// <summary>Persist the current order of tower buttons to <see cref="_toolbarPrefs"/>.</summary>
    private void SaveCustomOrder()
    {
        _toolbarPrefs.CustomOrder.Clear();
        foreach (var b in _toolbar)
            if (b.TowerType.HasValue && b.Bounds.Width > 0)
                _toolbarPrefs.CustomOrder.Add((int)b.TowerType.Value);
        _toolbarPrefs.Save();
    }

    /// <summary>Swap two tower buttons in _toolbar (by reference) and re-pack bounds along the row.</summary>
    private void SwapTowerButtons(ToolbarButton a, ToolbarButton b)
    {
        int ia = _toolbar.IndexOf(a), ib = _toolbar.IndexOf(b);
        if (ia < 0 || ib < 0) return;
        _toolbar[ia] = b; _toolbar[ib] = a;
        // Re-pack: walk the tower buttons in their new order and reassign bounds based on
        // their original visual position.
        int x = 10, y = 44, gap = 3, bw = 72;
        foreach (var btn in _toolbar)
        {
            if (!btn.TowerType.HasValue || btn.Bounds.Width == 0) continue;
            btn.SetBounds(new Rectangle(x, y, bw, 28));
            x += bw + gap;
        }
        SaveCustomOrder();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    { if (_screen != GameScreen.Title || !_seedInputActive) return;
      if (e.Key == Keys.Back) { if (_seedInput.Length > 0) _seedInput = _seedInput[..^1]; }
      else if (char.IsDigit(e.Character) && _seedInput.Length < 6) _seedInput += e.Character; }

    private GameTime ScaleGameTime(GameTime o) { float m = _state.SpeedMultiplier;
        return m == 1f ? o : new GameTime(TimeSpan.FromTicks((long)(o.TotalGameTime.Ticks * m)),
            TimeSpan.FromTicks((long)(o.ElapsedGameTime.Ticks * m))); }

    /// <summary>Builds a GameTime whose ElapsedGameTime is a fraction of one real frame —
    /// used for substepping at speeds &gt; 10x so projectile hit-tests and enemy waypoint
    /// advancement don't skip past objects.</summary>
    private static GameTime ScaleGameTimeBy(GameTime o, float scale) =>
        new GameTime(
            TimeSpan.FromTicks((long)(o.TotalGameTime.Ticks * scale)),
            TimeSpan.FromTicks((long)(o.ElapsedGameTime.Ticks * scale)));

    // ---- UPDATE ----

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState(); var mouse = Mouse.GetState();
        AudioManager.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_screen == GameScreen.Playing && JustPressed(kb, Keys.Escape)) { _paused = !_paused; _contextMenu.Close(); }
        switch (_screen)
        {
            case GameScreen.Title: UpdateTitle(kb, mouse); break;
            case GameScreen.Playing: UpdatePlaying(gameTime, kb, mouse); break;
            case GameScreen.GameOver: UpdateGameOver(kb, mouse); break;
            case GameScreen.Glossary: UpdateGlossary(kb, mouse); break;
            case GameScreen.Settings: UpdateSettings(kb, mouse); break;
        }
        _prevMouse = mouse; _prevKb = kb; base.Update(gameTime);
    }

    private void UpdateTitle(KeyboardState kb, MouseState mouse)
    {
        bool lc = LeftClicked(mouse);
        if (JustPressed(kb, Keys.Tab)) _seedInputActive = !_seedInputActive;
        if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
        if (JustPressed(kb, Keys.S)) { OpenSettings(GameScreen.Title); return; }
        if (JustPressed(kb, Keys.Enter)) { StartGame(_seedInputActive && _seedInput.Length > 0 ? int.Parse(_seedInput) : _seeds.CurrentSeed, _selectedDifficulty); return; }
        if (JustPressed(kb, Keys.Space) && !_seedInputActive) _seeds.NewRandomSeed();
        if (JustPressed(kb, Keys.L) && SaveManager.HasSave()) { LoadGame(); return; }
        if (lc)
        { int favY = 540; foreach (var fav in _seeds.Favourites)
          { if (new Rectangle(440, favY - 2, 400, 24).Contains(mouse.X, mouse.Y))
            { _seedInput = fav.Seed.ToString(); _seedInputActive = true; AudioManager.Instance.Play("ui_click", 0.4f); break; }
            favY += 28; if (favY > 640) break; } }
    }

    private void UpdatePlaying(GameTime gameTime, KeyboardState kb, MouseState mouse)
    {
        if (_paused) { if (JustPressed(kb, Keys.Space)) _paused = false;
            if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
            if (LeftClicked(mouse)) HandlePauseClick(mouse); return; }
        if (!_state.IsGameOver)
        { _runStats.PlayTimeSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds; HandleInput(kb, mouse);
          // At 15x/20x, running the whole simulation in one step would skip projectile/enemy
          // collisions. Split into substeps so each substep stays at or below the previous
          // 10x maximum. Below the threshold we keep the original single-step path.
          float mul = _state.SpeedMultiplier;
          if (mul <= GameSettings.SubstepSpeedThreshold)
          { var st = ScaleGameTime(gameTime); _waves.Update(st, _enemies, _state);
            UpdateEnemies(st); UpdateEnemyAbilities(st); UpdateTowers(st);
            UpdateDrones(st); UpdateProjectiles(st); UpdateFlames(st); CleanUp(); }
          else
          { int steps = (int)MathF.Ceiling(mul / GameSettings.SubstepSpeedThreshold);
            float stepScale = mul / steps;
            for (int i = 0; i < steps && !_state.IsGameOver; i++)
            { var st = ScaleGameTimeBy(gameTime, stepScale);
              _waves.Update(st, _enemies, _state);
              UpdateEnemies(st); UpdateEnemyAbilities(st); UpdateTowers(st);
              UpdateDrones(st); UpdateProjectiles(st); UpdateFlames(st); CleanUp(); } } }
        else
        { _contextMenu.Close(); _seeds.UpdateBest(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);
          if (!_gameOverSoundPlayed) { AudioManager.Instance.Play("game_over", 0.8f); _gameOverSoundPlayed = true;
            _runStats.FinalScore = _state.Score; _runStats.WavesCompleted = _waves.CurrentWave;
            _lastRank = _leaderboard.Submit(_runStats); } _screen = GameScreen.GameOver; }
    }

    private void HandlePauseClick(MouseState mouse)
    {
        float cx = GameSettings.ScreenWidth / 2f, cy = GameSettings.ScreenHeight / 2f;
        int bw = 160, bh = 32, gap = 8, bx = (int)(cx - bw / 2f), by = (int)(cy + 10);
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y)) { _paused = false; AudioManager.Instance.Play("ui_click", 0.4f); return; } by += bh + gap;
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y) && !_waves.WaveActive) { SaveGame(); return; } by += bh + gap;
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y)) { OpenSettings(GameScreen.Playing); return; } by += bh + gap;
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y)) { StartGame(_seeds.CurrentSeed, _state.Difficulty); return; } by += bh + gap;
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y)) { OpenGlossary(); return; } by += bh + gap;
        if (new Rectangle(bx, by, bw, bh).Contains(mouse.X, mouse.Y)) { QuitToMenu(); return; }
    }

    private void UpdateGameOver(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.R)) { StartGame(_seeds.CurrentSeed, _state.Difficulty); return; }
        if (JustPressed(kb, Keys.N)) { _seeds.NewRandomSeed(); StartGame(_seeds.CurrentSeed, _selectedDifficulty); return; }
        if (JustPressed(kb, Keys.F)) _seeds.ToggleFavourite(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);
        if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
        if (JustPressed(kb, Keys.Escape)) { _seeds.NewRandomSeed(); _screen = GameScreen.Title; }
    }

    private void UpdateGlossary(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.Escape) || JustPressed(kb, Keys.G)) { _screen = _glossaryReturnScreen; AudioManager.Instance.Play("ui_click", 0.4f); return; }
        if (JustPressed(kb, Keys.Right) || JustPressed(kb, Keys.D)) { _glossaryIndex = (_glossaryIndex + 1) % GlossaryOrder.Length; AudioManager.Instance.Play("ui_click", 0.3f); }
        if (JustPressed(kb, Keys.Left) || JustPressed(kb, Keys.A)) { _glossaryIndex = (_glossaryIndex - 1 + GlossaryOrder.Length) % GlossaryOrder.Length; AudioManager.Instance.Play("ui_click", 0.3f); }
        if (LeftClicked(mouse)) { int sideY = 80; for (int i = 0; i < GlossaryOrder.Length; i++)
            { if (new Rectangle(10, sideY, 200, 40).Contains(mouse.X, mouse.Y)) { _glossaryIndex = i; AudioManager.Instance.Play("ui_click", 0.3f); break; } sideY += 44; } }
    }

    private void UpdateSettings(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.Escape)) { _screen = _settingsReturnScreen; if (_settingsReturnScreen == GameScreen.Playing) _paused = true; AudioManager.Instance.Play("ui_click", 0.4f); return; }

        bool held = mouse.LeftButton == ButtonState.Pressed;
        bool clicked = LeftClicked(mouse);

        float cx = GameSettings.ScreenWidth / 2f;
        int panelX = (int)(cx - 200), panelW = 400;
        int y = 140;

        // Difficulty buttons (only from title, click only)
        bool canChangeDiff = _settingsReturnScreen == GameScreen.Title;
        y += 30; // always advance past difficulty label (matches DrawSettings)
        if (canChangeDiff && clicked)
        {
            int dbw = 120;
            for (int i = 0; i < 3; i++)
            { var r = new Rectangle(panelX + i * (dbw + 10), y, dbw, 30);
              if (r.Contains(mouse.X, mouse.Y)) { _selectedDifficulty = (Difficulty)i; AudioManager.Instance.Play("ui_click", 0.4f); } }
        }
        y += 50;

        // Toolbar style picker (clickable; cycles or jumps to chosen style)
        y += 30;
        if (clicked)
        {
            int sbw = 95;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rectangle(panelX + i * (sbw + 5), y, sbw, 30);
                if (r.Contains(mouse.X, mouse.Y))
                {
                    var picked = (ToolbarStyle)i;
                    if (picked != _toolbarPrefs.Style)
                    {
                        _toolbarPrefs.Style = picked;
                        _toolbarPrefs.Save();
                        // Rebuild the toolbar if we have one (i.e. we're in-game).
                        if (_state != null) BuildToolbar();
                        AudioManager.Instance.Play("ui_click", 0.4f);
                    }
                }
            }
            // Reset-order button (only meaningful in Custom mode) — clears CustomOrder.
            if (_toolbarPrefs.Style == ToolbarStyle.Custom)
            {
                var resetR = new Rectangle(panelX, y + 36, 200, 24);
                if (resetR.Contains(mouse.X, mouse.Y))
                {
                    _toolbarPrefs.CustomOrder.Clear();
                    _toolbarPrefs.Save();
                    if (_state != null) BuildToolbar();
                    AudioManager.Instance.Play("ui_click", 0.4f);
                }
            }
        }
        y += 70; // header + style-row + (reset row reserved for Custom)

        // SFX slider (responds to click AND drag)
        y += 30;
        var sfxBar = new Rectangle(panelX, y, panelW, 20);
        if (held && sfxBar.Contains(mouse.X, mouse.Y))
            AudioManager.Instance.SfxVolume = Math.Clamp((mouse.X - panelX) / (float)panelW, 0f, 1f);
        y += 40;

        // Music slider (responds to click AND drag)
        y += 30;
        var musBar = new Rectangle(panelX, y, panelW, 20);
        if (held && musBar.Contains(mouse.X, mouse.Y))
        { AudioManager.Instance.MusicVolume = Math.Clamp((mouse.X - panelX) / (float)panelW, 0f, 1f); AudioManager.Instance.UpdateMusicVolume(); }
        y += 60;

        // Back button (click only)
        if (clicked)
        { var backBtn = new Rectangle((int)(cx - 60), y, 120, 32);
          if (backBtn.Contains(mouse.X, mouse.Y))
          { _screen = _settingsReturnScreen; if (_settingsReturnScreen == GameScreen.Playing) _paused = true; AudioManager.Instance.Play("ui_click", 0.4f); } }
    }

    // ---- INPUT ----

    private void HandleInput(KeyboardState kb, MouseState mouse)
    {
        bool lc = LeftClicked(mouse);
        bool ld = mouse.LeftButton == ButtonState.Pressed;
        bool lr = mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed;
        bool rc = mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;
        if (JustPressed(kb, Keys.OemPlus) || JustPressed(kb, Keys.Add)) { _state.SpeedUp(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.3f); }
        if (JustPressed(kb, Keys.OemMinus) || JustPressed(kb, Keys.Subtract)) { _state.SlowDown(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.3f); }

        // Mouse-wheel cone-aim rotation: each notch of the wheel rotates the next
        // Mortar/Artillery placement by 90°. Sign chosen so wheel-up = clockwise (positive Y up).
        int wheelDelta = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
        if (wheelDelta != 0 && _state.Mode == PlacementMode.Tower
            && (_state.SelectedTower == TowerType.Mortar || _state.SelectedTower == TowerType.Artillery))
        {
            int ticks = wheelDelta / 120;
            if (ticks != 0)
            {
                _pendingConeAimOffset += ticks * (MathF.PI / 2f);
                while (_pendingConeAimOffset > MathF.PI) _pendingConeAimOffset -= MathF.Tau;
                while (_pendingConeAimOffset < -MathF.PI) _pendingConeAimOffset += MathF.Tau;
                AudioManager.Instance.Play("ui_click", 0.3f);
            }
        }

        if (!_contextMenu.IsOpen) foreach (var btn in _toolbar) if (btn.Hotkey.HasValue && JustPressed(kb, btn.Hotkey.Value)) btn.OnClick();
        if (JustPressed(kb, Keys.R)) { StartGame(_seeds.CurrentSeed, _state.Difficulty); return; }
        if (lc && _contextMenu.IsOpen) { _contextMenu.HandleClick(mouse.X, mouse.Y); return; }
        if ((lc || rc) && _contextMenu.IsOpen) { _contextMenu.Close(); if (lc) return; }

        // Dropdown click handling (Grouped style)
        if (_openDropdown != null)
        {
            if (lc)
            {
                foreach (var item in _openDropdown)
                    if (item.Bounds.Contains(mouse.X, mouse.Y)) { item.OnClick(); return; }
                // Clicking outside the dropdown and outside the anchor closes it.
                if (!_openDropdownBounds.Contains(mouse.X, mouse.Y)
                    && (_openDropdownAnchor == null || !_openDropdownAnchor.Bounds.Contains(mouse.X, mouse.Y)))
                { CloseDropdown(); }
            }
        }

        // Drag handling (Custom style)
        if (_toolbarPrefs.Style == ToolbarStyle.Custom)
        {
            if (lc)
            {
                foreach (var btn in _toolbar)
                {
                    if (!btn.TowerType.HasValue) continue;
                    if (!btn.Bounds.Contains(mouse.X, mouse.Y)) continue;
                    _dragCandidate = btn; _dragStart = new Point(mouse.X, mouse.Y); _dragActive = false;
                    break;
                }
            }
            else if (ld && _dragCandidate != null)
            {
                int dx = mouse.X - _dragStart.X, dy = mouse.Y - _dragStart.Y;
                if (!_dragActive && dx * dx + dy * dy > 64) _dragActive = true;
            }
            else if (lr && _dragCandidate != null)
            {
                if (_dragActive)
                {
                    ToolbarButton? target = null;
                    foreach (var btn in _toolbar)
                    {
                        if (!btn.TowerType.HasValue || ReferenceEquals(btn, _dragCandidate)) continue;
                        if (btn.Bounds.Contains(mouse.X, mouse.Y)) { target = btn; break; }
                    }
                    if (target != null) SwapTowerButtons(_dragCandidate, target);
                    _dragCandidate = null; _dragActive = false;
                    return; // consume the release so it doesn't also click
                }
                // No drag occurred: fall through to normal click handling below.
                _dragCandidate = null; _dragActive = false;
            }
        }

        if (lc) foreach (var btn in _toolbar) if (btn.Bounds.Width > 0 && btn.Bounds.Contains(mouse.X, mouse.Y)) { btn.OnClick(); return; }
        if (JustPressed(kb, Keys.Space)) _waves.RequestStart();
        _mouseWorld = new Vector2(mouse.X, mouse.Y);
        _hoverCell = Map.WorldToGrid(_mouseWorld);
        // Two-pass hover: grid-based first so block-mode towers always match at any point
        // inside their cell (including corners), then pixel-distance so zone-mode towers —
        // which may share a cell with other towers — resolve to the one closest to the cursor.
        _hoveredTower = null;
        foreach (var t in _towers)
        {
            if (t.IsZonePlaced) continue;
            if (t.Is2x2)
            {
                int dx = _hoverCell.X - t.GridPos.X, dy = _hoverCell.Y - t.GridPos.Y;
                if (dx >= 0 && dx < 2 && dy >= 0 && dy < 2) { _hoveredTower = t; break; }
            }
            // Fence sits on a path cell — grid-cell match still works for hover.
            else if (t.GridPos == _hoverCell) { _hoveredTower = t; break; }
        }
        if (_hoveredTower == null)
        {
            float bestHoverDist = float.MaxValue;
            foreach (var t in _towers)
            {
                if (!t.IsZonePlaced) continue;
                float radius = GameSettings.CellSize / 2f;
                float d = Vector2.Distance(t.WorldPos, _mouseWorld);
                if (d <= radius && d < bestHoverDist) { bestHoverDist = d; _hoveredTower = t; }
            }
        }
        if (lc && mouse.Y > GameSettings.UIHeight) { if (_state.Mode == PlacementMode.Tower) TryPlaceTower(); else if (_state.Mode == PlacementMode.Wall) TryPlaceWall(); }
        if (rc && mouse.Y > GameSettings.UIHeight) HandleRightClick(mouse);
    }

    private void HandleRightClick(MouseState mouse)
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y)) return;
        var cell = _map.Grid[_hoverCell.X, _hoverCell.Y]; var items = new List<ContextMenuItem>(); bool bw = !_waves.WaveActive;
        // Zone-placed towers sit on CellType.Wall cells, and ElectricFences sit on path
        // (Empty) cells — use the hovered-tower reference to unlock the tower context menu
        // regardless of the underlying cell type.
        bool isTowerHovered = cell == CellType.Tower
            || (_hoveredTower != null && (_hoveredTower.IsZonePlaced || _hoveredTower.IsPathPlaced));
        if (isTowerHovered && _hoveredTower != null)
        {
            var t = _hoveredTower;
            if (bw && t.PlacedDuringPrep) items.Add(new ContextMenuItem($"Remove (${t.FullRefundValue})", () => { RemoveTower(t, t.FullRefundValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.LightGreen));
            else items.Add(new ContextMenuItem($"Sell (${t.SellValue})", () => { RemoveTower(t, t.SellValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.Tomato));
            if (t.Type == TowerType.DroneController)
            {
                // Split upgrade — Range and Count are mutually exclusive. Once a track is
                // picked, the other appears as a locked-out menu entry so the player can
                // see why it disappeared rather than wondering.
                int cost = t.UpgradeCost; bool ca = _state.Money >= cost;
                bool rangeLocked = t.ChosenPath == UpgradePath.Count;
                bool countLocked = t.ChosenPath == UpgradePath.Range;
                bool rangeMax = !t.CanUpgradeRange && !rangeLocked;
                bool countMax = !t.CanUpgradeCount && !countLocked;
                string rangeLabel = rangeLocked ? "Range path locked"
                    : rangeMax ? "Range maxed"
                    : $"Upgrade Range (${cost}) Lv{t.Level + 1}";
                string countLabel = countLocked ? "Count path locked"
                    : countMax ? $"Max drones ({GameSettings.DroneControllerMaxDrones})"
                    : $"+1 Drone (${cost})  [now {t.DroneCount}]";
                bool rangeEnabled = !rangeLocked && !rangeMax && ca;
                bool countEnabled = !countLocked && !countMax && ca;
                items.Add(new ContextMenuItem(rangeLabel,
                    () => { UpgradeTowerRange(t); AudioManager.Instance.Play("ui_upgrade", 0.6f); },
                    rangeEnabled ? new Color(120, 180, 255) : new Color(60, 80, 120), rangeEnabled));
                items.Add(new ContextMenuItem(countLabel,
                    () => { UpgradeTowerCount(t); AudioManager.Instance.Play("ui_upgrade", 0.6f); },
                    countEnabled ? new Color(160, 220, 120) : new Color(60, 100, 80), countEnabled));
            }
            else if (t.CanUpgrade) { int cost = t.UpgradeCost; bool ca = _state.Money >= cost;
                string desc = t.Type switch { TowerType.Flame => $"Upgrade Lv{t.Level+1} (${cost}) +Burn", TowerType.Rocket => $"Upgrade Lv{t.Level+1} (${cost}) +Splash",
                    TowerType.Tesla => $"Upgrade Lv{t.Level+1} (${cost}) +Vuln", TowerType.Tachyon => $"Upgrade Lv{t.Level+1} (${cost}) +Slow",
                    TowerType.Mortar or TowerType.Artillery => $"Upgrade Lv{t.Level+1} (${cost}) +Dmg +Splash",
                    TowerType.ElectricFence => $"Recharge & Reinforce (${cost})",
                    TowerType.Repair => $"Upgrade Lv{t.Level+1} (${cost}) +Drone +Range", _ => $"Upgrade Lv{t.Level+1} (${cost})" };
                items.Add(new ContextMenuItem(desc, () => { UpgradeTower(t); AudioManager.Instance.Play("ui_upgrade", 0.6f); }, ca ? Color.Gold : new Color(80, 60, 0), ca)); }
            else if (t.Type == TowerType.Grinder) items.Add(new ContextMenuItem("Not upgradeable", () => { }, Color.DimGray, false));
            else items.Add(new ContextMenuItem("Max Level", () => { }, Color.DimGray, false));
            if (t.Type == TowerType.Repair) { string arLabel = t.AutoRebuildEnabled ? "Auto-Rebuild: ON" : "Auto-Rebuild: OFF"; var tCopy = t;
                items.Add(new ContextMenuItem(arLabel, () => { tCopy.AutoRebuildEnabled = !tCopy.AutoRebuildEnabled; AudioManager.Instance.Play("ui_click", 0.4f); },
                    t.AutoRebuildEnabled ? new Color(80, 220, 80) : new Color(180, 180, 180))); }
        }
        else if (cell == CellType.Wall && bw && _map.IsPlayerWall(_hoverCell.X, _hoverCell.Y))
        { int col = _hoverCell.X, row = _hoverCell.Y;
          items.Add(new ContextMenuItem("Remove Wall (+1)", () => { RemovePlayerWall(col, row); AudioManager.Instance.Play("ui_click", 0.5f); }, new Color(160, 120, 200))); }
        if (items.Count > 0) _contextMenu.Open(_hoverCell, new Vector2(mouse.X, mouse.Y), items);
    }

    private void RemoveTower(Tower t, int refund) { _state.Money += refund;
        // Zone-placed towers never marked the cell as Tower, so leave the wall untouched.
        // Electric Fence lives on a path cell and never marked the grid either.
        if (t.Is2x2) _map.Remove2x2Tower(t.GridPos.X, t.GridPos.Y);
        else if (!t.IsZonePlaced && t.Type != TowerType.ElectricFence) _map.RemoveTower(t.GridPos.X, t.GridPos.Y);
        RemoveDronesForTower(t);
        _attackDrones.RemoveAll(a => ReferenceEquals(a.GetHomeTower(), t));
        _towers.Remove(t); _hoveredTower = null; }
    private void UpgradeTower(Tower t) { if (_state.Money < t.UpgradeCost) return; int db = t.DroneCount;
        _state.Money -= t.UpgradeCost; t.Upgrade();
        // Repair towers expand their drone fleet on upgrade; Drone Controllers grow theirs via UpgradeCount.
        for (int i = db; i < t.DroneCount; i++)
        { if (t.Type == TowerType.Repair) _drones.Add(new RepairDrone(t));
          else if (t.Type == TowerType.DroneController) _attackDrones.Add(new AttackDrone(t, i, t.DroneCount)); } }

    /// <summary>Drone Controller: extend per-drone attack range. Locks the upgrade path.</summary>
    private void UpgradeTowerRange(Tower t)
    {
        if (!t.CanUpgradeRange || _state.Money < t.UpgradeCost) return;
        _state.Money -= t.UpgradeCost;
        t.UpgradeRange();
    }

    /// <summary>Drone Controller: add another drone. Locks the upgrade path.</summary>
    private void UpgradeTowerCount(Tower t)
    {
        if (!t.CanUpgradeCount || _state.Money < t.UpgradeCost) return;
        int db = t.DroneCount;
        _state.Money -= t.UpgradeCost;
        t.UpgradeCount();
        for (int i = db; i < t.DroneCount; i++)
            _attackDrones.Add(new AttackDrone(t, i, t.DroneCount));
    }
    private void RemovePlayerWall(int c, int r) { if (_map.RemoveWall(c, r)) { _state.Walls++; NotifyEnemiesPathChanged(); } }

    private void TryPlaceTower()
    {
        if (!_state.CanAffordTower()) return;
        var sel = _state.SelectedTower;
        if (sel == TowerType.Repair)
        { // Repair is 2x2 — always block-mode, even when Zone is enabled. Zone placement doesn't
          // make sense for a structure that spans a 2x2 area.
          if (!_map.CanPlace2x2Tower(_hoverCell.X, _hoverCell.Y)) return; _state.SpendTower(); _map.Place2x2Tower(_hoverCell.X, _hoverCell.Y);
          AudioManager.Instance.Play("ui_click", 0.5f); var t = new Tower(_hoverCell.X, _hoverCell.Y, TowerType.Repair);
          if (_waves.WaveActive) t.PlacedDuringPrep = false; _towers.Add(t); _drones.Add(new RepairDrone(t)); _runStats.RecordTowerBuilt(); }
        else if (sel == TowerType.ElectricFence)
        {
            // Fence is placed on a path tile, not a wall. It must not block the only remaining
            // path (it shouldn't anyway — it sits *on* the path).
            if (!_map.CanPlaceFence(_hoverCell.X, _hoverCell.Y, _towers)) return;
            _state.SpendTower();
            AudioManager.Instance.Play("ui_click", 0.5f);
            var t = new Tower(_hoverCell.X, _hoverCell.Y, TowerType.ElectricFence);
            var pd = _map.GetPathDirectionAt(_hoverCell.X, _hoverCell.Y);
            t.SetFacing(MathF.Atan2(pd.Y, pd.X));
            if (_waves.WaveActive) t.PlacedDuringPrep = false;
            _towers.Add(t); _runStats.RecordTowerBuilt();
        }
        else if (_state.UsesZonePlacement && sel != TowerType.DroneController)
        { if (!CanZonePlaceAt(_hoverCell, _mouseWorld)) return;
          _state.SpendTower();
          // Zone towers do NOT mark the grid cell as CellType.Tower — multiple zone towers may
          // coexist on the same wall cell, constrained only by the minimum-spacing rule.
          AudioManager.Instance.Play("ui_click", 0.5f);
          var t = new Tower(_hoverCell.X, _hoverCell.Y, sel, _mouseWorld);
          if (sel == TowerType.Mortar || sel == TowerType.Artillery) { ConfigureConeFacing(t); _pendingConeAimOffset = 0f; }
          if (_waves.WaveActive) t.PlacedDuringPrep = false;
          _towers.Add(t); _runStats.RecordTowerBuilt(); }
        else
        { if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y)) return; _state.SpendTower(); _map.PlaceTower(_hoverCell.X, _hoverCell.Y);
          AudioManager.Instance.Play("ui_click", 0.5f); var t = new Tower(_hoverCell.X, _hoverCell.Y, sel);
          if (sel == TowerType.Mortar || sel == TowerType.Artillery) { ConfigureConeFacing(t); _pendingConeAimOffset = 0f; }
          if (sel == TowerType.DroneController) { SyncAttackDronesFor(t); AudioManager.Instance.Play("tower_drone_launch", 0.55f); }
          if (_waves.WaveActive) t.PlacedDuringPrep = false; _towers.Add(t); _runStats.RecordTowerBuilt(); }
    }

    /// <summary>Lock the cone-of-fire facing of a freshly-placed Mortar/Artillery tower
    /// to the nearest path waypoint, plus any wheel-applied rotation offset. Cone towers
    /// do not rotate after placement, so this is permanent.</summary>
    private void ConfigureConeFacing(Tower t)
    {
        if (_map.CurrentPath.Count == 0) { t.SetFacing(_pendingConeAimOffset); return; }
        Vector2 nearest = _map.CurrentPath[0]; float bd = float.MaxValue;
        foreach (var wp in _map.CurrentPath)
        { float d = Vector2.Distance(t.WorldPos, wp); if (d < bd) { bd = d; nearest = wp; } }
        var diff = nearest - t.WorldPos;
        float baseAngle = diff.LengthSquared() < 0.001f ? 0f : MathF.Atan2(diff.Y, diff.X);
        t.SetFacing(baseAngle + _pendingConeAimOffset);
    }

    /// <summary>Refresh the attack-drone roster for a Drone Controller so it matches the
    /// tower's current DroneCount. Called on placement and after each Count upgrade.</summary>
    private void SyncAttackDronesFor(Tower controller)
    {
        if (controller.Type != TowerType.DroneController) return;
        int current = 0;
        foreach (var d in _attackDrones)
            if (ReferenceEquals(GetAttackDroneHome(d), controller)) current++;
        for (int i = current; i < controller.DroneCount; i++)
            _attackDrones.Add(new AttackDrone(controller, i, controller.DroneCount));
    }

    /// <summary>Reflection-free home-tower lookup. We don't expose the field publicly to
    /// avoid leaking the orbit slot index, so we round-trip through a small helper.</summary>
    private static Tower? GetAttackDroneHome(AttackDrone d) => d.GetHomeTower();

    /// <summary>Zone-mode placement check: cursor must be over a wall cell (not path/spawn/exit
    /// or a block-mode tower), and must be at least <see cref="GameSettings.ZoneMinTowerSpacing"/>
    /// pixels away from every other tower. Repair towers are treated as large footprints.</summary>
    private bool CanZonePlaceAt(Point cell, Vector2 worldPos)
    {
        if (!_map.IsInBounds(cell.X, cell.Y)) return false;
        if (_map.Grid[cell.X, cell.Y] != CellType.Wall) return false;
        foreach (var t in _towers)
        {
            float spacing = t.Is2x2
                ? GameSettings.ZoneMinTowerSpacing + GameSettings.CellSize / 2f
                : GameSettings.ZoneMinTowerSpacing;
            if (Vector2.Distance(t.WorldPos, worldPos) < spacing) return false;
        }
        return true;
    }

    private void TryPlaceWall()
    {
        if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y)) return;
        if (_waves.WaveActive && _map.WouldAlterPath(_hoverCell.X, _hoverCell.Y)) return;
        _state.SpendWall(); _map.PlaceWall(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f); NotifyEnemiesPathChanged();
    }

    private void NotifyEnemiesPathChanged() { foreach (var e in _enemies) if (e.IsAlive) e.UpdatePath(_map.CurrentPath); }
    private void RemoveDronesForTower(Tower t) { _drones.Clear(); foreach (var tw in _towers) { if (tw == t || tw.Type != TowerType.Repair) continue;
        for (int d = 0; d < tw.DroneCount; d++) _drones.Add(new RepairDrone(tw)); } }

    // ---- SIMULATION ----

    private void UpdateEnemies(GameTime gt) { foreach (var e in _enemies) { e.Update(gt);
        if (e.ReachedEnd && !e.IsAlive) { _state.LoseLife(); _runStats.RecordLifeLost(); AudioManager.Instance.Play("life_lost", 0.7f, 0.1f); e.ReachedEnd = false; } } }

    private void UpdateEnemyAbilities(GameTime gt)
    {
        foreach (var e in _enemies) { if (!e.IsAlive) continue; switch (e.Type)
        {
            case EnemyType.Medic: if (e.AbilityCooldown <= 0f) { foreach (var o in _enemies) { if (!o.IsAlive || o == e || o.Health >= o.MaxHealth) continue;
                if (Vector2.Distance(e.Position, o.Position) <= GameSettings.MedicHealRange) o.Heal(GameSettings.MedicHealAmount); } e.AbilityCooldown = GameSettings.MedicHealCooldown; } break;
            case EnemyType.Hacker: if (e.AbilityCooldown <= 0f) { Tower? n = null; float bd = float.MaxValue;
                foreach (var t in _towers) { float d = Vector2.Distance(e.Position, t.WorldPos); if (d <= GameSettings.HackerDisableRange && d < bd) { bd = d; n = t; } }
                if (n != null) { n.DisabledTimer = GameSettings.HackerDisableDuration; e.AbilityCooldown = GameSettings.HackerDisableCooldown; } } break;
            case EnemyType.Blaster: if (e.AbilityCooldown <= 0f) { Tower? n = null; float bd = float.MaxValue;
                foreach (var t in _towers) { float d = Vector2.Distance(e.Position, t.WorldPos); if (d <= GameSettings.BlasterAttackRange && d < bd) { bd = d; n = t; } }
                if (n != null) { n.TakeTowerDamage(GameSettings.BlasterDamageToTower); e.AbilityCooldown = GameSettings.BlasterAttackCooldown; } } break;
        } }
    }

    private void UpdateTowers(GameTime gt) { float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        foreach (var t in _towers)
        {
            t.Update(gt, _enemies, _projectiles, _flames);
            if (t.IsConeTower) t.UpdateCone(gt, _enemies, _shells);
            if (t.Type == TowerType.ElectricFence) t.UpdateFence(gt, _enemies);
            t.UpdatePassiveHeal(dt, _towers);
        } }
    private void UpdateDrones(GameTime gt)
    {
        foreach (var d in _drones) d.Update(gt, _towers, _state);
        foreach (var a in _attackDrones) a.Update(gt, _enemies);
    }
    private void UpdateProjectiles(GameTime gt) { foreach (var p in _projectiles) p.Update(gt); foreach (var s in _shells) s.Update(gt); }
    private void UpdateFlames(GameTime gt) { foreach (var f in _flames) f.Update(gt, _enemies); }

    private void CleanUp()
    {
        _pendingSpawns.Clear();
        for (int i = _enemies.Count - 1; i >= 0; i--)
        { var e = _enemies[i]; if (!e.IsAlive) {
            if (!e.ReachedEnd && e.Health <= 0)
            { int reward = e.Reward; float grind = 0f;
              foreach (var t in _towers) { if (t.Type == TowerType.Grinder && Vector2.Distance(t.WorldPos, e.Position) <= t.Range) grind = Math.Max(grind, t.GrinderBonusRatio); }
              int total = reward + (int)(reward * grind); _state.EarnReward(total); _runStats.RecordKill(e.Type); _runStats.RecordMoney(total);
              if (e.LastDamageSource.HasValue) _runStats.RecordTowerKill(e.LastDamageSource.Value);
              if (grind > 0) AudioManager.Instance.PlayVaried("tower_grinder", 0.3f, 0.15f, 0.1f);
              if (e.Reward > 25) AudioManager.Instance.PlayVaried("death_boss", 0.5f, 0.1f, 0.08f);
              else if (e.Reward > 15) AudioManager.Instance.PlayVaried("death_medium", 0.4f, 0.15f, 0.05f);
              else AudioManager.Instance.PlayVaried("death_small", 0.3f, 0.2f, 0.04f);
              if (e.Type == EnemyType.Kamikaze) { foreach (var t in _towers) if (Vector2.Distance(e.Position, t.WorldPos) <= GameSettings.KamikazeExplosionRadius) t.TakeTowerDamage(GameSettings.KamikazeTowerDamage);
                  AudioManager.Instance.PlayVaried("rocket_explode", 0.7f, 0.1f, 0.05f); }
              if (e.Type == EnemyType.Spreader && !e.IsChild) for (int c = 0; c < GameSettings.SpreaderChildCount; c++)
              { var ch = new Enemy(e.Path, e.MaxHealth * GameSettings.SpreaderChildHealthRatio, e.BaseSpeed * GameSettings.SpreaderChildSpeedBonus,
                  Math.Max(1, e.Reward / 3), _sprites.GetSprite(EnemyType.Spreader), EnemyType.Spreader) { IsChild = true };
                ch.Position = e.Position + new Vector2((c - 0.5f) * 10f, 0); ch.UpdatePath(e.Path); _pendingSpawns.Add(ch); }
              if (e.Type == EnemyType.Centipede && !e.IsChild) for (int c = 0; c < GameSettings.CentipedeSegmentCount; c++)
              { var seg = new Enemy(e.Path, e.MaxHealth * GameSettings.CentipedeSegmentHealthRatio, e.BaseSpeed * GameSettings.CentipedeSegmentSpeedBonus,
                  Math.Max(1, e.Reward / 4), _sprites.GetSprite(EnemyType.Centipede), EnemyType.Centipede) { IsChild = true };
                seg.Position = e.Position + new Vector2(c * -8f, c * -4f); seg.UpdatePath(e.Path); _pendingSpawns.Add(seg); } }
            _enemies.RemoveAt(i); } }
        if (_pendingSpawns.Count > 0) _enemies.AddRange(_pendingSpawns);
        bool rebuildDr = false;
        for (int i = _towers.Count - 1; i >= 0; i--) { if (_towers[i].IsDestroyed) { var t = _towers[i];
            if (t.Type != TowerType.Repair) _destroyedThisWave.Add(new DestroyedTowerRecord(t.Type, t.GridPos, t.Level, t.TotalInvested, t.IsZonePlaced, t.WorldPos));
            // Zone-placed towers don't own their cell, so leave the wall intact on destruction.
            if (t.Is2x2) _map.Remove2x2Tower(t.GridPos.X, t.GridPos.Y);
            else if (!t.IsZonePlaced) _map.RemoveTower(t.GridPos.X, t.GridPos.Y);
            if (t.Type == TowerType.Repair) rebuildDr = true; _towers.RemoveAt(i); if (_hoveredTower == t) _hoveredTower = null; } }
        if (rebuildDr) { _drones.Clear(); foreach (var t in _towers) if (t.Type == TowerType.Repair) for (int d = 0; d < t.DroneCount; d++) _drones.Add(new RepairDrone(t)); }
        _projectiles.RemoveAll(p => !p.IsActive); _flames.RemoveAll(f => !f.IsAlive);
        _shells.RemoveAll(s => !s.IsActive);
        // Drop attack drones whose home tower is gone.
        _attackDrones.RemoveAll(a => { var h = a.GetHomeTower(); return h == null || h.IsDestroyed || !_towers.Contains(h); });
    }

    // ---- DRAW ----

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 8, 16));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        switch (_screen) { case GameScreen.Title: DrawTitle(); break; case GameScreen.Playing: DrawGame(); if (_paused) DrawPauseOverlay(); break;
            case GameScreen.GameOver: DrawGame(); DrawGameOverOverlay(); break; case GameScreen.Glossary: DrawGlossary(); break; case GameScreen.Settings: DrawSettings(); break; }
        _spriteBatch.End(); base.Draw(gameTime);
    }

    private void DrawTitle()
    {
        float cx = GameSettings.ScreenWidth / 2f;
        DrawCentred("RAD DEFENCE", cx, 80, Color.CornflowerBlue);
        DrawCentred("Tower Defence with Seeded Maps", cx, 115, Color.Gray);
        DrawCentred($"Seed: {_seeds.CurrentSeed}  |  Difficulty: {_selectedDifficulty}", cx, 170, Color.Gold);
        DrawCentred("[ENTER] Start  [SPACE] New Seed  [TAB] Enter Seed  [S] Settings  [G] Glossary", cx, 200, Color.LightGray);
        if (SaveManager.HasSave()) DrawCentred("[L] Load Saved Game", cx, 225, new Color(100, 220, 100));
        if (_seedInputActive) DrawCentred($"Enter seed: {(_seedInput.Length > 0 ? _seedInput : "_")}", cx, 250, Color.White);
        if (_leaderboard.TopRuns.Count > 0)
        { DrawCentred("--- High Scores ---", cx, 290, Color.Gold); int y = 318;
          for (int i = 0; i < Math.Min(8, _leaderboard.TopRuns.Count); i++)
          { var run = _leaderboard.TopRuns[i]; DrawCentred($"#{i+1}  {run.Score} pts  Wave {run.Wave}  Kills {run.Kills}  Seed {run.Seed}", cx, y, i == 0 ? Color.Gold : Color.LightGray); y += 22; } }
        if (_seeds.Favourites.Count > 0)
        { DrawCentred("--- Favourites (click to load) ---", cx, 510, new Color(160, 120, 200)); int y = 540;
          foreach (var fav in _seeds.Favourites) { string star = _seeds.CurrentSeed == fav.Seed ? "> " : "  ";
            DrawCentred($"{star}Seed {fav.Seed}  |  Best: {fav.BestScore} pts  Wave {fav.BestWave}", cx, y, Color.LightGray); y += 24; if (y > 640) break; } }
        if (_leaderboard.Career.TotalGamesPlayed > 0)
        { var c = _leaderboard.Career; DrawCentred($"Career: {c.TotalGamesPlayed} games  {c.TotalKills} kills  Best wave {c.HighestWave}  Best score {c.HighestScore}", cx, GameSettings.ScreenHeight - 60, Color.DimGray); }
        DrawCentred("1-9/0/A/D/E: Towers | W: Wall | Z: Block/Zone | +/-: Speed | R-Click: Sell/Upgrade | SPACE: Wave | M: Mute", cx, GameSettings.ScreenHeight - 30, Color.DimGray);
    }

    private void DrawSettings()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(8, 8, 16));
        float cx = GameSettings.ScreenWidth / 2f; DrawCentred("SETTINGS", cx, 60, Color.CornflowerBlue);
        int panelX = (int)(cx - 200), panelW = 400; int y = 140;
        bool canChangeDiff = _settingsReturnScreen == GameScreen.Title;
        _spriteBatch.DrawString(_font, canChangeDiff ? "Difficulty:" : "Difficulty: (locked during game)", new Vector2(panelX, y), canChangeDiff ? Color.White : Color.DimGray);
        y += 30;
        string[] diffs = { "Easy", "Normal", "Hard" }; Color[] dc = { new Color(100, 220, 100), Color.CornflowerBlue, new Color(220, 80, 80) }; int dbw = 120;
        for (int i = 0; i < 3; i++) { var r = new Rectangle(panelX + i * (dbw + 10), y, dbw, 30); bool sel = (int)_selectedDifficulty == i;
            _spriteBatch.Draw(_sprites.Pixel, r, sel ? dc[i] * 0.4f : new Color(40, 40, 60));
            DrawCentred(diffs[i], r.X + r.Width / 2f, r.Y + 6, sel ? dc[i] : (canChangeDiff ? Color.Gray : Color.DimGray)); }
        y += 50;
        // Toolbar style picker
        _spriteBatch.DrawString(_font, "Toolbar Layout:", new Vector2(panelX, y), Color.White);
        y += 30;
        string[] styleNames = { "Compact", "Grouped", "Two-Row", "Custom" };
        Color[] styleColors = { new Color(160, 200, 240), new Color(220, 200, 80), new Color(160, 220, 160), new Color(255, 140, 80) };
        int sbw = 95;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(panelX + i * (sbw + 5), y, sbw, 30);
            bool sel = (int)_toolbarPrefs.Style == i;
            _spriteBatch.Draw(_sprites.Pixel, r, sel ? styleColors[i] * 0.4f : new Color(40, 40, 60));
            DrawCentred(styleNames[i], r.X + r.Width / 2f, r.Y + 6, sel ? styleColors[i] : Color.Gray);
        }
        y += 36;
        string styleDesc = _toolbarPrefs.Style switch
        {
            ToolbarStyle.Compact  => "Single row of compact buttons (default).",
            ToolbarStyle.Grouped  => "Category buttons open dropdown menus.",
            ToolbarStyle.TwoRow   => "Two rows — projectiles up top, utility below.",
            ToolbarStyle.Custom   => "Click-drag tower buttons to reorder. Saved automatically.",
            _ => ""
        };
        _spriteBatch.DrawString(_font, styleDesc, new Vector2(panelX, y), Color.DimGray);
        if (_toolbarPrefs.Style == ToolbarStyle.Custom)
        {
            var resetR = new Rectangle(panelX, y + 18, 200, 24);
            _spriteBatch.Draw(_sprites.Pixel, resetR, new Color(60, 40, 80));
            DrawCentred("Reset to Default Order", resetR.X + resetR.Width / 2f, resetR.Y + 4, new Color(220, 180, 255));
        }
        y += 34;

        _spriteBatch.DrawString(_font, $"SFX Volume: {(int)(AudioManager.Instance.SfxVolume * 100)}%", new Vector2(panelX, y), Color.White); y += 30;
        DrawSlider(panelX, y, panelW, 20, AudioManager.Instance.SfxVolume, new Color(100, 180, 255)); y += 40;
        _spriteBatch.DrawString(_font, $"Music Volume: {(int)(AudioManager.Instance.MusicVolume * 100)}%", new Vector2(panelX, y), Color.White); y += 30;
        DrawSlider(panelX, y, panelW, 20, AudioManager.Instance.MusicVolume, new Color(180, 100, 255)); y += 60;
        var backRect = new Rectangle((int)(cx - 60), y, 120, 32);
        _spriteBatch.Draw(_sprites.Pixel, backRect, new Color(60, 60, 80)); DrawCentred("[ESC] Back", cx, y + 7, Color.LightGray); y += 60;
        string desc = _selectedDifficulty switch { Difficulty.Easy => "Easy: 30 lives, $200 start, enemies have 70% HP / 90% speed, 130% rewards",
            Difficulty.Hard => "Hard: 12 lives, $85 start, enemies have 125% HP / 105% speed, 85% rewards",
            _ => "Normal: 20 lives, $100 start, standard enemy stats and rewards" };
        DrawCentred(desc, cx, y, Color.DimGray);
    }

    private void DrawSlider(int x, int y, int w, int h, float value, Color fillColor)
    { _spriteBatch.Draw(_sprites.Pixel, new Rectangle(x, y, w, h), new Color(30, 30, 50));
      int fillW = (int)(w * Math.Clamp(value, 0f, 1f));
      if (fillW > 0) _spriteBatch.Draw(_sprites.Pixel, new Rectangle(x, y, fillW, h), fillColor * 0.6f);
      _spriteBatch.Draw(_sprites.Pixel, new Rectangle(Math.Max(x, x + fillW - 3), y - 2, 6, h + 4), fillColor); }

    private void DrawPauseOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.7f);
        float cx = GameSettings.ScreenWidth / 2f, cy = GameSettings.ScreenHeight / 2f;
        DrawCentred("PAUSED", cx, cy - 60, Color.White);
        int bw = 160, bh = 32, gap = 8, bx = (int)(cx - bw / 2f), by = (int)(cy + 10);
        DrawButton(bx, by, bw, bh, "Resume", Color.LightGreen); by += bh + gap;
        bool cs = !_waves.WaveActive; DrawButton(bx, by, bw, bh, cs ? "Save Game" : "Save (in wave)", cs ? new Color(100, 200, 255) : Color.DimGray); by += bh + gap;
        DrawButton(bx, by, bw, bh, "Settings", Color.LightGray); by += bh + gap;
        DrawButton(bx, by, bw, bh, "Restart", Color.Yellow); by += bh + gap;
        DrawButton(bx, by, bw, bh, "Glossary", Color.CornflowerBlue); by += bh + gap;
        DrawButton(bx, by, bw, bh, "Quit to Menu", Color.Tomato);
        DrawCentred("[ESC] Resume  [+/-] Speed", cx, GameSettings.ScreenHeight - 30, Color.DimGray);
    }

    private void DrawButton(int x, int y, int w, int h, string label, Color color)
    { _spriteBatch.Draw(_sprites.Pixel, new Rectangle(x, y, w, h), new Color(40, 40, 60));
      _spriteBatch.Draw(_sprites.Pixel, new Rectangle(x, y, w, 2), color * 0.5f);
      DrawCentred(label, x + w / 2f, y + h / 2f - 8, color); }

    private void DrawGame()
    { _map.Draw(_spriteBatch, _sprites); DrawPlacementGhost(); DrawTowers(); DrawDrones(); DrawFlames(); DrawEnemies(); DrawProjectiles();
      DrawHUD(); DrawToolbar(); _contextMenu.Draw(_spriteBatch, _sprites.Pixel, _font); }

    private void DrawPlacementGhost()
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y) || _contextMenu.IsOpen) return;
        if (_state.Mode == PlacementMode.Tower && _state.SelectedTower == TowerType.ElectricFence)
        {
            var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y); int size = GameSettings.CellSize - 4;
            var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);
            bool ok = _map.CanPlaceFence(_hoverCell.X, _hoverCell.Y, _towers) && _state.CanAffordTower();
            _spriteBatch.Draw(_sprites.Pixel, rect, (ok ? new Color(255, 220, 80) : Color.Red) * 0.2f);
            if (ok) _spriteBatch.Draw(_sprites.Towers[TowerType.ElectricFence], rect, Color.White * 0.5f);
            return;
        }
        if (_state.Mode == PlacementMode.Tower && _state.SelectedTower == TowerType.Repair)
        { bool canPlace = _map.CanPlace2x2Tower(_hoverCell.X, _hoverCell.Y) && _state.CanAffordTower();
          for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
          { int cx2 = _hoverCell.X + dx, cy2 = _hoverCell.Y + dy; if (!_map.IsInBounds(cx2, cy2)) continue;
            var pos = Map.GridToWorld(cx2, cy2); int sz = GameSettings.CellSize - 4;
            _spriteBatch.Draw(_sprites.Pixel, new Rectangle((int)(pos.X - sz / 2f), (int)(pos.Y - sz / 2f), sz, sz), (canPlace ? new Color(80, 220, 80) : Color.Red) * 0.2f); }
          if (canPlace) { var t = new Tower(_hoverCell.X, _hoverCell.Y, TowerType.Repair); int size = GameSettings.CellSize * 2 - 4;
            _spriteBatch.Draw(_sprites.Towers[TowerType.Repair], new Rectangle((int)(t.WorldPos.X - size / 2f), (int)(t.WorldPos.Y - size / 2f), size, size), Color.White * 0.5f);
            t.DrawRange(_spriteBatch, _sprites.Ring); } }
        else if (_state.Mode == PlacementMode.Tower && _state.UsesZonePlacement)
        { // Zone ghost follows the mouse exactly. Visual cue: a faint zone outline on the
          // hovered wall cell so players see where the placement zone starts.
          int size = GameSettings.CellSize - 4;
          var ghostRect = new Rectangle((int)(_mouseWorld.X - size / 2f), (int)(_mouseWorld.Y - size / 2f), size, size);
          if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] == CellType.Wall)
          {
              var cellRect = new Rectangle(_hoverCell.X * GameSettings.CellSize, _hoverCell.Y * GameSettings.CellSize + GameSettings.UIHeight,
                  GameSettings.CellSize, GameSettings.CellSize);
              _spriteBatch.Draw(_sprites.Pixel, cellRect, new Color(255, 140, 80) * 0.15f);
          }
          if (!CanZonePlaceAt(_hoverCell, _mouseWorld) || !_state.CanAffordTower())
          { if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y) || _map.Grid[_hoverCell.X, _hoverCell.Y] != CellType.Wall) return;
            _spriteBatch.Draw(_sprites.Pixel, ghostRect, Color.Red * 0.25f); return; }
          _spriteBatch.Draw(_sprites.Towers[_state.SelectedTower], ghostRect, Color.White * 0.5f);
          new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower, _mouseWorld).DrawRange(_spriteBatch, _sprites.Ring); }
        else if (_state.Mode == PlacementMode.Tower)
        { var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y); int size = GameSettings.CellSize - 4;
          var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);
          if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y) || !_state.CanAffordTower())
          { if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] != CellType.Wall) return;
            _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return; }
          _spriteBatch.Draw(_sprites.Towers[_state.SelectedTower], rect, Color.White * 0.5f);
          var ghost = new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower);
          if (_state.SelectedTower == TowerType.Mortar || _state.SelectedTower == TowerType.Artillery)
              ConfigureConeFacing(ghost);
          ghost.DrawRange(_spriteBatch, _sprites.Ring);
          if (ghost.IsConeTower) ghost.DrawConeOverlay(_spriteBatch, _sprites.Pixel,
              _state.SelectedTower == TowerType.Mortar ? new Color(150, 120, 90) : new Color(180, 140, 60), 0.22f); }
        else if (_state.Mode == PlacementMode.Wall)
        { var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y); int size = GameSettings.CellSize - 4;
          var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);
          if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y))
          { if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] == CellType.Empty)
              _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return; }
          if (_waves.WaveActive && _map.WouldAlterPath(_hoverCell.X, _hoverCell.Y))
          { _spriteBatch.Draw(_sprites.Pixel, rect, new Color(255, 140, 0) * 0.3f); return; }
          _spriteBatch.Draw(_sprites.TileWall, rect, Color.White * 0.5f); }
    }

    private void DrawTowers() { foreach (var t in _towers)
        {
            t.Draw(_spriteBatch, _sprites, t == _hoveredTower);
            // Cone-of-fire wedge: faint when not hovered, brighter on hover.
            if (t.IsConeTower)
            {
                Color rc = t.Type == TowerType.Mortar ? new Color(150, 120, 90) : new Color(180, 140, 60);
                t.DrawConeOverlay(_spriteBatch, _sprites.Pixel, rc, t == _hoveredTower ? 0.22f : 0.10f);
            }
            if (t == _hoveredTower) t.DrawRange(_spriteBatch, _sprites.Ring);
        } }
    private void DrawDrones() { foreach (var d in _drones) d.Draw(_spriteBatch, _sprites);
        foreach (var a in _attackDrones) a.Draw(_spriteBatch, _sprites); }
    private void DrawEnemies() { foreach (var e in _enemies) e.Draw(_spriteBatch, _sprites.Pixel); }
    private void DrawProjectiles() { foreach (var p in _projectiles) p.Draw(_spriteBatch, _sprites);
        foreach (var s in _shells) s.Draw(_spriteBatch, _sprites); }
    private void DrawFlames() { foreach (var f in _flames) f.Draw(_spriteBatch, _sprites.Pixel); }

    private void DrawHUD()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.UIHeight), new Color(20, 20, 35));
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, GameSettings.UIHeight - 1, GameSettings.ScreenWidth, 1), new Color(50, 50, 80));
        float y1 = 6;
        _spriteBatch.DrawString(_font, $"Lives: {_state.Lives}", new Vector2(10, y1), Color.Tomato);
        _spriteBatch.DrawString(_font, $"${_state.Money}", new Vector2(150, y1), Color.Gold);
        _spriteBatch.DrawString(_font, $"Score: {_state.Score}", new Vector2(260, y1), Color.White);
        _spriteBatch.DrawString(_font, $"Walls: {_state.Walls}", new Vector2(420, y1), new Color(160, 120, 200));
        var seedText = $"Seed: {_seeds.CurrentSeed}"; float seedX = GameSettings.ScreenWidth - _font.MeasureString(seedText).X - 10;
        _spriteBatch.DrawString(_font, seedText, new Vector2(seedX, y1), Color.DimGray);
        var killsText = $"Kills: {_runStats.TotalKills}";
        _spriteBatch.DrawString(_font, killsText, new Vector2(seedX - _font.MeasureString(killsText).X - 16, y1), new Color(180, 180, 180));
        string wave;
        if (_waves.WaveActive) wave = $"Wave {_waves.CurrentWave}  (R-click towers to sell/upgrade)";
        else if (_waves.CurrentWave == 0) wave = "Place towers on walls, then SPACE to start";
        else { string cl = $"Wave {_waves.CurrentWave} cleared! +{_waves.LastWallGrant} walls  R-click to edit";
            wave = _state.AutoStartWaves ? $"{cl}  Next in {Math.Max(0, 10 - _waves.BreakTimer):0}s  [SPACE]" : $"{cl}  Press SPACE when ready"; }
        var ws = _font.MeasureString(wave); _spriteBatch.DrawString(_font, wave, new Vector2((GameSettings.ScreenWidth - ws.X) / 2f, 26), Color.CornflowerBlue);
    }

    private void DrawToolbar()
    {
        foreach (var btn in _toolbar)
            if (btn.Bounds.Width > 0) btn.Draw(_spriteBatch, _sprites.Pixel, _font);

        // Dropdown panel (Grouped style)
        if (_openDropdown != null)
        {
            _spriteBatch.Draw(_sprites.Pixel, _openDropdownBounds, new Color(25, 25, 45));
            int b = 1;
            var rb = _openDropdownBounds;
            _spriteBatch.Draw(_sprites.Pixel, new Rectangle(rb.X, rb.Y, rb.Width, b), new Color(120, 120, 160));
            _spriteBatch.Draw(_sprites.Pixel, new Rectangle(rb.X, rb.Bottom - b, rb.Width, b), new Color(120, 120, 160));
            _spriteBatch.Draw(_sprites.Pixel, new Rectangle(rb.X, rb.Y, b, rb.Height), new Color(120, 120, 160));
            _spriteBatch.Draw(_sprites.Pixel, new Rectangle(rb.Right - b, rb.Y, b, rb.Height), new Color(120, 120, 160));
            foreach (var item in _openDropdown) item.Draw(_spriteBatch, _sprites.Pixel, _font);
        }

        // Drag preview (Custom style)
        if (_toolbarPrefs.Style == ToolbarStyle.Custom && _dragActive && _dragCandidate != null)
        {
            var ms = Mouse.GetState();
            var srcRect = _dragCandidate.Bounds;
            var ghost = new Rectangle(ms.X - srcRect.Width / 2, ms.Y - srcRect.Height / 2, srcRect.Width, srcRect.Height);
            _spriteBatch.Draw(_sprites.Pixel, ghost, _dragCandidate.Accent * 0.5f);
            var lbl = _dragCandidate.Label;
            var ts = _font.MeasureString(lbl);
            _spriteBatch.DrawString(_font, lbl, new Vector2(ghost.X + (ghost.Width - ts.X) / 2f, ghost.Y + (ghost.Height - ts.Y) / 2f), Color.White);
        }
    }

    private void DrawGlossary()
    {
        var type = GlossaryOrder[_glossaryIndex]; var entry = EnemyGlossary.Get(type);
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(8, 8, 16));
        DrawCentred("ENEMY GLOSSARY", GameSettings.ScreenWidth / 2f, 20, Color.CornflowerBlue);
        int sideX = 15, sideY = 70;
        for (int i = 0; i < GlossaryOrder.Length; i++)
        { var et = GlossaryOrder[i]; var ge = EnemyGlossary.Get(et); bool sel = i == _glossaryIndex;
          if (sel) _spriteBatch.Draw(_sprites.Pixel, new Rectangle(sideX - 4, sideY - 2, 198, 40), ge.AccentColor * 0.2f);
          _spriteBatch.Draw(_sprites.GetSprite(et), new Rectangle(sideX, sideY + 2, 32, 32), Color.White);
          _spriteBatch.DrawString(_font, ge.Name, new Vector2(sideX + 38, sideY + 8), sel ? ge.AccentColor : Color.Gray); sideY += 44; }
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(220, 60, 2, GameSettings.ScreenHeight - 80), new Color(40, 40, 60));
        int panelX = 240, panelW = GameSettings.ScreenWidth - panelX - 20; float dy = 70; int spriteSize = 80;
        _spriteBatch.Draw(_sprites.GetSprite(type), new Rectangle(panelX, (int)dy, spriteSize, spriteSize), Color.White);
        _spriteBatch.DrawString(_font, entry.Name, new Vector2(panelX + spriteSize + 16, dy + 6), entry.AccentColor);
        _spriteBatch.DrawString(_font, $"[{type}]", new Vector2(panelX + spriteSize + 16, dy + 30), Color.DimGray);
        if (_leaderboard.Career.TotalKillsByEnemyType.TryGetValue(type, out int ck) && ck > 0)
            _spriteBatch.DrawString(_font, $"Lifetime kills: {ck}", new Vector2(panelX + spriteSize + 16, dy + 52), new Color(120, 120, 140));
        dy += spriteSize + 16;
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(panelX, (int)dy, panelW, 3), entry.AccentColor * 0.5f); dy += 14;
        _spriteBatch.DrawString(_font, "INTEL:", new Vector2(panelX, dy), new Color(160, 160, 180)); dy += 22;
        DrawWrapped(entry.Lore, panelX, (int)dy, panelW, Color.LightGray, out float loreH); dy += loreH + 16;
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(panelX, (int)dy, panelW, 2), entry.AccentColor * 0.3f); dy += 10;
        _spriteBatch.DrawString(_font, "ABILITY:", new Vector2(panelX, dy), entry.AccentColor); dy += 22;
        DrawWrapped(entry.Ability, panelX, (int)dy, panelW, Color.White, out _);
        DrawCentred("[A/Left] Prev    [D/Right] Next    [ESC/G] Back", GameSettings.ScreenWidth / 2f, GameSettings.ScreenHeight - 30, Color.DimGray);
        DrawCentred($"{_glossaryIndex + 1} / {GlossaryOrder.Length}", GameSettings.ScreenWidth / 2f, GameSettings.ScreenHeight - 55, Color.Gray);
    }

    private void DrawGameOverOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.8f);
        float cx = GameSettings.ScreenWidth / 2f; float y = 160;
        DrawCentred("GAME OVER", cx, y, Color.Red); y += 40;
        if (_lastRank > 0) { DrawCentred(_lastRank == 1 ? "NEW HIGH SCORE!" : $"Rank #{_lastRank} on leaderboard!", cx, y, _lastRank <= 3 ? Color.Gold : Color.CornflowerBlue); y += 30; }
        DrawCentred($"Score: {_state.Score}  |  Wave: {_waves.CurrentWave}  |  Seed: {_seeds.CurrentSeed}  |  {_state.Difficulty}", cx, y, Color.White); y += 35;
        DrawCentred($"Kills: {_runStats.TotalKills}  |  Towers Built: {_runStats.TowersBuilt}  |  Time: {_runStats.PlayTimeSeconds:0}s", cx, y, Color.LightGray); y += 30;
        if (_runStats.KillsByEnemyType.Count > 0) { string kb = ""; foreach (var (et, c) in _runStats.KillsByEnemyType) kb += $"{et}: {c}  "; DrawCentred(kb.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 25; }
        if (_runStats.KillsByTowerType.Count > 0) { string tb = "Kills by tower: "; foreach (var (tt, c) in _runStats.KillsByTowerType) tb += $"{Tower.GetName(tt)}: {c}  "; DrawCentred(tb.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 35; }
        bool isFav = _seeds.IsFavourite(_seeds.CurrentSeed);
        DrawCentred(isFav ? "Favourited!" : "[F] Favourite this seed", cx, y, isFav ? Color.Gold : new Color(160, 120, 200)); y += 35;
        DrawCentred("[R] Replay    [N] New Seed    [ESC] Menu    [G] Glossary", cx, y, Color.LightGray);
    }

    // ---- HELPERS ----

    private void DrawWrapped(string text, int x, int y, int maxWidth, Color color, out float totalHeight)
    { totalHeight = 0; string[] words = text.Split(' '); string line = ""; float lineHeight = _font.MeasureString("M").Y + 2;
      foreach (var word in words) { string test = line.Length == 0 ? word : line + " " + word;
        if (_font.MeasureString(test).X > maxWidth && line.Length > 0) { _spriteBatch.DrawString(_font, line, new Vector2(x, y + totalHeight), color); totalHeight += lineHeight; line = word; }
        else line = test; }
      if (line.Length > 0) { _spriteBatch.DrawString(_font, line, new Vector2(x, y + totalHeight), color); totalHeight += lineHeight; } }

    private void DrawCentred(string text, float cx, float y, Color color)
    { var s = _font.MeasureString(text); _spriteBatch.DrawString(_font, text, new Vector2(cx - s.X / 2f, y), color); }

    private bool JustPressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && !_prevKb.IsKeyDown(key);
    private bool LeftClicked(MouseState m) => m.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
    private bool ClickedRect(MouseState m, int x, int y, int w, int h) => new Rectangle(x, y, w, h).Contains(m.X, m.Y);

    private Texture2D CreateRing(int diameter, int thickness)
    { var tex = new Texture2D(GraphicsDevice, diameter, diameter); var data = new Color[diameter * diameter]; float r = diameter / 2f;
      for (int y = 0; y < diameter; y++) for (int x = 0; x < diameter; x++)
      { float dx = x - r + 0.5f, dy = y - r + 0.5f; float dist = MathF.Sqrt(dx * dx + dy * dy);
        data[y * diameter + x] = MathF.Abs(dist - r + thickness) <= thickness ? Color.White : Color.Transparent; }
      tex.SetData(data); return tex; }
}
