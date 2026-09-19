using System;
using Godot;

namespace IdleLineage.Ui;

public sealed partial class DraggableChatBar : PanelContainer
{
    private bool _isDragging = false;
    private Vector2 _dragOffset;

    public LineEdit ChatInput { get; }

    public DraggableChatBar(float width, Action<string> onSubmitted)
    {
        Name = "DraggableChatBar";
        CustomMinimumSize = new Vector2(width, 24f);
        Size = new Vector2(width, 24f);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ZIndex = 2200;

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.88f),
            BorderColor = new Color(0.35f, 0.45f, 0.60f, 0.70f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 5,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        AddThemeStyleboxOverride("panel", panelStyle);

        HBoxContainer hbox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 6);
        AddChild(hbox);

        // Drag handle on left
        Label dragLabel = new Label
        {
            Text = "✥ 聊天",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            MouseDefaultCursorShape = CursorShape.Move,
            TooltipText = "按住左鍵可拖曳移動此對話輸入框"
        };
        dragLabel.AddThemeFontSizeOverride("font_size", 11);
        dragLabel.AddThemeColorOverride("font_color", Color.FromHtml("#66d9ef"));
        dragLabel.AddThemeColorOverride("font_outline_color", Color.FromHtml("#000000"));
        dragLabel.AddThemeConstantOverride("outline_size", 2);
        hbox.AddChild(dragLabel);

        // Input LineEdit
        ChatInput = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "按 Enter 或點擊輸入聊天訊息...",
            CaretBlink = true,
            ContextMenuEnabled = false,
            FocusMode = FocusModeEnum.Click,
            MouseFilter = MouseFilterEnum.Stop
        };
        ChatInput.AddThemeFontSizeOverride("font_size", 11);
        ChatInput.AddThemeColorOverride("font_color", Colors.White);
        ChatInput.AddThemeColorOverride("font_placeholder_color", Color.FromHtml("#7e889b"));
        ChatInput.AddThemeColorOverride("font_outline_color", Color.FromHtml("#12100c"));
        ChatInput.AddThemeConstantOverride("outline_size", 2);

        var normalBox = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.08f, 0.6f),
            BorderColor = new Color(0.2f, 0.25f, 0.35f, 0.5f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 1,
            ContentMarginBottom = 1
        };

        var focusBox = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.10f, 0.16f, 0.95f),
            BorderColor = Color.FromHtml("#ffd76a"),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 1,
            ContentMarginBottom = 1
        };

        ChatInput.AddThemeStyleboxOverride("normal", normalBox);
        ChatInput.AddThemeStyleboxOverride("focus", focusBox);
        ChatInput.TextSubmitted += (s) => onSubmitted(s);
        hbox.AddChild(ChatInput);
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
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
        else if (@event is InputEventMouseMotion mm && _isDragging)
        {
            Vector2 targetPos = mm.GlobalPosition - _dragOffset;
            Vector2 viewportSize = GetViewportRect().Size;
            targetPos.X = Mathf.Clamp(targetPos.X, 0f, Math.Max(0f, viewportSize.X - Size.X));
            targetPos.Y = Mathf.Clamp(targetPos.Y, 0f, Math.Max(0f, viewportSize.Y - Size.Y));
            Position = targetPos;
            AcceptEvent();
        }
    }
}
