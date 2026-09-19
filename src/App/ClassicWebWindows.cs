using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Core;

namespace IdleLineage.App;

internal static class ClassicWebWindows
{
	private readonly record struct EquipmentSlot(string Key, string? Alternate, float X, float Y, float Width, float Height);

	public readonly record struct BagShell(Control Panel, BagGrid Grid, Label Status, Label Page, Button Previous, Button Next, ClassicInventoryTabs Tabs);

	private static readonly Vector2 AbilitySize = new Vector2(430f, 492f);

	private const float AbilityDetailTop = 142f;

	private const float AbilityDetailRowHeight = 23f;

	private const string SkillBaseTexture = "res://assets/ui/skills/2056.png";

	private const string SkillFrameTexture = "res://assets/ui/skills/2057.png";

	private const float SkillArtWidth = 193f;

	private const float SkillArtHeight = 282f;

	private const float SkillWidth = 309f;

	private const float SkillScale = 1.6010363f;

	private const float SkillHeight = 451.49225f;

	private const float SkillModeStrip = 56f;

	private const float SkillFooterValueTop = 255f;

	private const float SkillFooterValueHeight = 12f;

	private const float SkillSidebarSlotPitch = 19f;

	private const float SkillSidebarSlotHeight = 19f;

	private const float BagArtWidth = 193f;

	private const float BagArtHeight = 282f;

	private const float BagHeight = 451.49225f;

	private const string EquipmentTextureRoot = "res://assets/ui/windows/equipment";

	private const float CharacterArtWidth = 183f;

	private const float CharacterArtHeight = 408f;

	private const float CharacterHeight = 462f;

	private const float CharacterScale = 1.132353f;

	private const float CharacterWidth = 207.2206f;

	private static readonly IReadOnlyDictionary<string, string> EquipmentTemplateClass = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["royal"] = "王族",
		["knight"] = "騎士",
		["elf"] = "妖精",
		["mage"] = "法師",
		["dark"] = "黑妖",
		["illusion"] = "幻術",
		["dragon"] = "龍騎",
		["warrior"] = "戰士"
	};

	private static readonly Color Text = Color.FromHtml("#ded9cc".AsSpan());

	private static readonly Color Gold = Color.FromHtml("#ead69b".AsSpan());

	private static readonly Color Good = Color.FromHtml("#8fdd8f".AsSpan());

	private static readonly Color Bad = Color.FromHtml("#e2938f".AsSpan());

	private static readonly EquipmentSlot[] CharacterSlots = new EquipmentSlot[18]
	{
		Well("helm", null, 50f, 15.81f),
		Well("ear1", null, 19.4f, 18.01f),
		Well("ear2", null, 80.1f, 18.01f),
		Well("amulet", null, 50f, 33.46f),
		Well("gloves", null, 19.4f, 31.5f),
		Well("cloak", null, 80.1f, 31.5f),
		Well("tshirt", null, 50f, 42.77f),
		Well("wpn", null, 19.4f, 44.98f),
		Well("shield", "offwpn", 80.1f, 44.98f),
		Well("armor", null, 50f, 52.08f),
		Well("ring1", null, 19.4f, 58.46f),
		Well("ring2", null, 80.1f, 58.46f),
		Well("belt", null, 50f, 63.36f),
		Well("ring3", null, 19.4f, 67.77f),
		Well("ring4", null, 80.1f, 67.77f),
		Well("lantern", null, 50f, 72.67f),
		Well("boots", null, 50f, 81.99f),
		Well("arrow", null, 80.1f, 80.76f)
	};

	public const int BagCellsPerPage = 24;

	public static Control CreateAbility(GameSession session, Vector2 position, Action close, Func<string, (bool Ok, string Text)>? allocate = null)
	{
		Combatant player = session.Player;
		var (control, control2) = ClassicMapFrame.Create(position, AbilitySize, close, 2100);
		control.AddChild(ClassicMapFrame.Title("角色能力"), forceReadableName: false, Node.InternalMode.Disabled);
		Control control3 = new Control
		{
			Position = new Vector2(0f, 30f),
			Size = new Vector2(control2.Size.X, Mathf.Max(0f, control2.Size.Y - 30f))
		};
		control2.AddChild(control3, forceReadableName: false, Node.InternalMode.Disabled);
		string[] array = new string[6] { "str", "dex", "con", "int", "wis", "cha" };
		string[] array2 = new string[6] { "力量", "敏捷", "體質", "智力", "精神", "魅力" };
		Label[] statLabels = new Label[6];
		Label status = AddText(control3, "", new Rect2(8f, 4f, control3.Size.X - 16f, 20f), Text, 11, HorizontalAlignment.Center, VerticalAlignment.Center);
		AddText(control3, "剩餘點數", new Rect2(control3.Size.X - 118f, 28f, 76f, 20f), Text, 11, HorizontalAlignment.Right, VerticalAlignment.Center);
		Label remaining = AddText(control3, "", new Rect2(control3.Size.X - 38f, 28f, 30f, 20f), Gold, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
		float num = (control3.Size.X - 24f) * 0.5f;
		for (int i = 0; i < 6; i++)
		{
			bool flag = i >= 3;
			float num2 = 8f + (flag ? (num + 8f) : 0f);
			float y = 52 + i % 3 * 27;
			AddText(control3, array2[i], new Rect2(num2, y, 58f, 22f), Text, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
			statLabels[i] = AddText(control3, "", new Rect2(num2 + 62f, y, num - 96f, 22f), Gold, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
			if (allocate != null)
			{
				string key = array[i];
				Button button = new Button
				{
					Text = "+",
					Flat = true,
					Position = new Vector2(num2 + num - 30f, y),
					Size = new Vector2(24f, 22f),
					FocusMode = Control.FocusModeEnum.None,
					TooltipText = "分配 1 點能力值"
				};
				button.AddThemeColorOverride("font_color", Gold);
				button.AddThemeFontSizeOverride("font_size", 13);
				control3.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
				button.Pressed += delegate
				{
					var (flag2, text) = allocate(key);
					status.Text = text;
					status.AddThemeColorOverride("font_color", flag2 ? Good : Bad);
					RefreshStatLabels();
				};
			}
		}
		RefreshStatLabels();
		DerivedStats d = player.D;
		string item = ((player.Level >= 99) ? "MAX" : $"{ProgressionRules.ExperienceProgressRatio(GameDataProvider.Shared, player.Level, player.Experience) * 100.0:0.00}%");
		(string, string)[] array3 = new(string, string)[15]
		{
			("等級", $"{player.Level}"),
			("經驗", item),
			("體力", $"{(int)player.Hp}/{(int)player.MaxHp}"),
			("魔力", $"{(int)player.Mp}/{(int)player.MaxMp}"),
			("防禦", F(d.ArmorClass)),
			("魔法防禦", F(d.MagicResist)),
			("魔法傷害", F(d.MagicDamage)),
			("ER遠程迴避", F(d.EvasionRating)),
			("負重", $"{WeightRules.WeightPercent(player)}%"),
			("飽食度", $"{SatietyRules.Percent(player):0}%"),
			("魔法等級", $"{MagicLevelOf(player)}"),
			("傷害減免", F(d.DamageReduction)),
			("性向", $"{(int)player.Alignment}"),
			("近戰命中", F(d.MeleeHit)),
			("近戰傷害", F(d.MeleeDamage))
		};
		double[] array4 = new double[4] { d.ResistEarth, d.ResistWater, d.ResistFire, d.ResistWind };
		string[] array5 = new string[4] { "地", "水", "火", "風" };
		(string, string)[] array6 = new(string, string)[15];
		for (int num3 = 0; num3 < 4; num3++)
		{
			array6[num3] = (array5[num3] + "屬性抗", F(array4[num3]));
			array6[num3 + 4] = (array5[num3] + "實際減傷", $"{CombatCurveMath.EffectiveResistancePercent(array4[num3]):0}%");
		}
		array6[8] = ("遠程命中", F(d.RangedHit));
		array6[9] = ("遠程傷害", F(d.RangedDamage));
		array6[10] = ("追加命中", F(d.ExtraHit));
		array6[11] = ("追加傷害", F(d.ExtraDamage));
		array6[12] = ("攻擊間隔", $"{d.AttackInterval:0.00}s");
		array6[13] = ("魔力回復", F(d.ManaRegen));
		array6[14] = ("DR近戰迴避", F(d.MeleeEvasion));
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Position = new Vector2(8f, 142f),
			Size = new Vector2(control3.Size.X - 16f, control3.Size.Y - 142f - 6f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		control3.AddChild(scrollContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		Control control4 = new Control
		{
			CustomMinimumSize = new Vector2(scrollContainer.Size.X - 8f, (float)array3.Length * 23f)
		};
		scrollContainer.AddChild(control4, forceReadableName: false, Node.InternalMode.Disabled);
		float num4 = (control4.CustomMinimumSize.X - 10f) * 0.5f;
		for (int num5 = 0; num5 < array3.Length; num5++)
		{
			AbilityRow(control4, num5, array3[num5].Item1, array3[num5].Item2, 0f, num4);
			AbilityRow(control4, num5, array6[num5].Item1, array6[num5].Item2, num4 + 10f, num4);
		}
		return control;
		void RefreshStatLabels()
		{
			DerivedStats d2 = player.D;
			double[] array7 = new double[6] { d2.Str, d2.Dex, d2.Con, d2.Int, d2.Wis, d2.Cha };
			for (int j = 0; j < statLabels.Length; j++)
			{
				statLabels[j].Text = F(array7[j]);
			}
			remaining.Text = L1jLevelStatRules.RemainingPoints(player).ToString();
		}
	}

	private static void AbilityRow(Control root, int row, string label, string value, float x, float width)
	{
		float y = (float)row * 23f;
		float num = 66f;
		AddText(root, label, new Rect2(x + 4f, y, width - num - 8f, 21f), Text, 11, HorizontalAlignment.Left, VerticalAlignment.Center);
		AddText(root, value, new Rect2(x + width - num, y, num, 21f), Gold, 11, HorizontalAlignment.Right, VerticalAlignment.Center);
	}

	private static int MagicLevelOf(Combatant player)
	{
		return ClassGrowthRules.MagicLevel(player.ClassId, player.Level);
	}

	private static EquipmentSlot Well(string key, string? alternate, float centerX, float centerY)
	{
		return new EquipmentSlot(key, alternate, centerX / 100f * 183f - 18f, centerY / 100f * 408f - 18f, 36f, 36f);
	}

	public static Control CreateCharacter(GameSession session, Vector2 position, Action close, Func<string, (bool Ok, string Text)> unequip, Action refresh, string statusText, Action? openAbility = null, Action? openTitle = null)
	{
		Combatant player = session.Player;
		Control control = Shell(EquipmentTexture(session, player), position, new Vector2(207.2206f, 462f));
		AddCharacterCloseHit(control, close);
		IReadOnlyDictionary<string, ItemStack> equippedItems = player.EquippedItems;
		for (int i = 0; i < CharacterSlots.Length; i++)
		{
			EquipmentSlot equipmentSlot = CharacterSlots[i];
			string actual = ((equipmentSlot.Alternate != null && equippedItems.ContainsKey(equipmentSlot.Alternate)) ? equipmentSlot.Alternate : equipmentSlot.Key);
			if (equippedItems.TryGetValue(actual, out var value))
			{
				Rect2 rect = new Rect2(CharacterNative(equipmentSlot.X), CharacterNative(equipmentSlot.Y), CharacterNative(equipmentSlot.Width), CharacterNative(equipmentSlot.Height));
				bool flag = actual == "lantern";
				ClassicSlotButton classicSlotButton = new ClassicSlotButton
				{
					Position = rect.Position,
					Size = rect.Size,
					Flat = true,
					ItemKey = value.ItemKey,
					TooltipText = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, value) + ((value.IsIdentified && value.AttrEnchantLevel > 0) ? ("\n屬性：" + AttributeScrollText.Describe(value)) : "") + ((value.IsIdentified && value.BrokenBladeStacks > 0) ? $"\n壞刀 ×{value.BrokenBladeStacks}（物理最終傷害 -{value.BrokenBladeStacks}）" : "") + ItemInstanceText.DetailTooltip(GameDataProvider.Shared, value) + ((value.IsIdentified && flag) ? $"\n油量 {LanternRules.OilOf(value):0.#}%（每分鐘 -1%）" : "") + "\n雙擊卸下",
					OnActivate = delegate
					{
						(bool, string) result = unequip(actual);
						refresh();
						return ((bool Ok, string Text))result;
					}
				};
				Texture2D texture2D = ItemIcons.For(value.ItemKey);
				if (texture2D != null)
				{
					classicSlotButton.SetIcon(texture2D);
					classicSlotButton.SetBlessingGlow(value.IsIdentified ? value.Blessing : ItemBlessing.Normal);
					classicSlotButton.SetBrokenBlade(value.IsIdentified && value.BrokenBladeStacks > 0);
				}
				else
				{
					classicSlotButton.Text = "E";
					classicSlotButton.AddThemeColorOverride("font_color", Color.FromHtml("#d9c17d".AsSpan()));
					classicSlotButton.AddThemeFontSizeOverride("font_size", 14);
				}
				control.AddChild(classicSlotButton, forceReadableName: false, Node.InternalMode.Disabled);
				if (ItemQualityColors.Framed(value))
				{
					Panel panel = new Panel
					{
						Position = rect.Position,
						Size = rect.Size,
						MouseFilter = Control.MouseFilterEnum.Ignore
					};
					StyleBoxFlat stylebox = new StyleBoxFlat
					{
						BgColor = Colors.Transparent,
						BorderColor = ItemQualityColors.FrameOf(value),
						BorderWidthLeft = 3,
						BorderWidthTop = 3,
						BorderWidthRight = 3,
						BorderWidthBottom = 3
					};
					panel.AddThemeStyleboxOverride("panel", stylebox);
					control.AddChild(panel, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (value.IsIdentified && value.Enhancement != 0)
				{
					AddText(control, ItemInstanceText.StackCorner(value), new Rect2(rect.End.X - 25f, rect.End.Y - 17f, 24f, 16f), ItemQualityColors.Of(value), 11, HorizontalAlignment.Right, VerticalAlignment.Center);
				}
				else if (value.IsIdentified && value.Blessing == ItemBlessing.Blessed)
				{
					AddText(control, "祝", new Rect2(rect.End.X - 25f, rect.End.Y - 17f, 24f, 16f), Color.FromHtml("#f2c14e".AsSpan()), 11, HorizontalAlignment.Right, VerticalAlignment.Center);
				}
				else if (value.IsIdentified && value.Blessing == ItemBlessing.Cursed)
				{
					AddText(control, "詛", new Rect2(rect.End.X - 25f, rect.End.Y - 17f, 24f, 16f), Color.FromHtml("#e2938f".AsSpan()), 11, HorizontalAlignment.Right, VerticalAlignment.Center);
				}
				if (value.IsIdentified && flag)
				{
					AddText(control, $"{LanternRules.OilOf(value):0}%", new Rect2(rect.Position.X, rect.End.Y - 15f, rect.Size.X, 14f), (LanternRules.OilOf(value) > 20.0) ? Color.FromHtml("#ffd479".AsSpan()) : Color.FromHtml("#e2938f".AsSpan()), 10, HorizontalAlignment.Center, VerticalAlignment.Center);
				}
			}
		}
		AddCharacterFooter(control, openAbility, openTitle);
		if (!string.IsNullOrWhiteSpace(statusText))
		{
			AddText(control, statusText, CharacterRect(10f, 27f, 163f, 10f), statusText.StartsWith("✓", StringComparison.Ordinal) ? Good : Bad, 11, HorizontalAlignment.Center, VerticalAlignment.Center);
		}
		return control;
	}

	private static string EquipmentTexture(GameSession session, Combatant player)
	{
		string key = ClassKitRegistry.NormalizeClassId(player.ClassId);
		if (!EquipmentTemplateClass.TryGetValue(key, out string value))
		{
			return "res://assets/ui/windows/equipment/原圖.png";
		}
		return $"{"res://assets/ui/windows/equipment"}/{(session.Build.Male ? "男" : "女")}{value}.png";
	}

	private static float CharacterNative(float pixels)
	{
		return pixels * 1.132353f;
	}

	private static Rect2 CharacterRect(float x, float y, float width, float height)
	{
		return new Rect2(CharacterNative(x), CharacterNative(y), CharacterNative(width), CharacterNative(height));
	}

	private static void AddCharacterCloseHit(Control root, Action close)
	{
		Button button = new Button
		{
			Flat = true,
			Position = new Vector2(CharacterNative(156.465f), CharacterNative(5.304f)),
			Size = new Vector2(CharacterNative(22.875f), CharacterNative(23.255999f)),
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = "關閉"
		};
		button.Pressed += close;
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static void AddCharacterFooter(Control root, Action? openAbility, Action? openTitle)
	{
		Rect2 rect = CharacterRect(15f, 377f, 153f, 23f);
		Panel panel = new Panel
		{
			Position = rect.Position,
			Size = rect.Size,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#191517".AsSpan()),
			BorderColor = Color.FromHtml("#584850".AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1
		});
		root.AddChild(panel, forceReadableName: false, Node.InternalMode.Disabled);
		(string, string, Action)[] array = ((openAbility == null) ? ((openTitle == null) ? Array.Empty<(string, string, Action)>() : new(string, string, Action)[1] { ("稱號", "設定 L1J-TW 角色封號", openTitle) }) : ((openTitle == null) ? new(string, string, Action)[1] { ("能力", "角色能力（六維／屬性抗／迴避率）", openAbility) } : new(string, string, Action)[2]
		{
			("能力", "角色能力（六維／屬性抗／迴避率）", openAbility),
			("稱號", "設定 L1J-TW 角色封號", openTitle)
		}));
		(string, string, Action)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string, Action) tuple = array2[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			Action item3 = tuple.Item3;
			float num = rect.Size.X / (float)array2.Length;
			Button button = new Button
			{
				Text = item,
				Flat = true,
				Position = new Vector2(rect.Position.X + num * (float)i, rect.Position.Y),
				Size = new Vector2(num, rect.Size.Y),
				FocusMode = Control.FocusModeEnum.None,
				TooltipText = item2
			};
			button.AddThemeFontSizeOverride("font_size", 10);
			button.AddThemeColorOverride("font_color", Gold);
			button.Pressed += item3;
			root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	public static Control CreateTitleEditor(Combatant player, Vector2 position, Action close, Func<string, (bool Ok, string Text)> apply)
	{
		var (control, control2) = ClassicMapFrame.Create(position, new Vector2(330f, 190f), close, 1950);
		control.AddChild(ClassicMapFrame.Title("角色稱號"), forceReadableName: false, Node.InternalMode.Disabled);
		Label current = new Label
		{
			Text = ((player.Title.Length == 0) ? "目前稱號：無" : ("目前稱號：" + player.Title)),
			Position = new Vector2(8f, 30f),
			Size = new Vector2(control2.Size.X - 16f, 22f)
		};
		current.AddThemeFontSizeOverride("font_size", 13);
		current.AddThemeColorOverride("font_color", Gold);
		control2.AddChild(current, forceReadableName: false, Node.InternalMode.Disabled);
		LineEdit input = new LineEdit
		{
			Text = player.Title,
			PlaceholderText = "輸入角色封號（最多 35 字）",
			MaxLength = 35,
			Position = new Vector2(8f, 58f),
			Size = new Vector2(control2.Size.X - 16f, 32f)
		};
		control2.AddChild(input, forceReadableName: false, Node.InternalMode.Disabled);
		Label status = new Label
		{
			Position = new Vector2(8f, 96f),
			Size = new Vector2(control2.Size.X - 16f, 42f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		status.AddThemeFontSizeOverride("font_size", 12);
		control2.AddChild(status, forceReadableName: false, Node.InternalMode.Disabled);
		Button button = new Button
		{
			Text = "設定稱號",
			Position = new Vector2(control2.Size.X - 118f, control2.Size.Y - 36f),
			Size = new Vector2(110f, 30f)
		};
		button.Pressed += Submit;
		input.TextSubmitted += delegate
		{
			Submit();
		};
		control2.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		return control;
		void Submit()
		{
			var (flag, text) = apply(input.Text);
			status.Text = text;
			status.AddThemeColorOverride("font_color", flag ? Good : Bad);
			if (flag)
			{
				current.Text = "目前稱號：" + player.Title;
			}
		}
	}

	public static Control CreateSkills(Combatant player, Vector2 position, Action close, Action<string>? cast, Action? openLearning, HashSet<string>? autoCast = null, Action<string, bool>? onToggleAutoCast = null)
	{
		bool flag = openLearning != null;
		Control root = Shell("res://assets/ui/skills/2056.png", position, new Vector2(309f + (flag ? 56f : 0f), 451.49225f), new Vector2(SkillNative(181f), SkillNative(271f)), new Vector2(SkillNative(6f), SkillNative(8f)));
		TextureRect node = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/skills/2057.png"),
			Size = new Vector2(309f, 451.49225f),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		AddBadgeCloseHit(root, SkillNative, close);
		if (openLearning != null)
		{
			Button button = new Button
			{
				Text = "學習",
				Flat = true,
				Position = new Vector2(311f, 425.49225f),
				Size = new Vector2(52f, 22f),
				TooltipText = "開啟技能書學習"
			};
			button.AddThemeColorOverride("font_color", Gold);
			button.AddThemeFontSizeOverride("font_size", 11);
			button.Pressed += openLearning;
			root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		}
		List<ClassSkillEntry> skills = (from s in ClassKitRegistry.SkillsFor(player, GameDataProvider.Shared)
			where s.Learned || s.Granted
			where !SkillInfo.IsProcOnly(s.SkillId)
			orderby SkillInfo.Tier(s.SkillId)
			select s).ToList();
		ClassicSkillSidebarSpec sidebarSpec = ClassicSkillSidebarCatalog.For(player.ClassId);
		List<ClassSkillEntry>[] skillGroups = ClassicSkillSidebarCatalog.Group(skills, sidebarSpec);
		int selectedGroup = Array.FindIndex(skillGroups, (List<ClassSkillEntry> list) => list.Count > 0);
		if (selectedGroup < 0)
		{
			selectedGroup = 0;
		}
		List<ClassSkillEntry> known = skillGroups[selectedGroup];
		float gridX = SkillNative(28f);
		float gridY = SkillNative(36f);
		float cell = SkillNative(34f);
		float inner = SkillNative(34f);
		int pageCount = Math.Max(1, (known.Count + 24 - 1) / 24);
		int page = 0;
		TextureRect skillSidebar = new TextureRect
		{
			Position = new Vector2(SkillNative(12f), SkillNative(37f)),
			Size = new Vector2(SkillNative(14f), SkillNative(212f)),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.AddChild(skillSidebar, forceReadableName: false, Node.InternalMode.Disabled);
		Control cellsHost = new Control();
		root.AddChild(cellsHost, forceReadableName: false, Node.InternalMode.Disabled);
		for (int num = 0; num < sidebarSpec.SlotCount; num++)
		{
			int group = num;
			Rect2 rect = SkillSidebarHitRect(num);
			Button button2 = new Button
			{
				Flat = true,
				FocusMode = Control.FocusModeEnum.None,
				Disabled = (skillGroups[num].Count == 0),
				Position = rect.Position,
				Size = rect.Size,
				TooltipText = ((num < sidebarSpec.GeneralSlots) ? $"第 {num + 1} 階魔法" : $"職業技能 {num - sidebarSpec.GeneralSlots + 1}")
			};
			button2.Pressed += delegate
			{
				SelectGroup(group);
			};
			root.AddChild(button2, forceReadableName: false, Node.InternalMode.Disabled);
		}
		skillSidebar.Texture = GD.Load<Texture2D>($"res://assets/ui/skills/{sidebarSpec.FirstSurface + selectedGroup}.png");
		root.GuiInput += delegate(InputEvent @event)
		{
			if (@event is InputEventMouseButton { Pressed: not false, ButtonIndex: var buttonIndex })
			{
				int num2 = buttonIndex switch
				{
					MouseButton.WheelDown => 1, 
					MouseButton.WheelUp => -1, 
					_ => 0, 
				};
				if (num2 != 0)
				{
					int num3 = Math.Clamp(page + num2, 0, pageCount - 1);
					if (num3 != page)
					{
						page = num3;
						Rebuild();
						root.AcceptEvent();
					}
				}
			}
		};
		ClassicSideScrollBar node2 = new ClassicSideScrollBar(() => page, () => Math.Max(0, pageCount - 1), delegate(double value)
		{
			page = Math.Clamp((int)Math.Round(value), 0, pageCount - 1);
			Rebuild();
		}, 1.0, SkillNative(195f), 1.6010363f, hideWhenUnused: false)
		{
			Position = new Vector2(SkillNative(171f), SkillNative(44f))
		};
		root.AddChild(node2, forceReadableName: false, Node.InternalMode.Disabled);
		Rebuild();
		AddText(root, F(player.D.IntelligenceSpellPower), SkillFooterValueRect(64f), Text, 11, HorizontalAlignment.Center, VerticalAlignment.Center);
		AddText(root, F(player.D.MagicResist), SkillFooterValueRect(142f), Text, 11, HorizontalAlignment.Center, VerticalAlignment.Center);
		return root;
		void Rebuild()
		{
			foreach (Node child in cellsHost.GetChildren())
			{
				cellsHost.RemoveChild(child);
				child.QueueFree();
			}
			int num2 = page * 24;
			for (int i = 0; i < 24 && num2 + i < known.Count; i++)
			{
				ClassSkillEntry classSkillEntry = known[num2 + i];
				int num3 = i % 4;
				int num4 = i / 4;
				string id = classSkillEntry.SkillId;
				string text = SkillInfo.Name(id);
				int num5 = SkillInfo.Tier(id);
				bool castable = cast != null && SkillInfo.IsCastable(id);
				JsonObject jsonObject = GameDataProvider.Shared.Skill(id);
				int manaCost = ((jsonObject != null) ? RelicConditionalCombatRules.SkillManaCost(GameDataProvider.Shared, player, id, CombatModifierRules.SkillMpCost(player, jsonObject, id)) : SkillInfo.Mp(id));
				bool flag2 = ArpgEngineScreen.CanAssignSkillToQuickBar(id);
				SkillDragButton skillDragButton = new SkillDragButton
				{
					SkillId = id,
					Draggable = flag2,
					Position = new Vector2(gridX + (float)num3 * cell, gridY + (float)num4 * cell),
					Size = new Vector2(inner, inner),
					Flat = true,
					Disabled = (cast != null && !castable)
				};
				string[] obj = new string[5]
				{
					$"{text}\n{SkillInfo.TypeLabel(id)} · {SkillInfo.ResourceLabel(id, manaCost)}",
					(num5 > 0) ? $" · 第 {num5} 階" : "",
					null,
					null,
					null
				};
				string text2 = SkillInfo.UsageDescription(id);
				obj[2] = ((text2 != null && text2.Length > 0) ? ("\n" + text2) : "");
				obj[3] = (castable ? "\n點擊施放" : "");
				obj[4] = (flag2 ? "\n拖曳到右下快捷欄可指派 F5~F12" : "");
				skillDragButton.TooltipText = string.Concat(obj);
				skillDragButton.ClipText = true;
				skillDragButton.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
				SkillDragButton skillDragButton2 = skillDragButton;
				Texture2D texture2D = SkillIcons.For(id);
				if (texture2D != null)
				{
					skillDragButton2.SetSkillIcon(texture2D);
				}
				else
				{
					skillDragButton2.Text = CompactSkillName(text);
					skillDragButton2.AddThemeFontSizeOverride("font_size", 10);
				}
				skillDragButton2.Pressed += delegate
				{
					GameAudio.Instance?.PlayUi("skillAction", 40.0);
					if (castable)
					{
						cast(id);
					}
				};
				bool canAuto = onToggleAutoCast != null && !SkillInfo.IsProcOnly(id) && !SkillExecutionRules.IsManualOnly(GameDataProvider.Shared, id) && id != "sk_teleport";
				if (canAuto)
				{
					Button badge = new Button
					{
						ToggleMode = true,
						ButtonPressed = autoCast != null && autoCast.Contains(id),
						Text = "自",
						TooltipText = "自動施放：" + text + "\n（點一下開啟·再點一下取消）",
						MouseFilter = Control.MouseFilterEnum.Stop
					};
					badge.AddThemeFontSizeOverride("font_size", 9);
					StyleBoxFlat flat = new StyleBoxFlat
					{
						BgColor = new Color(0f, 0f, 0f, 0.7f),
						BorderColor = Color.FromHtml(badge.ButtonPressed ? "#e8c07a" : "#6b7686")
					};
					flat.SetBorderWidthAll(1);
					flat.SetContentMarginAll(0f);
					string[] styles = new string[4] { "normal", "hover", "pressed", "focus" };
					foreach (string s in styles) badge.AddThemeStyleboxOverride(s, flat);
					badge.AddThemeColorOverride("font_color", Color.FromHtml(badge.ButtonPressed ? "#e8c07a" : "#8b95a6"));
					badge.Toggled += delegate(bool on)
					{
						onToggleAutoCast?.Invoke(id, on);
						flat.BorderColor = Color.FromHtml(on ? "#e8c07a" : "#6b7686");
						badge.AddThemeColorOverride("font_color", Color.FromHtml(on ? "#e8c07a" : "#8b95a6"));
					};
					badge.CustomMinimumSize = new Vector2(13f, 13f);
					skillDragButton2.AddChild(badge, forceReadableName: false, Node.InternalMode.Disabled);
					badge.SetAnchorsPreset(Control.LayoutPreset.TopRight);
					badge.OffsetLeft = -14f;
					badge.OffsetTop = 1f;
					badge.OffsetRight = -1f;
					badge.OffsetBottom = 14f;
				}
				cellsHost.AddChild(skillDragButton2, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		void SelectGroup(int num2)
		{
			if (num2 >= 0 && num2 < skillGroups.Length && skillGroups[num2].Count != 0)
			{
				selectedGroup = num2;
				known = skillGroups[num2];
				pageCount = Math.Max(1, (known.Count + 24 - 1) / 24);
				page = 0;
				skillSidebar.Texture = GD.Load<Texture2D>($"res://assets/ui/skills/{sidebarSpec.FirstSurface + selectedGroup}.png");
				Rebuild();
			}
		}
	}

	private static void AddBadgeCloseHit(Control root, Func<float, float> native, Action close)
	{
		Button button = new Button
		{
			Flat = true,
			Position = new Vector2(native(175f), native(9f)),
			Size = new Vector2(native(14f), native(15f)),
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = "關閉"
		};
		button.Pressed += close;
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
	}

	public static BagShell CreateBagShell(Vector2 position, Action close, string hint)
	{
		Control panel = Shell("res://assets/ui/inventory/2046.png", position, new Vector2(309f, 451.49225f), new Vector2(BagNative(181f), BagNative(271f)), new Vector2(BagNative(6f), BagNative(8f)));
		TextureRect node = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/ui/inventory/2047.png"),
			Size = new Vector2(309f, 451.49225f),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panel.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		AddBadgeCloseHit(panel, BagNative, close);
		BagGrid bagGrid = new BagGrid
		{
			Position = new Vector2(BagNative(28f), BagNative(30f)),
			Size = new Vector2(BagNative(136f), BagNative(204f)),
			Columns = 4,
			Rows = 6
		};
		panel.AddChild(bagGrid, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicInventoryTabs classicInventoryTabs = new ClassicInventoryTabs(1.6010363f)
		{
			Position = new Vector2(BagNative(26f), BagNative(233f))
		};
		panel.AddChild(classicInventoryTabs, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label
		{
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Text = hint,
			Position = new Vector2(BagNative(28f), BagNative(258f)),
			Size = new Vector2(BagNative(84f), BagNative(20f)),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#a9a497".AsSpan()));
		panel.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		Button previous = StripPageButton(panel, "◀", BagNative(114f), "上一頁");
		Button next = StripPageButton(panel, "▶", BagNative(150f), "下一頁");
		Label label2 = new Label
		{
			Position = new Vector2(BagNative(126f), BagNative(258f)),
			Size = new Vector2(BagNative(26f), BagNative(20f)),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label2.AddThemeFontSizeOverride("font_size", 11);
		label2.AddThemeColorOverride("font_color", Color.FromHtml("#d5c49a".AsSpan()));
		panel.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicSideScrollBar classicSideScrollBar = ClassicSideScrollBar.ForPages(previous, next, label2, BagNative(195f), 1.6010363f, hideWhenUnused: false);
		classicSideScrollBar.Position = new Vector2(BagNative(171f), BagNative(41f));
		panel.AddChild(classicSideScrollBar, forceReadableName: false, Node.InternalMode.Disabled);
		panel.GuiInput += delegate(InputEvent inputEvent)
		{
			if (inputEvent is InputEventMouseButton { Pressed: not false, ButtonIndex: var buttonIndex })
			{
				BaseButton baseButton = buttonIndex switch
				{
					MouseButton.WheelUp => previous, 
					MouseButton.WheelDown => next, 
					_ => null, 
				};
				if (baseButton != null && !baseButton.Disabled)
				{
					baseButton.EmitSignal(BaseButton.SignalName.Pressed);
					panel.AcceptEvent();
				}
			}
		};
		return new BagShell(panel, bagGrid, label, label2, previous, next, classicInventoryTabs);
	}

	private static Button StripPageButton(Control root, string glyph, float x, string tooltip)
	{
		Button button = new Button
		{
			Text = glyph,
			Flat = true,
			Visible = false,
			Position = new Vector2(x, BagNative(232f)),
			Size = new Vector2(BagNative(14f), BagNative(22f)),
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = tooltip
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", Color.FromHtml("#d5c49a".AsSpan()));
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		return button;
	}

	private static float SkillNative(float pixels)
	{
		return pixels * 1.6010363f;
	}

	private static Rect2 SkillSidebarHitRect(int index)
	{
		return new Rect2(SkillNative(12f), SkillNative(37f + (float)index * 19f), SkillNative(14f), SkillNative(19f));
	}

	private static Rect2 SkillFooterValueRect(float left)
	{
		return new Rect2(SkillNative(left), SkillNative(255f), SkillNative(24f), SkillNative(12f));
	}

	private static float BagNative(float pixels)
	{
		return pixels * 1.6010363f;
	}

	private static void AddBakedCloseTab(Control root, Action close)
	{
		Button button = new Button
		{
			Flat = true,
			Position = new Vector2(11f, 3f),
			Size = new Vector2(53f, 21f),
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = "關閉"
		};
		button.Pressed += close;
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static Button RailPageButton(Control root, bool top, string tooltip)
	{
		Button button = new Button
		{
			Flat = true,
			Position = new Vector2(BagNative(199f), BagNative(top ? 27 : 287)),
			Size = new Vector2(BagNative(16f), BagNative(17f)),
			FocusMode = Control.FocusModeEnum.None,
			TooltipText = tooltip
		};
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		return button;
	}

	private static Control Shell(string texturePath, Vector2 position, Vector2 size)
	{
		return Shell(texturePath, position, size, size);
	}

	private static Control Shell(string texturePath, Vector2 position, Vector2 rootSize, Vector2 artSize, Vector2? artPosition = null)
	{
		Control control = new Control();
		control.Position = position;
		control.Size = rootSize;
		control.ZIndex = 2100;
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		TextureRect node = new TextureRect
		{
			Texture = GD.Load<Texture2D>(texturePath),
			Position = (artPosition ?? Vector2.Zero),
			Size = artSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		control.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		return control;
	}

	private static void AddClose(Control root, Vector2 position, Action close)
	{
		Button button = new Button
		{
			Text = "×",
			Position = position,
			Size = new Vector2(24f, 24f),
			Flat = true,
			TooltipText = "關閉"
		};
		button.AddThemeColorOverride("font_color", Colors.White);
		button.AddThemeFontSizeOverride("font_size", 16);
		button.Pressed += close;
		root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static Label AddText(Control root, string text, Rect2 rect, Color color, int size, HorizontalAlignment horizontal = HorizontalAlignment.Left, VerticalAlignment vertical = VerticalAlignment.Center, bool wrap = false)
	{
		Label label = new Label
		{
			ClipText = !wrap,
			AutowrapMode = (TextServer.AutowrapMode)(wrap ? 1 : 0),
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Text = text,
			Position = rect.Position,
			Size = rect.Size,
			HorizontalAlignment = horizontal,
			VerticalAlignment = vertical,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			TooltipText = text
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#080808".AsSpan()));
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeFontSizeOverride("font_size", size);
		root.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		return label;
	}

	private static string ItemName(string itemKey)
	{
		return GameDataProvider.Shared.Item(itemKey)?["n"]?.GetValue<string>() ?? itemKey;
	}

	private static string CompactSkillName(string name)
	{
		if (name.Length > 4)
		{
			return name.Substring(0, 4);
		}
		return name;
	}

	private static string F(double value)
	{
		return value.ToString("0.##");
	}
}
