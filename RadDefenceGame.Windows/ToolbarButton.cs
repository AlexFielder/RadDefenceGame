namespace RadDefenceGame.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

public class ToolbarButton
{
    public Rectangle Bounds { get; }
    public string Label { get; private set; }
    public Keys? Hotkey { get; }
    public Action OnClick { get; }
    public Color Accent { get; }

    private readonly Func<bool> _isSelected;
    private readonly Func<bool> _isEnabled;

    public ToolbarButton(Rectangle bounds, string label, Keys? hotkey,
        Action onClick, Func<bool> isSelected, Func<bool> isEnabled, Color accent)
    {
        Bounds = bounds;
        Label = label;
        Hotkey = hotkey;
        OnClick = onClick;
        Accent = accent;
        _isSelected = isSelected;
        _isEnabled = isEnabled;
    }

    public void SetLabel(string label) => Label = label;

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)
    {
        bool selected = _isSelected();
        bool enabled = _isEnabled();

        Color bg = selected ? Accent * 0.3f : new Color(30, 30, 50);
        sb.Draw(pixel, Bounds, bg);

        Color border = selected ? Accent : (enabled ? new Color(60, 60, 90) : new Color(40, 30, 30));
        int b = selected ? 2 : 1;
        sb.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, b), border);
        sb.Draw(pixel, new Rectangle(Bounds.X, Bounds.Bottom - b, Bounds.Width, b), border);
        sb.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, b, Bounds.Height), border);
        sb.Draw(pixel, new Rectangle(Bounds.Right - b, Bounds.Y, b, Bounds.Height), border);

        Color textCol = selected ? Color.White : (enabled ? Color.LightGray : new Color(100, 50, 50));
        var textSize = font.MeasureString(Label);
        var textPos = new Vector2(
            Bounds.X + (Bounds.Width - textSize.X) / 2f,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2f);
        sb.DrawString(font, Label, textPos, textCol);
    }
}
