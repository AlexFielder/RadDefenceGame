namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public class Map
{
    public CellType[,] Grid { get; }
    public Point SpawnCell { get; }
    public Point ExitCell { get; }
    public List<Vector2> CurrentPath { get; private set; } = new();
    public int Seed { get; }

    public HashSet<Point> PlayerPlacedWalls { get; } = new();

    private static readonly Point[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public Map(int seed)
    {
        Seed = seed;
        Grid = new CellType[GameSettings.GridCols, GameSettings.GridRows];
        SpawnCell = new Point(0, 1);
        ExitCell = new Point(GameSettings.GridCols - 1, GameSettings.GridRows - 2);
        Grid[SpawnCell.X, SpawnCell.Y] = CellType.Spawn;
        Grid[ExitCell.X, ExitCell.Y] = CellType.Exit;
        GenerateWalls(seed);
        RecalculatePath();
    }

    private void GenerateWalls(int seed)
    {
        var rng = new Random(seed);
        int placed = 0, attempts = 0;
        while (placed < GameSettings.InitialWallCount && attempts < 5000)
        {
            attempts++;
            int col = rng.Next(GameSettings.GridCols), row = rng.Next(GameSettings.GridRows);
            if (Grid[col, row] != CellType.Empty) continue;
            if (ManhattanDistance(col, row, SpawnCell.X, SpawnCell.Y) <= 2) continue;
            if (ManhattanDistance(col, row, ExitCell.X, ExitCell.Y) <= 2) continue;
            Grid[col, row] = CellType.Wall;
            if (FindPath(SpawnCell, ExitCell) == null) { Grid[col, row] = CellType.Empty; continue; }
            placed++;
        }
    }

    private static int ManhattanDistance(int x1, int y1, int x2, int y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    public bool RecalculatePath()
    {
        var path = FindPath(SpawnCell, ExitCell);
        if (path == null) return false;
        CurrentPath = new List<Vector2>();
        foreach (var cell in path) CurrentPath.Add(GridToWorld(cell.X, cell.Y));
        return true;
    }

    private List<Point>? FindPath(Point start, Point end)
    {
        var visited = new bool[GameSettings.GridCols, GameSettings.GridRows];
        var parent = new Point?[GameSettings.GridCols, GameSettings.GridRows];
        var queue = new Queue<Point>();
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == end) return ReconstructPath(parent, start, end);
            foreach (var d in Dirs)
            {
                int nx = cur.X + d.X, ny = cur.Y + d.Y;
                if (!IsInBounds(nx, ny) || visited[nx, ny] || !IsWalkable(nx, ny)) continue;
                visited[nx, ny] = true; parent[nx, ny] = cur; queue.Enqueue(new Point(nx, ny));
            }
        }
        return null;
    }

    private static List<Point> ReconstructPath(Point?[,] parent, Point start, Point end)
    {
        var path = new List<Point>();
        var cur = end;
        while (cur != start) { path.Add(cur); cur = parent[cur.X, cur.Y]!.Value; }
        path.Add(start); path.Reverse(); return path;
    }

    private bool IsWalkable(int col, int row)
    {
        var cell = Grid[col, row];
        return cell == CellType.Empty || cell == CellType.Spawn || cell == CellType.Exit;
    }

    public bool CanPlaceWall(int col, int row)
    {
        if (!IsInBounds(col, row) || Grid[col, row] != CellType.Empty) return false;
        Grid[col, row] = CellType.Wall;
        bool ok = FindPath(SpawnCell, ExitCell) != null;
        Grid[col, row] = CellType.Empty;
        return ok;
    }

    public bool CanPlaceTower(int col, int row)
    {
        if (!IsInBounds(col, row)) return false;
        return Grid[col, row] == CellType.Wall;
    }

    /// <summary>Check if a 2x2 area starting at (col,row) is all walls and fits in bounds.</summary>
    public bool CanPlace2x2Tower(int col, int row)
    {
        for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
            {
                int cx = col + dx, cy = row + dy;
                if (!IsInBounds(cx, cy)) return false;
                if (Grid[cx, cy] != CellType.Wall) return false;
            }
        return true;
    }

    /// <summary>Place a 2x2 tower. Marks all 4 cells as Tower. Anchor = top-left.</summary>
    public void Place2x2Tower(int col, int row)
    {
        for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
                Grid[col + dx, row + dy] = CellType.Tower;
    }

    /// <summary>Remove a 2x2 tower. Reverts all 4 cells to Wall.</summary>
    public void Remove2x2Tower(int col, int row)
    {
        for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
                Grid[col + dx, row + dy] = CellType.Wall;
    }

    public void PlaceWall(int col, int row, bool playerPlaced = true)
    {
        Grid[col, row] = CellType.Wall;
        if (playerPlaced) PlayerPlacedWalls.Add(new Point(col, row));
        RecalculatePath();
    }

    public void PlaceTower(int col, int row) { Grid[col, row] = CellType.Tower; }

    public bool RemoveWall(int col, int row)
    {
        var pt = new Point(col, row);
        if (!PlayerPlacedWalls.Contains(pt)) return false;
        Grid[col, row] = CellType.Empty; PlayerPlacedWalls.Remove(pt); RecalculatePath(); return true;
    }

    public void RemoveTower(int col, int row) { Grid[col, row] = CellType.Wall; }

    public bool IsPlayerWall(int col, int row) => PlayerPlacedWalls.Contains(new Point(col, row));

    public static Vector2 GridToWorld(int col, int row)
    {
        return new Vector2(
            col * GameSettings.CellSize + GameSettings.CellSize / 2f,
            row * GameSettings.CellSize + GameSettings.CellSize / 2f + GameSettings.UIHeight);
    }

    public static Point WorldToGrid(Vector2 world)
    {
        int col = (int)(world.X / GameSettings.CellSize);
        int row = (int)((world.Y - GameSettings.UIHeight) / GameSettings.CellSize);
        return new Point(col, row);
    }

    public bool IsInBounds(int col, int row) => col >= 0 && col < GameSettings.GridCols && row >= 0 && row < GameSettings.GridRows;

    public void Draw(SpriteBatch sb, SpriteSet sprites)
    {
        var pathSet = new HashSet<Point>();
        foreach (var wp in CurrentPath) pathSet.Add(WorldToGrid(wp));

        for (int x = 0; x < GameSettings.GridCols; x++)
            for (int y = 0; y < GameSettings.GridRows; y++)
            {
                var rect = new Rectangle(x * GameSettings.CellSize, y * GameSettings.CellSize + GameSettings.UIHeight,
                    GameSettings.CellSize, GameSettings.CellSize);
                var cell = Grid[x, y];

                if (cell == CellType.Wall || cell == CellType.Tower)
                {
                    bool isPlayer = PlayerPlacedWalls.Contains(new Point(x, y));
                    sb.Draw(sprites.TileWall, rect, isPlayer ? new Color(200, 180, 255) : Color.White);
                }
                else if (cell == CellType.Spawn || cell == CellType.Exit || pathSet.Contains(new Point(x, y)))
                    sb.Draw(sprites.TilePath, rect, Color.White);
                else
                    sb.Draw(sprites.TilePath, rect, new Color(60, 60, 80));

                sb.Draw(sprites.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(25, 25, 45) * 0.5f);
                sb.Draw(sprites.Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Color(25, 25, 45) * 0.5f);
            }

        DrawCellMarker(sb, sprites.Pixel, SpawnCell, new Color(60, 200, 60));
        DrawCellMarker(sb, sprites.Pixel, ExitCell, new Color(220, 60, 60));
    }

    private static void DrawCellMarker(SpriteBatch sb, Texture2D pixel, Point cell, Color color)
    {
        var rect = new Rectangle(cell.X * GameSettings.CellSize + 1, cell.Y * GameSettings.CellSize + GameSettings.UIHeight + 1,
            GameSettings.CellSize - 2, GameSettings.CellSize - 2);
        int b = 2;
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, b), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - b, rect.Width, b), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, b, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - b, rect.Y, b, rect.Height), color);
    }
}
