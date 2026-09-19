using System;
using System.Collections.Generic;
using Godot;
using IdleLineage.App;

namespace IdleLineage.Ui;

public sealed partial class CreateScreen : Control
{
	private const float DesignWidth = 640f;

	private const float DesignHeight = 480f;

	private const string AssetRoot = "res://assets/ui/web-login";

	private const string MaleNormalAsset = "res://assets/ui/web-login/gender_male_normal.png";

	private const string MaleSelectedAsset = "res://assets/ui/web-login/gender_male_selected.png";

	private const string FemaleNormalAsset = "res://assets/ui/web-login/gender_female_normal.png";

	private const string FemaleSelectedAsset = "res://assets/ui/web-login/gender_female_selected.png";

	private static readonly string[] ClassNormalAssets = new string[8] { "res://assets/ui/web-login/class-icons/royal_normal.png", "res://assets/ui/web-login/class-icons/knight_normal.png", "res://assets/ui/web-login/class-icons/elf_normal.png", "res://assets/ui/web-login/class-icons/mage_normal.png", "res://assets/ui/web-login/class-icons/dark_normal.png", "res://assets/ui/web-login/class-icons/illusion_normal.png", "res://assets/ui/web-login/class-icons/dragon_normal.png", "res://assets/ui/web-login/class-icons/warrior_normal.png" };

	private static readonly string[] ClassSelectedAssets = new string[8] { "res://assets/ui/web-login/class-icons/royal_selected.png", "res://assets/ui/web-login/class-icons/knight_selected.png", "res://assets/ui/web-login/class-icons/elf_selected.png", "res://assets/ui/web-login/class-icons/mage_selected.png", "res://assets/ui/web-login/class-icons/dark_selected.png", "res://assets/ui/web-login/class-icons/illusion_selected.png", "res://assets/ui/web-login/class-icons/dragon_selected.png", "res://assets/ui/web-login/class-icons/warrior_selected.png" };

	private static readonly string[] StatKeys = new string[6] { "str", "dex", "con", "wis", "cha", "int" };

	private static readonly Color Gold = Color.FromHtml("#f1d47a".AsSpan());

	private static readonly Color Text = Color.FromHtml("#efe1bd".AsSpan());

	private const float StatRowTop = 316f;

	private const float StatRowPitch = 15f;

	private const float StatValueWidth = 20f;

	private const float StatValueHeight = 13f;

	private const float StatLeftValueCentre = 410f;

	private const float StatRightValueCentre = 534f;

	private Action<PlayerBuild> _onCreate;

	private Control _stage;

	private Label _classTitle;

	private Label _classDescription;

	private Label _points;

	private LineEdit _nameInput;

	private TextureButton _start;

	private TextureButton _maleButton;

	private TextureButton _femaleButton;

	private readonly Button[] _classButtons = new Button[ClassCatalog.All.Length];

	private readonly TextureRect[] _classIcons = new TextureRect[ClassCatalog.All.Length];

	private readonly Dictionary<string, Label> _statValues = new Dictionary<string, Label>(StringComparer.Ordinal);

	private readonly Dictionary<string, int> _allocations = new Dictionary<string, int>(StringComparer.Ordinal);

	private WebStartSequence? _preview;

	private int _classIndex;

	private bool _male = true;

	private const float NameSlotX = 439f;

	private const float NameSlotTop = 380f;

	private const float NameSlotWidth = 103f;

	private const float NameSlotHeight = 13f;

	private const int NameSlotTextPadding = 2;

	public void Init(AtlasBridge atlas, Action<PlayerBuild> onCreate, Action onBack)
	{
		_onCreate = onCreate;
		string[] statKeys = StatKeys;
		foreach (string key in statKeys)
		{
			_allocations[key] = 0;
		}
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
		_stage.AddChild(Image("res://assets/ui/web-login/824c.png", Vector2.Zero, _stage.Size), forceReadableName: false, InternalMode.Disabled);
		BuildDescription();
		BuildClassButtons();
		BuildGenderButtons();
		BuildStats();
		BuildActions(onBack);
		Refresh();
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

	private void BuildDescription()
	{
		_classTitle = LabelAt("", new Vector2(50f, 66f), new Vector2(200f, 24f), 12, Gold);
		_classTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_stage.AddChild(_classTitle, forceReadableName: false, InternalMode.Disabled);
		_classDescription = LabelAt("", new Vector2(56f, 101f), new Vector2(190f, 210f), 8, Text);
		_classDescription.VerticalAlignment = VerticalAlignment.Top;
		_classDescription.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
		_classDescription.AddThemeConstantOverride("line_spacing", 1);
		_stage.AddChild(_classDescription, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildClassButtons()
	{
		for (int i = 0; i < ClassCatalog.All.Length; i++)
		{
			int index = i;
			ClassDef classDef = ClassCatalog.All[i];
			bool flag = i < 4;
			int num = (flag ? i : (i - 4));
			Button button = new Button
			{
				Position = new Vector2(flag ? 345.5f : 576.5f, 45f + (float)(num * 58)),
				Size = new Vector2(34f, 34f),
				FocusMode = FocusModeEnum.None,
				TooltipText = classDef.Name
			};
			StyleBoxEmpty stylebox = new StyleBoxEmpty();
			button.AddThemeStyleboxOverride("normal", stylebox);
			button.AddThemeStyleboxOverride("hover", stylebox);
			button.AddThemeStyleboxOverride("pressed", stylebox);
			button.AddThemeStyleboxOverride("focus", stylebox);
			button.Pressed += delegate
			{
				_classIndex = index;
				ResetAllocations();
				Refresh();
			};
			TextureRect textureRect = new TextureRect
			{
				Position = Vector2.Zero,
				Size = button.Size,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = MouseFilterEnum.Ignore
			};
			button.AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
			_stage.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			_classButtons[i] = button;
			_classIcons[i] = textureRect;
		}
	}

	private void BuildGenderButtons()
	{
		_maleButton = GenderButton("男性", new Vector2(427.5f, 267.5f));
		_maleButton.Pressed += delegate
		{
			_male = true;
			Refresh();
		};
		_stage.AddChild(_maleButton, forceReadableName: false, InternalMode.Disabled);
		_femaleButton = GenderButton("女性", new Vector2(501.5f, 267.5f));
		_femaleButton.Pressed += delegate
		{
			_male = false;
			Refresh();
		};
		_stage.AddChild(_femaleButton, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildStats()
	{
		_points = LabelAt("", new Vector2(463f, 327f), new Vector2(21f, 24f), 9, Text);
		_points.HorizontalAlignment = HorizontalAlignment.Center;
		_stage.AddChild(_points, forceReadableName: false, InternalMode.Disabled);
		for (int i = 0; i < StatKeys.Length; i++)
		{
			bool num = i < 3;
			int num2 = (num ? i : (i - 3));
			float x = (num ? 410f : 534f) - 10f;
			float num3 = (num ? 424 : 498);
			float y = 316f + (float)num2 * 15f;
			string key = StatKeys[i];
			Label label = LabelAt("", new Vector2(x, y), new Vector2(20f, 13f), 8, Text);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			_stage.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			_statValues[key] = label;
			Button button = HitButton("減少", new Vector2(num3, y), new Vector2(11f, 12f));
			button.Pressed += delegate
			{
				Adjust(key, -1);
			};
			_stage.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			Button button2 = HitButton("增加", new Vector2(num3 + 10f, y), new Vector2(11f, 12f));
			button2.Pressed += delegate
			{
				Adjust(key, 1);
			};
			_stage.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void CenterNameInputOnSlot()
	{
		_nameInput.Position = new Vector2(439f, 380f + (13f - _nameInput.Size.Y) * 0.5f);
	}

	private void BuildActions(Action onBack)
	{
		_nameInput = new LineEdit
		{
			Position = new Vector2(439f, 380f),
			Size = new Vector2(103f, 13f),
			MaxLength = 12,
			CaretBlink = true,
			ClearButtonEnabled = false,
			ContextMenuEnabled = false,
			SelectAllOnFocus = true,
			TooltipText = "輸入角色名稱"
		};
		_nameInput.AddThemeFontSizeOverride("font_size", 8);
		_nameInput.AddThemeColorOverride("font_color", Text);
		_nameInput.AddThemeColorOverride("caret_color", Gold);
		StyleBoxEmpty stylebox = new StyleBoxEmpty
		{
			ContentMarginLeft = 2f,
			ContentMarginRight = 2f
		};
		_nameInput.AddThemeStyleboxOverride("normal", stylebox);
		_nameInput.AddThemeStyleboxOverride("focus", stylebox);
		_nameInput.SetDeferred(Control.PropertyName.Size, new Vector2(103f, 13f));
		CallDeferred("CenterNameInputOnSlot");
		_nameInput.TextChanged += delegate
		{
			RefreshCreateAvailability();
		};
		_nameInput.TextSubmitted += delegate
		{
			if (!_start.Disabled)
			{
				Create();
			}
		};
		_stage.AddChild(_nameInput, forceReadableName: false, InternalMode.Disabled);
		_start = GoldButton("創立角色", new Vector2(438f, 400f), new Vector2(105f, 20f));
		_start.Pressed += Create;
		_stage.AddChild(_start, forceReadableName: false, InternalMode.Disabled);
		TextureButton textureButton = GoldButton("返回", new Vector2(548f, 400f), new Vector2(70f, 20f));
		textureButton.Pressed += onBack;
		_stage.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);
	}

	private void Adjust(string key, int delta)
	{
		ClassDef classDef = ClassCatalog.All[_classIndex];
		int num = _allocations[key];
		if (delta > 0)
		{
			if (RemainingPoints(classDef) <= 0 || classDef.BaseStat(key) + num >= 18)
			{
				return;
			}
		}
		else if (num <= 0)
		{
			return;
		}
		_allocations[key] = num + delta;
		RefreshStats(classDef);
	}

	public bool RunPreviewCreation(string classId, bool male, string characterName)
	{
		int num = Array.FindIndex(ClassCatalog.All, (ClassDef def) => string.Equals(def.Id, classId, StringComparison.OrdinalIgnoreCase));
		if (num < 0)
		{
			return false;
		}
		_classIndex = num;
		_male = male;
		ResetAllocations();
		Refresh();
		_nameInput.Text = characterName;
		Create();
		return true;
	}

	private void Create()
	{
		ClassDef def = ClassCatalog.All[_classIndex];
		string text = _nameInput.Text.Trim();
		if (text.Length == 0)
		{
			_nameInput.GrabFocus();
			return;
		}
		Dictionary<string, int> allocations = new Dictionary<string, int>(_allocations, StringComparer.Ordinal);
		_onCreate(ClassCatalog.ToBuild(def, _male, allocations, 1, text));
	}

	private void Refresh()
	{
		ClassDef classDef = ClassCatalog.All[_classIndex];
		GameAudio instance = GameAudio.Instance;
		instance?.PlayScene(instance.CreateScene(classDef.Id));
		_classTitle.Text = classDef.Name;
		_classDescription.Text = classDef.Description;
		_classTitle.TooltipText = classDef.Name;
		_classDescription.TooltipText = classDef.Description;
		for (int i = 0; i < _classButtons.Length; i++)
		{
			ApplyClassButton(_classIcons[i], i, i == _classIndex);
		}
		_maleButton.TextureNormal = GD.Load<Texture2D>(_male ? "res://assets/ui/web-login/gender_male_selected.png" : "res://assets/ui/web-login/gender_male_normal.png");
		_femaleButton.TextureNormal = GD.Load<Texture2D>(_male ? "res://assets/ui/web-login/gender_female_normal.png" : "res://assets/ui/web-login/gender_female_selected.png");
		_preview?.QueueFree();
		_preview = new WebStartSequence
		{
			Position = new Vector2(477f, 263f),
			ZIndex = 3
		};
		_preview.Init(WebStartSequence.KeyFor(classDef, _male), new Vector2(146f, 190f), animate: true, 0.082);
		_stage.AddChild(_preview, forceReadableName: false, InternalMode.Disabled);
		_stage.MoveChild(_preview, 2);
		RefreshStats(classDef);
	}

	private void RefreshStats(ClassDef def)
	{
		string[] statKeys = StatKeys;
		foreach (string text in statKeys)
		{
			_statValues[text].Text = (def.BaseStat(text) + _allocations[text]).ToString();
		}
		int num = RemainingPoints(def);
		_points.Text = num.ToString();
		_points.AddThemeColorOverride("font_color", (num == 0) ? Gold : Text);
		RefreshCreateAvailability();
	}

	private void RefreshCreateAvailability()
	{
		bool flag = !string.IsNullOrWhiteSpace(_nameInput.Text);
		_start.Disabled = !flag;
		_start.Modulate = (flag ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.75f));
	}

	private int RemainingPoints(ClassDef def)
	{
		int num = 0;
		foreach (int value in _allocations.Values)
		{
			num += value;
		}
		return def.BonusPoints - num;
	}

	private void ResetAllocations()
	{
		string[] statKeys = StatKeys;
		foreach (string key in statKeys)
		{
			_allocations[key] = 0;
		}
	}

	private static TextureRect Image(string path, Vector2 position, Vector2 size)
	{
		return new TextureRect
		{
			Texture = GD.Load<Texture2D>(path),
			Position = position,
			Size = size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore
		};
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

	private static TextureButton GoldButton(string text, Vector2 pos, Vector2 size)
	{
		return FixedButton(text, pos, size, 9, Gold, Box("#171006", "#9f7b36", 2), Box("#34230c", "#e0b655", 2));
	}

	private static TextureButton GenderButton(string tooltip, Vector2 pos)
	{
		return new TextureButton
		{
			Position = pos,
			Size = new Vector2(34f, 34f),
			FocusMode = FocusModeEnum.None,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			TooltipText = tooltip
		};
	}

	private static TextureButton FixedButton(string text, Vector2 pos, Vector2 size, int fontSize, Color color, StyleBoxFlat normal, StyleBoxFlat hover)
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
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		panel.AddThemeStyleboxOverride("panel", normal);
		obj.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		Label label = new Label
		{
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
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

	private static Button HitButton(string tooltip, Vector2 pos, Vector2 size)
	{
		Button button = new Button();
		button.Position = pos;
		button.Size = size;
		button.Flat = true;
		button.FocusMode = FocusModeEnum.None;
		button.TooltipText = tooltip;
		StyleBoxEmpty stylebox = new StyleBoxEmpty();
		button.AddThemeStyleboxOverride("normal", stylebox);
		button.AddThemeStyleboxOverride("hover", stylebox);
		button.AddThemeStyleboxOverride("pressed", stylebox);
		button.AddThemeStyleboxOverride("focus", stylebox);
		return button;
	}

	private static void ApplyClassButton(TextureRect icon, int index, bool selected)
	{
		string path = (selected ? ClassSelectedAssets[index] : ClassNormalAssets[index]);
		icon.Texture = GD.Load<Texture2D>(path);
	}

	private static StyleBoxFlat Box(string background, string border, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = Color.FromHtml(background.AsSpan()),
			BorderColor = Color.FromHtml(border.AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = 3f,
			ContentMarginRight = 3f,
			ContentMarginTop = 2f,
			ContentMarginBottom = 2f
		};
	}
}
