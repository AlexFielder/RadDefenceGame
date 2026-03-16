namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

public class ContextMenuItem
{
    public string Label { get; }
    public Action OnClick { get; }
    public Color Color { get; }
    public bool Enabled { get; }

    public ContextMenuItem(string label, Action onClick, Color color, bool enabled = true)
    {
        Label = label;
        OnClick = onClick;
        Color = color;
        Enabled = enabled;
    }
}

public class ContextMenu
{
    public bool IsOpen { get; private set; }
    public Point AnchorCell { get; private set; }

    private readonly List<ContextMenuItem> _items = new();
    private Rectangle _bounds;
    private readonly List<Rectangle> _itemBounds = new();

    private const int ItemWidth = 210;
    private const int ItemHeight = 26;
    private const int Padding = 4;

    public void Open(Point cell, Vector2 screenPos, List<ContextMenuItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        AnchorCell = cell;
        IsOpen = true;

        int totalH = Padding * 2 + _items.Count * ItemHeight;
        int totalW = ItemWidth + Padding * 2;

        int x = (int)screenPos.X;
        int y = (int)screenPos.Y;
        if (x + totalW > GameSettings.ScreenWidth) x = GameSettings.ScreenWidth - totalW;
        if (y + totalH > GameSettings.ScreenHeight) y = GameSettings.ScreenHeight - totalH;

        _bounds = new Rectangle(x, y, totalW, totalH);

        _itemBounds.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            _itemBounds.Add(new Rectangle(
                x + Padding, y + Padding + i * ItemHeight,
                ItemWidth, ItemHeight));
        }
    }

    public void Close() => IsOpen = false;

    public bool HandleClick(int mx, int my)
    {
        if (!IsOpen) return false;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_itemBounds[i].Contains(mx, my) && _items[i].Enabled)
            {
                _items[i].OnClick();
                Close();
                return true;
            }
        }

        Close();
        return true;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)
    {
        if (!IsOpen) return;

        sb.Draw(pixel, _bounds, new Color(25, 25, 45));

        int b = 1;
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, b), new Color(80, 80, 120));
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Bottom - b, _bounds.Width, b), new Color(80, 80, 120));
        sb.Draw(pixel, new Rectangle(_bounds.X, _bounds.Y, b, _bounds.Height), new Color(80, 80, 120));
        sb.Draw(pixel, new Rectangle(_bounds.Right - b, _bounds.Y, b, _bounds.Height), new Color(80, 80, 120));

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var rect = _itemBounds[i];

            Color textCol = item.Enabled ? item.Color : new Color(60, 60, 60);
            var textSize = font.MeasureString(item.Label);
            var textPos = new Vector2(
                rect.X + 8,
                rect.Y + (rect.Height - textSize.Y) / 2f);
            sb.DrawString(font, item.Label, textPos, textCol);
        }
    }
}
