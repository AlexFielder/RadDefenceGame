namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

public enum GameScreen { Title, Playing, GameOver, Glossary }

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

    // auto-rebuild: track towers destroyed during the current wave
    private record DestroyedTowerRecord(TowerType Type, Point GridPos, int Level, int TotalInvested);
    private readonly List<DestroyedTowerRecord> _destroyedThisWave = new();

    private GameStats _runStats = null!;
    private Leaderboard _leaderboard = null!;
    private int _lastRank;

    private MouseState _prevMouse;
    private KeyboardState _prevKb;
    private Point _hoverCell;
    private Tower? _hoveredTower;

    private readonly List<ToolbarButton> _toolbar = new();
    private ToolbarButton? _speedButton;
    private ToolbarButton? _autoStartButton;
    private ToolbarButton? _muteButton;
    private readonly ContextMenu _contextMenu = new();

    private bool _autoStartPref = false;
    private GameScreen _screen = GameScreen.Title;
    private string _seedInput = "";
    private bool _seedInputActive;
    private bool _gameOverSoundPlayed;
    private bool _paused;

    // -- glossary --
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
        _seeds = new SeedManager();
        _seeds.NewRandomSeed();
    }

    private void StartGame(int seed)
    {
        _seeds.SetSeed(seed);
        _map = new Map(seed);
        _state = new GameState();
        _state.AutoStartWaves = _autoStartPref;
        _waves = new WaveManager(_map);
        _waves.SetSprites(_sprites);
        _waves.OnWaveStarting += OnWaveStarting;
        _waves.OnWaveCompleted += OnWaveCompleted;
        _enemies.Clear(); _towers.Clear(); _projectiles.Clear(); _flames.Clear();
        _pendingSpawns.Clear(); _drones.Clear(); _destroyedThisWave.Clear();
        _contextMenu.Close();
        _screen = GameScreen.Playing;
        _seedInput = ""; _seedInputActive = false;
        _gameOverSoundPlayed = false; _paused = false; _lastRank = 0;
        _runStats = new GameStats { Seed = seed };
        BuildToolbar();
    }

    private void OnWaveStarting()
    {
        foreach (var t in _towers) t.PlacedDuringPrep = false;
        _destroyedThisWave.Clear();
        AudioManager.Instance.Play("wave_start", 0.8f);
    }

    private void OnWaveCompleted()
    {
        ProcessAutoRebuild();
    }

    /// <summary>At wave end, repair towers with auto-rebuild enabled attempt to rebuild destroyed towers.</summary>
    private void ProcessAutoRebuild()
    {
        if (_destroyedThisWave.Count == 0) return;

        // collect repair towers with auto-rebuild enabled, sorted by range (widest coverage first)
        var repairTowers = new List<Tower>();
        foreach (var t in _towers)
            if (t.Type == TowerType.Repair && t.AutoRebuildEnabled && !t.IsDestroyed)
                repairTowers.Add(t);
        if (repairTowers.Count == 0) return;

        for (int i = _destroyedThisWave.Count - 1; i >= 0; i--)
        {
            var record = _destroyedThisWave[i];
            if (record.Type == TowerType.Repair) continue; // don't auto-rebuild repair towers

            // find nearest repair tower with auto-rebuild
            Tower? nearestRepair = null; float bestDist = float.MaxValue;
            var destroyedWorldPos = Map.GridToWorld(record.GridPos.X, record.GridPos.Y);
            foreach (var rt in repairTowers)
            {
                float d = Vector2.Distance(rt.WorldPos, destroyedWorldPos);
                if (d < bestDist) { bestDist = d; nearestRepair = rt; }
            }
            if (nearestRepair == null) continue;

            int rebuildCost = (int)(record.TotalInvested * GameSettings.AutoRebuildCostMultiplier);
            if (_state.Money < rebuildCost) continue;

            // check the grid cell is still a wall (not occupied by another tower)
            if (!_map.CanPlaceTower(record.GridPos.X, record.GridPos.Y)) continue;

            // spend credits and rebuild
            _state.Money -= rebuildCost;
            _map.PlaceTower(record.GridPos.X, record.GridPos.Y);
            var newTower = new Tower(record.GridPos.X, record.GridPos.Y, record.Type);

            // upgrade to original level
            for (int lvl = 1; lvl < record.Level; lvl++)
                newTower.Upgrade();

            _towers.Add(newTower);
            _destroyedThisWave.RemoveAt(i);
            AudioManager.Instance.Play("ui_upgrade", 0.5f);
        }
    }

    private void OpenGlossary()
    {
        _glossaryReturnScreen = _screen;
        _screen = GameScreen.Glossary;
        _glossaryIndex = 0;
        AudioManager.Instance.Play("ui_click", 0.4f);
    }

    private void BuildToolbar()
    {
        _toolbar.Clear();
        int y = 44, x = 10, gap = 3, bw = 82;
        AddTowerButton(ref x, y, gap, bw, "1:Gun $50", Keys.D1, TowerType.Basic, GameSettings.BasicTowerCost, new Color(0, 150, 255));
        AddTowerButton(ref x, y, gap, bw, "2:Snp $100", Keys.D2, TowerType.Sniper, GameSettings.SniperTowerCost, new Color(255, 100, 0));
        AddTowerButton(ref x, y, gap, bw, "3:Rpd $75", Keys.D3, TowerType.Rapid, GameSettings.RapidTowerCost, new Color(0, 255, 100));
        AddTowerButton(ref x, y, gap, bw, "4:Rkt $150", Keys.D4, TowerType.Rocket, GameSettings.RocketTowerCost, new Color(200, 50, 30));
        AddTowerButton(ref x, y, gap, bw, "5:Flm $125", Keys.D5, TowerType.Flame, GameSettings.FlameTowerCost, new Color(255, 140, 0));
        AddTowerButton(ref x, y, gap, bw, "6:Tsl $120", Keys.D6, TowerType.Tesla, GameSettings.TeslaTowerCost, new Color(100, 220, 255));
        AddTowerButton(ref x, y, gap, bw, "7:Tch $100", Keys.D7, TowerType.Tachyon, GameSettings.TachyonTowerCost, new Color(220, 200, 50));
        AddTowerButton(ref x, y, gap, bw, "8:Grd $200", Keys.D8, TowerType.Grinder, GameSettings.GrinderTowerCost, new Color(200, 80, 80));
        AddTowerButton(ref x, y, gap, bw, "9:Rpr $300", Keys.D9, TowerType.Repair, GameSettings.RepairTowerCost, new Color(80, 220, 80));
        x += 4;
        _toolbar.Add(new ToolbarButton(new Rectangle(x, y, 60, 28), "W:Wall", Keys.W,
            () => { _state.Mode = PlacementMode.Wall; AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Mode == PlacementMode.Wall, () => _state.HasWalls(), new Color(160, 120, 200)));

        int rx = GameSettings.ScreenWidth - 10;
        _speedButton = new ToolbarButton(new Rectangle(rx - 90, y, 90, 28), $"Spd {_state.SpeedLabel}", null,
            () => { _state.CycleSpeed(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.4f); },
            () => !_state.IsNormalSpeed, () => true, Color.Yellow);
        _toolbar.Add(_speedButton); rx -= 93;
        _autoStartButton = new ToolbarButton(new Rectangle(rx - 80, y, 80, 28), _state.AutoStartWaves ? "Auto ON" : "Auto OFF", null,
            () => {
                _state.AutoStartWaves = !_state.AutoStartWaves; _autoStartPref = _state.AutoStartWaves;
                _autoStartButton!.SetLabel(_state.AutoStartWaves ? "Auto ON" : "Auto OFF"); AudioManager.Instance.Play("ui_click", 0.4f);
            },
            () => _state.AutoStartWaves, () => true, new Color(100, 180, 220));
        _toolbar.Add(_autoStartButton); rx -= 83;
        _muteButton = new ToolbarButton(new Rectangle(rx - 50, y, 50, 28), "M:Snd", Keys.M,
            () => { AudioManager.Instance.ToggleMute(); _muteButton!.SetLabel(AudioManager.Instance.Muted ? "M:OFF" : "M:Snd"); },
            () => AudioManager.Instance.Muted, () => true, new Color(180, 180, 180));
        _toolbar.Add(_muteButton);
    }

    private void UpdateSpeedLabel() => _speedButton?.SetLabel($"Spd {_state.SpeedLabel}");

    private void AddTowerButton(ref int x, int y, int gap, int w, string label, Keys hotkey, TowerType type, int cost, Color accent)
    {
        _toolbar.Add(new ToolbarButton(new Rectangle(x, y, w, 28), label, hotkey,
            () => { _state.Mode = PlacementMode.Tower; _state.SelectedTower = type; AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Mode == PlacementMode.Tower && _state.SelectedTower == type, () => _state.Money >= cost, accent));
        x += w + gap;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_screen != GameScreen.Title || !_seedInputActive) return;
        if (e.Key == Keys.Back) { if (_seedInput.Length > 0) _seedInput = _seedInput[..^1]; }
        else if (char.IsDigit(e.Character) && _seedInput.Length < 6) _seedInput += e.Character;
    }

    private GameTime ScaleGameTime(GameTime o)
    {
        float m = _state.SpeedMultiplier;
        return m == 1f ? o : new GameTime(
            TimeSpan.FromTicks((long)(o.TotalGameTime.Ticks * m)),
            TimeSpan.FromTicks((long)(o.ElapsedGameTime.Ticks * m)));
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState(); var mouse = Mouse.GetState();
        AudioManager.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_screen == GameScreen.Playing && JustPressed(kb, Keys.Escape))
        {
            if (_paused) { _paused = false; _contextMenu.Close(); _screen = GameScreen.Title; _seeds.NewRandomSeed(); }
            else { _paused = true; _contextMenu.Close(); }
        }
        switch (_screen)
        {
            case GameScreen.Title: UpdateTitle(kb, mouse); break;
            case GameScreen.Playing: UpdatePlaying(gameTime, kb, mouse); break;
            case GameScreen.GameOver: UpdateGameOver(kb, mouse); break;
            case GameScreen.Glossary: UpdateGlossary(kb, mouse); break;
        }
        _prevMouse = mouse; _prevKb = kb;
        base.Update(gameTime);
    }

    private void UpdateTitle(KeyboardState kb, MouseState mouse)
    {
        bool lc = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (JustPressed(kb, Keys.Tab)) _seedInputActive = !_seedInputActive;
        if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
        if (JustPressed(kb, Keys.Enter))
        { StartGame(_seedInputActive && _seedInput.Length > 0 ? int.Parse(_seedInput) : _seeds.CurrentSeed); return; }
        if (JustPressed(kb, Keys.Space) && !_seedInputActive) _seeds.NewRandomSeed();
        if (lc)
        {
            int favY = 420;
            foreach (var fav in _seeds.Favourites)
            {
                if (new Rectangle(440, favY - 2, 400, 24).Contains(mouse.X, mouse.Y))
                { _seedInput = fav.Seed.ToString(); _seedInputActive = true; AudioManager.Instance.Play("ui_click", 0.4f); break; }
                favY += 28; if (favY > 600) break;
            }
        }
    }

    private void UpdatePlaying(GameTime gameTime, KeyboardState kb, MouseState mouse)
    {
        if (_paused)
        {
            if (JustPressed(kb, Keys.Space)) _paused = false;
            if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
            return;
        }
        if (!_state.IsGameOver)
        {
            _runStats.PlayTimeSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
            HandleInput(kb, mouse);
            var st = ScaleGameTime(gameTime);
            _waves.Update(st, _enemies, _state);
            UpdateEnemies(st); UpdateEnemyAbilities(st); UpdateTowers(st);
            UpdateDrones(st); UpdateProjectiles(st); UpdateFlames(st);
            CleanUp();
        }
        else
        {
            _contextMenu.Close();
            _seeds.UpdateBest(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);
            if (!_gameOverSoundPlayed)
            {
                AudioManager.Instance.Play("game_over", 0.8f); _gameOverSoundPlayed = true;
                _runStats.FinalScore = _state.Score; _runStats.WavesCompleted = _waves.CurrentWave;
                _lastRank = _leaderboard.Submit(_runStats);
            }
            _screen = GameScreen.GameOver;
        }
    }

    private void UpdateGameOver(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.R)) { StartGame(_seeds.CurrentSeed); return; }
        if (JustPressed(kb, Keys.N)) { _seeds.NewRandomSeed(); StartGame(_seeds.CurrentSeed); return; }
        if (JustPressed(kb, Keys.F)) _seeds.ToggleFavourite(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);
        if (JustPressed(kb, Keys.G)) { OpenGlossary(); return; }
        if (JustPressed(kb, Keys.Escape)) { _seeds.NewRandomSeed(); _screen = GameScreen.Title; }
    }

    private void UpdateGlossary(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.Escape) || JustPressed(kb, Keys.G))
        { _screen = _glossaryReturnScreen; AudioManager.Instance.Play("ui_click", 0.4f); return; }
        if (JustPressed(kb, Keys.Right) || JustPressed(kb, Keys.D))
        { _glossaryIndex = (_glossaryIndex + 1) % GlossaryOrder.Length; AudioManager.Instance.Play("ui_click", 0.3f); }
        if (JustPressed(kb, Keys.Left) || JustPressed(kb, Keys.A))
        { _glossaryIndex = (_glossaryIndex - 1 + GlossaryOrder.Length) % GlossaryOrder.Length; AudioManager.Instance.Play("ui_click", 0.3f); }
        bool lc = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (lc)
        {
            int sideY = 80;
            for (int i = 0; i < GlossaryOrder.Length; i++)
            {
                if (new Rectangle(10, sideY, 200, 40).Contains(mouse.X, mouse.Y))
                { _glossaryIndex = i; AudioManager.Instance.Play("ui_click", 0.3f); break; }
                sideY += 44;
            }
        }
    }

    private void HandleInput(KeyboardState kb, MouseState mouse)
    {
        bool lc = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        bool rc = mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

        // speed +/- keys
        if (JustPressed(kb, Keys.OemPlus) || JustPressed(kb, Keys.Add))
        { _state.SpeedUp(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.3f); }
        if (JustPressed(kb, Keys.OemMinus) || JustPressed(kb, Keys.Subtract))
        { _state.SlowDown(); UpdateSpeedLabel(); AudioManager.Instance.Play("ui_click", 0.3f); }

        if (!_contextMenu.IsOpen)
            foreach (var btn in _toolbar)
                if (btn.Hotkey.HasValue && JustPressed(kb, btn.Hotkey.Value)) btn.OnClick();
        if (JustPressed(kb, Keys.R)) { StartGame(_seeds.CurrentSeed); return; }
        if (lc && _contextMenu.IsOpen) { _contextMenu.HandleClick(mouse.X, mouse.Y); return; }
        if ((lc || rc) && _contextMenu.IsOpen) { _contextMenu.Close(); if (lc) return; }
        if (lc) foreach (var btn in _toolbar) if (btn.Bounds.Contains(mouse.X, mouse.Y)) { btn.OnClick(); return; }
        if (JustPressed(kb, Keys.Space)) _waves.RequestStart();
        _hoverCell = Map.WorldToGrid(new Vector2(mouse.X, mouse.Y));

        // find hovered tower (check 2x2 towers too)
        _hoveredTower = null;
        foreach (var t in _towers)
        {
            if (t.Is2x2)
            {
                int dx = _hoverCell.X - t.GridPos.X, dy = _hoverCell.Y - t.GridPos.Y;
                if (dx >= 0 && dx < 2 && dy >= 0 && dy < 2) { _hoveredTower = t; break; }
            }
            else if (t.GridPos == _hoverCell) { _hoveredTower = t; break; }
        }

        if (lc && mouse.Y > GameSettings.UIHeight)
        { if (_state.Mode == PlacementMode.Tower) TryPlaceTower(); else if (_state.Mode == PlacementMode.Wall) TryPlaceWall(); }
        if (rc && mouse.Y > GameSettings.UIHeight) HandleRightClick(mouse);
    }

    private void HandleRightClick(MouseState mouse)
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y)) return;
        var cell = _map.Grid[_hoverCell.X, _hoverCell.Y];
        var items = new List<ContextMenuItem>();
        bool bw = !_waves.WaveActive;
        if (cell == CellType.Tower && _hoveredTower != null)
        {
            var t = _hoveredTower;
            if (bw && t.PlacedDuringPrep)
                items.Add(new ContextMenuItem($"Remove (${t.FullRefundValue})", () => { RemoveTower(t, t.FullRefundValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.LightGreen));
            else
                items.Add(new ContextMenuItem($"Sell (${t.SellValue})", () => { RemoveTower(t, t.SellValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.Tomato));
            if (t.CanUpgrade)
            {
                int cost = t.UpgradeCost; bool ca = _state.Money >= cost;
                string desc = t.Type switch
                {
                    TowerType.Flame => $"Upgrade Lv{t.Level + 1} (${cost}) +Burn",
                    TowerType.Rocket => $"Upgrade Lv{t.Level + 1} (${cost}) +Splash",
                    TowerType.Tesla => $"Upgrade Lv{t.Level + 1} (${cost}) +Vuln",
                    TowerType.Tachyon => $"Upgrade Lv{t.Level + 1} (${cost}) +Slow",
                    TowerType.Repair => $"Upgrade Lv{t.Level + 1} (${cost}) +Drone +Range",
                    _ => $"Upgrade Lv{t.Level + 1} (${cost})"
                };
                items.Add(new ContextMenuItem(desc, () => { UpgradeTower(t); AudioManager.Instance.Play("ui_upgrade", 0.6f); },
                    ca ? Color.Gold : new Color(80, 60, 0), ca));
            }
            else if (t.Type == TowerType.Grinder) items.Add(new ContextMenuItem("Not upgradeable", () => { }, Color.DimGray, false));
            else items.Add(new ContextMenuItem("Max Level", () => { }, Color.DimGray, false));

            // auto-rebuild toggle for repair towers
            if (t.Type == TowerType.Repair)
            {
                string arLabel = t.AutoRebuildEnabled ? "Auto-Rebuild: ON" : "Auto-Rebuild: OFF";
                Color arColor = t.AutoRebuildEnabled ? new Color(80, 220, 80) : new Color(180, 180, 180);
                var tCopy = t;
                items.Add(new ContextMenuItem(arLabel, () => { tCopy.AutoRebuildEnabled = !tCopy.AutoRebuildEnabled;
                    AudioManager.Instance.Play("ui_click", 0.4f); }, arColor));
            }
        }
        else if (cell == CellType.Wall && bw && _map.IsPlayerWall(_hoverCell.X, _hoverCell.Y))
        {
            int col = _hoverCell.X, row = _hoverCell.Y;
            items.Add(new ContextMenuItem("Remove Wall (+1)", () => { RemovePlayerWall(col, row); AudioManager.Instance.Play("ui_click", 0.5f); }, new Color(160, 120, 200)));
        }
        if (items.Count > 0) _contextMenu.Open(_hoverCell, new Vector2(mouse.X, mouse.Y), items);
    }

    private void RemoveTower(Tower t, int refund)
    {
        _state.Money += refund;
        if (t.Is2x2)
            _map.Remove2x2Tower(t.GridPos.X, t.GridPos.Y);
        else
            _map.RemoveTower(t.GridPos.X, t.GridPos.Y);

        RemoveDronesForTower(t);
        _towers.Remove(t);
        _hoveredTower = null;
    }

    private void UpgradeTower(Tower t)
    {
        if (_state.Money < t.UpgradeCost) return;
        int dronesBefore = t.DroneCount;
        _state.Money -= t.UpgradeCost;
        t.Upgrade();
        // spawn new drones if repair tower gained a drone
        int dronesAfter = t.DroneCount;
        for (int i = dronesBefore; i < dronesAfter; i++)
            _drones.Add(new RepairDrone(t));
    }

    private void RemovePlayerWall(int c, int r) { if (_map.RemoveWall(c, r)) { _state.Walls++; NotifyEnemiesPathChanged(); } }

    private void TryPlaceTower()
    {
        if (!_state.CanAffordTower()) return;

        if (_state.SelectedTower == TowerType.Repair)
        {
            // 2x2 placement: hoverCell is top-left anchor
            if (!_map.CanPlace2x2Tower(_hoverCell.X, _hoverCell.Y)) return;
            _state.SpendTower();
            _map.Place2x2Tower(_hoverCell.X, _hoverCell.Y);
            AudioManager.Instance.Play("ui_click", 0.5f);
            var t = new Tower(_hoverCell.X, _hoverCell.Y, TowerType.Repair);
            if (_waves.WaveActive) t.PlacedDuringPrep = false;
            _towers.Add(t);
            // spawn initial drone (level 1 = 1 drone)
            _drones.Add(new RepairDrone(t));
            _runStats.RecordTowerBuilt();
        }
        else
        {
            if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y)) return;
            _state.SpendTower();
            _map.PlaceTower(_hoverCell.X, _hoverCell.Y);
            AudioManager.Instance.Play("ui_click", 0.5f);
            var t = new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower);
            if (_waves.WaveActive) t.PlacedDuringPrep = false;
            _towers.Add(t);
            _runStats.RecordTowerBuilt();
        }
    }

    private void TryPlaceWall()
    {
        if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y)) return;
        _state.SpendWall(); _map.PlaceWall(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f); NotifyEnemiesPathChanged();
    }

    private void NotifyEnemiesPathChanged() { foreach (var e in _enemies) if (e.IsAlive) e.UpdatePath(_map.CurrentPath); }

    private void RemoveDronesForTower(Tower t)
    {
        // rebuild drone list from scratch for all living repair towers
        _drones.Clear();
        foreach (var tw in _towers)
        {
            if (tw == t) continue; // skip the one being removed
            if (tw.Type != TowerType.Repair) continue;
            for (int d = 0; d < tw.DroneCount; d++)
                _drones.Add(new RepairDrone(tw));
        }
    }

    // --- SIMULATION ---

    private void UpdateEnemies(GameTime gt)
    {
        foreach (var e in _enemies)
        {
            e.Update(gt);
            if (e.ReachedEnd && !e.IsAlive)
            { _state.LoseLife(); _runStats.RecordLifeLost(); AudioManager.Instance.Play("life_lost", 0.7f, 0.1f); e.ReachedEnd = false; }
        }
    }

    private void UpdateEnemyAbilities(GameTime gt)
    {
        foreach (var e in _enemies)
        {
            if (!e.IsAlive) continue;
            switch (e.Type)
            {
                case EnemyType.Medic:
                    if (e.AbilityCooldown <= 0f)
                    {
                        foreach (var other in _enemies)
                        {
                            if (!other.IsAlive || other == e || other.Health >= other.MaxHealth) continue;
                            if (Vector2.Distance(e.Position, other.Position) <= GameSettings.MedicHealRange)
                                other.Heal(GameSettings.MedicHealAmount);
                        }
                        e.AbilityCooldown = GameSettings.MedicHealCooldown;
                    }
                    break;
                case EnemyType.Hacker:
                    if (e.AbilityCooldown <= 0f)
                    {
                        Tower? near = null; float bd = float.MaxValue;
                        foreach (var t in _towers) { float d = Vector2.Distance(e.Position, t.WorldPos); if (d <= GameSettings.HackerDisableRange && d < bd) { bd = d; near = t; } }
                        if (near != null) { near.DisabledTimer = GameSettings.HackerDisableDuration; e.AbilityCooldown = GameSettings.HackerDisableCooldown; }
                    }
                    break;
                case EnemyType.Blaster:
                    if (e.AbilityCooldown <= 0f)
                    {
                        Tower? near = null; float bd = float.MaxValue;
                        foreach (var t in _towers) { float d = Vector2.Distance(e.Position, t.WorldPos); if (d <= GameSettings.BlasterAttackRange && d < bd) { bd = d; near = t; } }
                        if (near != null) { near.TakeTowerDamage(GameSettings.BlasterDamageToTower); e.AbilityCooldown = GameSettings.BlasterAttackCooldown; }
                    }
                    break;
            }
        }
    }

    private void UpdateTowers(GameTime gt)
    {
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        foreach (var t in _towers)
        {
            t.Update(gt, _enemies, _projectiles, _flames);
            t.UpdatePassiveHeal(dt, _towers);
        }
    }

    private void UpdateDrones(GameTime gt)
    {
        foreach (var d in _drones) d.Update(gt, _towers, _state);
    }

    private void UpdateProjectiles(GameTime gt) { foreach (var p in _projectiles) p.Update(gt); }
    private void UpdateFlames(GameTime gt) { foreach (var f in _flames) f.Update(gt, _enemies); }

    private void CleanUp()
    {
        _pendingSpawns.Clear();
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            if (!e.IsAlive)
            {
                if (!e.ReachedEnd && e.Health <= 0)
                {
                    int reward = e.Reward; float grindBonus = 0f;
                    foreach (var t in _towers) { if (t.Type != TowerType.Grinder) continue; if (Vector2.Distance(t.WorldPos, e.Position) <= t.Range) grindBonus = Math.Max(grindBonus, t.GrinderBonusRatio); }
                    int totalReward = reward + (int)(reward * grindBonus);
                    _state.EarnReward(totalReward); _runStats.RecordKill(e.Type); _runStats.RecordMoney(totalReward);
                    if (e.LastDamageSource.HasValue) _runStats.RecordTowerKill(e.LastDamageSource.Value);
                    if (grindBonus > 0) AudioManager.Instance.PlayVaried("tower_grinder", 0.3f, 0.15f, 0.1f);
                    if (e.Reward > 25) AudioManager.Instance.PlayVaried("death_boss", 0.5f, 0.1f, 0.08f);
                    else if (e.Reward > 15) AudioManager.Instance.PlayVaried("death_medium", 0.4f, 0.15f, 0.05f);
                    else AudioManager.Instance.PlayVaried("death_small", 0.3f, 0.2f, 0.04f);

                    if (e.Type == EnemyType.Kamikaze)
                    {
                        foreach (var t in _towers) if (Vector2.Distance(e.Position, t.WorldPos) <= GameSettings.KamikazeExplosionRadius) t.TakeTowerDamage(GameSettings.KamikazeTowerDamage);
                        AudioManager.Instance.PlayVaried("rocket_explode", 0.7f, 0.1f, 0.05f);
                    }

                    if (e.Type == EnemyType.Spreader && !e.IsChild)
                        for (int c = 0; c < GameSettings.SpreaderChildCount; c++)
                        {
                            var ch = new Enemy(e.Path, e.MaxHealth * GameSettings.SpreaderChildHealthRatio, e.BaseSpeed * GameSettings.SpreaderChildSpeedBonus,
                                Math.Max(1, e.Reward / 3), _sprites.GetSprite(EnemyType.Spreader), EnemyType.Spreader)
                            { IsChild = true };
                            ch.Position = e.Position + new Vector2((c - 0.5f) * 10f, 0); ch.UpdatePath(e.Path); _pendingSpawns.Add(ch);
                        }

                    if (e.Type == EnemyType.Centipede && !e.IsChild)
                        for (int c = 0; c < GameSettings.CentipedeSegmentCount; c++)
                        {
                            var seg = new Enemy(e.Path, e.MaxHealth * GameSettings.CentipedeSegmentHealthRatio, e.BaseSpeed * GameSettings.CentipedeSegmentSpeedBonus,
                                Math.Max(1, e.Reward / 4), _sprites.GetSprite(EnemyType.Centipede), EnemyType.Centipede)
                            { IsChild = true };
                            seg.Position = e.Position + new Vector2(c * -8f, c * -4f); seg.UpdatePath(e.Path); _pendingSpawns.Add(seg);
                        }
                }
                _enemies.RemoveAt(i);
            }
        }
        if (_pendingSpawns.Count > 0) _enemies.AddRange(_pendingSpawns);

        // remove destroyed towers (and their drones)
        bool rebuildDrones = false;
        for (int i = _towers.Count - 1; i >= 0; i--)
        {
            if (_towers[i].IsDestroyed)
            {
                var t = _towers[i];
                // record non-repair destroyed towers for auto-rebuild
                if (t.Type != TowerType.Repair)
                    _destroyedThisWave.Add(new DestroyedTowerRecord(t.Type, t.GridPos, t.Level, t.TotalInvested));
                if (t.Is2x2) _map.Remove2x2Tower(t.GridPos.X, t.GridPos.Y);
                else _map.RemoveTower(t.GridPos.X, t.GridPos.Y);
                if (t.Type == TowerType.Repair) rebuildDrones = true;
                _towers.RemoveAt(i);
                if (_hoveredTower == t) _hoveredTower = null;
            }
        }
        if (rebuildDrones)
        {
            _drones.Clear();
            foreach (var t in _towers)
                if (t.Type == TowerType.Repair)
                    for (int d = 0; d < t.DroneCount; d++)
                        _drones.Add(new RepairDrone(t));
        }

        _projectiles.RemoveAll(p => !p.IsActive);
        _flames.RemoveAll(f => !f.IsAlive);
    }

    // --- DRAW ---

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 8, 16));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        switch (_screen)
        {
            case GameScreen.Title: DrawTitle(); break;
            case GameScreen.Playing: DrawGame(); if (_paused) DrawPauseOverlay(); break;
            case GameScreen.GameOver: DrawGame(); DrawGameOverOverlay(); break;
            case GameScreen.Glossary: DrawGlossary(); break;
        }
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawTitle()
    {
        float cx = GameSettings.ScreenWidth / 2f;
        DrawCentred("RAD DEFENCE", cx, 80, Color.CornflowerBlue);
        DrawCentred("Tower Defence with Seeded Maps", cx, 115, Color.Gray);
        DrawCentred($"Seed: {_seeds.CurrentSeed}", cx, 170, Color.Gold);
        DrawCentred("[SPACE] New Seed  [TAB] Enter Seed  [ENTER] Start  [G] Glossary", cx, 200, Color.LightGray);
        if (_seedInputActive)
            DrawCentred($"Enter seed: {(_seedInput.Length > 0 ? _seedInput : "_")}", cx, 235, Color.White);
        if (_leaderboard.TopRuns.Count > 0)
        {
            DrawCentred("--- High Scores ---", cx, 280, Color.Gold);
            int y = 310;
            for (int i = 0; i < Math.Min(8, _leaderboard.TopRuns.Count); i++)
            {
                var run = _leaderboard.TopRuns[i];
                DrawCentred($"#{i + 1}  {run.Score} pts  Wave {run.Wave}  Kills {run.Kills}  Seed {run.Seed}", cx, y, i == 0 ? Color.Gold : Color.LightGray);
                y += 22;
            }
        }
        if (_seeds.Favourites.Count > 0)
        {
            DrawCentred("--- Favourites (click to load) ---", cx, 510, new Color(160, 120, 200));
            int y = 540;
            foreach (var fav in _seeds.Favourites)
            {
                string star = _seeds.CurrentSeed == fav.Seed ? "> " : "  ";
                DrawCentred($"{star}Seed {fav.Seed}  |  Best: {fav.BestScore} pts  Wave {fav.BestWave}", cx, y, Color.LightGray);
                y += 24; if (y > 640) break;
            }
        }
        if (_leaderboard.Career.TotalGamesPlayed > 0)
        {
            var c = _leaderboard.Career;
            DrawCentred($"Career: {c.TotalGamesPlayed} games  {c.TotalKills} kills  Best wave {c.HighestWave}  Best score {c.HighestScore}", cx, GameSettings.ScreenHeight - 60, Color.DimGray);
        }
        DrawCentred("1-9: Towers | W: Wall | +/-: Speed | R-Click: Sell/Upgrade | SPACE: Wave | M: Mute", cx, GameSettings.ScreenHeight - 30, Color.DimGray);
    }

    private void DrawGlossary()
    {
        var type = GlossaryOrder[_glossaryIndex]; var entry = EnemyGlossary.Get(type);
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(8, 8, 16));
        DrawCentred("ENEMY GLOSSARY", GameSettings.ScreenWidth / 2f, 20, Color.CornflowerBlue);
        int sideX = 15; int sideY = 70;
        for (int i = 0; i < GlossaryOrder.Length; i++)
        {
            var et = GlossaryOrder[i]; var ge = EnemyGlossary.Get(et); bool sel = i == _glossaryIndex;
            if (sel) _spriteBatch.Draw(_sprites.Pixel, new Rectangle(sideX - 4, sideY - 2, 198, 40), ge.AccentColor * 0.2f);
            _spriteBatch.Draw(_sprites.GetSprite(et), new Rectangle(sideX, sideY + 2, 32, 32), Color.White);
            _spriteBatch.DrawString(_font, ge.Name, new Vector2(sideX + 38, sideY + 8), sel ? ge.AccentColor : Color.Gray);
            sideY += 44;
        }
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(220, 60, 2, GameSettings.ScreenHeight - 80), new Color(40, 40, 60));
        int panelX = 240; int panelW = GameSettings.ScreenWidth - panelX - 20; float dy = 70; int spriteSize = 80;
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

    private void DrawWrapped(string text, int x, int y, int maxWidth, Color color, out float totalHeight)
    {
        totalHeight = 0; string[] words = text.Split(' '); string line = "";
        float lineHeight = _font.MeasureString("M").Y + 2;
        foreach (var word in words)
        {
            string test = line.Length == 0 ? word : line + " " + word;
            if (_font.MeasureString(test).X > maxWidth && line.Length > 0)
            { _spriteBatch.DrawString(_font, line, new Vector2(x, y + totalHeight), color); totalHeight += lineHeight; line = word; }
            else line = test;
        }
        if (line.Length > 0) { _spriteBatch.DrawString(_font, line, new Vector2(x, y + totalHeight), color); totalHeight += lineHeight; }
    }

    private void DrawGame()
    {
        _map.Draw(_spriteBatch, _sprites);
        DrawPlacementGhost(); DrawTowers(); DrawDrones(); DrawFlames(); DrawEnemies(); DrawProjectiles();
        DrawHUD(); DrawToolbar();
        _contextMenu.Draw(_spriteBatch, _sprites.Pixel, _font);
    }

    private void DrawPauseOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.6f);
        float cx = GameSettings.ScreenWidth / 2f; float cy = GameSettings.ScreenHeight / 2f;
        DrawCentred("PAUSED", cx, cy - 40, Color.White);
        DrawCentred("[SPACE] Resume  [ESC] Quit to Menu  [R] Restart  [G] Glossary  [+/-] Speed", cx, cy + 10, Color.LightGray);
    }

    private void DrawPlacementGhost()
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y) || _contextMenu.IsOpen) return;

        if (_state.Mode == PlacementMode.Tower && _state.SelectedTower == TowerType.Repair)
        {
            bool canPlace = _map.CanPlace2x2Tower(_hoverCell.X, _hoverCell.Y) && _state.CanAffordTower();
            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                {
                    int cx = _hoverCell.X + dx, cy = _hoverCell.Y + dy;
                    if (!_map.IsInBounds(cx, cy)) continue;
                    var pos = Map.GridToWorld(cx, cy);
                    int sz = GameSettings.CellSize - 4;
                    var rect = new Rectangle((int)(pos.X - sz / 2f), (int)(pos.Y - sz / 2f), sz, sz);
                    _spriteBatch.Draw(_sprites.Pixel, rect, (canPlace ? new Color(80, 220, 80) : Color.Red) * 0.2f);
                }
            if (canPlace)
            {
                var t = new Tower(_hoverCell.X, _hoverCell.Y, TowerType.Repair);
                int size = GameSettings.CellSize * 2 - 4;
                var r = new Rectangle((int)(t.WorldPos.X - size / 2f), (int)(t.WorldPos.Y - size / 2f), size, size);
                _spriteBatch.Draw(_sprites.Towers[TowerType.Repair], r, Color.White * 0.5f);
                t.DrawRange(_spriteBatch, _sprites.Ring);
            }
        }
        else if (_state.Mode == PlacementMode.Tower)
        {
            var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y);
            int size = GameSettings.CellSize - 4;
            var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);
            if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y) || !_state.CanAffordTower())
            {
                if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] != CellType.Wall) return;
                _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return;
            }
            _spriteBatch.Draw(_sprites.Towers[_state.SelectedTower], rect, Color.White * 0.5f);
            new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower).DrawRange(_spriteBatch, _sprites.Ring);
        }
        else if (_state.Mode == PlacementMode.Wall)
        {
            var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y);
            int size = GameSettings.CellSize - 4;
            var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);
            if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y))
            {
                if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] == CellType.Empty)
                    _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return;
            }
            _spriteBatch.Draw(_sprites.TileWall, rect, Color.White * 0.5f);
        }
    }

    private void DrawTowers() { foreach (var t in _towers) { t.Draw(_spriteBatch, _sprites, t == _hoveredTower); if (t == _hoveredTower) t.DrawRange(_spriteBatch, _sprites.Ring); } }
    private void DrawDrones() { foreach (var d in _drones) d.Draw(_spriteBatch, _sprites); }
    private void DrawEnemies() { foreach (var e in _enemies) e.Draw(_spriteBatch, _sprites.Pixel); }
    private void DrawProjectiles() { foreach (var p in _projectiles) p.Draw(_spriteBatch, _sprites); }
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
        var seedText = $"Seed: {_seeds.CurrentSeed}";
        float seedX = GameSettings.ScreenWidth - _font.MeasureString(seedText).X - 10;
        _spriteBatch.DrawString(_font, seedText, new Vector2(seedX, y1), Color.DimGray);
        var killsText = $"Kills: {_runStats.TotalKills}";
        _spriteBatch.DrawString(_font, killsText, new Vector2(seedX - _font.MeasureString(killsText).X - 16, y1), new Color(180, 180, 180));
        string wave;
        if (_waves.WaveActive) wave = $"Wave {_waves.CurrentWave}  (R-click towers to sell/upgrade)";
        else if (_waves.CurrentWave == 0) wave = "Place towers on walls, then SPACE to start";
        else
        {
            string cleared = $"Wave {_waves.CurrentWave} cleared! +{_waves.LastWallGrant} walls  R-click to edit";
            wave = _state.AutoStartWaves ? $"{cleared}  Next in {Math.Max(0, 10 - _waves.BreakTimer):0}s  [SPACE]" : $"{cleared}  Press SPACE when ready";
        }
        var ws = _font.MeasureString(wave);
        _spriteBatch.DrawString(_font, wave, new Vector2((GameSettings.ScreenWidth - ws.X) / 2f, 26), Color.CornflowerBlue);
    }

    private void DrawToolbar() { foreach (var btn in _toolbar) btn.Draw(_spriteBatch, _sprites.Pixel, _font); }

    private void DrawGameOverOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.8f);
        float cx = GameSettings.ScreenWidth / 2f; float y = 160;
        DrawCentred("GAME OVER", cx, y, Color.Red); y += 40;
        if (_lastRank > 0) { DrawCentred(_lastRank == 1 ? "NEW HIGH SCORE!" : $"Rank #{_lastRank} on leaderboard!", cx, y, _lastRank <= 3 ? Color.Gold : Color.CornflowerBlue); y += 30; }
        DrawCentred($"Score: {_state.Score}  |  Wave: {_waves.CurrentWave}  |  Seed: {_seeds.CurrentSeed}", cx, y, Color.White); y += 35;
        DrawCentred($"Kills: {_runStats.TotalKills}  |  Towers Built: {_runStats.TowersBuilt}  |  Time: {_runStats.PlayTimeSeconds:0}s", cx, y, Color.LightGray); y += 30;
        if (_runStats.KillsByEnemyType.Count > 0)
        { string kb = ""; foreach (var (et, c) in _runStats.KillsByEnemyType) kb += $"{et}: {c}  "; DrawCentred(kb.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 25; }
        if (_runStats.KillsByTowerType.Count > 0)
        { string tb = "Kills by tower: "; foreach (var (tt, c) in _runStats.KillsByTowerType) tb += $"{Tower.GetName(tt)}: {c}  "; DrawCentred(tb.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 35; }
        bool isFav = _seeds.IsFavourite(_seeds.CurrentSeed);
        DrawCentred(isFav ? "Favourited!" : "[F] Favourite this seed", cx, y, isFav ? Color.Gold : new Color(160, 120, 200)); y += 35;
        DrawCentred("[R] Replay    [N] New Seed    [ESC] Menu    [G] Glossary", cx, y, Color.LightGray);
    }

    private void DrawCentred(string text, float cx, float y, Color color)
    { var s = _font.MeasureString(text); _spriteBatch.DrawString(_font, text, new Vector2(cx - s.X / 2f, y), color); }
    private bool JustPressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && !_prevKb.IsKeyDown(key);

    private Texture2D CreateRing(int diameter, int thickness)
    {
        var tex = new Texture2D(GraphicsDevice, diameter, diameter);
        var data = new Color[diameter * diameter]; float r = diameter / 2f;
        for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f; float dist = MathF.Sqrt(dx * dx + dy * dy);
                data[y * diameter + x] = MathF.Abs(dist - r + thickness) <= thickness ? Color.White : Color.Transparent;
            }
        tex.SetData(data); return tex;
    }
}
