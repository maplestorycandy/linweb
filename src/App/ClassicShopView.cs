using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal sealed partial class ClassicShopView : Control
{
	private const string AssetRoot = "res://assets/ui/shop";

	private const int VisibleRows = 7;

	private const int RowPitch = 36;

	private const int FirstRowTop = 37;

	private const int RowLeft = 15;

	private const int RowWidth = 197;

	private const int RowHeight = 33;

	private const int IconWellLeft = 18;

	private static readonly Vector2 IconWellSize = new Vector2(33f, 33f);

	private const int IconContentInset = 3;

	private static readonly Vector2 IconContentSize = new Vector2(28f, 28f);

	private const int TextLeft = 56;

	private const int TextWidth = 151;

	private static readonly Vector2 FrameArtSize = new Vector2(239f, 317f);

	private const float StatusStripHeight = 24f;

	private readonly List<Node> _rowNodes = new List<Node>();

	private readonly VScrollBar _scroll;

	private IReadOnlyList<ClassicShopEntry> _entries = Array.Empty<ClassicShopEntry>();

	private int _maximumOffset;

	public TextureButton BuyButton { get; }

	public TextureButton SellButton { get; }

	public Label GoldLabel { get; }

	public Label Status { get; }

	private static int RowTop(int row)
	{
		return 37 + row * 36;
	}

	private static int IconWellTop(int row)
	{
		return RowTop(row);
	}

	public ClassicShopView(Vector2 viewport, string shopName, Action onClose)
	{
		ArgumentNullException.ThrowIfNull(onClose, "onClose");
		base.Size = new Vector2(FrameArtSize.X, FrameArtSize.Y + 24f);
		base.Position = (viewport - base.Size) * 0.5f;
		base.MouseFilter = MouseFilterEnum.Stop;
		AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/shop/1983.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Position = new Vector2(6f, 9f),
			Size = new Vector2(227f, 304f),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/shop/1984.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Position = Vector2.Zero,
			Size = FrameArtSize,
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		_scroll = new VScrollBar
		{
			Position = new Vector2(217f, 37f),
			Size = new Vector2(12f, 252f),
			MinValue = 0.0,
			MaxValue = 1.0,
			Page = 1.0,
			Step = 1.0,
			MouseDefaultCursorShape = CursorShape.Drag
		};
		MakeInvisible(_scroll);
		_scroll.ValueChanged += OnScrollChanged;
		AddChild(_scroll, forceReadableName: false, InternalMode.Disabled);
		ClassicSideScrollBar classicSideScrollBar = ClassicSideScrollBar.ForRange(_scroll, 252f, 1.0, hideWhenUnused: false);
		classicSideScrollBar.Position = new Vector2(217f, 37f);
		AddChild(classicSideScrollBar, forceReadableName: false, InternalMode.Disabled);
		Button button = TransparentButton(new Vector2(219f, 8f), new Vector2(18f, 18f), "關閉");
		button.Pressed += onClose;
		AddChild(button, forceReadableName: false, InternalMode.Disabled);
		AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/shop/1991c.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Position = new Vector2(19f, 23f),
			Size = new Vector2(35f, 11f),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		GoldLabel = MakeLabel("", new Vector2(58f, 18f), new Vector2(110f, 18f), 11, Color.FromHtml("#e6c76a".AsSpan()));
		AddChild(GoldLabel, forceReadableName: false, InternalMode.Disabled);
		BuyButton = ClassicTradeButtons.Buy();
		BuyButton.Position = new Vector2(60f, 292f);
		SellButton = ClassicTradeButtons.Sell();
		SellButton.Position = new Vector2(127f, 292f);
		AddChild(BuyButton, forceReadableName: false, InternalMode.Disabled);
		AddChild(SellButton, forceReadableName: false, InternalMode.Disabled);
		Status = MakeLabel(shopName, new Vector2(2f, FrameArtSize.Y + 3f), new Vector2(FrameArtSize.X - 4f, 18f), 11, Color.FromHtml("#c9d1de".AsSpan()));
		Status.HorizontalAlignment = HorizontalAlignment.Center;
		AddChild(Status, forceReadableName: false, InternalMode.Disabled);
	}

	public void SetBuying(bool buying)
	{
		BuyButton.Disabled = buying;
		SellButton.Disabled = !buying;
		BuyButton.SelfModulate = (buying ? Colors.White : new Color(0.66f, 0.66f, 0.66f));
		SellButton.SelfModulate = (buying ? new Color(0.66f, 0.66f, 0.66f) : Colors.White);
	}

	public void SetGold(long gold)
	{
		GoldLabel.Text = gold.ToString("N0");
	}

	public void SetEntries(IReadOnlyList<ClassicShopEntry> entries, bool resetScroll = false)
	{
		_entries = entries ?? throw new ArgumentNullException("entries");
		_maximumOffset = Math.Max(0, entries.Count - 7);
		_scroll.MaxValue = _maximumOffset + 1;
		_scroll.Page = 1.0;
		_scroll.MouseFilter = (MouseFilterEnum)((_maximumOffset > 0) ? 0 : 2);
		if (resetScroll)
		{
			_scroll.Value = 0.0;
		}
		else if (_scroll.Value > (double)_maximumOffset)
		{
			_scroll.Value = _maximumOffset;
		}
		RenderRows();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: not false, ButtonIndex: var buttonIndex })
		{
			int num = buttonIndex switch
			{
				MouseButton.WheelUp => -1, 
				MouseButton.WheelDown => 1, 
				_ => 0, 
			};
			if (num != 0 && _maximumOffset != 0)
			{
				_scroll.Value = Math.Clamp(_scroll.Value + (double)num, 0.0, _maximumOffset);
				AcceptEvent();
			}
		}
	}

	private void OnScrollChanged(double value)
	{
		RenderRows();
	}

	private void RenderRows()
	{
		foreach (Node rowNode in _rowNodes)
		{
			rowNode.QueueFree();
		}
		_rowNodes.Clear();
		int num = Math.Clamp((int)Math.Round(_scroll.Value), 0, _maximumOffset);
		for (int i = 0; i < 7; i++)
		{
			int num2 = num + i;
			if (num2 < _entries.Count)
			{
				ClassicShopEntry classicShopEntry = _entries[num2];
				int num3 = RowTop(i);
				Button button = TransparentButton(new Vector2(15f, num3), new Vector2(197f, 33f), classicShopEntry.Tooltip);
				button.Disabled = !classicShopEntry.Enabled;
				button.MouseFilter = MouseFilterEnum.Pass;
				button.Pressed += classicShopEntry.Activate;
				AddRowNode(button);
				Texture2D texture2D = ItemIcons.For(classicShopEntry.ItemKey);
				if (texture2D != null)
				{
					if (!classicShopEntry.ItemKey.StartsWith("scroll_"))
					{
						TextureRect? glow = ItemBlessingGlow.Create(texture2D, classicShopEntry.Blessing, TextureRect.StretchModeEnum.KeepAspectCentered);
						if (glow != null)
						{
							glow.Position = new Vector2(21f, IconWellTop(i) + 3);
							glow.Size = IconContentSize;
							glow.MouseFilter = MouseFilterEnum.Ignore;
							AddRowNode(glow);
						}
					}
					AddRowNode(new TextureRect
					{
						Texture = texture2D,
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						Position = new Vector2(21f, IconWellTop(i) + 3),
						Size = IconContentSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						TextureFilter = TextureFilterEnum.Nearest,
						MouseFilter = MouseFilterEnum.Ignore
					});
				}
				Color color = (!classicShopEntry.Enabled)
					? Color.FromHtml("#796f63".AsSpan())
					: (classicShopEntry.Blessing switch
					{
						ItemBlessing.Blessed => Color.FromHtml("#ffd700".AsSpan()),
						ItemBlessing.Cursed => Color.FromHtml("#ff5555".AsSpan()),
						_ => Color.FromHtml("#e7dfc8".AsSpan())
					});
				AddRowNode(MakeLabel(classicShopEntry.Name, new Vector2(56f, num3 + 1), new Vector2(151f, 17f), 11, color));
				AddRowNode(MakeLabel(classicShopEntry.Detail, new Vector2(56f, num3 + 17), new Vector2(151f, 14f), 9, classicShopEntry.Enabled ? Color.FromHtml("#bda86a".AsSpan()) : Color.FromHtml("#6d6253".AsSpan())));
				continue;
			}
			break;
		}
	}

	private void AddRowNode(Control node)
	{
		_rowNodes.Add(node);
		AddChild(node, forceReadableName: false, InternalMode.Disabled);
	}

	private static Button TransparentButton(Vector2 position, Vector2 size, string tooltip)
	{
		Button button = new Button();
		button.Flat = true;
		button.Text = "";
		button.Position = position;
		button.Size = size;
		button.TooltipText = tooltip;
		button.FocusMode = FocusModeEnum.None;
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
		{
			BgColor = new Color(0.92f, 0.73f, 0.28f, 0.1f)
		});
		button.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
		{
			BgColor = new Color(0.92f, 0.73f, 0.28f, 0.18f)
		});
		return button;
	}

	private static Label MakeLabel(string text, Vector2 position, Vector2 size, int fontSize, Color color)
	{
		Label label = new Label();
		label.Text = text;
		label.Position = position;
		label.Size = size;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("outline_size", 2);
		return label;
	}

	private static void MakeInvisible(ScrollBar bar)
	{
		string[] array = new string[5] { "scroll", "scroll_focus", "grabber", "grabber_highlight", "grabber_pressed" };
		foreach (string text in array)
		{
			bar.AddThemeStyleboxOverride(text, new StyleBoxEmpty());
		}
	}
}
