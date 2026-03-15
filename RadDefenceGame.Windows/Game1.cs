namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

public enum GameScreen { Title, Playing, GameOver }

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

    // -- stats + leaderboard --
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
        _enemies.Clear(); _towers.Clear(); _projectiles.Clear(); _flames.Clear();
        _contextMenu.Close();
        _screen = GameScreen.Playing;
        _seedInput = ""; _seedInputActive = false;
        _gameOverSoundPlayed = false;
        _lastRank = 0;

        _runStats = new GameStats { Seed = seed };

        BuildToolbar();
    }

    private void OnWaveStarting()
    {
        foreach (var t in _towers) t.PlacedDuringPrep = false;
        AudioManager.Instance.Play("wave_start", 0.8f);
    }

    private void BuildToolbar()
    {
        _toolbar.Clear();
        int y = 44, x = 10, gap = 3, bw = 88;

        AddTowerButton(ref x, y, gap, bw, "1:Gun $50", Keys.D1, TowerType.Basic, GameSettings.BasicTowerCost, new Color(0, 150, 255));
        AddTowerButton(ref x, y, gap, bw, "2:Snpr $100", Keys.D2, TowerType.Sniper, GameSettings.SniperTowerCost, new Color(255, 100, 0));
        AddTowerButton(ref x, y, gap, bw, "3:Rpd $75", Keys.D3, TowerType.Rapid, GameSettings.RapidTowerCost, new Color(0, 255, 100));
        AddTowerButton(ref x, y, gap, bw, "4:Rkt $150", Keys.D4, TowerType.Rocket, GameSettings.RocketTowerCost, new Color(200, 50, 30));
        AddTowerButton(ref x, y, gap, bw, "5:Flm $125", Keys.D5, TowerType.Flame, GameSettings.FlameTowerCost, new Color(255, 140, 0));
        AddTowerButton(ref x, y, gap, bw, "6:Tsl $120", Keys.D6, TowerType.Tesla, GameSettings.TeslaTowerCost, new Color(100, 220, 255));
        AddTowerButton(ref x, y, gap, bw, "7:Tch $100", Keys.D7, TowerType.Tachyon, GameSettings.TachyonTowerCost, new Color(220, 200, 50));
        AddTowerButton(ref x, y, gap, bw, "8:Grd $200", Keys.D8, TowerType.Grinder, GameSettings.GrinderTowerCost, new Color(200, 80, 80));
        x += 8;

        _toolbar.Add(new ToolbarButton(new Rectangle(x, y, 70, 28), "W:Wall", Keys.W,
            () => { _state.Mode = PlacementMode.Wall; AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Mode == PlacementMode.Wall, () => _state.HasWalls(), new Color(160, 120, 200)));

        int rx = GameSettings.ScreenWidth - 10;
        _speedButton = new ToolbarButton(new Rectangle(rx - 90, y, 90, 28), $"Spd {_state.SpeedLabel}", Keys.OemPlus,
            () => { _state.CycleSpeed(); _speedButton!.SetLabel($"Spd {_state.SpeedLabel}"); AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Speed != GameSpeed.Normal, () => true, Color.Yellow);
        _toolbar.Add(_speedButton); rx -= 93;

        _autoStartButton = new ToolbarButton(new Rectangle(rx - 80, y, 80, 28), _state.AutoStartWaves ? "Auto ON" : "Auto OFF", null,
            () => { _state.AutoStartWaves = !_state.AutoStartWaves; _autoStartPref = _state.AutoStartWaves;
                _autoStartButton!.SetLabel(_state.AutoStartWaves ? "Auto ON" : "Auto OFF"); AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.AutoStartWaves, () => true, new Color(100, 180, 220));
        _toolbar.Add(_autoStartButton); rx -= 83;

        _muteButton = new ToolbarButton(new Rectangle(rx - 50, y, 50, 28), "M:Snd", Keys.M,
            () => { AudioManager.Instance.ToggleMute(); _muteButton!.SetLabel(AudioManager.Instance.Muted ? "M:OFF" : "M:Snd"); },
            () => AudioManager.Instance.Muted, () => true, new Color(180, 180, 180));
        _toolbar.Add(_muteButton);
    }

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

    // --- UPDATE ---

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState(); var mouse = Mouse.GetState();
        AudioManager.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (kb.IsKeyDown(Keys.Escape) && _screen == GameScreen.Playing)
        { _contextMenu.Close(); _screen = GameScreen.Title; _seeds.NewRandomSeed(); }

        switch (_screen)
        {
            case GameScreen.Title: UpdateTitle(kb, mouse); break;
            case GameScreen.Playing: UpdatePlaying(gameTime, kb, mouse); break;
            case GameScreen.GameOver: UpdateGameOver(kb, mouse); break;
        }
        _prevMouse = mouse; _prevKb = kb;
        base.Update(gameTime);
    }

    private void UpdateTitle(KeyboardState kb, MouseState mouse)
    {
        bool lc = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (JustPressed(kb, Keys.Tab)) _seedInputActive = !_seedInputActive;
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
        if (!_state.IsGameOver)
        {
            _runStats.PlayTimeSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            HandleInput(kb, mouse);
            var st = ScaleGameTime(gameTime);
            _waves.Update(st, _enemies, _state);
            UpdateEnemies(st); UpdateTowers(st); UpdateProjectiles(st); UpdateFlames(st);
            CleanUp();
        }
        else
        {
            _contextMenu.Close();
            _seeds.UpdateBest(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);

            if (!_gameOverSoundPlayed)
            {
                AudioManager.Instance.Play("game_over", 0.8f);
                _gameOverSoundPlayed = true;

                // submit stats to leaderboard
                _runStats.FinalScore = _state.Score;
                _runStats.WavesCompleted = _waves.CurrentWave;
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
        if (JustPressed(kb, Keys.Escape)) { _seeds.NewRandomSeed(); _screen = GameScreen.Title; }
    }

    private void HandleInput(KeyboardState kb, MouseState mouse)
    {
        bool lc = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        bool rc = mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

        if (!_contextMenu.IsOpen)
            foreach (var btn in _toolbar)
                if (btn.Hotkey.HasValue && JustPressed(kb, btn.Hotkey.Value)) btn.OnClick();

        if (JustPressed(kb, Keys.R)) { StartGame(_seeds.CurrentSeed); return; }
        if (lc && _contextMenu.IsOpen) { _contextMenu.HandleClick(mouse.X, mouse.Y); return; }
        if ((lc || rc) && _contextMenu.IsOpen) { _contextMenu.Close(); if (lc) return; }
        if (lc) foreach (var btn in _toolbar) if (btn.Bounds.Contains(mouse.X, mouse.Y)) { btn.OnClick(); return; }
        if (JustPressed(kb, Keys.Space)) _waves.RequestStart();

        _hoverCell = Map.WorldToGrid(new Vector2(mouse.X, mouse.Y));
        _hoveredTower = null;
        foreach (var t in _towers) if (t.GridPos == _hoverCell) { _hoveredTower = t; break; }

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
                items.Add(new ContextMenuItem($"Remove (${t.FullRefundValue})",
                    () => { RemoveTower(t, t.FullRefundValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.LightGreen));
            else
                items.Add(new ContextMenuItem($"Sell (${t.SellValue})",
                    () => { RemoveTower(t, t.SellValue); AudioManager.Instance.Play("ui_sell", 0.6f); }, Color.Tomato));

            if (t.CanUpgrade)
            {
                int cost = t.UpgradeCost; bool ca = _state.Money >= cost;
                string desc = t.Type switch
                {
                    TowerType.Flame => $"Upgrade Lv{t.Level+1} (${cost}) +Burn",
                    TowerType.Rocket => $"Upgrade Lv{t.Level+1} (${cost}) +Splash",
                    TowerType.Tesla => $"Upgrade Lv{t.Level+1} (${cost}) +Vuln",
                    TowerType.Tachyon => $"Upgrade Lv{t.Level+1} (${cost}) +Slow",
                    _ => $"Upgrade Lv{t.Level+1} (${cost})"
                };
                items.Add(new ContextMenuItem(desc,
                    () => { UpgradeTower(t); AudioManager.Instance.Play("ui_upgrade", 0.6f); },
                    ca ? Color.Gold : new Color(80, 60, 0), ca));
            }
            else if (t.Type == TowerType.Grinder)
                items.Add(new ContextMenuItem("Not upgradeable", () => { }, Color.DimGray, false));
            else
                items.Add(new ContextMenuItem("Max Level", () => { }, Color.DimGray, false));
        }
        else if (cell == CellType.Wall && bw && _map.IsPlayerWall(_hoverCell.X, _hoverCell.Y))
        {
            int col = _hoverCell.X, row = _hoverCell.Y;
            items.Add(new ContextMenuItem("Remove Wall (+1)",
                () => { RemovePlayerWall(col, row); AudioManager.Instance.Play("ui_click", 0.5f); }, new Color(160, 120, 200)));
        }
        if (items.Count > 0) _contextMenu.Open(_hoverCell, new Vector2(mouse.X, mouse.Y), items);
    }

    private void RemoveTower(Tower t, int refund) { _state.Money += refund; _map.RemoveTower(t.GridPos.X, t.GridPos.Y); _towers.Remove(t); _hoveredTower = null; }
    private void UpgradeTower(Tower t) { if (_state.Money < t.UpgradeCost) return; _state.Money -= t.UpgradeCost; t.Upgrade(); }
    private void RemovePlayerWall(int c, int r) { _map.RemoveWall(c, r); _state.Walls++; NotifyEnemiesPathChanged(); }

    private void TryPlaceTower()
    {
        if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y) || !_state.CanAffordTower()) return;
        _state.SpendTower(); _map.PlaceTower(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f);
        var t = new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower);
        if (_waves.WaveActive) t.PlacedDuringPrep = false;
        _towers.Add(t);
        _runStats.RecordTowerBuilt();
    }

    private void TryPlaceWall()
    {
        if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y)) return;
        _state.SpendWall(); _map.PlaceWall(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f); NotifyEnemiesPathChanged();
    }

    private void NotifyEnemiesPathChanged() { foreach (var e in _enemies) if (e.IsAlive) e.UpdatePath(_map.CurrentPath); }

    // --- SIMULATION ---

    private void UpdateEnemies(GameTime gt)
    {
        foreach (var e in _enemies)
        {
            e.Update(gt);
            if (e.ReachedEnd && !e.IsAlive)
            {
                _state.LoseLife();
                _runStats.RecordLifeLost();
                AudioManager.Instance.Play("life_lost", 0.7f, 0.1f);
                e.ReachedEnd = false;
            }
        }
    }

    private void UpdateTowers(GameTime gt) { foreach (var t in _towers) t.Update(gt, _enemies, _projectiles, _flames); }
    private void UpdateProjectiles(GameTime gt) { foreach (var p in _projectiles) p.Update(gt); }
    private void UpdateFlames(GameTime gt) { foreach (var f in _flames) f.Update(gt, _enemies); }

    private void CleanUp()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            if (!e.IsAlive)
            {
                if (!e.ReachedEnd && e.Health <= 0)
                {
                    int reward = e.Reward;
                    float grindBonus = 0f;
                    foreach (var t in _towers)
                    {
                        if (t.Type != TowerType.Grinder) continue;
                        if (Vector2.Distance(t.WorldPos, e.Position) <= t.Range)
                            grindBonus = Math.Max(grindBonus, t.GrinderBonusRatio);
                    }
                    int totalReward = reward + (int)(reward * grindBonus);
                    _state.EarnReward(totalReward);

                    // track stats
                    _runStats.RecordKill(e.Type);
                    _runStats.RecordMoney(totalReward);
                    if (e.LastDamageSource.HasValue)
                        _runStats.RecordTowerKill(e.LastDamageSource.Value);

                    if (grindBonus > 0) AudioManager.Instance.PlayVaried("tower_grinder", 0.3f, 0.15f, 0.1f);
                    if (e.Reward > 25) AudioManager.Instance.PlayVaried("death_boss", 0.5f, 0.1f, 0.08f);
                    else if (e.Reward > 15) AudioManager.Instance.PlayVaried("death_medium", 0.4f, 0.15f, 0.05f);
                    else AudioManager.Instance.PlayVaried("death_small", 0.3f, 0.2f, 0.04f);
                }
                _enemies.RemoveAt(i);
            }
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
            case GameScreen.Playing: DrawGame(); break;
            case GameScreen.GameOver: DrawGame(); DrawGameOverOverlay(); break;
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
        DrawCentred("[SPACE] New Random Seed    [TAB] Enter Seed    [ENTER] Start", cx, 200, Color.LightGray);

        if (_seedInputActive)
            DrawCentred($"Enter seed: {(_seedInput.Length > 0 ? _seedInput : "_")}", cx, 235, Color.White);

        // leaderboard on title screen
        if (_leaderboard.TopRuns.Count > 0)
        {
            DrawCentred("--- High Scores ---", cx, 280, Color.Gold);
            int y = 310;
            for (int i = 0; i < Math.Min(8, _leaderboard.TopRuns.Count); i++)
            {
                var run = _leaderboard.TopRuns[i];
                string line = $"#{i+1}  {run.Score} pts  Wave {run.Wave}  Kills {run.Kills}  Seed {run.Seed}";
                DrawCentred(line, cx, y, i == 0 ? Color.Gold : Color.LightGray);
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

        // career stats at bottom
        if (_leaderboard.Career.TotalGamesPlayed > 0)
        {
            var c = _leaderboard.Career;
            DrawCentred($"Career: {c.TotalGamesPlayed} games  {c.TotalKills} kills  Best wave {c.HighestWave}  Best score {c.HighestScore}",
                cx, GameSettings.ScreenHeight - 60, Color.DimGray);
        }

        DrawCentred("1-8: Towers | W: Wall | R-Click: Sell/Upgrade | SPACE: Wave | M: Mute | R: Restart",
            cx, GameSettings.ScreenHeight - 30, Color.DimGray);
    }

    private void DrawGame()
    {
        _map.Draw(_spriteBatch, _sprites);
        DrawPlacementGhost(); DrawTowers(); DrawFlames(); DrawEnemies(); DrawProjectiles();
        DrawHUD(); DrawToolbar();
        _contextMenu.Draw(_spriteBatch, _sprites.Pixel, _font);
    }

    private void DrawPlacementGhost()
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y) || _contextMenu.IsOpen) return;
        var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y);
        int size = GameSettings.CellSize - 4;
        var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);

        if (_state.Mode == PlacementMode.Tower)
        {
            if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y) || !_state.CanAffordTower())
            { if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] != CellType.Wall) return;
              _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return; }
            _spriteBatch.Draw(_sprites.Towers[_state.SelectedTower], rect, Color.White * 0.5f);
            new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower).DrawRange(_spriteBatch, _sprites.Ring);
        }
        else if (_state.Mode == PlacementMode.Wall)
        {
            if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y))
            { if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y) && _map.Grid[_hoverCell.X, _hoverCell.Y] == CellType.Empty)
                _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f); return; }
            _spriteBatch.Draw(_sprites.TileWall, rect, Color.White * 0.5f);
        }
    }

    private void DrawTowers() { foreach (var t in _towers) { t.Draw(_spriteBatch, _sprites, t == _hoveredTower); if (t == _hoveredTower) t.DrawRange(_spriteBatch, _sprites.Ring); } }
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
        _spriteBatch.DrawString(_font, $"Kills: {_runStats.TotalKills}", new Vector2(530, y1), new Color(180, 180, 180));

        var seedText = $"Seed: {_seeds.CurrentSeed}";
        _spriteBatch.DrawString(_font, seedText, new Vector2(GameSettings.ScreenWidth - _font.MeasureString(seedText).X - 10, y1), Color.DimGray);

        string wave;
        if (_waves.WaveActive) wave = $"Wave {_waves.CurrentWave}  (R-click towers to sell/upgrade)";
        else if (_waves.CurrentWave == 0) wave = "Place towers on walls, then SPACE to start";
        else
        {
            string cleared = $"Wave {_waves.CurrentWave} cleared! +{_waves.LastWallGrant} walls  R-click to edit";
            wave = _state.AutoStartWaves ? $"{cleared}  Next in {Math.Max(0, 10 - _waves.BreakTimer):0}s  [SPACE]" : $"{cleared}  Press SPACE when ready";
        }
        var ws = _font.MeasureString(wave);
        _spriteBatch.DrawString(_font, wave, new Vector2((GameSettings.ScreenWidth - ws.X) / 2f, y1), Color.CornflowerBlue);
    }

    private void DrawToolbar() { foreach (var btn in _toolbar) btn.Draw(_spriteBatch, _sprites.Pixel, _font); }

    private void DrawGameOverOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), Color.Black * 0.8f);
        float cx = GameSettings.ScreenWidth / 2f;
        float y = 160;

        DrawCentred("GAME OVER", cx, y, Color.Red); y += 40;

        if (_lastRank > 0)
        {
            string rankText = _lastRank == 1 ? "NEW HIGH SCORE!" : $"Rank #{_lastRank} on leaderboard!";
            DrawCentred(rankText, cx, y, _lastRank <= 3 ? Color.Gold : Color.CornflowerBlue);
            y += 30;
        }

        DrawCentred($"Score: {_state.Score}  |  Wave: {_waves.CurrentWave}  |  Seed: {_seeds.CurrentSeed}", cx, y, Color.White); y += 35;
        DrawCentred($"Kills: {_runStats.TotalKills}  |  Towers Built: {_runStats.TowersBuilt}  |  Time: {_runStats.PlayTimeSeconds:0}s", cx, y, Color.LightGray); y += 30;

        // kill breakdown
        if (_runStats.KillsByEnemyType.Count > 0)
        {
            string killBreak = "";
            foreach (var (et, count) in _runStats.KillsByEnemyType)
                killBreak += $"{et}: {count}  ";
            DrawCentred(killBreak.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 25;
        }

        // tower kill attribution
        if (_runStats.KillsByTowerType.Count > 0)
        {
            string towerBreak = "Kills by tower: ";
            foreach (var (tt, count) in _runStats.KillsByTowerType)
                towerBreak += $"{Tower.GetName(tt)}: {count}  ";
            DrawCentred(towerBreak.TrimEnd(), cx, y, new Color(150, 150, 180)); y += 35;
        }

        bool isFav = _seeds.IsFavourite(_seeds.CurrentSeed);
        DrawCentred(isFav ? "Favourited!" : "[F] Favourite this seed", cx, y, isFav ? Color.Gold : new Color(160, 120, 200)); y += 35;
        DrawCentred("[R] Replay Same Seed    [N] New Random Seed    [ESC] Menu", cx, y, Color.LightGray);
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
            { float dx = x-r+0.5f, dy = y-r+0.5f; float dist = MathF.Sqrt(dx*dx+dy*dy);
              data[y*diameter+x] = MathF.Abs(dist-r+thickness) <= thickness ? Color.White : Color.Transparent; }
        tex.SetData(data); return tex;
    }
}
