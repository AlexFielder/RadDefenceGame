namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

public enum GameScreen
{
    Title,
    Playing,
    GameOver
}

public class Game1 : Game
{
    // -- core --
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    // -- sprites --
    private SpriteSet _sprites = null!;
    private SpriteFont _font = null!;

    // -- game systems --
    private Map _map = null!;
    private GameState _state = null!;
    private WaveManager _waves = null!;
    private SeedManager _seeds = null!;
    private readonly List<Enemy> _enemies = new();
    private readonly List<Tower> _towers = new();
    private readonly List<Projectile> _projectiles = new();

    // -- input --
    private MouseState _prevMouse;
    private KeyboardState _prevKb;
    private Point _hoverCell;
    private Tower? _hoveredTower;

    // -- toolbar + context menu --
    private readonly List<ToolbarButton> _toolbar = new();
    private ToolbarButton? _speedButton;
    private ToolbarButton? _autoStartButton;
    private ToolbarButton? _muteButton;
    private readonly ContextMenu _contextMenu = new();

    // -- persistent player preferences --
    private bool _autoStartPref = false;

    // -- screens --
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

    // --- INIT ---

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
        var ring = CreateRing(64, 2);
        _sprites = new SpriteSet(Content, pixel, ring);

        // load audio from Content/Audio folder (WAV files loaded directly, no MGCB needed)
        var audioPath = Path.Combine(Content.RootDirectory, "Audio");
        AudioManager.Init();
        AudioManager.Instance.LoadFromDirectory(audioPath);

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
        _enemies.Clear();
        _towers.Clear();
        _projectiles.Clear();
        _contextMenu.Close();
        _screen = GameScreen.Playing;
        _seedInput = "";
        _seedInputActive = false;
        _gameOverSoundPlayed = false;

        BuildToolbar();
    }

    private void OnWaveStarting()
    {
        // commit towers
        foreach (var t in _towers)
            t.PlacedDuringPrep = false;

        AudioManager.Instance.Play("wave_start", 0.8f);
    }

    private void BuildToolbar()
    {
        _toolbar.Clear();
        int y = 44;
        int x = 10;
        int gap = 4;

        AddTowerButton(ref x, y, gap, "1:Gun $50", Keys.D1, TowerType.Basic,
            GameSettings.BasicTowerCost, new Color(0, 150, 255));
        AddTowerButton(ref x, y, gap, "2:Snpr $100", Keys.D2, TowerType.Sniper,
            GameSettings.SniperTowerCost, new Color(255, 100, 0));
        AddTowerButton(ref x, y, gap, "3:Rpd $75", Keys.D3, TowerType.Rapid,
            GameSettings.RapidTowerCost, new Color(0, 255, 100));
        AddTowerButton(ref x, y, gap, "4:Rkt $150", Keys.D4, TowerType.Rocket,
            GameSettings.RocketTowerCost, new Color(200, 50, 30));
        AddTowerButton(ref x, y, gap, "5:Flm $125", Keys.D5, TowerType.Flame,
            GameSettings.FlameTowerCost, new Color(255, 140, 0));

        x += 12;

        _toolbar.Add(new ToolbarButton(
            new Rectangle(x, y, 80, 28),
            "W: Wall", Keys.W,
            () => { _state.Mode = PlacementMode.Wall; AudioManager.Instance.Play("ui_click", 0.4f); },
            () => _state.Mode == PlacementMode.Wall,
            () => _state.HasWalls(),
            new Color(160, 120, 200)));

        // right-side buttons
        int rx = GameSettings.ScreenWidth - 10;

        _speedButton = new ToolbarButton(
            new Rectangle(rx - 100, y, 100, 28),
            $"Speed {_state.SpeedLabel}", Keys.OemPlus,
            () => {
                _state.CycleSpeed();
                _speedButton!.SetLabel($"Speed {_state.SpeedLabel}");
                AudioManager.Instance.Play("ui_click", 0.4f);
            },
            () => _state.Speed != GameSpeed.Normal,
            () => true,
            Color.Yellow);
        _toolbar.Add(_speedButton);
        rx -= 100 + gap;

        string autoLabel = _state.AutoStartWaves ? "Auto ON" : "Auto OFF";
        _autoStartButton = new ToolbarButton(
            new Rectangle(rx - 90, y, 90, 28),
            autoLabel, null,
            () =>
            {
                _state.AutoStartWaves = !_state.AutoStartWaves;
                _autoStartPref = _state.AutoStartWaves;
                _autoStartButton!.SetLabel(_state.AutoStartWaves ? "Auto ON" : "Auto OFF");
                AudioManager.Instance.Play("ui_click", 0.4f);
            },
            () => _state.AutoStartWaves,
            () => true,
            new Color(100, 180, 220));
        _toolbar.Add(_autoStartButton);
        rx -= 90 + gap;

        // mute button
        _muteButton = new ToolbarButton(
            new Rectangle(rx - 60, y, 60, 28),
            "M: Snd", Keys.M,
            () =>
            {
                AudioManager.Instance.ToggleMute();
                _muteButton!.SetLabel(AudioManager.Instance.Muted ? "M: OFF" : "M: Snd");
            },
            () => AudioManager.Instance.Muted,
            () => true,
            new Color(180, 180, 180));
        _toolbar.Add(_muteButton);
    }

    private void AddTowerButton(ref int x, int y, int gap, string label, Keys hotkey,
        TowerType type, int cost, Color accent)
    {
        int w = 100;
        _toolbar.Add(new ToolbarButton(
            new Rectangle(x, y, w, 28),
            label, hotkey,
            () => {
                _state.Mode = PlacementMode.Tower;
                _state.SelectedTower = type;
                AudioManager.Instance.Play("ui_click", 0.4f);
            },
            () => _state.Mode == PlacementMode.Tower && _state.SelectedTower == type,
            () => _state.Money >= cost,
            accent));
        x += w + gap;
    }

    // --- TEXT INPUT ---

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_screen != GameScreen.Title || !_seedInputActive) return;

        if (e.Key == Keys.Back)
        {
            if (_seedInput.Length > 0)
                _seedInput = _seedInput[..^1];
        }
        else if (char.IsDigit(e.Character) && _seedInput.Length < 6)
        {
            _seedInput += e.Character;
        }
    }

    // --- TIME SCALING ---

    private GameTime ScaleGameTime(GameTime original)
    {
        float m = _state.SpeedMultiplier;
        if (m == 1f) return original;

        var scaledElapsed = TimeSpan.FromTicks((long)(original.ElapsedGameTime.Ticks * m));
        var scaledTotal = TimeSpan.FromTicks((long)(original.TotalGameTime.Ticks * m));
        return new GameTime(scaledTotal, scaledElapsed);
    }

    // --- UPDATE ---

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // tick audio cooldowns (always, even on menus)
        AudioManager.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (kb.IsKeyDown(Keys.Escape) && _screen == GameScreen.Playing)
        {
            _contextMenu.Close();
            _screen = GameScreen.Title;
            _seeds.NewRandomSeed();
        }

        switch (_screen)
        {
            case GameScreen.Title:
                UpdateTitle(kb, mouse);
                break;
            case GameScreen.Playing:
                UpdatePlaying(gameTime, kb, mouse);
                break;
            case GameScreen.GameOver:
                UpdateGameOver(kb, mouse);
                break;
        }

        _prevMouse = mouse;
        _prevKb = kb;
        base.Update(gameTime);
    }

    private void UpdateTitle(KeyboardState kb, MouseState mouse)
    {
        bool leftClick = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;

        if (JustPressed(kb, Keys.Tab))
            _seedInputActive = !_seedInputActive;

        if (JustPressed(kb, Keys.Enter))
        {
            int seed = _seedInputActive && _seedInput.Length > 0
                ? int.Parse(_seedInput)
                : _seeds.CurrentSeed;
            StartGame(seed);
            return;
        }

        if (JustPressed(kb, Keys.Space) && !_seedInputActive)
            _seeds.NewRandomSeed();

        if (leftClick)
        {
            int favY = 420;
            foreach (var fav in _seeds.Favourites)
            {
                var bounds = new Rectangle(440, favY - 2, 400, 24);
                if (bounds.Contains(mouse.X, mouse.Y))
                {
                    _seedInput = fav.Seed.ToString();
                    _seedInputActive = true;
                    AudioManager.Instance.Play("ui_click", 0.4f);
                    break;
                }
                favY += 28;
                if (favY > 600) break;
            }
        }
    }

    private void UpdatePlaying(GameTime gameTime, KeyboardState kb, MouseState mouse)
    {
        if (!_state.IsGameOver)
        {
            HandleInput(kb, mouse);

            var scaledTime = ScaleGameTime(gameTime);

            _waves.Update(scaledTime, _enemies, _state);
            UpdateEnemies(scaledTime);
            UpdateTowers(scaledTime);
            UpdateProjectiles(scaledTime);
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
            }
            _screen = GameScreen.GameOver;
        }
    }

    private void UpdateGameOver(KeyboardState kb, MouseState mouse)
    {
        if (JustPressed(kb, Keys.R))
        {
            StartGame(_seeds.CurrentSeed);
            return;
        }

        if (JustPressed(kb, Keys.N))
        {
            _seeds.NewRandomSeed();
            StartGame(_seeds.CurrentSeed);
            return;
        }

        if (JustPressed(kb, Keys.F))
            _seeds.ToggleFavourite(_seeds.CurrentSeed, _state.Score, _waves.CurrentWave);

        if (JustPressed(kb, Keys.Escape))
        {
            _seeds.NewRandomSeed();
            _screen = GameScreen.Title;
        }
    }

    private void HandleInput(KeyboardState kb, MouseState mouse)
    {
        bool leftClick = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        bool rightClick = mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

        if (!_contextMenu.IsOpen)
        {
            foreach (var btn in _toolbar)
                if (btn.Hotkey.HasValue && JustPressed(kb, btn.Hotkey.Value))
                    btn.OnClick();
        }

        if (JustPressed(kb, Keys.R))
        {
            StartGame(_seeds.CurrentSeed);
            return;
        }

        if (leftClick && _contextMenu.IsOpen)
        {
            _contextMenu.HandleClick(mouse.X, mouse.Y);
            return;
        }

        if ((leftClick || rightClick) && _contextMenu.IsOpen)
        {
            _contextMenu.Close();
            if (leftClick) return;
        }

        if (leftClick)
        {
            foreach (var btn in _toolbar)
            {
                if (btn.Bounds.Contains(mouse.X, mouse.Y))
                {
                    btn.OnClick();
                    return;
                }
            }
        }

        if (JustPressed(kb, Keys.Space))
            _waves.RequestStart();

        _hoverCell = Map.WorldToGrid(new Vector2(mouse.X, mouse.Y));

        _hoveredTower = null;
        foreach (var t in _towers)
            if (t.GridPos == _hoverCell) { _hoveredTower = t; break; }

        if (leftClick && mouse.Y > GameSettings.UIHeight)
        {
            if (_state.Mode == PlacementMode.Tower)
                TryPlaceTower();
            else if (_state.Mode == PlacementMode.Wall)
                TryPlaceWall();
        }

        if (rightClick && mouse.Y > GameSettings.UIHeight)
            HandleRightClick(mouse);
    }

    // --- RIGHT-CLICK CONTEXT MENU ---

    private void HandleRightClick(MouseState mouse)
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y)) return;

        var cell = _map.Grid[_hoverCell.X, _hoverCell.Y];
        var items = new List<ContextMenuItem>();
        bool betweenWaves = !_waves.WaveActive;

        if (cell == CellType.Tower && _hoveredTower != null)
        {
            var tower = _hoveredTower;

            if (betweenWaves && tower.PlacedDuringPrep)
            {
                items.Add(new ContextMenuItem(
                    $"Remove (${tower.FullRefundValue})",
                    () => { RemoveTower(tower, tower.FullRefundValue); AudioManager.Instance.Play("ui_sell", 0.6f); },
                    Color.LightGreen));
            }
            else
            {
                items.Add(new ContextMenuItem(
                    $"Sell (${tower.SellValue})",
                    () => { RemoveTower(tower, tower.SellValue); AudioManager.Instance.Play("ui_sell", 0.6f); },
                    Color.Tomato));
            }

            if (tower.CanUpgrade)
            {
                int cost = tower.UpgradeCost;
                bool canAfford = _state.Money >= cost;
                string desc = tower.Type == TowerType.Flame
                    ? $"Upgrade Lv{tower.Level + 1} (${cost}) +DMG/Burn"
                    : tower.Type == TowerType.Rocket
                        ? $"Upgrade Lv{tower.Level + 1} (${cost}) +DMG/Splash"
                        : $"Upgrade Lv{tower.Level + 1} (${cost})";
                items.Add(new ContextMenuItem(
                    desc,
                    () => { UpgradeTower(tower); AudioManager.Instance.Play("ui_upgrade", 0.6f); },
                    canAfford ? Color.Gold : new Color(80, 60, 0),
                    canAfford));
            }
            else
            {
                items.Add(new ContextMenuItem("Max Level", () => { }, Color.DimGray, false));
            }
        }
        else if (cell == CellType.Wall && betweenWaves && _map.IsPlayerWall(_hoverCell.X, _hoverCell.Y))
        {
            int col = _hoverCell.X, row = _hoverCell.Y;
            items.Add(new ContextMenuItem(
                "Remove Wall (+1)",
                () => { RemovePlayerWall(col, row); AudioManager.Instance.Play("ui_click", 0.5f); },
                new Color(160, 120, 200)));
        }

        if (items.Count > 0)
            _contextMenu.Open(_hoverCell, new Vector2(mouse.X, mouse.Y), items);
    }

    private void RemoveTower(Tower tower, int refund)
    {
        _state.Money += refund;
        _map.RemoveTower(tower.GridPos.X, tower.GridPos.Y);
        _towers.Remove(tower);
        _hoveredTower = null;
    }

    private void UpgradeTower(Tower tower)
    {
        int cost = tower.UpgradeCost;
        if (_state.Money < cost) return;
        _state.Money -= cost;
        tower.Upgrade();
    }

    private void RemovePlayerWall(int col, int row)
    {
        _map.RemoveWall(col, row);
        _state.Walls++;
        NotifyEnemiesPathChanged();
    }

    // --- PLACEMENT ---

    private void TryPlaceTower()
    {
        if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y)) return;
        if (!_state.CanAffordTower()) return;

        _state.SpendTower();
        _map.PlaceTower(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f);

        var tower = new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower);
        if (_waves.WaveActive)
            tower.PlacedDuringPrep = false;
        _towers.Add(tower);
    }

    private void TryPlaceWall()
    {
        if (!_state.HasWalls()) return;
        if (!_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y)) return;

        _state.SpendWall();
        _map.PlaceWall(_hoverCell.X, _hoverCell.Y);
        AudioManager.Instance.Play("ui_click", 0.5f);
        NotifyEnemiesPathChanged();
    }

    private void NotifyEnemiesPathChanged()
    {
        foreach (var e in _enemies)
            if (e.IsAlive)
                e.UpdatePath(_map.CurrentPath);
    }

    // --- SIMULATION ---

    private void UpdateEnemies(GameTime gt)
    {
        foreach (var e in _enemies)
        {
            e.Update(gt);
            if (e.ReachedEnd && !e.IsAlive)
            {
                _state.LoseLife();
                AudioManager.Instance.Play("life_lost", 0.7f, 0.1f);
                e.ReachedEnd = false;
            }
        }
    }

    private void UpdateTowers(GameTime gt)
    {
        foreach (var t in _towers)
            t.Update(gt, _enemies, _projectiles);
    }

    private void UpdateProjectiles(GameTime gt)
    {
        foreach (var p in _projectiles)
            p.Update(gt);
    }

    private void CleanUp()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            if (!e.IsAlive)
            {
                if (!e.ReachedEnd && e.Health <= 0)
                {
                    _state.EarnReward(e.Reward);

                    // death sound based on enemy reward (proxy for toughness)
                    if (e.Reward > 25)
                        AudioManager.Instance.PlayVaried("death_boss", 0.5f, 0.1f, 0.08f);
                    else if (e.Reward > 15)
                        AudioManager.Instance.PlayVaried("death_medium", 0.4f, 0.15f, 0.05f);
                    else
                        AudioManager.Instance.PlayVaried("death_small", 0.3f, 0.2f, 0.04f);
                }
                _enemies.RemoveAt(i);
            }
        }
        _projectiles.RemoveAll(p => !p.IsActive);
    }

    // --- DRAW ---

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 8, 16));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        switch (_screen)
        {
            case GameScreen.Title:
                DrawTitle();
                break;
            case GameScreen.Playing:
                DrawGame();
                break;
            case GameScreen.GameOver:
                DrawGame();
                DrawGameOverOverlay();
                break;
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawTitle()
    {
        float cx = GameSettings.ScreenWidth / 2f;

        DrawCentred("RAD DEFENCE", cx, 100, Color.CornflowerBlue);
        DrawCentred("Tower Defence with Seeded Maps", cx, 140, Color.Gray);

        DrawCentred($"Seed: {_seeds.CurrentSeed}", cx, 220, Color.Gold);
        DrawCentred("[SPACE] New Random Seed    [TAB] Enter Seed    [ENTER] Start", cx, 260, Color.LightGray);

        if (_seedInputActive)
        {
            string display = _seedInput.Length > 0 ? _seedInput : "_";
            DrawCentred($"Enter seed: {display}", cx, 300, Color.White);
        }

        if (_seeds.Favourites.Count > 0)
        {
            DrawCentred("--- Favourites (click to load) ---", cx, 380, new Color(160, 120, 200));

            int y = 420;
            foreach (var fav in _seeds.Favourites)
            {
                string star = _seeds.CurrentSeed == fav.Seed ? "> " : "  ";
                string line = $"{star}Seed {fav.Seed}  |  Best: {fav.BestScore} pts  Wave {fav.BestWave}";
                DrawCentred(line, cx, y, Color.LightGray);
                y += 28;
                if (y > 600) break;
            }
        }

        DrawCentred("1-5: Towers | W: Wall | R-Click: Sell/Upgrade | SPACE: Wave | M: Mute | R: Restart",
            cx, GameSettings.ScreenHeight - 40, Color.DimGray);
    }

    private void DrawGame()
    {
        _map.Draw(_spriteBatch, _sprites);
        DrawPlacementGhost();
        DrawTowers();
        DrawEnemies();
        DrawProjectiles();
        DrawHUD();
        DrawToolbar();
        _contextMenu.Draw(_spriteBatch, _sprites.Pixel, _font);
    }

    private void DrawPlacementGhost()
    {
        if (!_map.IsInBounds(_hoverCell.X, _hoverCell.Y)) return;
        if (_contextMenu.IsOpen) return;

        var pos = Map.GridToWorld(_hoverCell.X, _hoverCell.Y);
        int size = GameSettings.CellSize - 4;
        var rect = new Rectangle(
            (int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), size, size);

        if (_state.Mode == PlacementMode.Tower)
        {
            if (!_map.CanPlaceTower(_hoverCell.X, _hoverCell.Y) || !_state.CanAffordTower())
            {
                if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y)
                    && _map.Grid[_hoverCell.X, _hoverCell.Y] != CellType.Wall)
                    return;

                _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f);
                return;
            }

            var towerTex = _sprites.Towers[_state.SelectedTower];
            _spriteBatch.Draw(towerTex, rect, Color.White * 0.5f);

            var tmp = new Tower(_hoverCell.X, _hoverCell.Y, _state.SelectedTower);
            tmp.DrawRange(_spriteBatch, _sprites.Ring);
        }
        else if (_state.Mode == PlacementMode.Wall)
        {
            if (!_state.HasWalls() || !_map.CanPlaceWall(_hoverCell.X, _hoverCell.Y))
            {
                if (_map.IsInBounds(_hoverCell.X, _hoverCell.Y)
                    && _map.Grid[_hoverCell.X, _hoverCell.Y] == CellType.Empty)
                    _spriteBatch.Draw(_sprites.Pixel, rect, Color.Red * 0.2f);
                return;
            }
            _spriteBatch.Draw(_sprites.TileWall, rect, Color.White * 0.5f);
        }
    }

    private void DrawTowers()
    {
        foreach (var t in _towers)
        {
            t.Draw(_spriteBatch, _sprites, t == _hoveredTower);
            if (t == _hoveredTower)
                t.DrawRange(_spriteBatch, _sprites.Ring);
        }
    }

    private void DrawEnemies()
    {
        foreach (var e in _enemies)
            e.Draw(_spriteBatch, _sprites.Pixel);
    }

    private void DrawProjectiles()
    {
        foreach (var p in _projectiles)
            p.Draw(_spriteBatch, _sprites);
    }

    private void DrawHUD()
    {
        _spriteBatch.Draw(_sprites.Pixel,
            new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.UIHeight),
            new Color(20, 20, 35));
        _spriteBatch.Draw(_sprites.Pixel,
            new Rectangle(0, GameSettings.UIHeight - 1, GameSettings.ScreenWidth, 1),
            new Color(50, 50, 80));

        float y1 = 6;

        _spriteBatch.DrawString(_font, $"Lives: {_state.Lives}", new Vector2(10, y1), Color.Tomato);
        _spriteBatch.DrawString(_font, $"${_state.Money}", new Vector2(150, y1), Color.Gold);
        _spriteBatch.DrawString(_font, $"Score: {_state.Score}", new Vector2(260, y1), Color.White);
        _spriteBatch.DrawString(_font, $"Walls: {_state.Walls}", new Vector2(420, y1), new Color(160, 120, 200));

        string seedText = $"Seed: {_seeds.CurrentSeed}";
        var seedSize = _font.MeasureString(seedText);
        _spriteBatch.DrawString(_font, seedText,
            new Vector2(GameSettings.ScreenWidth - seedSize.X - 10, y1), Color.DimGray);

        string wave;
        if (_waves.WaveActive)
        {
            wave = $"Wave {_waves.CurrentWave}  (R-click towers to sell/upgrade)";
        }
        else if (_waves.CurrentWave == 0)
        {
            wave = "Place towers on walls, then SPACE to start";
        }
        else
        {
            string cleared = $"Wave {_waves.CurrentWave} cleared! +{_waves.LastWallGrant} walls  R-click to edit";
            if (_state.AutoStartWaves)
            {
                float remaining = Math.Max(0, 10 - _waves.BreakTimer);
                wave = $"{cleared}  Next in {remaining:0}s  [SPACE]";
            }
            else
            {
                wave = $"{cleared}  Press SPACE when ready";
            }
        }

        var waveSize = _font.MeasureString(wave);
        _spriteBatch.DrawString(_font, wave,
            new Vector2((GameSettings.ScreenWidth - waveSize.X) / 2f, y1), Color.CornflowerBlue);
    }

    private void DrawToolbar()
    {
        foreach (var btn in _toolbar)
            btn.Draw(_spriteBatch, _sprites.Pixel, _font);
    }

    private void DrawGameOverOverlay()
    {
        _spriteBatch.Draw(_sprites.Pixel,
            new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
            Color.Black * 0.75f);

        float cx = GameSettings.ScreenWidth / 2f;
        float y = GameSettings.ScreenHeight / 2f - 80;

        DrawCentred("GAME OVER", cx, y, Color.Red);
        y += 40;
        DrawCentred($"Score: {_state.Score}  |  Wave: {_waves.CurrentWave}  |  Seed: {_seeds.CurrentSeed}", cx, y, Color.White);
        y += 50;

        bool isFav = _seeds.IsFavourite(_seeds.CurrentSeed);
        string favText = isFav ? "Favourited!" : "[F] Favourite this seed";
        DrawCentred(favText, cx, y, isFav ? Color.Gold : new Color(160, 120, 200));
        y += 40;

        DrawCentred("[R] Replay Same Seed    [N] New Random Seed    [ESC] Menu", cx, y, Color.LightGray);
    }

    // --- HELPERS ---

    private void DrawCentred(string text, float cx, float y, Color color)
    {
        var size = _font.MeasureString(text);
        _spriteBatch.DrawString(_font, text, new Vector2(cx - size.X / 2f, y), color);
    }

    private bool JustPressed(KeyboardState kb, Keys key)
        => kb.IsKeyDown(key) && !_prevKb.IsKeyDown(key);

    private Texture2D CreateRing(int diameter, int thickness)
    {
        var tex = new Texture2D(GraphicsDevice, diameter, diameter);
        var data = new Color[diameter * diameter];
        float r = diameter / 2f;
        for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                data[y * diameter + x] = MathF.Abs(dist - r + thickness) <= thickness
                    ? Color.White : Color.Transparent;
            }
        tex.SetData(data);
        return tex;
    }
}
