using System;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public sealed partial class InventoryGridSlot : Control
{
	private TextureRect? _blessingGlow;

	private TextureRect? _icon;

	private Label? _corner;

	private bool _hovered;

	private bool _dragging;

	internal static readonly Color BrokenTint = new Color(1f, 0.45f, 0.45f);

	internal static readonly Color BrokenWash = new Color(0.85f, 0.1f, 0.1f, 0.22f);

	private const int MarkFontSize = 11;

	private const float IconInset = 3f / 34f;

	private Rect2I _iconContent;

	public string ItemKey { get; init; } = "";

	public string StackUid { get; init; } = "";

	public bool Draggable { get; init; }

	public bool Locked { get; init; }

	public ItemBlessing BlessingState { get; init; }

	public Color? Quality { get; init; }

	public Color? FrameQuality { get; init; }

	public bool QualityFrame { get; init; }

	public bool BrokenBlade { get; init; }

	public bool SingleClick { get; init; }

	public Action? OnActivate { get; init; }

	public Action<string, string>? OnDropSwap { get; set; }

	public Action<string, string>? OnDropToEmpty { get; set; }

	public Func<string, string, bool>? CanDropItem { get; set; }

	public InventoryGridSlot()
	{
		base.MouseEntered += delegate
		{
			_hovered = true;
			if (ItemKey == ContentAdditions.GuardianSoulKey || ItemKey == "item_guardian_soul")
			{
				TooltipText = "守護者的靈魂\n" + ArpgEngineScreen.GetGuardianSoulFullTooltipText();
			}
			QueueRedraw();
		};
		base.MouseExited += delegate
		{
			_hovered = false;
			QueueRedraw();
		};
	}

	private void PlaceAnchored(Control control, LayoutPreset preset, float left, float top, float right, float bottom)
	{
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		control.SetAnchorsPreset(preset);
		control.OffsetLeft = left;
		control.OffsetTop = top;
		control.OffsetRight = right;
		control.OffsetBottom = bottom;
	}

	public void SetIcon(Texture2D? texture)
	{
		if (texture != null)
		{
			_icon = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				Texture = texture,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				TextureFilter = TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore
			};
			_iconContent = (string.IsNullOrEmpty(ItemKey) ? new Rect2I(Vector2I.Zero, (Vector2I)texture.GetSize().Round()) : ItemIcons.ContentRect(ItemKey));
			_blessingGlow = (ItemKey != null && ItemKey.StartsWith("scroll_")) ? null : ItemBlessingGlow.Create(texture, BlessingState, TextureRect.StretchModeEnum.Scale);
			if (_blessingGlow != null)
			{
				AddChild(_blessingGlow, forceReadableName: false, InternalMode.Disabled);
			}
			AddChild(_icon, forceReadableName: false, InternalMode.Disabled);
			base.ClipContents = true;
			LayoutIcon();
			if (BrokenBlade)
			{
				_icon.Modulate = BrokenTint;
			}
			if (Locked)
			{
				CornerMark("鎖", LayoutPreset.TopRight, -22f, 1f, -4f, 17f, "#7fd0ff", HorizontalAlignment.Right);
			}
			if (BlessingState == ItemBlessing.Blessed)
			{
				CornerMark("祝", LayoutPreset.TopLeft, 3f, 1f, 25f, 17f, "#f2c14e", HorizontalAlignment.Left);
			}
			else if (BlessingState == ItemBlessing.Cursed)
			{
				CornerMark("詛", LayoutPreset.TopLeft, 3f, 1f, 25f, 17f, "#e2938f", HorizontalAlignment.Left);
			}
		}
	}

	private void LayoutIcon()
	{
		if (_icon?.Texture == null)
		{
			return;
		}
		Vector2 size = _icon.Texture.GetSize();
		if (!(size.X <= 0f) && !(size.Y <= 0f) && !(base.Size.X <= 0f) && !(base.Size.Y <= 0f))
		{
			float num = Mathf.Min(base.Size.X, base.Size.Y) * 0.8235294f / Mathf.Max(size.X, size.Y);
			_icon.Size = size * num;
			_icon.Position = base.Size * 0.5f - (_iconContent.Position + (Vector2)_iconContent.Size * 0.5f) * num;
			if (_blessingGlow != null)
			{
				_blessingGlow.Size = _icon.Size;
				_blessingGlow.Position = _icon.Position;
			}
		}
	}

	public override void _Notification(int what)
	{
		if ((long)what == 40)
		{
			LayoutIcon();
		}
		if (_dragging && ((long)what == 22 || (long)what == 11))
		{
			_dragging = false;
			DragCursorFeedback.End();
		}
	}

	private void CornerMark(string text, LayoutPreset preset, float left, float top, float right, float bottom, string color, HorizontalAlignment alignment)
	{
		PlaceAnchored(MarkLabel(text, Color.FromHtml(color.AsSpan()), alignment, VerticalAlignment.Top), preset, left, top, right, bottom);
	}

	public void SetCorner(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		bool flag = text.StartsWith('+') || text.StartsWith('−');
		Color color;
		if (flag)
		{
			Color? quality = Quality;
			if (quality.HasValue)
			{
				Color valueOrDefault = quality.GetValueOrDefault();
				color = valueOrDefault;
				goto IL_0073;
			}
		}
		color = ((text == "E" || flag) ? Color.FromHtml("#45b4ff".AsSpan()) : Color.FromHtml("#d5a94f".AsSpan()));
		goto IL_0073;
		IL_0073:
		_corner = MarkLabel(text, color, HorizontalAlignment.Right, VerticalAlignment.Bottom);
		_corner.TooltipText = text;
		PlaceAnchored(_corner, LayoutPreset.BottomWide, 2f, -18f, -4f, -2f);
	}

	private static Label MarkLabel(string text, Color color, HorizontalAlignment horizontal, VerticalAlignment vertical)
	{
		return new Label
		{
			LabelSettings = new LabelSettings
			{
				FontSize = 11,
				FontColor = color,
				ShadowColor = new Color(0f, 0f, 0f, 0.85f),
				ShadowOffset = new Vector2(1f, 1f),
				ShadowSize = 1
			},
			Text = text,
			HorizontalAlignment = horizontal,
			VerticalAlignment = vertical,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
		};
	}

	public override void _Draw()
	{
		if (BrokenBlade)
		{
			DrawRect(new Rect2(new Vector2(2f, 2f), base.Size - new Vector2(4f, 4f)), BrokenWash);
		}
		if (QualityFrame)
		{
			Color? color = FrameQuality ?? Quality;
			if (color.HasValue)
			{
				Color valueOrDefault = color.GetValueOrDefault();
				DrawRect(new Rect2(new Vector2(1.5f, 1.5f), base.Size - new Vector2(3f, 3f)), valueOrDefault, filled: false, 3f);
			}
		}
		if (_hovered)
		{
			DrawRect(new Rect2(Vector2.One, base.Size - new Vector2(2f, 2f)), Color.FromHtml("#c6b47a".AsSpan()), filled: false, 1f);
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: not false } inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && (SingleClick ? (!inputEventMouseButton.DoubleClick) : inputEventMouseButton.DoubleClick) && OnActivate != null)
		{
			AcceptEvent();
			GameAudio.Instance?.PlayUi("inventoryAction", 40.0, 0.48f);
			OnActivate();
		}
	}

	public override Control? _MakeCustomTooltip(string forText)
	{
		return ClassicItemTooltip.Build(forText, ItemKey);
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (!Draggable || string.IsNullOrEmpty(ItemKey))
		{
			return default(Variant);
		}
		_dragging = true;
		DragCursorFeedback.Begin();
		string text = GameDataProvider.Shared.Item(ItemKey)?["n"]?.GetValue<string>() ?? ItemKey;
		Label label = new Label
		{
			Text = "→ " + text
		};
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		SetDragPreview(label);
		return ItemDragPayload.Encode(ItemKey, StackUid);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.String)
		{
			return false;
		}
		string text = data.AsString();
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		var (dragItemKey, dragStackUid, hasUid) = ItemDragPayload.Decode(text);
		if (string.IsNullOrEmpty(dragItemKey))
		{
			return false;
		}
		if (hasUid && !string.IsNullOrEmpty(StackUid) && dragStackUid == StackUid)
		{
			return false;
		}
		if (CanDropItem != null)
		{
			return CanDropItem(dragItemKey, dragStackUid);
		}
		return OnDropSwap != null || OnDropToEmpty != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		string text = data.AsString();
		var (dragItemKey, dragStackUid, hasUid) = ItemDragPayload.Decode(text);
		if (!string.IsNullOrEmpty(StackUid) || !string.IsNullOrEmpty(ItemKey))
		{
			OnDropSwap?.Invoke(dragItemKey, dragStackUid);
		}
		else
		{
			OnDropToEmpty?.Invoke(dragItemKey, dragStackUid);
		}
	}
}
