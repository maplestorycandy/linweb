using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal static class CollectionBookUi
{
	private static readonly Color CGold = Color.FromHtml("#e6c76a".AsSpan());

	private static readonly Color CText = Color.FromHtml("#c9d1de".AsSpan());

	private static readonly Color CDim = Color.FromHtml("#8b95a6".AsSpan());

	private static readonly Color CGood = Color.FromHtml("#8fdd8f".AsSpan());

	private static readonly Color CRule = new Color(1f, 1f, 1f, 0.09f);

	private static readonly Color CBookBg = new Color(0.055f, 0.063f, 0.078f, 0.88f);

	private static readonly Color CCardBg = new Color(0.075f, 0.086f, 0.105f, 0.88f);

	private static readonly Color Silhouette = new Color(0.14f, 0.16f, 0.2f, 0.58f);

	public static Control Build(GameSession session, AtlasBridge atlas, (CollectionBookKind Book, string Label)[] books, Vector2 position, Vector2 size, Action close, Func<string, string> itemName, string bonusSummary = "", int zIndex = 0)
	{
		var (control, control2) = ClassicMapFrame.Create(position, size, close, zIndex);
		control.AddChild(ClassicMapFrame.Title("收集冊"), forceReadableName: false, Node.InternalMode.Disabled);
		float bodyWidth = Mathf.Max(0f, control2.Size.X - 16f);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Name = "CollectionBookLayout",
			Position = new Vector2(8f, 30f),
			Size = new Vector2(bodyWidth, Mathf.Max(0f, control2.Size.Y - 30f - 8f))
		};
		vBoxContainer.AddThemeConstantOverride("separation", 5);
		control2.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Name = "CollectionBookTabs"
		};
		hBoxContainer.AddThemeConstantOverride("separation", 6);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		PanelContainer panelContainer = FramedPanel(CBookBg, new Color(0.55f, 0.44f, 0.23f, 0.55f));
		panelContainer.Name = "CollectionBonusSummary";
		panelContainer.CustomMinimumSize = new Vector2(bodyWidth, 40f);
		MarginContainer marginContainer = Margin(8, 6, 8, 6);
		Label summary = Row("", CText, 11);
		summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summary.VerticalAlignment = VerticalAlignment.Center;
		marginContainer.AddChild(summary, forceReadableName: false, Node.InternalMode.Disabled);
		panelContainer.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Name = "CollectionCategoryScroll",
			CustomMinimumSize = new Vector2(bodyWidth, 72f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		HFlowContainer categoryTabs = new HFlowContainer
		{
			Name = "CollectionCategoryTabs",
			CustomMinimumSize = new Vector2(bodyWidth - 10f, 0f)
		};
		categoryTabs.AddThemeConstantOverride("h_separation", 5);
		categoryTabs.AddThemeConstantOverride("v_separation", 5);
		scrollContainer.AddChild(categoryTabs, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		vBoxContainer.AddChild(new ColorRect
		{
			Color = CRule,
			CustomMinimumSize = new Vector2(bodyWidth, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			Name = "CollectionCategoryHeader"
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 2);
		HBoxContainer hBoxContainer2 = new HBoxContainer();
		Label categoryTitle = Row("", CGold, 15);
		categoryTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hBoxContainer2.AddChild(categoryTitle, forceReadableName: false, Node.InternalMode.Disabled);
		Label categoryCount = Row("", CText, 12);
		categoryCount.HorizontalAlignment = HorizontalAlignment.Right;
		hBoxContainer2.AddChild(categoryCount, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer2.AddChild(hBoxContainer2, forceReadableName: false, Node.InternalMode.Disabled);
		Label categoryBonus = Row("", CDim, 11);
		categoryBonus.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		vBoxContainer2.AddChild(categoryBonus, forceReadableName: false, Node.InternalMode.Disabled);
		ProgressBar progress = Progress(CGood);
		vBoxContainer2.AddChild(progress, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, Node.InternalMode.Disabled);
		ScrollContainer scrollContainer2 = new ScrollContainer
		{
			Name = "CollectionEntryScroll",
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			CustomMinimumSize = new Vector2(bodyWidth, 0f)
		};
		VBoxContainer content = new VBoxContainer
		{
			Name = "CollectionEntryHost",
			CustomMinimumSize = new Vector2(bodyWidth - 10f, 0f)
		};
		scrollContainer2.AddChild(content, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer2, forceReadableName: false, Node.InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer2);
		CollectionState state = session.Collections;
		CollectionBookKind book = books[0].Book;
		Dictionary<CollectionBookKind, string> selectedCategory = new Dictionary<CollectionBookKind, string>();
		List<(CollectionBookKind Book, Button Button)> bookButtons = new List<(CollectionBookKind, Button)>();
		for (int i = 0; i < books.Length; i++)
		{
			(CollectionBookKind Book, string Label) tuple2 = books[i];
			CollectionBookKind item = tuple2.Book;
			string item2 = tuple2.Label;
			Button button = new Button
			{
				Text = ((item == CollectionBookKind.Card) ? "怪物圖鑑" : (item2 + "收集冊")),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, 31f),
				FocusMode = Control.FocusModeEnum.None
			};
			button.AddThemeFontSizeOverride("font_size", 13);
			CollectionBookKind picked = item;
			button.Pressed += delegate
			{
				ShowBook(picked);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
			bookButtons.Add((item, button));
		}
		ShowBook(book);
		return control;
		void ShowBook(CollectionBookKind collectionBookKind)
		{
			book = collectionBookKind;
			foreach (var item3 in bookButtons)
			{
				var (collectionBookKind2, _) = item3;
				StyleTab(item3.Button, Accent(collectionBookKind2), collectionBookKind2 == book);
			}
			IReadOnlyList<CollectionCategoryProgress> readOnlyList = VisibleCategories(book);
			int value = readOnlyList.Count((CollectionCategoryProgress collectionCategoryProgress) => collectionCategoryProgress.BonusActive);
			int value2 = readOnlyList.Sum((CollectionCategoryProgress collectionCategoryProgress) => collectionCategoryProgress.Collected);
			int value3 = readOnlyList.Sum((CollectionCategoryProgress collectionCategoryProgress) => collectionCategoryProgress.Total);
			string text = BookName(book);
			string value4 = ((bonusSummary.Length == 0) ? "尚無永久加成" : bonusSummary);
			summary.Text = $"{text}\u3000分類完成 {value}/{readOnlyList.Count}・收集 {value2}/{value3}\n{value4}";
			summary.AddThemeColorOverride("font_color", Accent(book).Lightened(0.35f));
			if (readOnlyList.Count == 0)
			{
				Clear(categoryTabs);
				Clear(content);
				categoryTitle.Text = text;
				categoryCount.Text = "0/0";
				categoryBonus.Text = "目前沒有可收集內容";
				progress.MaxValue = 1.0;
				progress.Value = 0.0;
				content.AddChild(Centered("目前沒有可收集內容", CDim, 13), forceReadableName: false, Node.InternalMode.Disabled);
			}
			else
			{
				CollectionCategoryProgress cat = readOnlyList.FirstOrDefault((CollectionCategoryProgress collectionCategoryProgress) => selectedCategory.TryGetValue(book, out string value5) && collectionCategoryProgress.Key == value5);
				if (cat.Total == 0)
				{
					cat = readOnlyList[0];
				}
				ShowCategory(cat);
			}
		}
		void ShowCategory(CollectionCategoryProgress cat)
		{
			selectedCategory[book] = cat.Key;
			Clear(categoryTabs);
			Color accent = Accent(book);
			foreach (CollectionCategoryProgress item4 in VisibleCategories(book))
			{
				string value = ((item4.Group.Length > 0) ? (item4.Group + "・") : "");
				string value2 = ((item4.Book == CollectionBookKind.Card && item4.Tier > 0) ? $"  T{item4.Tier}" : "");
				Button button2 = new Button
				{
					Text = $"{value}{item4.Name}  {item4.Collected}/{item4.Total}{value2}",
					CustomMinimumSize = new Vector2(112f, 27f),
					FocusMode = Control.FocusModeEnum.None,
					TooltipText = CategoryRewardText(state, item4)
				};
				button2.AddThemeFontSizeOverride("font_size", 11);
				bool active = item4.Key == cat.Key;
				StyleTab(button2, accent, active);
				CollectionCategoryProgress picked2 = item4;
				button2.Pressed += delegate
				{
					ShowCategory(picked2);
				};
				categoryTabs.AddChild(button2, forceReadableName: false, Node.InternalMode.Disabled);
			}
			Clear(content);
			categoryTitle.Text = ((cat.Group.Length > 0) ? (cat.Group + "・" + cat.Name) : cat.Name);
			categoryCount.Text = ((cat.Book == CollectionBookKind.Card) ? $"已登錄 {cat.Collected}/{cat.Total}\u3000區域階級 {cat.Tier}/3" : $"已收集 {cat.Collected}/{cat.Total}");
			categoryBonus.Text = CategoryRewardText(state, cat);
			categoryBonus.TooltipText = categoryBonus.Text;
			progress.MaxValue = Math.Max(1, cat.Total);
			progress.Value = cat.Collected;
			bool flag = book == CollectionBookKind.Card;
			int num = (flag ? 4 : 5);
			GridContainer gridContainer = new GridContainer
			{
				Name = "CollectionBookGrid",
				Columns = num,
				CustomMinimumSize = new Vector2(bodyWidth - 10f, 0f)
			};
			gridContainer.AddThemeConstantOverride("h_separation", 7);
			gridContainer.AddThemeConstantOverride("v_separation", 7);
			content.AddChild(gridContainer, forceReadableName: false, Node.InternalMode.Disabled);
			float num2 = (bodyWidth - 10f - (float)((num - 1) * 7)) / (float)num;
			foreach (string item5 in state.RequiredItems(book, cat.Key))
			{
				int num3 = (flag ? state.Kills(item5) : 0);
				int num4 = (flag ? CollectionState.KillTier(num3) : 0);
				bool flag2 = (flag ? (num4 > 0) : state.Contains(book, item5));
				JsonObject jsonObject = (flag ? state.Data.Mob(item5) : null);
				string text = ((!flag) ? itemName(item5) : (jsonObject?["n"]?.GetValue<string>() ?? item5));
				Texture2D texture2D = (flag ? MobFirstFrame(atlas, item5) : ItemIcons.For(item5));
				PanelContainer panelContainer2 = FramedPanel(flag2 ? CCardBg : new Color(0.045f, 0.05f, 0.06f, 0.82f), flag2 ? new Color(accent.R, accent.G, accent.B, 0.6f) : new Color(0.3f, 0.33f, 0.38f, 0.35f));
				panelContainer2.CustomMinimumSize = new Vector2(num2, flag ? 126 : 88);
				if (flag2)
				{
					panelContainer2.TooltipText = text;
				}
				MarginContainer marginContainer2 = Margin(5, 4, 5, 4);
				VBoxContainer vBoxContainer3 = new VBoxContainer();
				vBoxContainer3.AddThemeConstantOverride("separation", 1);
				marginContainer2.AddChild(vBoxContainer3, forceReadableName: false, Node.InternalMode.Disabled);
				panelContainer2.AddChild(marginContainer2, forceReadableName: false, Node.InternalMode.Disabled);
				if (texture2D != null)
				{
					TextureRect textureRect = new TextureRect
					{
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
						Texture = texture2D,
						CustomMinimumSize = new Vector2(num2 - 10f, flag ? 57 : 50),
						MouseFilter = Control.MouseFilterEnum.Ignore
					};
					if (!flag2)
					{
						textureRect.SelfModulate = Silhouette;
					}
					vBoxContainer3.AddChild(textureRect, forceReadableName: false, Node.InternalMode.Disabled);
				}
				else
				{
					Label label = Row("？", CDim, 22);
					label.HorizontalAlignment = HorizontalAlignment.Center;
					label.CustomMinimumSize = new Vector2(num2 - 10f, flag ? 57 : 50);
					label.VerticalAlignment = VerticalAlignment.Center;
					vBoxContainer3.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
				}
				Label label2 = Row(flag2 ? text : "？？？", flag2 ? CText : CDim, flag ? 11 : 12);
				label2.HorizontalAlignment = HorizontalAlignment.Center;
				label2.CustomMinimumSize = new Vector2(num2 - 10f, 0f);
				label2.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
				label2.ClipText = true;
				if (flag2)
				{
					label2.TooltipText = text;
				}
				vBoxContainer3.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
				if (flag && flag2)
				{
					string value3 = jsonObject?["lv"]?.ToString() ?? "?";
					vBoxContainer3.AddChild(Centered($"Lv {value3}\u3000擊殺 {num3}/100", (num4 >= 3) ? CGold : CText, 10), forceReadableName: false, Node.InternalMode.Disabled);
					if (num4 >= 2)
					{
						string text2 = jsonObject?["hp"]?.ToString() ?? "?";
						string text3 = ElementName(jsonObject?["e"]?.GetValue<string>() ?? "none");
						vBoxContainer3.AddChild(Centered("HP " + text2 + "・屬性 " + text3, CText, 10), forceReadableName: false, Node.InternalMode.Disabled);
					}
					if (num4 >= 3)
					{
						string text4 = jsonObject?["ac"]?.ToString() ?? "?";
						string text5 = jsonObject?["mr"]?.ToString() ?? "?";
						vBoxContainer3.AddChild(Centered("AC " + text4 + "・MR " + text5, CText, 10), forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
				gridContainer.AddChild(panelContainer2, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		IReadOnlyList<CollectionCategoryProgress> VisibleCategories(CollectionBookKind kind)
		{
			return (from cat in state.Categories(kind)
				where cat.Total > 0
				select cat).ToArray();
		}
	}

	private static string BookName(CollectionBookKind book)
	{
		return book switch
		{
			CollectionBookKind.Equipment => "裝備收集冊", 
			CollectionBookKind.Misc => "道具收集冊", 
			CollectionBookKind.Card => "怪物圖鑑", 
			_ => "收集冊", 
		};
	}

	private static Color Accent(CollectionBookKind book)
	{
		return book switch
		{
			CollectionBookKind.Equipment => Color.FromHtml("#68a9dc".AsSpan()), 
			CollectionBookKind.Misc => Color.FromHtml("#d2a84f".AsSpan()), 
			CollectionBookKind.Card => Color.FromHtml("#c47bc8".AsSpan()), 
			_ => CGold, 
		};
	}

	private static string CategoryRewardText(CollectionState state, CollectionCategoryProgress cat)
	{
		if (cat.Book != CollectionBookKind.Card)
		{
			if (cat.BonusStat.Length == 0 && cat.BonusLabel.Length == 0)
			{
				return "此分類沒有永久能力";
			}
			string text = ((cat.BonusLabel.Length > 0) ? cat.BonusLabel : $"{TownScreen.CollStatLabel(cat.BonusStat)} +{cat.BonusValue:0.##}");
			if (!cat.BonusActive)
			{
				return "全收集加成：" + text + "（未完成）";
			}
			return "全收集加成：" + text + "（已啟用）";
		}
		CollectionCategory collectionCategory = state.Catalog.Categories(CollectionBookKind.Card).FirstOrDefault((CollectionCategory value) => value.Key == cat.Key);
		if ((object)collectionCategory == null || collectionCategory.TierValues.Count == 0)
		{
			return "此區域沒有永久能力";
		}
		string stat = TownScreen.CollStatLabel(cat.BonusStat);
		string[] tierNames = new string[3] { "登錄", "半開", "全開" };
		return "區域完成加成（取最高）：" + string.Join("\u3000/\u3000", collectionCategory.TierValues.Select(delegate(double value, int index)
		{
			string value2 = ((cat.Tier >= index + 1) ? "✓" : "○");
			return $"{value2}{tierNames[Math.Min(index, tierNames.Length - 1)]} {stat}+{value:0.##}";
		}));
	}

	private static string ElementName(string element)
	{
		return element.ToLowerInvariant() switch
		{
			"fire" => "火", 
			"water" => "水", 
			"wind" => "風", 
			"earth" => "地", 
			_ => "無", 
		};
	}

	private static Texture2D? MobFirstFrame(AtlasBridge atlas, string mobKey)
	{
		string name = ArpgEngineScreen.ResolveMobAtlas(atlas, mobKey);
		SpriteFrames spriteFrames = atlas.BuildFrames("anim", name);
		if (spriteFrames == null)
		{
			return null;
		}
		string[] array = new string[3] { "idle", "d5/idle", "d0/idle" };
		foreach (string text in array)
		{
			if (spriteFrames.HasAnimation(text) && spriteFrames.GetFrameCount(text) > 0)
			{
				return spriteFrames.GetFrameTexture(text, 0);
			}
		}
		array = spriteFrames.GetAnimationNames();
		foreach (string text2 in array)
		{
			if (spriteFrames.GetFrameCount(text2) > 0)
			{
				return spriteFrames.GetFrameTexture(text2, 0);
			}
		}
		return null;
	}

	private static ProgressBar Progress(Color good)
	{
		ProgressBar progressBar = new ProgressBar();
		progressBar.ShowPercentage = false;
		progressBar.CustomMinimumSize = new Vector2(0f, 5f);
		progressBar.MouseFilter = Control.MouseFilterEnum.Ignore;
		progressBar.AddThemeStyleboxOverride("background", Box(new Color(0.12f, 0.13f, 0.15f, 0.9f), new Color(0f, 0f, 0f, 0f), 0, 2));
		progressBar.AddThemeStyleboxOverride("fill", Box(new Color(good.R, good.G, good.B, 0.75f), new Color(0f, 0f, 0f, 0f), 0, 2));
		return progressBar;
	}

	private static PanelContainer FramedPanel(Color background, Color border)
	{
		PanelContainer panelContainer = new PanelContainer();
		panelContainer.AddThemeStyleboxOverride("panel", Box(background, border, 1, 3));
		return panelContainer;
	}

	private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius
		};
	}

	private static void StyleTab(Button button, Color accent, bool active)
	{
		button.Disabled = active;
		button.AddThemeColorOverride("font_color", active ? accent.Lightened(0.35f) : CText);
		button.AddThemeColorOverride("font_hover_color", accent.Lightened(0.45f));
		button.AddThemeColorOverride("font_pressed_color", accent.Lightened(0.45f));
		button.AddThemeColorOverride("font_disabled_color", accent.Lightened(0.35f));
		button.AddThemeStyleboxOverride("normal", Box(CBookBg, new Color(0.32f, 0.35f, 0.4f, 0.45f), 1, 3));
		button.AddThemeStyleboxOverride("hover", Box(new Color(accent.R * 0.22f, accent.G * 0.22f, accent.B * 0.22f, 0.96f), new Color(accent.R, accent.G, accent.B, 0.75f), 1, 3));
		button.AddThemeStyleboxOverride("pressed", Box(new Color(accent.R * 0.26f, accent.G * 0.26f, accent.B * 0.26f, 0.98f), accent, 1, 3));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(accent.R * 0.24f, accent.G * 0.24f, accent.B * 0.24f, 0.95f), new Color(accent.R, accent.G, accent.B, 0.82f), 1, 3));
	}

	private static MarginContainer Margin(int left, int top, int right, int bottom)
	{
		MarginContainer marginContainer = new MarginContainer();
		marginContainer.AddThemeConstantOverride("margin_left", left);
		marginContainer.AddThemeConstantOverride("margin_top", top);
		marginContainer.AddThemeConstantOverride("margin_right", right);
		marginContainer.AddThemeConstantOverride("margin_bottom", bottom);
		return marginContainer;
	}

	private static void Clear(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Label Centered(string text, Color color, int size)
	{
		Label label = Row(text, color, size);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.ClipText = true;
		return label;
	}

	private static Label Row(string text, Color color, int size)
	{
		Label label = new Label();
		label.Text = text;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}
