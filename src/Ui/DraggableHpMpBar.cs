using System;
using Godot;

namespace IdleLineage.Ui;

public sealed partial class DraggableHpMpBar : Control
{
    private bool _isDragging = false;
    private Vector2 _dragOffset;

    public event Action<Vector2>? OnPositionChanged;

    public TextureRect HpFill { get; }
    public Label HpTxt { get; }
    public TextureRect MpFill { get; }
    public Label MpTxt { get; }

    public DraggableHpMpBar()
    {
        Name = "DraggableHpMpBar";
        CustomMinimumSize = new Vector2(334f, 34f);
        Size = new Vector2(334f, 34f);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ZIndex = 2150;
        TooltipText = "按住左鍵可拖曳移動血魔條";

        // Frame texture
        Texture2D frameTex = GD.Load<Texture2D>("res://assets/ui/hpmp_frame.png");
        TextureRect frame = new TextureRect
        {
            Texture = frameTex,
            Position = Vector2.Zero,
            Size = new Vector2(334f, 34f),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(frame);

        // HP Fill
        Texture2D hpTex = GD.Load<Texture2D>("res://assets/ui/hpmp_hp_fill.png");
        HpFill = new TextureRect
        {
            Texture = new AtlasTexture
            {
                Atlas = hpTex,
                Region = new Rect2(0f, 0f, hpTex.GetWidth(), hpTex.GetHeight())
            },
            Position = new Vector2(9f, 10f),
            Size = new Vector2(142f, 14f),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(HpFill);

        // HP Text
        HpTxt = new Label
        {
            Position = new Vector2(9f, 8f),
            Size = new Vector2(142f, 14f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        HpTxt.AddThemeFontSizeOverride("font_size", 11);
        HpTxt.AddThemeColorOverride("font_color", Colors.White);
        HpTxt.AddThemeColorOverride("font_outline_color", Color.FromHtml("#1a0d06"));
        HpTxt.AddThemeConstantOverride("outline_size", 1);
        AddChild(HpTxt);

        // MP Fill
        Texture2D mpTex = GD.Load<Texture2D>("res://assets/ui/hpmp_mp_fill.png");
        MpFill = new TextureRect
        {
            Texture = new AtlasTexture
            {
                Atlas = mpTex,
                Region = new Rect2(0f, 0f, mpTex.GetWidth(), mpTex.GetHeight())
            },
            Position = new Vector2(184f, 10f),
            Size = new Vector2(142f, 14f),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(MpFill);

        // MP Text
        MpTxt = new Label
        {
            Position = new Vector2(184f, 8f),
            Size = new Vector2(142f, 14f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        MpTxt.AddThemeFontSizeOverride("font_size", 11);
        MpTxt.AddThemeColorOverride("font_color", Colors.White);
        MpTxt.AddThemeColorOverride("font_outline_color", Color.FromHtml("#1a0d06"));
        MpTxt.AddThemeConstantOverride("outline_size", 1);
        AddChild(MpTxt);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isDragging = true;
                    _dragOffset = mb.GlobalPosition - GlobalPosition;
                    AcceptEvent();
                }
                else if (_isDragging)
                {
                    _isDragging = false;
                    AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            Vector2 targetPos = mm.GlobalPosition - _dragOffset;
            Vector2 viewportSize = GetViewportRect().Size;
            targetPos.X = Mathf.Clamp(targetPos.X, 0f, Math.Max(0f, viewportSize.X - Size.X));
            targetPos.Y = Mathf.Clamp(targetPos.Y, 0f, Math.Max(0f, viewportSize.Y - Size.Y));
            Position = targetPos;
            OnPositionChanged?.Invoke(targetPos);
            AcceptEvent();
        }
    }
}
