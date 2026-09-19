using System;
using System.Linq;
using Godot;
using IdleLineage.App;

namespace IdleLineage.Ui;

public sealed partial class MenuScreen : Control
{
	private const float DesignWidth = 640f;

	private const float DesignHeight = 480f;

	private const string TitleText = "天堂單機版";

	private const float PanelLeft = 30f;

	private const float PanelWidth = 182f;

	private const string AssetRoot = "res://assets/ui/web-login";

	private static readonly Color Gold = Color.FromHtml("#f1d47a".AsSpan());

	private static readonly Color Text = Color.FromHtml("#eee2c7".AsSpan());

	private static readonly Color Dim = Color.FromHtml("#aa9d7c".AsSpan());

	private static readonly Color Good = Color.FromHtml("#86efac".AsSpan());

	private static readonly Color Bad = Color.FromHtml("#fca5a5".AsSpan());

	private const float InfoLeftX = 185f;

	private const float InfoLeftWidth = 89f;

	private const float InfoRightX = 409f;

	private const float InfoRightWidth = 92f;

	private const float InfoBoxHeight = 13f;

	private const float InfoTextPadding = 3f;

	private const int InfoFontSize = 7;

	private static readonly float[] InfoLeftTops = new float[7] { 310f, 330f, 355f, 375f, 401f, 421f, 441f };

	private static readonly float[] InfoRightTops = new float[7] { 314f, 341f, 361f, 381f, 401f, 421f, 441f };

	private Action<int> _onNew;

	private Action<int> _onLoad;

	private Func<int, bool> _onDelete;

	private Func<int, bool> _onExport;

	private Func<int, (bool ok, string msg)> _onImport;

	private Control _stage;

	private Control _titleLayer;

	private Control _loadLayer;

	private TextureRect _loginAnimation;

	private readonly Texture2D[] _loginFrames = new Texture2D[28];

	private double _animationElapsed;

	private int _animationFrame;

	private int _page;

	private int _selectedSlot = 1;

	private string _status = "";

	private bool _statusGood = true;

	public void Init(AtlasBridge atlas, Action<int> onNew, Action<int> onLoad, Func<int, bool> onDelete, Func<int, bool> onExport, Func<int, (bool ok, string msg)> onImport)
	{
		_onNew = onNew;
		_onLoad = onLoad;
		_onDelete = onDelete;
		_onExport = onExport;
		_onImport = onImport;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		ColorRect colorRect = new ColorRect
		{
			Color = Colors.Black,
			MouseFilter = MouseFilterEnum.Ignore
		};
		colorRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		AddChild(colorRect, forceReadableName: false, InternalMode.Disabled);
		_stage = new Control
		{
			Size = new Vector2(640f, 480f)
		};
		AddChild(_stage, forceReadableName: false, InternalMode.Disabled);
		base.Resized += LayoutStage;
		LayoutStage();
		BuildTitle();
		BuildLoad();
		if (string.Equals(OS.GetEnvironment("IDLE_LINEAGE_PREVIEW_SCREEN"), "load", StringComparison.OrdinalIgnoreCase))
		{
			ShowLoad();
		}
		else
		{
			ShowTitle();
		}
		SetProcess(enable: true);
	}

	public override void _Process(double delta)
	{
		if (_titleLayer.Visible)
		{
			_animationElapsed += delta;
			if (!(_animationElapsed < 0.09))
			{
				_animationElapsed -= 0.09;
				_animationFrame = (_animationFrame + 1) % _loginFrames.Length;
				_loginAnimation.Texture = _loginFrames[_animationFrame];
			}
		}
	}

	private void LayoutStage()
	{
		float num = Mathf.Min(base.Size.X / 640f, base.Size.Y / 480f);
		if (num <= 0f)
		{
			num = 1f;
		}
		_stage.Scale = new Vector2(num, num);
		_stage.Position = new Vector2(Mathf.Floor((base.Size.X - 640f * num) * 0.5f), Mathf.Floor((base.Size.Y - 480f * num) * 0.5f));
	}

	private void BuildTitle()
	{
		_titleLayer = new Control
		{
			Size = new Vector2(640f, 480f)
		};
		_stage.AddChild(_titleLayer, forceReadableName: false, InternalMode.Disabled);
		_titleLayer.AddChild(Image("res://assets/ui/web-login/310.png", Vector2.Zero, _titleLayer.Size), forceReadableName: false, InternalMode.Disabled);
		for (int i = 0; i < _loginFrames.Length; i++)
		{
			_loginFrames[i] = GD.Load<Texture2D>($"{"res://assets/ui/web-login"}/{273 + i}.png");
		}
		_loginAnimation = Image("", new Vector2(0f, 306f), new Vector2(501f, 174f));
		_loginAnimation.Texture = _loginFrames[0];
		_loginAnimation.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_loginAnimation.StretchMode = TextureRect.StretchModeEnum.Scale;
		_titleLayer.AddChild(_loginAnimation, forceReadableName: false, InternalMode.Disabled);
		Label label = LabelAt("天堂單機版", new Vector2(30f, 58f), new Vector2(182f, 34f), 24, Gold);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeColorOverride("font_shadow_color", Colors.Black);
		label.AddThemeConstantOverride("shadow_offset_x", 2);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		_titleLayer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label label2 = LabelAt("本遊戲為非官方免費同人作品，絕無營利意圖；遊戲內圖片與音樂版權歸原權利方所有。", new Vector2(34f, 230f), new Vector2(174f, 52f), 8, Text);
		label2.HorizontalAlignment = HorizontalAlignment.Center;
		label2.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
		_titleLayer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		Texture2D texture2D = ProjectileArt.LoadPng("res://assets/ui/web-login/6996.png") ?? throw new InvalidOperationException("首頁 Login 按鈕素材 6996.png 載入失敗");
		TextureButton textureButton = new TextureButton
		{
			Name = "LoginButton",
			Position = new Vector2(313f, 228f),
			Size = new Vector2(180f, 34f),
			TextureNormal = texture2D,
			TextureHover = texture2D,
			TexturePressed = texture2D,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			FocusMode = FocusModeEnum.None,
			TooltipText = "開始遊戲"
		};
		textureButton.Pressed += ShowLoad;
		_titleLayer.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);

		Button mpBtn = new Button
		{
			Name = "MultiplayerButton",
			Text = "🌐 多人連線 (開房 / 加入)",
			Position = new Vector2(313f, 268f),
			Size = new Vector2(180f, 30f),
			FocusMode = FocusModeEnum.None
		};
		mpBtn.Pressed += () =>
		{
			var lobby = MultiplayerLobbyWindow.Create(() => ShowLoad());
			lobby.Position = new Vector2((_stage.Size.X - lobby.Size.X) * 0.5f, (_stage.Size.Y - lobby.Size.Y) * 0.5f);
			_stage.AddChild(lobby);
		};
		_titleLayer.AddChild(mpBtn, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildLoad()
	{
		_loadLayer = new Control
		{
			Size = new Vector2(640f, 480f),
			Visible = false
		};
		_stage.AddChild(_loadLayer, forceReadableName: false, InternalMode.Disabled);
	}

	private void ShowTitle()
	{
		_loadLayer.Visible = false;
		_titleLayer.Visible = true;
		_animationElapsed = 0.0;
	}

	private void ShowLoad()
	{
		_titleLayer.Visible = false;
		_loadLayer.Visible = true;
		_status = "";
		_page = 0;
		SaveManager.SlotInfo slotInfo = SaveManager.ReadAllSlots().Take(4).FirstOrDefault((SaveManager.SlotInfo slot) => !slot.Empty);
		_selectedSlot = ((slotInfo.Slot <= 0) ? 1 : slotInfo.Slot);
		RebuildLoad();
	}

	private void RebuildLoad()
	{
		foreach (Node child in _loadLayer.GetChildren())
		{
			child.QueueFree();
		}
		_loadLayer.AddChild(Image("res://assets/ui/web-login/load.png", Vector2.Zero, _loadLayer.Size), forceReadableName: false, InternalMode.Disabled);
		SaveManager.SlotInfo[] array = SaveManager.ReadAllSlots();
		int num = _page * 4;
		if (_selectedSlot < num + 1 || _selectedSlot > num + 4)
		{
			_selectedSlot = num + 1;
		}
		for (int i = 0; i < 4; i++)
		{
			AddSlotCard(array[num + i], i);
		}
		TextureRect textureRect = Image("res://assets/ui/web-login/load1.png", Vector2.Zero, _loadLayer.Size);
		textureRect.ZIndex = 10;
		_loadLayer.AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
		float[] pageCenterX = new float[4] { 276.5f, 307.5f, 338.5f, 369.5f };
		Label pageHintLabel = LabelAt($"第 {_page + 1} 頁", new Vector2(250f, 305f), new Vector2(146f, 15f), 8, Gold);
		pageHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_loadLayer.AddChild(pageHintLabel, forceReadableName: false, InternalMode.Disabled);

		for (int p = 0; p < 4; p++)
		{
			int pageIndex = p;
			string pageName = (p + 1).ToString();
			TextureButton btn = PageButton(pageName, new Vector2(pageCenterX[p], 288.5f), _page == p);
			btn.Pressed += delegate
			{
				SelectPage(pageIndex);
			};
			btn.MouseEntered += delegate
			{
				pageHintLabel.Text = $"第 {pageName} 頁";
			};
			btn.MouseExited += delegate
			{
				pageHintLabel.Text = $"第 {_page + 1} 頁";
			};
			_loadLayer.AddChild(btn, forceReadableName: false, InternalMode.Disabled);
		}
		SaveManager.SlotInfo info = array[_selectedSlot - 1];
		AddSelectedInfo(info);
		AddActions(info);
	}

	private void AddSlotCard(SaveManager.SlotInfo info, int column)
	{
		Button card = new Button
		{
			Position = new Vector2(11 + column * 156, 25f),
			Size = new Vector2(149f, 231f),
			ClipContents = false,
			FocusMode = FocusModeEnum.None,
			TooltipText = (info.Empty ? $"角色槽 {info.Slot}：空白" : $"{info.DisplayName} Lv{info.Level}")
		};
		ApplyTransparentButton(card, info.Slot == _selectedSlot);
		int slot = info.Slot;
		bool empty = info.Empty;
		card.Pressed += delegate
		{
			_selectedSlot = slot;
			RebuildLoad();
		};
		card.GuiInput += delegate(InputEvent @event)
		{
			if (@event is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && inputEventMouseButton.Pressed && inputEventMouseButton.DoubleClick)
			{
				card.AcceptEvent();
				if (_selectedSlot != slot)
				{
					_selectedSlot = slot;
					RebuildLoad();
				}
				if (empty)
				{
					_onNew(slot);
				}
				else
				{
					_onLoad(slot);
				}
			}
		};
		_loadLayer.AddChild(card, forceReadableName: false, InternalMode.Disabled);
		ClassDef classDef = (info.Empty ? null : ClassCatalog.Find(string.IsNullOrWhiteSpace(info.ClassId) ? info.ClassName : info.ClassId));
		string avatar = (info.Empty ? "" : ((!string.IsNullOrWhiteSpace(info.Avatar)) ? info.Avatar : (classDef?.Avatar(info.Male) ?? "")));
		string key = (info.Empty ? "none" : WebStartSequence.KeyFor(classDef, avatar, info.Male));
		WebStartSequence webStartSequence = new WebStartSequence
		{
			Position = new Vector2(74.5f, 231f),
			ZIndex = 2,
			Modulate = (info.Empty ? new Color(0.86f, 0.86f, 0.86f, 0.72f) : ((info.Slot == _selectedSlot) ? Colors.White : new Color(0.48f, 0.48f, 0.48f, 0.78f)))
		};
		webStartSequence.Init(key, new Vector2(149f, 231f), info.Empty || info.Slot == _selectedSlot, 0.092, WebStartSequence.IsWarrior(key) ? 1.2f : 1.04f);
		card.AddChild(webStartSequence, forceReadableName: false, InternalMode.Disabled);
		if (!info.Empty && info.Slot == _selectedSlot)
		{
			SelectedFlameEffect flame = new SelectedFlameEffect
			{
				Position = new Vector2(74.5f, 228f)
			};
			card.AddChild(flame, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void AddSelectedInfo(SaveManager.SlotInfo info)
	{
		if (!info.Empty)
		{
			string value = ((info.Alignment >= 1000.0) ? "正義" : ((info.Alignment <= -1000.0) ? "邪惡" : "中立"));
			AddInfoValue(info.DisplayName, left: true, 0);
			AddInfoValue("無", left: true, 1);
			AddInfoValue(info.ClassName, left: true, 2);
			AddInfoValue($"{value} {(int)info.Alignment}", left: true, 3);
			AddInfoValue($"{(int)info.Hp} / {(int)info.MaxHp}", left: true, 4);
			AddInfoValue($"{(int)info.Mp} / {(int)info.MaxMp}", left: true, 5);
			AddInfoValue(((int)info.ArmorClass).ToString(), left: true, 6);
			AddInfoValue(info.Level.ToString(), left: false, 0);
			AddInfoValue(((int)info.Str).ToString(), left: false, 1);
			AddInfoValue(((int)info.Dex).ToString(), left: false, 2);
			AddInfoValue(((int)info.Con).ToString(), left: false, 3);
			AddInfoValue(((int)info.Wis).ToString(), left: false, 4);
			AddInfoValue(((int)info.Cha).ToString(), left: false, 5);
			AddInfoValue(((int)info.Int).ToString(), left: false, 6);
		}
	}

	private void AddInfoValue(string text, bool left, int row)
	{
		float num = (left ? 185f : 409f);
		float num2 = (left ? 89f : 92f);
		float y = (left ? InfoLeftTops : InfoRightTops)[row];
		Label label = LabelAt(text, new Vector2(num + 3f, y), new Vector2(num2 - 6f, 13f), 7, Text);
		label.ClipText = true;
		_loadLayer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
	}

	private void AddActions(SaveManager.SlotInfo info)
	{
		float num = 308f;
		TextureButton textureButton = SmallAction(info.Empty ? "創新角色" : "進入遊戲", new Vector2(518f, num));
		textureButton.Pressed += (info.Empty ? ((Action)delegate
		{
			_onNew(info.Slot);
		}) : ((Action)delegate
		{
			_onLoad(info.Slot);
		}));
		_loadLayer.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);
		num += 20f;
		if (!info.Empty)
		{
			TextureButton textureButton2 = SmallAction("匯出進度", new Vector2(518f, num));
			textureButton2.Pressed += delegate
			{
				bool flag = _onExport(info.Slot);
				SetStatus(flag ? "存檔已複製到剪貼簿" : "匯出失敗", flag);
			};
			_loadLayer.AddChild(textureButton2, forceReadableName: false, InternalMode.Disabled);
			num += 20f;
		}
		else
		{
			TextureButton textureButton3 = SmallAction("匯入進度", new Vector2(518f, num));
			textureButton3.Pressed += delegate
			{
				var (good, text) = _onImport(info.Slot);
				SetStatus(text, good);
				RebuildLoad();
			};
			_loadLayer.AddChild(textureButton3, forceReadableName: false, InternalMode.Disabled);
			num += 20f;
		}
		if (!info.Empty)
		{
			TextureButton delete = SmallAction("刪除角色", new Vector2(518f, num), Bad);
			bool armed = false;
			delete.Pressed += delegate
			{
				if (!armed)
				{
					armed = true;
					SetButtonText(delete, "再次確認");
					SetStatus("再次按下會永久刪除此角色", good: false);
				}
				else
				{
					bool flag = _onDelete(info.Slot);
					SetStatus(flag ? "角色已刪除" : "刪除失敗", flag);
					RebuildLoad();
				}
			};
			_loadLayer.AddChild(delete, forceReadableName: false, InternalMode.Disabled);
			num += 20f;
		}
		TextureButton textureButton4 = SmallAction("返回", new Vector2(518f, num));
		textureButton4.Pressed += ShowTitle;
		_loadLayer.AddChild(textureButton4, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(_status))
		{
			Label label = LabelAt(_status, new Vector2(35f, 458f), new Vector2(455f, 20f), 9, _statusGood ? Good : Bad);
			label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
			_loadLayer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void SetStatus(string text, bool good)
	{
		_status = text;
		_statusGood = good;
	}

	private void SelectPage(int page)
	{
		_page = Math.Clamp(page, 0, 3);
		SaveManager.SlotInfo[] source = SaveManager.ReadAllSlots();
		int num = _page * 4;
		SaveManager.SlotInfo slotInfo = source.Skip(num).Take(4).FirstOrDefault((SaveManager.SlotInfo slot) => !slot.Empty);
		_selectedSlot = ((slotInfo.Slot > 0) ? slotInfo.Slot : (num + 1));
		RebuildLoad();
	}

	private static string TownName(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return "";
		}
		return GameDataProvider.Shared.Towns[key]?["n"]?.GetValue<string>() ?? key;
	}

	private static TextureRect Image(string path, Vector2 position, Vector2 size)
	{
		TextureRect textureRect = new TextureRect
		{
			Position = position,
			Size = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
		if (!string.IsNullOrWhiteSpace(path))
		{
			textureRect.Texture = GodotDataFiles.TryLoadTexture(path) ?? ProjectileArt.LoadPng(path) ?? GD.Load<Texture2D>(path);
		}
		return textureRect;
	}

	private static Label LabelAt(string text, Vector2 pos, Vector2 size, int fontSize, Color color)
	{
		Label label = new Label();
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.Text = text;
		label.Position = pos;
		label.Size = size;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.TooltipText = text;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.SetDeferred(Control.PropertyName.Size, size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.68f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		return label;
	}

	private static TextureButton GoldButton(string text, Vector2 pos, Vector2 size, int fontSize, Color? color = null)
	{
		TextureButton obj = new TextureButton
		{
			Position = pos,
			Size = size,
			FocusMode = FocusModeEnum.None,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TooltipText = text
		};
		Panel panel = new Panel
		{
			Name = "Background",
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		StyleBoxFlat normal = Box("#160f07", "#9f7b36", 1);
		StyleBoxFlat hover = Box("#2c1e0b", "#dfb85e", 1);
		panel.AddThemeStyleboxOverride("panel", normal);
		obj.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			Name = "Caption",
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color ?? Gold);
		label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.68f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		obj.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		obj.MouseEntered += delegate
		{
			panel.AddThemeStyleboxOverride("panel", hover);
		};
		obj.MouseExited += delegate
		{
			panel.AddThemeStyleboxOverride("panel", normal);
		};
		return obj;
	}

	private static TextureButton SmallAction(string text, Vector2 pos, Color? color = null)
	{
		return GoldButton(text, pos, new Vector2(48f, 16f), 9, color);
	}

	private static void SetButtonText(TextureButton button, string text)
	{
		button.GetNode<Label>("Caption").Text = text;
		button.TooltipText = text;
	}

	private static TextureButton PageButton(string text, Vector2 centre, bool selected)
	{
		int pageNum = int.TryParse(text, out int pn) ? pn : 1;
		int normId = 1769 + (pageNum - 1) * 2;
		int actId = 1770 + (pageNum - 1) * 2;
		string path = $"res://assets/ui/web-login/{normId}.png";
		string path2 = $"res://assets/ui/web-login/{actId}.png";
		Texture2D? texture2D = GodotDataFiles.TryLoadTexture(path) ?? ProjectileArt.LoadPng(path) ?? GD.Load<Texture2D>(path);
		Texture2D? texture2D2 = GodotDataFiles.TryLoadTexture(path2) ?? ProjectileArt.LoadPng(path2) ?? GD.Load<Texture2D>(path2);
		return new TextureButton
		{
			TextureNormal = (selected ? (texture2D2 ?? texture2D) : (texture2D ?? texture2D2)),
			TextureHover = (texture2D2 ?? texture2D),
			TexturePressed = (texture2D2 ?? texture2D),
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			Position = centre - Vector2.One * 16f,
			Size = Vector2.One * 32f,
			FocusMode = FocusModeEnum.None,
			TooltipText = "第 " + text + " 頁"
		};
	}

	private static void ApplyTransparentButton(Button button, bool selected)
	{
		button.AddThemeStyleboxOverride("normal", Box("#00000000", "#00000000", 2));
		button.AddThemeStyleboxOverride("hover", Box("#8c6b1825", "#d7b55a88", 2));
		button.AddThemeStyleboxOverride("pressed", Box("#8c6b1835", "#f4d477bb", 2));
	}

	private static StyleBoxFlat Box(string bg, string border, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = Color.FromHtml(bg.AsSpan()),
			BorderColor = Color.FromHtml(border.AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 5f,
			ContentMarginRight = 5f,
			ContentMarginTop = 3f,
			ContentMarginBottom = 3f
		};
	}
}

public sealed partial class SelectedFlameEffect : Node2D
{
	private Sprite2D? _rear;
	private Sprite2D? _front;
	private SpotlightBeam? _spotlight;
	private double _time;

	public override void _Ready()
	{
		base._Ready();
		CanvasItemMaterial addMaterial = new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Add
		};

		// 1. 開燈的特效：由上方垂直照射角色的柔和聚光燈光束
		_spotlight = new SpotlightBeam();
		AddChild(_spotlight, forceReadableName: false, InternalMode.Disabled);

		// 2. 底座火焰後層
		Texture2D? rearTex = ProjectileArt.LoadPng("res://assets/ui/web-login/select_flame_rear.png") ?? GD.Load<Texture2D>("res://assets/ui/web-login/select_flame_rear.png");
		if (rearTex != null)
		{
			_rear = new Sprite2D
			{
				Texture = rearTex,
				Centered = true,
				Position = new Vector2(0f, -46f),
				ZIndex = 1,
				Material = addMaterial
			};
			AddChild(_rear, forceReadableName: false, InternalMode.Disabled);
		}

		// 3. 底座火焰前層
		Texture2D? frontTex = ProjectileArt.LoadPng("res://assets/ui/web-login/select_flame_front.png") ?? GD.Load<Texture2D>("res://assets/ui/web-login/select_flame_front.png");
		if (frontTex != null)
		{
			_front = new Sprite2D
			{
				Texture = frontTex,
				Centered = true,
				Position = new Vector2(0f, -46f),
				ZIndex = 3,
				Material = addMaterial
			};
			AddChild(_front, forceReadableName: false, InternalMode.Disabled);
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		_time += delta * 4.2;

		// 持續的小到大的火光循環（呼吸起伏與有機微顫動）
		float breath = 0.5f + 0.5f * (float)Math.Sin(_time);
		float scaleX = Mathf.Lerp(0.82f, 1.18f, breath) + 0.035f * (float)Math.Sin(_time * 3.3);
		float scaleY = Mathf.Lerp(0.78f, 1.22f, breath) + 0.045f * (float)Math.Cos(_time * 4.1);
		Scale = new Vector2(scaleX, scaleY);

		float alpha = Mathf.Lerp(0.72f, 1.0f, breath) + 0.05f * (float)Math.Sin(_time * 6.5);
		Modulate = new Color(1.15f, 1.08f, 0.95f, alpha);
	}
}

public sealed partial class SpotlightBeam : Node2D
{
	private double _time;
	private float _turnOnProgress;

	public override void _Ready()
	{
		base._Ready();
		ZIndex = 1;
		Material = new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Add
		};
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		_time += delta * 3.0;
		if (_turnOnProgress < 1f)
		{
			_turnOnProgress = Mathf.Min(1f, _turnOnProgress + (float)delta * 3.5f);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float shimmer = 0.92f + 0.08f * (float)Math.Sin(_time);
		float alpha = _turnOnProgress * shimmer;

		// 外層柔和光柱
		Vector2[] points = new Vector2[]
		{
			new Vector2(-20f, -225f),
			new Vector2(20f, -225f),
			new Vector2(36f, 6f),
			new Vector2(-36f, 6f)
		};
		Color cTop = new Color(0.82f, 0.90f, 1.0f, 0.14f * alpha);
		Color cBot = new Color(1.0f, 0.94f, 0.82f, 0.28f * alpha);
		DrawPolygon(points, new Color[] { cTop, cTop, cBot, cBot });

		// 內層高亮核心光束
		Vector2[] innerPoints = new Vector2[]
		{
			new Vector2(-10f, -225f),
			new Vector2(10f, -225f),
			new Vector2(18f, 6f),
			new Vector2(-18f, 6f)
		};
		Color iTop = new Color(0.95f, 0.98f, 1.0f, 0.10f * alpha);
		Color iBot = new Color(1.0f, 1.0f, 0.90f, 0.22f * alpha);
		DrawPolygon(innerPoints, new Color[] { iTop, iTop, iBot, iBot });
	}
}

