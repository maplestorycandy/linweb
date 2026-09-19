using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class TownScreen : Control
{
	private sealed record TownBagEntry(string ItemKey, string Tooltip, string Corner, Action? Activate, bool Locked, Color? Quality, bool QualityFrame = false, bool BrokenBlade = false, Color? FrameQuality = null, ItemBlessing BlessingState = ItemBlessing.Normal);

	private readonly record struct CraftScrollShell(Control Root, VBoxContainer List);

	private sealed partial class WarehouseDropZone : Control
	{
		public Action<string>? OnPayloadDropped { get; set; }

		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.String) return false;
			string text = data.AsString();
			if (string.IsNullOrEmpty(text)) return false;
			var (itemKey, _, _) = ItemDragPayload.Decode(text);
			return !string.IsNullOrEmpty(itemKey) && !itemKey.StartsWith("skill:", StringComparison.Ordinal);
		}

		public override void _DropData(Vector2 atPosition, Variant data)
		{
			string text = data.AsString();
			if (!string.IsNullOrEmpty(text))
			{
				OnPayloadDropped?.Invoke(text);
			}
		}
	}

	private sealed class WarehouseSide
	{
		public Label Heading;

		public Label Status;

		public Control Grid;

		public Label PageLabel;

		public Button Previous;

		public Button Next;

		public OptionButton Category;

		public ItemCategory Filter;

		public int Page;
	}

	private string _clanMessage = "";

	private Color _clanMessageColor;

	private string _clanNameDraft = "";

	private byte[] _clanEmblemDraft = new byte[384];

	private ushort _clanEmblemPaint = ushort.MaxValue;

	private Control? _classicLeftPanel;

	private Control? _classicRightPanel;

	private string _classicLeftKind = "";

	private string _classicRightKind = "";

	private string _classicEquipmentStatus = "";

	private int _townBagPage;

	private Action? _townBagRefresh;

	private static readonly Vector2 BaseView = new Vector2(800f, 600f);

	private Vector2 _view = BaseView;

	private static readonly Vector2 TownPanelViewport = BaseView;

	private const string TownDesc = "安全區 · 冒險者的據點";

	private static readonly Color CTownLink = Color.FromHtml("#8fb4d8".AsSpan());

	private static readonly Color CGold = Color.FromHtml("#e6c76a".AsSpan());

	private static readonly Color CText = Color.FromHtml("#c9d1de".AsSpan());

	private static readonly Color CDim = Color.FromHtml("#8b95a6".AsSpan());

	private static readonly Color CGood = Color.FromHtml("#8fdd8f".AsSpan());

	private static readonly Color CBad = Color.FromHtml("#e2938f".AsSpan());

	private static readonly Color CPanel = new Color(0.06f, 0.07f, 0.09f, 0.96f);

	private AtlasBridge _atlas;

	private GameSession _session;

	private Action _onHunt;

	private Action _onTravel;

	private Action _onExit;

	private Control? _overlay;

	private string _overlayTitle = "";

	private readonly ICombatRandom _potionRng = new SeededCombatRandom(System.Environment.TickCount);

	private Label _hdrLine1;

	private Label _hdrLine2;

	private string _townKey = "town_aden";

	private string _townBg = "亞丁城鎮";

	private Vector2 _panelViewport = TownPanelViewport;

	private const float SheetW = 466f;

	private static readonly (string Key, string Name)[] SlotOrder = new(string, string)[18]
	{
		("wpn", "武器"),
		("offwpn", "副手"),
		("shield", "盾牌"),
		("arrow", "彈藥"),
		("helm", "頭盔"),
		("armor", "盔甲"),
		("tshirt", "內襯"),
		("cloak", "斗篷"),
		("gloves", "手套"),
		("boots", "靴子"),
		("amulet", "項鍊"),
		("belt", "腰帶"),
		("ring1", "戒指Ⅰ"),
		("ring2", "戒指Ⅱ"),
		("ring3", "戒指Ⅲ"),
		("ring4", "戒指Ⅳ"),
		("ear1", "耳環Ⅰ"),
		("ear2", "耳環Ⅱ")
	};

	private static readonly (CollectionBookKind Book, string Label)[] CollectionBooks = new(CollectionBookKind, string)[3]
	{
		(CollectionBookKind.Equipment, "裝備"),
		(CollectionBookKind.Misc, "道具"),
		(CollectionBookKind.Card, "怪物")
	};

	private Control? _sellBag;

	private int _sellBagPage;

	private Action? _sellBagRefresh;

	private Control? _buyPrompt;

	private const long MaxBuyQuantity = 999L;

	private Label? _townNameLabel;

	private string _houseMessage = "";

	private Color _houseMessageColor;

	private string _petMessage = "";

	private Color _petMessageColor;

	private const float WhPanelW = 309f;

	private const float WhPanelH = 451.5f;

	private const int WhColumns = 4;

	private const int WhRows = 6;

	private const int WhPageSize = 24;

	private const float WhMiddleGap = 34f;

	private const float WhContentW = 652f;

	private const float WhContentH = 451.5f;

	private const float WhPromptW = 340f;

	private const float WhPromptH = 124f;

	private Vector2 View => _view;

	public CombatEngine? LiveEngine { get; set; }

	public bool EmbeddedNpcHost { get; set; }

	private void OpenAttributeScrollPanel(string scrollUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		int num = ((itemStack != null) ? L1jAttrEnchantRules.KindOfScroll(shared, itemStack.ItemKey) : 0);
		if (itemStack == null || num == 0)
		{
			return;
		}
		string text = L1jAttrEnchantRules.KindName(num);
		VBoxContainer vBoxContainer = OpenPanel(text + "之武器強化卷軸", new Vector2(560f, 520f));
		Label label = Row($"選擇要賦予「{text}」屬性的武器。成功率 {10}%，" + "成功與失敗都會消耗一張卷軸；取消則不消耗。", CText);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 44f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, message.StartsWith("✓") ? CGood : CBad), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jAttrEnchantRules.EligibleTargets(shared, _session.Player, num);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(Row("沒有可以賦予「" + text + "」屬性的武器（安定值為負的武器不可強化；同屬性已達 3 階也不能再用）。", CDim), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(520f, 320f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			PanelContainer rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 34f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.11f, 0.95f),
				BorderColor = new Color(0.25f, 0.23f, 0.18f, 0.6f),
				BorderWidthBottom = 1,
				BorderWidthTop = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				ContentMarginLeft = 8f,
				ContentMarginRight = 8f,
				ContentMarginTop = 2f,
				ContentMarginBottom = 2f
			};
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

			HBoxContainer hBoxContainer = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Begin
			};
			hBoxContainer.AddThemeConstantOverride("separation", 8);
			rowPanel.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);

			Label label2 = Row(EquippedMark(captured) + StackLabel(captured), CText);
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			label2.AutowrapMode = TextServer.AutowrapMode.Off;
			label2.ClipText = true;
			hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);

			Button button = new Button
			{
				Text = "選擇賦予",
				CustomMinimumSize = new Vector2(96f, 28f),
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			button.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.65f));
			button.AddThemeFontSizeOverride("font_size", 13);
			button.Pressed += delegate
			{
				OpenAttributeScrollConfirmation(scrollUid, captured.Uid);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(rowPanel, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenAttributeScrollConfirmation(string scrollUid, string targetUid)
	{
		GameData data = GameDataProvider.Shared;
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		ItemStack itemStack2 = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid) ?? _session.Player.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		if (itemStack == null || itemStack2 == null)
		{
			OpenAttributeScrollPanel(scrollUid, "物品已不在身上；未消耗任何物品。");
			return;
		}
		int num = L1jAttrEnchantRules.KindOfScroll(data, itemStack.ItemKey);
		int num2 = ((itemStack2.AttrEnchantKind != num) ? 1 : (itemStack2.AttrEnchantLevel + 1));
		VBoxContainer vBoxContainer = OpenPanel("確認屬性強化", new Vector2(520f, 380f));
		Label label = Row($"武器：{EquippedMark(itemStack2)}{StackLabel(itemStack2)}\n目前屬性：{AttributeScrollText.Describe(itemStack2)}\n成功後：{L1jAttrEnchantRules.KindName(num)} {num2} 階（追加傷害 {AttributeScrollText.BonusOf(num2)}）\n\n成功率 {10}%。" + "成功與失敗都會消耗一張卷軸；失敗時武器完全不變。", CBad, 15);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 190f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		hBoxContainer.AddThemeConstantOverride("separation", 28);
		hBoxContainer.AddChild(ClassicArtButtons.Confirm(delegate
		{
			L1jAttrEnchantResult result = L1jAttrEnchantRules.TryEnchant(data, _session.Player, scrollUid, targetUid, confirmed: true, _potionRng);
			if (result.Attempted)
			{
				SaveManager.Save(_session);
			}
			string text = AttributeScrollText.Outcome(data, result);
			if (_session.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid))
			{
				OpenAttributeScrollPanel(scrollUid, text);
			}
			else
			{
				VBoxContainer vBoxContainer2 = OpenPanel("屬性強化結果", new Vector2(460f, 250f));
				Label label2 = Row(text, result.Succeeded ? CGood : CBad, 15);
				label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				label2.CustomMinimumSize = new Vector2(0f, 90f);
				vBoxContainer2.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
				vBoxContainer2.AddChild(ClassicArtButtons.Confirm(CloseOverlay, "關閉"), forceReadableName: false, InternalMode.Disabled);
			}
		}, "同意並使用"), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenAttributeScrollPanel(scrollUid);
		}, "取消；不消耗卷軸"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private string EquippedMark(ItemStack stack)
	{
		if (!_session.Player.EquippedItems.Values.Any((ItemStack equipped) => equipped.Uid == stack.Uid))
		{
			return "";
		}
		return "〔裝備中〕";
	}

	private void OpenClanPanel(string npcName)
	{
		_clanMessage = "";
		_clanNameDraft = "";
		ClanStore.Sync(_session);
		ReadOnlySpan<byte> span = ClanStore.Book.Emblem.Span;
		_clanEmblemDraft = ((span.Length == 384) ? span.ToArray() : new byte[384]);
		BuildClanPanel(npcName);
	}

	private void ClanStatus(Label status, string text, Color color)
	{
		SetStatus(status, text, color);
		_clanMessage = text;
		_clanMessageColor = color;
	}

	private void BuildClanPanel(string npcName)
	{
		ClanBook book = ClanStore.Book;
		VBoxContainer vBoxContainer = OpenClanWindow(npcName);
		Label label = Row("", CDim, 13);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(210f, 0f);
		if (_clanMessage.Length > 0)
		{
			SetStatus(label, _clanMessage, _clanMessageColor);
			_clanMessage = "";
		}
		if (!book.Exists)
		{
			BuildClanCreateSection(vBoxContainer, label, npcName);
		}
		else
		{
			BuildClanHomeSection(vBoxContainer, label, npcName, book);
		}
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
	}

	private VBoxContainer OpenClanWindow(string npcName)
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		var (node, result) = ClassicClanWindow.Create(_panelViewport, "血盟執行人", CloseOverlay);
		control.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = "血盟 · " + npcName;
		return result;
	}

	private void BuildClanCreateSection(VBoxContainer body, Label status, string npcName)
	{
		bool num = _session.Player.ClassId == "royal";
		body.AddChild(Row("這個帳號還沒有血盟。", CGold, 15), forceReadableName: false, InternalMode.Disabled);
		Label label = Row($"血盟由王族創立，費用 {30000L:N0} 金幣（原版手續費）。" + "血盟是整個帳號共用的——創立之後，這個帳號的每個角色都自動是成員，共用同一份血盟倉庫。", CDim, 12);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(210f, 0f);
		body.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!num)
		{
			body.AddChild(Row("\u3000只有王族可以創立血盟。", CBad), forceReadableName: false, InternalMode.Disabled);
			body.AddChild(Row("\u3000請先用王族角色創立，其他角色會自動成為成員。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		LineEdit name = new LineEdit
		{
			PlaceholderText = $"血盟名稱（1~{20} 字）",
			MaxLength = 20,
			Text = _clanNameDraft,
			CustomMinimumSize = new Vector2(210f, 32f)
		};
		name.TextChanged += delegate(string text)
		{
			_clanNameDraft = text;
		};
		body.AddChild(name, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "創立血盟",
			CustomMinimumSize = new Vector2(140f, 32f)
		};
		button.AddThemeFontSizeOverride("font_size", 14);
		button.Pressed += delegate
		{
			DoClanCreate(name.Text, status, npcName);
		};
		body.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private void DoClanCreate(string name, Label status, string npcName)
	{
		ClanCreateResult clanCreateResult = ClanStore.Book.TryCreate(_session.Player, _session.Identity, name, ClanStore.NowUnixMilliseconds());
		if (!clanCreateResult.Success)
		{
			ClanStatus(status, ClanRules.FailureText(clanCreateResult.Failure), CBad);
			return;
		}
		_clanNameDraft = "";
		PersistClan(status, "✓ 你創立了血盟「" + clanCreateResult.Name + "」。");
		RefreshHeaderGold();
		BuildClanPanel(npcName);
	}

	private void BuildClanHomeSection(VBoxContainer body, Label status, string npcName, ClanBook book)
	{
		string identity = _session.Identity;
		body.AddChild(Row($"「{book.Name}」\u3000成員 {book.MemberCount} 人", CGold, 17), forceReadableName: false, InternalMode.Disabled);
		body.AddChild(Row(book.OwnsHouse ? $"持有盟屋：房屋編號 {book.HouseId}" : "持有盟屋：無", CText, 13), forceReadableName: false, InternalMode.Disabled);
		if (!ClanStore.LeaderPresent(_session))
		{
			Label label = Row("⚠ 創立血盟的王族已不存在，這個血盟目前沒有盟主。", CBad, 13);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(210f, 0f);
			body.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			body.AddChild(Row("盟主：" + ClanStore.LeaderDisplayName(_session), CText, 13), forceReadableName: false, InternalMode.Disabled);
		}
		ClanMemberInfo clanMemberInfo = book.Member(identity);
		if ((object)clanMemberInfo != null)
		{
			body.AddChild(Row("你的階級：" + ClanRules.RankName(clanMemberInfo.Rank), CText, 13), forceReadableName: false, InternalMode.Disabled);
		}
		BuildClanRankRows(body, status, npcName, book);
		BuildClanEmblemEditor(body, status, npcName, book);
		BuildClanWarehouseRow(body, book);
	}

	private void BuildClanRankRows(VBoxContainer body, Label status, string npcName, ClanBook book)
	{
		body.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0f, 6f)
		}, forceReadableName: false, InternalMode.Disabled);
		body.AddChild(Row("── 血盟階級 ──", CGold), forceReadableName: false, InternalMode.Disabled);
		bool flag = book.IsLeader(_session.Identity);
		foreach (ClanMemberInfo item in book.Members())
		{
			ClanMemberInfo captured = item;
			HBoxContainer hBoxContainer = new HBoxContainer();
			hBoxContainer.AddThemeConstantOverride("separation", 8);
			hBoxContainer.AddChild(new Label
			{
				Text = ClanStore.MemberDisplayName(_session, item.Identity),
				CustomMinimumSize = new Vector2(112f, 30f),
				VerticalAlignment = VerticalAlignment.Center
			}, forceReadableName: false, InternalMode.Disabled);
			if (item.Rank == ClanRank.Prince)
			{
				hBoxContainer.AddChild(Row(ClanRules.RankName(item.Rank), CGold, 13), forceReadableName: false, InternalMode.Disabled);
			}
			else
			{
				OptionButton choice = new OptionButton
				{
					CustomMinimumSize = new Vector2(92f, 30f),
					Disabled = !flag
				};
				ClanRank[] array = new ClanRank[3]
				{
					ClanRank.Probation,
					ClanRank.Public,
					ClanRank.Guardian
				};
				foreach (ClanRank clanRank in array)
				{
					choice.AddItem(ClanRules.RankName(clanRank), (int)clanRank);
				}
				choice.Select((int)(item.Rank - 1));
				choice.ItemSelected += delegate(long index)
				{
					ClanRank itemId = (ClanRank)choice.GetItemId((int)index);
					ClanRankResult clanRankResult = book.TrySetRank(_session.Identity, captured.Identity, itemId);
					if (!clanRankResult.Success)
					{
						ClanStatus(status, ClanRules.RankFailureText(clanRankResult.Failure), CBad);
					}
					else
					{
						PersistClanOnly(status, $"✓ {ClanStore.MemberDisplayName(_session, captured.Identity)}已調整為{ClanRules.RankName(itemId)}。", npcName);
					}
				};
				hBoxContainer.AddChild(choice, forceReadableName: false, InternalMode.Disabled);
			}
			body.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void BuildClanEmblemEditor(VBoxContainer body, Label status, string npcName, ClanBook book)
	{
		body.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0f, 6f)
		}, forceReadableName: false, InternalMode.Disabled);
		body.AddChild(Row("── 盟徽（16×12）──", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer = new GridContainer
		{
			Columns = 6
		};
		gridContainer.AddThemeConstantOverride("h_separation", 3);
		gridContainer.AddThemeConstantOverride("v_separation", 3);
		ushort[] array = new ushort[12]
		{
			0, 65535, 63488, 2016, 31, 65504, 63519, 2047, 33808, 16904,
			64800, 32784
		};
		foreach (ushort num in array)
		{
			ushort captured = num;
			Button button = new Button
			{
				CustomMinimumSize = new Vector2(24f, 24f),
				TooltipText = $"RGB565 0x{num:x4}",
				FocusMode = FocusModeEnum.None
			};
			PaintClanColorButton(button, num, num == _clanEmblemPaint);
			button.Pressed += delegate
			{
				_clanEmblemPaint = captured;
				BuildClanPanel(npcName);
			};
			gridContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		body.AddChild(gridContainer, forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer2 = new GridContainer
		{
			Columns = 16
		};
		gridContainer2.AddThemeConstantOverride("h_separation", 1);
		gridContainer2.AddThemeConstantOverride("v_separation", 1);
		for (int num2 = 0; num2 < 192; num2++)
		{
			int capturedIndex = num2;
			ushort value = ReadClanRgb565(_clanEmblemDraft, num2);
			Button button2 = new Button
			{
				CustomMinimumSize = new Vector2(12f, 12f),
				FocusMode = FocusModeEnum.None
			};
			PaintClanColorButton(button2, value, selected: false);
			button2.Pressed += delegate
			{
				WriteClanRgb565(_clanEmblemDraft, capturedIndex, _clanEmblemPaint);
				BuildClanPanel(npcName);
			};
			gridContainer2.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		}
		body.AddChild(gridContainer2, forceReadableName: false, InternalMode.Disabled);
		Button button3 = new Button
		{
			Text = "儲存盟徽",
			CustomMinimumSize = new Vector2(140f, 32f)
		};
		button3.Pressed += delegate
		{
			if (!book.TrySetEmblem(_session.Identity, _clanEmblemDraft))
			{
				ClanStatus(status, "盟徽必須由血盟成員以完整 384-byte 圖樣儲存。", CBad);
			}
			else
			{
				PersistClanOnly(status, "✓ 盟徽已儲存。", npcName);
			}
		};
		body.AddChild(button3, forceReadableName: false, InternalMode.Disabled);
	}

	private static ushort ReadClanRgb565(byte[] bytes, int pixel)
	{
		int num = pixel * 2;
		return (ushort)(bytes[num] | (bytes[num + 1] << 8));
	}

	private static void WriteClanRgb565(byte[] bytes, int pixel, ushort value)
	{
		int num = pixel * 2;
		bytes[num] = (byte)value;
		bytes[num + 1] = (byte)(value >> 8);
	}

	private static Color ClanRgb565Color(ushort value)
	{
		return new Color((float)((value >> 11) & 0x1F) / 31f, (float)((value >> 5) & 0x3F) / 63f, (float)(value & 0x1F) / 31f);
	}

	private static void PaintClanColorButton(Button button, ushort value, bool selected)
	{
		StyleBoxFlat stylebox = new StyleBoxFlat
		{
			BgColor = ClanRgb565Color(value),
			BorderColor = (selected ? Color.FromHtml("#ffd56a".AsSpan()) : Color.FromHtml("#262018".AsSpan())),
			BorderWidthLeft = ((!selected) ? 1 : 3),
			BorderWidthTop = ((!selected) ? 1 : 3),
			BorderWidthRight = ((!selected) ? 1 : 3),
			BorderWidthBottom = ((!selected) ? 1 : 3)
		};
		button.AddThemeStyleboxOverride("normal", stylebox);
		button.AddThemeStyleboxOverride("hover", stylebox);
		button.AddThemeStyleboxOverride("pressed", stylebox);
	}

	private void PersistClanOnly(Label status, string successText, string npcName)
	{
		bool flag = ClanStore.Save();
		ClanStatus(status, flag ? successText : (successText + "（⚠ 血盟帳本寫入失敗）"), flag ? CGood : CBad);
		BuildClanPanel(npcName);
	}

	private void BuildClanWarehouseRow(VBoxContainer body, ClanBook book)
	{
		body.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0f, 6f)
		}, forceReadableName: false, InternalMode.Disabled);
		body.AddChild(Row("── 血盟倉庫 ──", CGold), forceReadableName: false, InternalMode.Disabled);
		Label label = Row($"血盟共用的倉庫（{200} 格），與個人倉庫分開存放。", CDim, 12);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(210f, 0f);
		body.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = $"\ud83c\udfdb 開啟血盟倉庫（{book.Warehouse.Items.Count} 件）",
			CustomMinimumSize = new Vector2(210f, 34f)
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += delegate
		{
			OpenClanWarehouse(book.Name);
		};
		body.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private void PersistClan(Label status, string successText)
	{
		var (text, color) = PersistBothBooks(successText);
		ClanStatus(status, text, color);
	}

	private (string Text, Color Color) PersistBothBooks(string successText)
	{
		bool flag = ClanStore.Save();
		bool flag2 = SaveManager.Save(_session);
		if (flag && flag2)
		{
			return (Text: successText, Color: CGood);
		}
		return (Text: successText + "（⚠ " + (flag ? "" : "血盟帳本") + ((!flag && !flag2) ? "與" : "") + (flag2 ? "" : "角色存檔") + "寫入失敗，請重新整理後確認）", Color: CBad);
	}

	private void ToggleClassicInfo()
	{
		ToggleClassicEquipment();
	}

	private void ToggleClassicEquipment()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "equipment")
		{
			CloseClassicLeft();
		}
		else
		{
			OpenClassicEquipment();
		}
	}

	private void OpenClassicEquipment()
	{
		CloseClassicLeft();
		_classicLeftKind = "equipment";
		_classicLeftPanel = ClassicWebWindows.CreateCharacter(_session, new Vector2(8f, 8f), CloseClassicLeft, delegate(string slot)
		{
			_session.Player.EquippedItems.TryGetValue(slot, out ItemStack value);
			ItemStack before = value?.Copy();
			(bool Ok, string Text) tuple = ItemActivation.Unequip(GameDataProvider.Shared, _session.Player, slot, _potionRng);
			bool item = tuple.Ok;
			string item2 = tuple.Text;
			_classicEquipmentStatus = (item ? "✓ " : "") + item2;
			if (item)
			{
				if (before != null)
				{
					ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack itemStack2) => itemStack2.Uid == before.Uid) ?? _session.Player.InventoryStacks.FirstOrDefault((ItemStack left) => ItemStackInventory.CanStack(left, before));
					if (itemStack != null)
					{
						QuickBar.RemapEquipmentAssignment(_session.QuickItems, before.Uid, itemStack.Uid, before.ItemKey);
					}
				}
				SaveManager.Save(_session);
			}
			return (Ok: item, Text: item2);
		}, OpenClassicEquipment, _classicEquipmentStatus, ToggleClassicAbility, ToggleClassicTitle);
		AddChild(_classicLeftPanel, forceReadableName: false, InternalMode.Disabled);
	}

	private void ToggleClassicAbility()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "ability")
		{
			CloseClassicLeft();
			return;
		}
		CloseClassicLeft();
		_classicLeftKind = "ability";
		_classicLeftPanel = ClassicWebWindows.CreateAbility(_session, new Vector2(8f, 8f), CloseClassicLeft, delegate(string key)
		{
			L1jLevelStatResult l1jLevelStatResult = L1jLevelStatRules.TryAllocate(GameDataProvider.Shared, _session.Player, key);
			if (!l1jLevelStatResult.Success)
			{
				return (Ok: false, Text: LevelStatFailureText(l1jLevelStatResult.Failure));
			}
			SaveManager.Save(_session);
			return (Ok: true, Text: $"能力值提高，剩餘 {l1jLevelStatResult.RemainingPoints} 點");
		});
		AddChild(_classicLeftPanel, forceReadableName: false, InternalMode.Disabled);
	}

	private static string LevelStatFailureText(L1jLevelStatFailure failure)
	{
		return failure switch
		{
			L1jLevelStatFailure.NoPoints => "沒有可分配的能力點數", 
			L1jLevelStatFailure.AttributeMaximum => "此能力值已達目前階段上限", 
			L1jLevelStatFailure.InvalidState => "角色配點資料不完整", 
			_ => "無法分配能力點數", 
		};
	}

	private void ToggleClassicTitle()
	{
		if (_classicLeftPanel != null && _classicLeftKind == "title")
		{
			CloseClassicLeft();
			return;
		}
		CloseClassicLeft();
		_classicLeftKind = "title";
		_classicLeftPanel = ClassicWebWindows.CreateTitleEditor(_session.Player, new Vector2(8f, 8f), CloseClassicLeft, ApplyClassicTitle);
		AddChild(_classicLeftPanel, forceReadableName: false, InternalMode.Disabled);
	}

	private (bool Ok, string Text) ApplyClassicTitle(string requestedTitle)
	{
		ClanBook book = ClanStore.Book;
		bool isClanMember = book.Exists && (object)book.Member(_session.Identity) != null;
		CharacterTitleResult characterTitleResult = CharacterTitleRules.TrySetSelf(_session.Player, requestedTitle, isClanMember, book.IsLeader(_session.Identity));
		if (!characterTitleResult.Success)
		{
			return (Ok: false, Text: CharacterTitleRules.FailureText(characterTitleResult.Failure));
		}
		SaveManager.Save(_session);
		if (_hdrLine1 != null)
		{
			_hdrLine1.Text = HeaderIdentityLine();
		}
		return (Ok: true, Text: "稱號已設定為「" + characterTitleResult.Title + "」。");
	}

	private void ToggleClassicSkills()
	{
		if (_classicRightPanel != null && _classicRightKind == "skills")
		{
			CloseClassicRight();
			return;
		}
		CloseClassicRight();
		_classicRightKind = "skills";
		_classicRightPanel = ClassicWebWindows.CreateSkills(_session.Player, new Vector2(View.X - 309f - 8f, 8f), CloseClassicRight, null, delegate
		{
			CloseClassicRight();
			OpenSkills();
		}, _session.AutoCast, delegate(string id, bool on)
		{
			if (on)
			{
				_session.AutoCast.Add(id);
			}
			else
			{
				_session.AutoCast.Remove(id);
			}
			SaveManager.Save(_session);
		});
		AddChild(_classicRightPanel, forceReadableName: false, InternalMode.Disabled);
		GameAudio.Instance?.PlayUi("skillOpen", 0.0, 0.52f);
	}

	private void CloseClassicLeft()
	{
		_classicLeftPanel?.QueueFree();
		_classicLeftPanel = null;
		_classicLeftKind = "";
	}

	private void CloseClassicRight()
	{
		bool flag = _classicRightPanel != null;
		string classicRightKind = _classicRightKind;
		_classicRightPanel?.QueueFree();
		_classicRightPanel = null;
		_classicRightKind = "";
		_townBagRefresh = null;
		if (flag && classicRightKind == "bag")
		{
			GameAudio.Instance?.PlayUi("inventoryClose", 0.0, 0.52f);
		}
		else if (flag && classicRightKind == "skills")
		{
			GameAudio.Instance?.PlayUi("skillClose", 0.0, 0.52f);
		}
	}

	private void OpenBag()
	{
		if (_classicRightPanel != null && _classicRightKind == "bag")
		{
			return;
		}
		CloseClassicRight();
		_classicRightKind = "bag";
		ClassicWebWindows.BagShell shell = ClassicWebWindows.CreateBagShell(new Vector2(View.X - 309f - 8f, 8f), CloseClassicRight, "");
		shell.Tabs.SelectFirstOccupied(GameDataProvider.Shared, _session.Player.InventoryStacks.Select((ItemStack stack) => stack.ItemKey));
		_classicRightPanel = shell.Panel;
		_townBagRefresh = delegate
		{
			RebuildTownBagGrid(shell.Grid, shell.Status, shell.Page, shell.Previous, shell.Next, shell.Tabs.Selected);
		};
		shell.Tabs.Changed += delegate
		{
			_townBagPage = 0;
			_townBagRefresh?.Invoke();
		};
		shell.Previous.Pressed += delegate
		{
			if (_townBagPage > 0)
			{
				_townBagPage--;
				_townBagRefresh?.Invoke();
			}
		};
		shell.Next.Pressed += delegate
		{
			_townBagPage++;
			_townBagRefresh?.Invoke();
		};
		_townBagRefresh();
		AddChild(shell.Panel, forceReadableName: false, InternalMode.Disabled);
		GameAudio.Instance?.PlayUi("inventoryOpen", 0.0, 0.52f);
	}

	private void RebuildTownBagGrid(BagGrid grid, Label status, Label pageLabel, Button previous, Button next, ClassicInventoryTab tab)
	{
		foreach (Node child in grid.GetChildren())
		{
			grid.RemoveChild(child);
			child.QueueFree();
		}
		GameData shared = GameDataProvider.Shared;
		Combatant player = _session.Player;
		List<TownBagEntry> list = new List<TownBagEntry>();
		long num = CombatWallet.Balance(player);
		if (num > 0 && tab == ClassicInventoryTab.Other)
		{
			ItemStack itemStack = CombatWallet.VirtualStack(player);
			list.Add(new TownBagEntry(itemStack.ItemKey, $"金幣 ×{num:N0}", ItemInstanceText.CompactCount(num), null, Locked: true, null));
		}
		foreach (ItemStack inventoryStack in player.InventoryStacks)
		{
			if (ClassicInventoryTabRules.Matches(shared, inventoryStack.ItemKey, tab))
			{
				string text = ItemInstanceText.DisplayName(shared, inventoryStack, _session.Pets);
				if (inventoryStack.IsIdentified && inventoryStack.BrokenBladeStacks > 0)
				{
					text += $"〔壞刀×{inventoryStack.BrokenBladeStacks}〕";
				}
				EquipmentEligibilityResult equipmentEligibilityResult = EquipmentRules.Evaluate(shared, player, inventoryStack);
				if (ItemActivation.IsPlayerGear(shared, player, inventoryStack) && !equipmentEligibilityResult.Allowed)
				{
					text = text + "\n✗ " + ItemActivation.EligFailText(equipmentEligibilityResult.Failure);
				}
				ItemStack stackRef = inventoryStack;
				list.Add(new TownBagEntry(inventoryStack.ItemKey, text + $" ×{inventoryStack.Quantity:N0}" + ItemInstanceText.DetailTooltip(shared, inventoryStack), ItemInstanceText.StackCorner(inventoryStack), delegate
				{
					ActivateBagItem(stackRef, _townBagRefresh, status);
				}, inventoryStack.Locked, ItemQualityColors.Highlighted(inventoryStack) ? new Color?(ItemQualityColors.Of(inventoryStack)) : ((Color?)null), ItemQualityColors.Framed(inventoryStack), inventoryStack.IsIdentified && inventoryStack.BrokenBladeStacks > 0, ItemQualityColors.Framed(inventoryStack) ? new Color?(ItemQualityColors.FrameOf(inventoryStack)) : ((Color?)null), inventoryStack.IsIdentified ? inventoryStack.Blessing : ItemBlessing.Normal));
			}
		}
		int num2 = 24;
		int num3 = Math.Max(1, (list.Count + num2 - 1) / num2);
		_townBagPage = Math.Clamp(_townBagPage, 0, num3 - 1);
		int num4 = _townBagPage * num2;
		for (int num5 = 0; num5 < num2; num5++)
		{
			int num6 = num4 + num5;
			if (num6 >= list.Count)
			{
				grid.AddChild(new InventoryGridSlot
				{
					MouseFilter = MouseFilterEnum.Ignore
				}, forceReadableName: false, InternalMode.Disabled);
				continue;
			}
			TownBagEntry townBagEntry = list[num6];
			InventoryGridSlot inventoryGridSlot = new InventoryGridSlot
			{
				ItemKey = townBagEntry.ItemKey,
				Draggable = false,
				Locked = townBagEntry.Locked,
				Quality = townBagEntry.Quality,
				QualityFrame = townBagEntry.QualityFrame,
				FrameQuality = townBagEntry.FrameQuality,
				BlessingState = townBagEntry.BlessingState,
				BrokenBlade = townBagEntry.BrokenBlade,
				TooltipText = townBagEntry.Tooltip,
				OnActivate = townBagEntry.Activate
			};
			inventoryGridSlot.SetIcon(ItemIcons.For(townBagEntry.ItemKey));
			inventoryGridSlot.SetCorner(townBagEntry.Corner);
			if (QuickBar.CanAutoUse(shared, townBagEntry.ItemKey))
			{
				string autoKey = townBagEntry.ItemKey;
				ArpgEngineScreen.AttachAutoBadge(inventoryGridSlot, _session.AutoUseItems.Contains(autoKey), ArpgEngineScreen.AutoUseHint(shared, autoKey, _session.AutoPotionHpPercent), delegate(bool on)
				{
					if (on)
					{
						_session.AutoUseItems.Add(autoKey);
					}
					else
					{
						_session.AutoUseItems.Remove(autoKey);
					}
					SaveManager.Save(_session);
				});
			}
			grid.AddChild(inventoryGridSlot, forceReadableName: false, InternalMode.Disabled);
		}
		pageLabel.Text = $"{_townBagPage + 1} / {num3}";
		previous.Disabled = _townBagPage == 0;
		next.Disabled = _townBagPage >= num3 - 1;
	}

	public void Init(AtlasBridge atlas, GameSession session, Action onHunt, Action onTravel, Action onExit)
	{
		_atlas = atlas;
		_session = session;
		_onHunt = onHunt;
		_onTravel = onTravel;
		_onExit = onExit;
		_panelViewport = TownPanelViewport;
		_townKey = session.TownKey;
		_townBg = ((WorldMapCatalog.TryGetDestination(GameDataProvider.Shared, _townKey, out MapDestination destination) && destination != null) ? destination.Name : _townKey);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		Vector2 size = GetViewportRect().Size;
		_view = new Vector2(Mathf.Max(BaseView.X, size.X), Mathf.Max(BaseView.Y, size.Y));
		_panelViewport = _view;
		BuildBackground();
		BuildPlayerSprite();
		BuildHeader();
		BuildActionBar();
	}

	public override void _Process(double delta)
	{
		if (!EmbeddedNpcHost)
		{
			CastleWarStore.Tick(delta);
		}
		ClanStore.Tick(_session);
		SatietyRules.Tick(_session.Player, delta);
		ConsumableRules.DecayInternalCooldowns(_session.Player, delta);
	}

	private void BuildBackground()
	{
		ColorRect colorRect = new ColorRect
		{
			Color = Color.FromHtml("#0f1218".AsSpan()),
			MouseFilter = MouseFilterEnum.Ignore
		};
		colorRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		AddChild(colorRect, forceReadableName: false, InternalMode.Disabled);
		TeleportOnlyTownDefinition teleportOnlyTownDefinition = TeleportOnlyTownCatalog.Find(_townKey);
		Texture2D texture2D = (((object)teleportOnlyTownDefinition == null) ? Backgrounds.Area(_townBg) : Backgrounds.Asset(teleportOnlyTownDefinition.OverviewAssetPath));
		if (texture2D != null)
		{
			TextureRect textureRect = new TextureRect
			{
				Texture = texture2D,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Modulate = new Color(0.74f, 0.76f, 0.82f),
				MouseFilter = MouseFilterEnum.Ignore
			};
			textureRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
			AddChild(textureRect, forceReadableName: false, InternalMode.Disabled);
		}
		ColorRect node = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.5f),
			Position = Vector2.Zero,
			Size = new Vector2(View.X, 84f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(node, forceReadableName: false, InternalMode.Disabled);
		ColorRect node2 = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.62f),
			Position = new Vector2(0f, View.Y - 80f),
			Size = new Vector2(View.X, 80f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(node2, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildPlayerSprite()
	{
		PlayerBuild build = _session.Build;
		string text = (string.IsNullOrEmpty(build.WeaponPrefix) ? "idle" : (build.WeaponPrefix + "_idle"));
		Vector2 position = new Vector2(640f, 566f);
		AnimatedSprite2D animatedSprite2D = _atlas.MakeSprite("classanim", build.Avatar, text + "_s");
		if (animatedSprite2D != null)
		{
			animatedSprite2D.Centered = false;
			animatedSprite2D.Position = position;
			animatedSprite2D.Scale = new Vector2(1.5f, 1.5f);
			animatedSprite2D.Modulate = new Color(1f, 1f, 1f, 0.4f);
			AddChild(animatedSprite2D, forceReadableName: false, InternalMode.Disabled);
		}
		AnimatedSprite2D animatedSprite2D2 = _atlas.MakeSprite("classanim", build.Avatar, text);
		if (animatedSprite2D2 != null)
		{
			animatedSprite2D2.Centered = false;
			animatedSprite2D2.Position = position;
			animatedSprite2D2.Scale = new Vector2(1.5f, 1.5f);
			AddChild(animatedSprite2D2, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void BuildHeader()
	{
		TownDefinition town;
		string text = ((TownCatalog.TryGetTown(GameDataProvider.Shared, _townKey, out town) && town != null) ? town.Name : "城鎮");
		Label label = new Label
		{
			Text = text,
			Position = new Vector2(28f, 12f)
		};
		label.AddThemeFontSizeOverride("font_size", 30);
		AddChild(label, forceReadableName: false, InternalMode.Disabled);
		_townNameLabel = label;
		RefreshTownNameColor();
		Label label2 = new Label
		{
			Text = "安全區 · 冒險者的據點",
			Position = new Vector2(30f, 52f)
		};
		label2.AddThemeFontSizeOverride("font_size", 14);
		label2.AddThemeColorOverride("font_color", CDim);
		AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		Combatant player = _session.Player;
		Label label3 = new Label
		{
			Text = HeaderIdentityLine(),
			Position = new Vector2(880f, 16f),
			Size = new Vector2(376f, 24f),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		label3.AddThemeFontSizeOverride("font_size", 19);
		label3.AddThemeColorOverride("font_color", CGold);
		AddChild(label3, forceReadableName: false, InternalMode.Disabled);
		_hdrLine1 = label3;
		Label label4 = new Label
		{
			Text = $"EXP {(long)player.Experience}",
			Position = new Vector2(880f, 48f),
			Size = new Vector2(376f, 20f),
			HorizontalAlignment = HorizontalAlignment.Right
		};
		label4.AddThemeFontSizeOverride("font_size", 14);
		label4.AddThemeColorOverride("font_color", CText);
		AddChild(label4, forceReadableName: false, InternalMode.Disabled);
		_hdrLine2 = label4;
	}

	private string HeaderIdentityLine()
	{
		Combatant player = _session.Player;
		string value = ((player.Title.Length == 0) ? "" : ("【" + player.Title + "】"));
		return $"{_session.Build.DisplayName}{value} Lv{player.Level}";
	}

	private void BuildActionBar()
	{
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Position = new Vector2(0f, View.Y - 64f),
			Size = new Vector2(View.X, 48f)
		};
		hBoxContainer.Alignment = BoxContainer.AlignmentMode.Center;
		hBoxContainer.AddThemeConstantOverride("separation", 7);
		AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("世界地圖", OpenMapMenu, primary: true), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("資訊", ToggleClassicInfo), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("裝備", ToggleClassicEquipment), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("技能", ToggleClassicSkills), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("背包", OpenBag), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("收集冊", OpenCollections), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(BarButton("登出", QuitToCharacterSelect), forceReadableName: false, InternalMode.Disabled);
	}

	private Button BarButton(string text, Action onPressed, bool primary = false)
	{
		Button button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(primary ? 150 : 118, 44f)
		};
		button.AddThemeFontSizeOverride("font_size", primary ? 18 : 16);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", CGold);
		}
		button.Pressed += delegate
		{
			onPressed();
		};
		return button;
	}

	private static bool HasExchangeOptions(string npcKey)
	{
		try
		{
			return ExchangeRules.ExchangeOptions(GameDataProvider.Shared, npcKey).Count > 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool HasShopOffers(string shopName)
	{
		GameData shared = GameDataProvider.Shared;
		if (L1jShopCatalog.TryResolveShopNpcId(shared, shopName, out var npcId))
		{
			return L1jShopCatalog.SellList(shared, npcId).Count > 0;
		}
		return false;
	}

	private void CloseOverlay()
	{
		_overlay?.QueueFree();
		_overlay = null;
		_overlayTitle = "";
		_sellBag = null;
		_sellBagRefresh = null;
		_buyPrompt = null;
	}

	private ClassicNpcDialogHandle OpenNpcDialog(string displayName, IEnumerable<string> lines)
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		ClassicNpcDialogHandle classicNpcDialogHandle = ClassicNpcDialogWindow.Create(_panelViewport, displayName.EndsWith('：') ? displayName : (displayName + "："), lines, CloseOverlay);
		control.AddChild(classicNpcDialogHandle.Root, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = displayName;
		return classicNpcDialogHandle;
	}

	private VBoxContainer OpenPanel(string title, Vector2 size, bool ornate = false, bool preserveRequestedFrame = false)
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		float num = (ornate ? 17f : 18f);
		float num2 = (ornate ? 34f : 24f);
		float num3 = (ornate ? 27f : 18f);
		float num4 = (ornate ? 12f : 18f);
		float b = View.Y - 24f - num2 - num4 - 30f;
		float b2 = View.X - 24f - num - num3;
		Vector2 vector = new Vector2(Mathf.Min(size.X - 36f, b2), Mathf.Min(size.Y - 72f, b));
		Vector2 vector2 = new Vector2(vector.X + num + num3, vector.Y + num2 + num4 + 30f);
		Control panel;
		Control control2;
		if (ornate)
		{
			(panel, control2) = OrnateFrame.Create((_panelViewport - vector2) * 0.5f, vector2, CloseOverlay);
		}
		else
		{
			(panel, control2) = ClassicMapFrame.Create((_panelViewport - vector2) * 0.5f, vector2, CloseOverlay);
		}
		if (ornate)
		{
			control2.AddChild(OrnateFrame.Title(title), forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			panel.AddChild(ClassicMapFrame.Title(title), forceReadableName: false, InternalMode.Disabled);
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Position = new Vector2(8f, 30f),
			Size = new Vector2(Mathf.Max(0f, vector.X - 16f), vector.Y),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer body = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		body.AddThemeConstantOverride("separation", 8);
		body.CustomMinimumSize = new Vector2(scrollContainer.Size.X - 14f, 0f);
		scrollContainer.AddChild(body, forceReadableName: false, InternalMode.Disabled);
		control2.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = title;
		Callable.From(delegate
		{
			FinishTownPanel(panel, body, 30f, preserveRequestedFrame);
		}).CallDeferred();
		return body;
	}

	private async void FinishTownPanel(Control panel, VBoxContainer body, float titleRow, bool preserveRequestedFrame)
	{
		if (!GodotObject.IsInstanceValid(panel) || !GodotObject.IsInstanceValid(body))
		{
			return;
		}
		if (preserveRequestedFrame)
		{
			ClassicMapFrame.MakeScrollbarsTransparent(panel);
			return;
		}
		Vector2 need = body.GetCombinedMinimumSize();
		if (need.X <= 0f || need.Y <= 0f)
		{
			ClassicMapFrame.MakeScrollbarsTransparent(panel);
			return;
		}
		body.Size = need;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(panel) || !GodotObject.IsInstanceValid(body))
		{
			return;
		}
		float num = body.GetThemeConstant("separation");
		float num2 = 0f;
		int num3 = 0;
		foreach (Node child in body.GetChildren())
		{
			if (child is Control { Visible: not false } control)
			{
				num2 += Mathf.Max(control.Size.Y, control.GetCombinedMinimumSize().Y);
				num3++;
			}
		}
		if (num3 > 1)
		{
			num2 += num * (float)(num3 - 1);
		}
		Vector2 vector = (body.Size = new Vector2(need.X, Mathf.Max(num2, need.Y)));
		Vector2 vector3 = new Vector2(vector.X + 18f + 18f, vector.Y + 24f + 18f + titleRow);
		ClassicMapFrame.Resize(panel, vector3);
		float num4 = Mathf.Min(1f, Mathf.Min((_panelViewport.X - 32f) / vector3.X, (_panelViewport.Y - 32f) / vector3.Y));
		panel.Scale = new Vector2(num4, num4);
		panel.Position = (_panelViewport - vector3 * num4) * 0.5f;
		ClassicMapFrame.MakeScrollbarsTransparent(panel);
	}

	private void OpenCharSheet()
	{
		Combatant player = _session.Player;
		DerivedStats d = player.D;
		VBoxContainer vBoxContainer = OpenPanel("角色資訊", new Vector2(520f, 656f));
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(484f, 578f)
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(466f, 0f)
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 8);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row($"{_session.Build.DisplayName} · Lv {player.Level}", CGold, 18), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row($"EXP {(long)player.Experience}", CDim, 13), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(new ColorRect
		{
			Color = new Color(1f, 1f, 1f, 0.1f),
			CustomMinimumSize = new Vector2(466f, 1f)
		}, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row("六維", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer = StatGrid();
		AddStat(gridContainer, "力量", $"{d.Str:0}");
		AddStat(gridContainer, "敏捷", $"{d.Dex:0}");
		AddStat(gridContainer, "體質", $"{d.Con:0}");
		AddStat(gridContainer, "智力", $"{d.Int:0}");
		AddStat(gridContainer, "精神", $"{d.Wis:0}");
		AddStat(gridContainer, "魅力", $"{d.Cha:0}");
		vBoxContainer2.AddChild(gridContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row("生存", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer2 = StatGrid();
		AddStat(gridContainer2, "生命 HP", $"{player.MaxHp:0}");
		AddStat(gridContainer2, "法力 MP", $"{player.MaxMp:0}");
		AddStat(gridContainer2, "防禦 AC", $"{d.ArmorClass:0}");
		AddStat(gridContainer2, "魔防 MR", $"{d.MagicResist:0}");
		AddStat(gridContainer2, "遠程迴避 ER", $"{d.EvasionRating:0}");
		AddStat(gridContainer2, "近戰迴避 DR", $"{d.MeleeEvasion:0}");
		AddStat(gridContainer2, "傷害減免", $"{d.DamageReduction:0}");
		vBoxContainer2.AddChild(gridContainer2, forceReadableName: false, InternalMode.Disabled);
		bool usesRangedAttack = d.UsesRangedAttack;
		vBoxContainer2.AddChild(Row("攻擊", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer3 = StatGrid();
		AddStat(gridContainer3, usesRangedAttack ? "遠程傷害" : "近戰傷害", $"{(usesRangedAttack ? d.RangedDamage : d.MeleeDamage):0}");
		AddStat(gridContainer3, "魔法傷害", $"{d.MagicDamage:0}");
		AddStat(gridContainer3, "命中", $"{(usesRangedAttack ? d.RangedHit : d.MeleeHit):0}");
		AddStat(gridContainer3, "爆擊", $"{(usesRangedAttack ? d.RangedCritical : d.MeleeCritical):0}%");
		AddStat(gridContainer3, "攻擊骰", $"{d.AttackDiceSmall}~{d.AttackDiceLarge}");
		AddStat(gridContainer3, "攻速", $"{((d.AttackInterval > 0.0) ? (1.0 / d.AttackInterval) : 0.0):0.00}/秒");
		vBoxContainer2.AddChild(gridContainer3, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row("元素抗性", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer4 = StatGrid();
		AddStat(gridContainer4, "火", $"{d.ResistFire:0}");
		AddStat(gridContainer4, "水", $"{d.ResistWater:0}");
		AddStat(gridContainer4, "風", $"{d.ResistWind:0}");
		AddStat(gridContainer4, "地", $"{d.ResistEarth:0}");
		vBoxContainer2.AddChild(gridContainer4, forceReadableName: false, InternalMode.Disabled);
		WeightReport w = WeightRules.Evaluate(GameDataProvider.Shared, player);
		vBoxContainer2.AddChild(Row("負重（裝備＋背包）", CGold), forceReadableName: false, InternalMode.Disabled);
		GridContainer gridContainer5 = StatGrid();
		AddStat(gridContainer5, "目前 / 上限", $"{w.CurrentWeight:0} / {w.TotalCapacity:0}");
		AddStat(gridContainer5, "負重率", $"{w.Percent}%");
		AddStat(gridContainer5, "基礎（力/體）", $"{w.BaseCapacity:0}");
		AddStat(gridContainer5, "裝備／腰帶", $"+{w.EquipmentCapacityBonus:0}");
		AddStat(gridContainer5, "增益（負重強化）", $"+{w.BuffCapacityBonus:0}");
		AddStat(gridContainer5, "收集冊", $"+{w.CollectionCapacityBonus:0}");
		vBoxContainer2.AddChild(gridContainer5, forceReadableName: false, InternalMode.Disabled);
		Label label = Row(WeightStateText(w), (w.LoadTier == 0) ? CGood : ((w.LoadTier == 1) ? CGold : CBad), 13);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(466f, 0f);
		vBoxContainer2.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(Row("套裝", CGold), forceReadableName: false, InternalMode.Disabled);
		foreach (Control item in SetLines(player))
		{
			vBoxContainer2.AddChild(item, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private IEnumerable<Control> SetLines(Combatant p)
	{
		List<IGrouping<string, SetTierState>> list = (from t in SetRules.Evaluate(GameDataProvider.Shared, p).Tiers
			where t.EquippedPieces > 0
			group t by t.Code into source
			orderby source.First().EquippedPieces descending
			select source).ToList();
		if (list.Count == 0)
		{
			Label label = Row("未穿戴任何套裝——同系列防具穿到指定件數就會自動生效（武器不計件數）。", CDim, 13);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(466f, 0f);
			yield return label;
			yield break;
		}
		foreach (IGrouping<string, SetTierState> g in list)
		{
			int owned = g.First().EquippedPieces;
			int num = g.Count((SetTierState t) => t.Active);
			yield return Row($"{g.First().DisplayName}\u3000已裝備 {owned} 件" + ((num > 0) ? $"\u3000生效 {num} 階" : "\u3000尚未生效"), (num > 0) ? CGood : CText, 13);
			foreach (SetTierState item in g.OrderBy((SetTierState t) => t.RequiredPieces))
			{
				string value = (item.Active ? "✓" : $"還差 {item.RequiredPieces - owned} 件");
				Label label2 = Row($"\u3000\u3000{item.RequiredPieces} 件\u3000{value}\u3000{item.Description}", item.Active ? CGood : CDim, 12);
				label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				label2.CustomMinimumSize = new Vector2(450f, 0f);
				yield return label2;
			}
		}
	}

	private static string WeightStateText(WeightReport w)
	{
		return w.LoadTier switch
		{
			0 => "✓ 負重正常：未阻擋自然回復", 
			1 => $"⚠ 過重 {w.Percent}%：自然回復停止（降到 {50}% 以下恢復）", 
			2 => $"⚠ 過重 {w.Percent}%：無法攻擊與施法、自然回復停止", 
			_ => $"⚠ 超重 {w.Percent}%：無法攻擊與施法、自然回復停止", 
		};
	}

	private static GridContainer StatGrid()
	{
		GridContainer gridContainer = new GridContainer();
		gridContainer.Columns = 4;
		gridContainer.CustomMinimumSize = new Vector2(466f, 0f);
		gridContainer.AddThemeConstantOverride("h_separation", 10);
		gridContainer.AddThemeConstantOverride("v_separation", 3);
		return gridContainer;
	}

	private static void AddStat(GridContainer g, string name, string val)
	{
		Label label = new Label
		{
			Text = name,
			CustomMinimumSize = new Vector2(112f, 0f)
		};
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", CDim);
		Label label2 = new Label
		{
			Text = val,
			CustomMinimumSize = new Vector2(106f, 0f)
		};
		label2.AddThemeFontSizeOverride("font_size", 13);
		label2.AddThemeColorOverride("font_color", CText);
		g.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		g.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
	}

	private void OpenSkills()
	{
		VBoxContainer vBoxContainer = OpenPanel("技能 · " + _session.Build.ClassName, new Vector2(560f, 620f));
		Label label = Row("用技能書學技能（消耗 1 本）；已學的攻擊/治療技能可在狩獵『施放列』使用。", CDim, 13);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(524f, 0f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(Row("背包技能書", CGold, 15), forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(524f, 206f)
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(508f, 0f)
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(new ColorRect
		{
			Color = new Color(1f, 1f, 1f, 0.1f),
			CustomMinimumSize = new Vector2(524f, 1f)
		}, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(Row("已學技能", CGold, 15), forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer2 = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(524f, 206f)
		};
		VBoxContainer vBoxContainer3 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(508f, 0f)
		};
		vBoxContainer3.AddThemeConstantOverride("separation", 4);
		scrollContainer2.AddChild(vBoxContainer3, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer2, forceReadableName: false, InternalMode.Disabled);
		Label label2 = Row("", CDim, 13);
		label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label2.CustomMinimumSize = new Vector2(524f, 0f);
		vBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		RebuildSkills(vBoxContainer2, vBoxContainer3, label2);
	}

	private void RebuildSkills(VBoxContainer bookList, VBoxContainer knownList, Label status)
	{
		foreach (Node child in bookList.GetChildren())
		{
			child.QueueFree();
		}
		foreach (Node child2 in knownList.GetChildren())
		{
			child2.QueueFree();
		}
		GameData data = GameDataProvider.Shared;
		Combatant player = _session.Player;
		IReadOnlyList<SkillBookInventoryEntry> readOnlyList = SkillLearningRules.InventoryEntries(data, player);
		if (readOnlyList.Count == 0)
		{
			bookList.AddChild(Row("（背包沒有技能書）", CDim, 13), forceReadableName: false, InternalMode.Disabled);
		}
		foreach (SkillBookInventoryEntry item in readOnlyList)
		{
			string skillId = item.Evaluation.SkillId;
			HBoxContainer hBoxContainer = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(500f, 0f)
			};
			hBoxContainer.AddThemeConstantOverride("separation", 8);
			VBoxContainer vBoxContainer = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			vBoxContainer.AddChild(Row($"{ItemName(item.Evaluation.ItemKey)} ×{item.Quantity}", CGold), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(Row(item.Evaluation.Allowed ? $"→ 學會：{SkillInfo.Name(skillId)}（{SkillInfo.TypeLabel(skillId)}）" : ("✗ " + LearnFailText(item.Evaluation.Failure)), item.Evaluation.Allowed ? CDim : CBad, 12), forceReadableName: false, InternalMode.Disabled);
			hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "學習",
				CustomMinimumSize = new Vector2(84f, 40f)
			};
			button.Disabled = !item.Evaluation.Allowed;
			string uid = item.ItemUid;
			button.Pressed += delegate
			{
				SkillLearningResult result = SkillLearningRules.TryLearn(data, player, uid);
				if (result.Success)
				{
					SaveManager.Save(_session);
					SetStatus(status, ItemActivation.SkillLearningSuccessText(result), CGood);
					RebuildSkills(bookList, knownList, status);
				}
				else
				{
					SetStatus(status, "無法學習：" + LearnFailText(result.Failure), CBad);
				}
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			bookList.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
			bookList.AddChild(new ColorRect
			{
				Color = new Color(1f, 1f, 1f, 0.06f),
				CustomMinimumSize = new Vector2(500f, 1f)
			}, forceReadableName: false, InternalMode.Disabled);
		}
		List<ClassSkillEntry> list = (from s in ClassKitRegistry.SkillsFor(player, data)
			where s.Learned || s.Granted
			select s).ToList();
		if (list.Count == 0)
		{
			knownList.AddChild(Row("（尚未學會任何技能）", CDim, 13), forceReadableName: false, InternalMode.Disabled);
		}
		foreach (ClassSkillEntry item2 in list)
		{
			bool num = SkillInfo.IsProcOnly(item2.SkillId);
			bool flag = !num && SkillInfo.IsCastable(item2.SkillId);
			VBoxContainer vBoxContainer2 = new VBoxContainer
			{
				CustomMinimumSize = new Vector2(500f, 0f)
			};
			vBoxContainer2.AddChild(Row($"{(item2.Granted ? "◆ " : "")}{SkillInfo.Name(item2.SkillId)}\u3000[{SkillInfo.TypeLabel(item2.SkillId)}]", CText), forceReadableName: false, InternalMode.Disabled);
			object obj;
			if (!num)
			{
				if (!flag)
				{
					obj = "施放待核心" + (item2.Granted ? " · 裝備授予" : "");
				}
				else
				{
					string skillId2 = item2.SkillId;
					JsonObject jsonObject = data.Skill(item2.SkillId);
					obj = "可施放 · " + SkillInfo.ResourceLabel(skillId2, (jsonObject != null) ? RelicConditionalCombatRules.SkillManaCost(data, player, item2.SkillId, CombatModifierRules.SkillMpCost(player, jsonObject, item2.SkillId)) : SkillInfo.Mp(item2.SkillId));
				}
			}
			else
			{
				obj = "由武器攻擊自動觸發";
			}
			string text = (string)obj;
			vBoxContainer2.AddChild(Row(text, flag ? CGood : CDim, 11), forceReadableName: false, InternalMode.Disabled);
			string text2 = SkillInfo.UsageDescription(item2.SkillId);
			if (text2.Length > 0)
			{
				vBoxContainer2.AddChild(Row(text2, CDim, 11), forceReadableName: false, InternalMode.Disabled);
			}
			knownList.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private static string LearnFailText(SkillLearningFailure f)
	{
		return f switch
		{
			SkillLearningFailure.ClassMismatch => "職業不符", 
			SkillLearningFailure.LevelTooLow => "等級不足", 
			SkillLearningFailure.ElementNotSelected => "需先選定精靈屬性", 
			SkillLearningFailure.ElementMismatch => "屬性不符", 
			SkillLearningFailure.NotSkillBook => "非技能書", 
			SkillLearningFailure.SkillDefinitionMissing => "技能定義缺失", 
			SkillLearningFailure.SkillReferenceMissing => "技能參照缺失", 
			SkillLearningFailure.ItemNotFound => "找不到技能書", 
			SkillLearningFailure.AlreadyLearned => "已經學會", 
			_ => "無法學習", 
		};
	}

	private void ActivateBagItem(ItemStack stack, Action refreshBag, Label status)
	{
		GameData shared = GameDataProvider.Shared;
		switch (ItemActivation.Classify(shared, _session.Player, stack))
		{
		case ItemAction.Equip:
			DoEquip(stack.Uid, refreshBag, status);
			break;
		case ItemAction.MainLight:
			var (flag2, text3) = ItemActivation.UseMainLight(shared, _session.Player, stack);
			SetStatus(status, flag2 ? ("✓ " + text3) : text3, flag2 ? CGood : CBad);
			if (flag2)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.MainWand:
			SetStatus(status, "魔杖需在狩獵地圖指定敵人使用", CDim);
			break;
		case ItemAction.MonsterCard:
		{
			MonsterCardToggleResult result = MonsterCardPartyRules.Toggle(shared, _session.Party, _session.Player, stack, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LiveEngine);
			if (!result.Success)
			{
				SetStatus(status, MonsterCardPartyRules.FailureText(result), CBad);
				break;
			}
			string text5 = shared.Mob(result.MobKey)?["n"]?.GetValue<string>() ?? result.MobKey;
			SaveManager.Save(_session);
			refreshBag();
			SetStatus(status, result.Joined ? ("✓ " + text5 + " 出戰") : ("✓ " + text5 + " 已收回卡片；冷卻 5 分鐘"), CGood);
			break;
		}
		case ItemAction.Painwand:
			SetStatus(status, "創造怪物魔杖只能在 mapids.painwand 允許的狩獵地圖使用。", CDim);
			break;
		case ItemAction.Elixir:
			var (flag13, text18) = ItemActivation.UseElixir(shared, _session.Player, stack);
			SetStatus(status, flag13 ? ("✓ " + text18) : ("無法使用：" + text18), flag13 ? CGood : CBad);
			if (flag13)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.Consumable:
			var (flag4, text8) = ItemActivation.UseConsumable(shared, _session.Player, stack.Uid, _potionRng);
			if (!flag4)
			{
				SetStatus(status, "無法使用：" + text8, CBad);
				break;
			}
			SaveManager.Save(_session);
			SetStatus(status, "✓ " + text8, CGood);
			refreshBag();
			break;
		case ItemAction.UncurseScroll:
			var (flag6, text10) = ItemActivation.UseUncurseScroll(_session.Player, stack);
			if (flag6)
			{
				CombatantBuilder.RefreshPlayer(_session.Player, shared);
			}
			SetStatus(status, flag6 ? ("✓ " + text10) : ("無法使用：" + text10), flag6 ? CGood : CBad);
			if (flag6)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.IdentifyScroll:
			OpenIdentifyScrollPanel(stack.Uid);
			break;
		case ItemAction.SoulOrbContainer:
			var (flag7, text11) = ItemActivation.UseSoulOrbContainer(shared, _session.Player, stack);
			SetStatus(status, flag7 ? ("✓ " + text11) : ("無法使用：" + text11), flag7 ? CGood : CBad);
			if (flag7)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.RoiBag:
			var (flag14, text19) = ItemActivation.UseRoiBag(shared, _session.Player, stack, _potionRng);
			SetStatus(status, flag14 ? ("✓ " + text19) : ("無法開啟：" + text19), flag14 ? CGood : CBad);
			if (flag14)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.IvoryQuiver:
			var (flag3, text4) = ItemActivation.UseIvoryQuiver(shared, _session.Player, stack, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			SetStatus(status, flag3 ? ("✓ " + text4) : ("無法使用：" + text4), flag3 ? CGood : CBad);
			if (flag3)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.Cooking:
			var (flag5, text9) = ItemActivation.UseCooking(shared, _session.Player, stack);
			if (flag5)
			{
				CombatantBuilder.RefreshPlayer(_session.Player, shared);
			}
			SetStatus(status, flag5 ? ("✓ " + text9) : ("無法食用：" + text9), flag5 ? CGood : CBad);
			if (flag5)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.PolymorphScroll:
			UsePolymorphScroll(stack, refreshBag, status);
			break;
		case ItemAction.OrcEmissaryPolymorph:
			var (flag11, text16) = ItemActivation.UseOrcEmissaryPolymorph(shared, _session.Player, stack.Uid);
			SetStatus(status, flag11 ? ("✓ " + text16) : ("無法使用：" + text16), flag11 ? CGood : CBad);
			if (flag11)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.DarkEntBark:
		{
			DarkEntBarkResult darkEntBarkResult = L1jTargetItemUseRules.TryUseDarkEntBark(shared, _session.Player, _session.Player, stack.Uid, _potionRng);
			string text2 = ((!darkEntBarkResult.Attempted) ? ItemActivation.DarkEntBarkFailureText(darkEntBarkResult.Failure) : (darkEntBarkResult.Transformed ? ("變形為 " + darkEntBarkResult.FormName) : "變形沒有生效；樹皮仍已消耗"));
			SetStatus(status, (darkEntBarkResult.Attempted ? "✓ " : "無法使用：") + text2, darkEntBarkResult.Attempted ? CGood : CBad);
			if (darkEntBarkResult.Attempted)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		}
		case ItemAction.MainTargetItem:
			OpenMainTargetItemPanel(stack.Uid);
			break;
		case ItemAction.LampOil:
			var (flag12, text17) = ItemActivation.UseLampOil(shared, _session.Player, stack);
			if (!flag12)
			{
				SetStatus(status, "無法使用：" + text17, CBad);
				break;
			}
			SaveManager.Save(_session);
			SetStatus(status, "✓ " + text17, CGood);
			refreshBag();
			break;
		case ItemAction.LearnSkill:
			var (flag9, text14) = ItemActivation.UseSkillBook(shared, _session.Player, stack);
			if (!flag9)
			{
				SetStatus(status, "無法學習：" + text14, CBad);
				break;
			}
			SaveManager.Save(_session);
			SetStatus(status, "✓ " + text14, CGood);
			refreshBag();
			break;
		case ItemAction.Resolvent:
			OpenResolventPanel(stack.Uid);
			break;
		case ItemAction.AttributeScroll:
			OpenAttributeScrollPanel(stack.Uid);
			break;
		case ItemAction.EnchantWeapon:
			OpenEnchantScrollPanel(stack.Uid, isWeapon: true);
			break;
		case ItemAction.EnchantArmor:
			OpenEnchantScrollPanel(stack.Uid, isWeapon: false);
			break;
		case ItemAction.SealScroll:
			OpenSealScrollPanel(stack.Uid, sealing: true);
			break;
		case ItemAction.UnsealScroll:
			OpenSealScrollPanel(stack.Uid, sealing: false);
			break;
		case ItemAction.RespecCandle:
			var (flag8, text12) = ItemActivation.UseRespecCandle(shared, _session.Player, stack);
			if (!flag8)
			{
				SetStatus(status, "無法使用：" + text12, CBad);
				break;
			}
			SaveManager.Save(_session);
			SetStatus(status, "✓ " + text12, CGood);
			refreshBag();
			break;
		case ItemAction.MagicDollContainer:
		{
			string text6 = MagicDollRules.RollBagReward(shared, new SeededCombatRandom((int)Time.GetTicksUsec()));
			if (!CombatInventory.TryRemove(_session.Player, stack.ItemKey, 1L))
			{
				SetStatus(status, "袋子不見了？", CBad);
				break;
			}
			CombatInventory.Add(_session.Player, text6, 1L);
			SaveManager.Save(_session);
			string text7 = shared.Item(text6)?["n"]?.GetValue<string>() ?? text6;
			SetStatus(status, "✓ 從袋子裡出現了「" + text7 + "」", CGood);
			refreshBag();
			break;
		}
		case ItemAction.MagicDollSummon:
			SetStatus(status, "魔法娃娃要在狩獵區召喚（城裡沒有牠跟得上的戰場）。", CDim);
			break;
		case ItemAction.PurifyStone:
			var (flag10, text15) = ItemActivation.UsePurifyStone(shared, _session.Player, stack, new SeededCombatRandom((int)Time.GetTicksUsec()));
			SetStatus(status, flag10 ? ("✓ " + text15) : text15, flag10 ? CGood : CDim);
			if (flag10)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.PetCollar:
		{
			PetCollarResult petCollarResult = PetCollarRules.Toggle(shared, _session.Pets, _session.Player, stack.Uid);
			if (!petCollarResult.Success)
			{
				SetStatus(status, "無法使用：" + ItemActivation.PetCollarFailureText(petCollarResult.Failure), CBad);
				break;
			}
			SaveManager.Save(_session);
			refreshBag();
			string text13 = petCollarResult.Pet?.DisplayName ?? "寵物";
			SetStatus(status, "✓ 已使用寵物哨子召喚 " + text13, CGood);
			break;
		}
		case ItemAction.PetEgg:
		{
			PetAcquisitionResult petAcquisitionResult = PetAcquisitionRules.TryHatchEgg(shared, _session.Pets, _session.Player, stack.Uid);
			if (!petAcquisitionResult.Success)
			{
				SetStatus(status, "無法使用：" + PetAcquisitionText.FailText(petAcquisitionResult.Failure), CBad);
				break;
			}
			SaveManager.Save(_session);
			refreshBag();
			SetStatus(status, "✓ 孵化 " + petAcquisitionResult.PetForm + "，已取得專屬項圈", CGood);
			break;
		}
		case ItemAction.PetTamingFood:
			SetStatus(status, "捕捉食物要在狩獵區點選活著的怪物給予。", CDim);
			break;
		case ItemAction.PetEvolutionFruit:
			SetStatus(status, "進化果實要在狩獵區點選出戰中的寵物給予。", CDim);
			break;
		case ItemAction.PetWhistle:
			SetStatus(status, "目前沒有狩獵中的寵物可召回。", CDim);
			break;
		case ItemAction.ReturnScroll:
			SetStatus(status, "已在安全城鎮村莊中，無需使用回家的卷軸。", CDim);
			break;
		case ItemAction.TeleportScroll:
		{
			var itemDef = GameDataProvider.Shared.Item(stack.ItemKey) as JsonObject;
			string targetMap = itemDef?["teleportMap"]?.GetValue<string>() ?? itemDef?["teleport_map"]?.GetValue<string>() ?? itemDef?["targetMap"]?.GetValue<string>() ?? "";
			int targetX = itemDef?["teleportX"]?.GetValue<int>() ?? itemDef?["teleport_x"]?.GetValue<int>() ?? 0;
			int targetY = itemDef?["teleportY"]?.GetValue<int>() ?? itemDef?["teleport_y"]?.GetValue<int>() ?? 0;
			int l1jId = CombatSkill.ReadInt(itemDef ?? new JsonObject(), "l1jItemId");
			string itemName = itemDef?["n"]?.GetValue<string>() ?? stack.ItemKey;

			if (string.IsNullOrEmpty(targetMap))
			{
				targetMap = ItemActivation.ResolveFallbackTeleportMap(l1jId, stack.ItemKey, itemName);
			}

			if (targetMap == "random" || stack.ItemKey == "scroll_teleport" || l1jId == 40100)
			{
				SetStatus(status, "城鎮安全區內無法使用瞬間移動卷軸（請在狩獵區使用）。", CDim);
				break;
			}
			if (targetMap == "home" || stack.ItemKey == "scroll_return" || l1jId == 40079 || l1jId == 40095)
			{
				SetStatus(status, "已在安全城鎮村莊中，無需使用回家的卷軸。", CDim);
				break;
			}

			if (!string.IsNullOrEmpty(targetMap))
			{
				if (!CombatInventory.TryRemove(_session.Player, stack.ItemKey, 1L))
				{
					SetStatus(status, "無法使用：背包中已沒有這張傳送卷軸。", CBad);
					break;
				}

				_session.HuntMap = targetMap;
				if (targetX > 0 && targetY > 0)
				{
					_session.PendingHuntSpawn = (targetX, targetY);
				}
				else
				{
					_session.PendingHuntSpawn = null;
				}
				_session.PendingMapEntryLandmark = null;
				SaveManager.Save(_session);
				_onHunt();
				break;
			}

			SetStatus(status, "傳送類卷軸在安全區裡沒有作用（狩獵區才用得到）。", CDim);
			break;
		}
		case ItemAction.ReviveScroll:
			SetStatus(status, "復活卷軸要在狩獵中雙擊後點選死亡的隊員或寵物。", CDim);
			break;
		case ItemAction.PrideTravel:
		{
			if (!TowerOfInsolenceCatalog.TryResolveTravelItem(GameDataProvider.Shared, stack.ItemKey, out var travel))
			{
				SetStatus(status, "這個物品沒有可以直接使用的效果。", CDim);
				break;
			}
			if (travel.Kind == TowerTravelItemKind.TeleportScroll && !CombatInventory.TryRemove(_session.Player, stack.ItemKey, 1L))
			{
				SetStatus(status, "無法使用：背包中已沒有這張傳送卷軸。", CBad);
				break;
			}
			_session.HuntMap = travel.DestinationMapKey;
			_session.PendingMapEntryLandmark = travel.ArrivalLandmarkId;
			SaveManager.Save(_session);
			_onHunt();
			break;
		}
		case ItemAction.PrideUnseal:
			var (flag, text) = ItemActivation.UsePrideUnseal(GameDataProvider.Shared, _session.Player, stack);
			SetStatus(status, flag ? ("✓ " + text) : text, flag ? CGood : CBad);
			if (flag)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		case ItemAction.MagicScroll:
			var (flagScroll, textScroll) = ItemActivation.UseMagicScroll(GameDataProvider.Shared, _session.Player, stack);
			SetStatus(status, flagScroll ? ("✓ " + textScroll) : textScroll, flagScroll ? CGood : CBad);
			if (flagScroll)
			{
				SaveManager.Save(_session);
				refreshBag();
			}
			break;
		default:
			SetStatus(status, "這個物品沒有可以直接使用的效果。", CDim);
			break;
		}
	}

	private void DoEquip(string uid, Action refreshBag, Label status)
	{
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == uid);
		string itemKey = itemStack?.ItemKey ?? "";
		EquipmentEligibilityResult equipmentEligibilityResult = ((itemStack == null) ? EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.MissingItemDefinition, "") : EquipmentRules.Evaluate(GameDataProvider.Shared, _session.Player, itemStack));
		var (flag, text) = ItemActivation.Equip(GameDataProvider.Shared, _session.Player, uid, _potionRng);
		SetStatus(status, flag ? ("✓ " + text) : text, flag ? CGood : CBad);
		if (flag)
		{
			if (_session.Player.EquippedItems.TryGetValue(equipmentEligibilityResult.Slot, out ItemStack value))
			{
				QuickBar.RemapEquipmentAssignment(_session.QuickItems, uid, value.Uid, itemKey);
			}
			GameAudio.Instance?.PlayEquipment(itemKey, _session.Player.ClassId);
			SaveManager.Save(_session);
			refreshBag();
		}
	}

	private void OpenCollections()
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		Vector2 vector = new Vector2(683f, Mathf.Min(560f, _panelViewport.Y - 48f));
		Control node = CollectionBookUi.Build(_session, AtlasBridge.Resolve(this), CollectionBooks, (_panelViewport - vector) * 0.5f, vector, CloseOverlay, ItemName, CollectionBonusText(_session));
		control.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = "收集冊";
	}

	internal static string CollectionCatBonusText(CollectionCategoryProgress cat)
	{
		if (cat.BonusStat.Length == 0 && cat.BonusLabel.Length == 0)
		{
			return "";
		}
		string text = ((cat.Book == CollectionBookKind.Card) ? $"{CollStatLabel(cat.BonusStat)}+{cat.BonusValue:0.##}（階級 {cat.Tier}）" : ((cat.BonusLabel.Length > 0) ? cat.BonusLabel : $"{CollStatLabel(cat.BonusStat)}+{cat.BonusValue:0.##}"));
		return (cat.BonusActive ? "完成：" : "完成可得：") + text;
	}

	internal static string CollStatLabel(string stat)
	{
		return stat switch
		{
			"allattr" => "全屬性", 
			"mhp" => "最大HP", 
			"mmp" => "最大MP", 
			"dr" => "傷害減免", 
			"mr" => "抗魔", 
			"hpR" => "HP回復", 
			"mpR" => "MP回復", 
			"er" => "遠程迴避", 
			"ac" => "AC", 
			"weight" => "負重", 
			"petHit" => "寵物命中", 
			"potion" => "藥水回復%", 
			"extraMp" => "魔法點數", 
			"extraDmg" => "額外傷害", 
			"extraHit" => "額外命中", 
			"resFire" => "火抗", 
			"resWater" => "水抗", 
			"resWind" => "風抗", 
			"resEarth" => "地抗", 
			_ => stat, 
		};
	}

	internal static string CollectionBonusText(GameSession session)
	{
		CollectionBonusSummary bonuses = session.Collections.Bonuses;
		List<string> parts = new List<string>();
		P("HP", bonuses.MaxHp);
		P("MP", bonuses.MaxMp);
		P("傷害減免", bonuses.DamageReduction);
		P("抗魔", bonuses.MagicResist);
		P("HP回復", bonuses.HealthRegen);
		P("MP回復", bonuses.ManaRegen);
		P("遠程迴避", bonuses.Evasion);
		P("AC", bonuses.ArmorClassReduction);
		P("負重", bonuses.WeightCapacity);
		P("寵物命中", bonuses.PetHit);
		P("藥水回復", bonuses.PotionHealingPercent, "%");
		P("魔法點數", bonuses.ItemSpellPower);
		P("額外傷害", bonuses.ExtraDamage);
		P("額外命中", bonuses.ExtraHit);
		P("火抗", bonuses.ResistFire);
		P("水抗", bonuses.ResistWater);
		P("風抗", bonuses.ResistWind);
		P("地抗", bonuses.ResistEarth);
		if (parts.Count != 0)
		{
			return "目前加成：" + string.Join("\u3000", parts);
		}
		return "尚無收集加成——集滿一個分類即獲得永久加成（隊長與傭兵共用同一本冊子）";
		void P(string label, double v, string suffix = "")
		{
			if (v != 0.0)
			{
				parts.Add($"{label}+{v:0.##}{suffix}");
			}
		}
	}

	private static string StackLabel(ItemStack st, bool withQty = true)
	{
		if (!st.IsIdentified)
		{
			return L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, st) + ((withQty && st.Quantity > 1) ? $" ×{st.Quantity}" : "");
		}
		string text = "";
		if (st.Enhancement != 0)
		{
			text = text + EnhStr(st.Enhancement) + " ";
		}
		text += L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, st);
		text += AttributeScrollText.Suffix(st);
		if (st.BrokenBladeStacks > 0)
		{
			text += $"〔壞刀×{st.BrokenBladeStacks}〕";
		}
		if (withQty && st.Quantity > 1)
		{
			text += $" ×{st.Quantity}";
		}
		return text;
	}

	private void OpenMapMenu()
	{
		GameData data = GameDataProvider.Shared;
		Combatant player = _session.Player;
		MapAccessState state = MapAccessState.From(player);
		L1jGetbackCatalog getback = L1jGetbackCatalog.Load(data);
		long value = MapSelectionTravelRules.MinimumMMenuTravelPriceAdena(data);
		VBoxContainer vBoxContainer = OpenPanel("世界地圖 · 移動", new Vector2(680f, 600f));
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(640f, 486f)
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(620f, 0f)
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		Label status = Row($"\ud83c\udfe0 城鎮＝旅行\u3000·\u3000其餘＝前往狩獵\u3000·\u3000最低 {value:N0} 金幣", CDim, 13);
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		status.CustomMinimumSize = new Vector2(640f, 0f);
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
		IReadOnlyList<MapRegionDefinition> readOnlyList;
		try
		{
			readOnlyList = WorldMapCatalog.GetRegions(data);
		}
		catch
		{
			readOnlyList = Array.Empty<MapRegionDefinition>();
		}
		foreach (MapRegionDefinition item in readOnlyList)
		{
			List<MapDestination> list = (from d in WorldMapDestinationRules.Effective(item)
				where d.Kind == MapDestinationKind.Hunt || d.Kind == MapDestinationKind.Town
				select d).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			Label label = Row(item.Name, CGold, 15);
			label.CustomMinimumSize = new Vector2(620f, 0f);
			vBoxContainer2.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			HFlowContainer hFlowContainer = new HFlowContainer
			{
				CustomMinimumSize = new Vector2(620f, 0f)
			};
			hFlowContainer.AddThemeConstantOverride("h_separation", 6);
			hFlowContainer.AddThemeConstantOverride("v_separation", 6);
			foreach (MapDestination item2 in list)
			{
				MapDestination dest = item2;
				bool isTown = dest.Kind == MapDestinationKind.Town;
				bool flag = string.Equals(MapMenuCatalog.GroupKeyOf(dest), "village", StringComparison.Ordinal);
				bool num = !dest.HasFixedLanding && isTown && dest.Key == _townKey;
				MapAccessResult res = MapAccessRules.Evaluate(data, player, state, dest);
				long price = MapSelectionTravelRules.MMenuTravelPriceOf(data, getback, dest);
				bool flag2 = CombatWallet.Balance(player) >= price;
				bool walkOnly = !dest.HasFixedLanding && !isTown && MapLinks.IsWalkOnly(dest.MapKey);
				string value2 = (num ? "\ud83d\udccd " : (flag ? "\ud83c\udfe0 " : (walkOnly ? "\ud83d\udeb6 " : ((!res.Allowed) ? "\ud83d\udd12 " : (res.ConsumesItem ? "\ud83d\udddd " : "")))));
				Button button = new Button
				{
					Text = $"{value2}{dest.Name}（{price:N0}）",
					CustomMinimumSize = new Vector2(0f, 34f)
				};
				button.AddThemeFontSizeOverride("font_size", 13);
				if (num)
				{
					button.AddThemeColorOverride("font_color", CGood);
				}
				else if (walkOnly || !res.Allowed || !flag2)
				{
					button.AddThemeColorOverride("font_color", CDim);
				}
				else if (flag)
				{
					button.AddThemeColorOverride("font_color", CTownLink);
				}
				if (walkOnly)
				{
					button.TooltipText = "只能從相鄰地圖走過去";
				}
				else if (res.Allowed && res.ConsumesItem)
				{
					button.TooltipText = $"傳送費用 {price:N0} 金幣\n進入將消耗：{ItemName(res.ConsumedItemKey)}";
				}
				else
				{
					button.TooltipText = $"傳送費用 {price:N0} 金幣";
				}
				button.Pressed += delegate
				{
					if (walkOnly)
					{
						SetStatus(status, "\ud83d\udeb6 " + dest.Name + "：只能從相鄰地圖走過去", CDim);
					}
					else if (!res.Allowed)
					{
						SetStatus(status, "\ud83d\udd12 " + dest.Name + "：" + MapFailText(res), CBad);
					}
					else
					{
						(double, double)? pendingHuntSpawn = ResolveTownMapFixedLanding(dest);
						if (dest.HasFixedLanding && !pendingHuntSpawn.HasValue)
						{
							SetStatus(status, "無法前往 " + dest.Name + "：指定落點不可用", CBad);
						}
						else if (CombatWallet.Balance(player) < price)
						{
							SetStatus(status, $"金幣不足：前往 {dest.Name} 需要 {price:N0}", CBad);
						}
						else
						{
							MapAccessResult r = MapAccessRules.TryEnter(data, player, state, dest);
							if (!r.Allowed)
							{
								SetStatus(status, "無法前往：" + MapFailText(r), CBad);
							}
							else if (!CombatWallet.TryCharge(player, price))
							{
								SetStatus(status, "扣款失敗：" + dest.Name, CBad);
							}
							else
							{
								CloseOverlay();
								if (isTown)
								{
									_session.TownKey = dest.Key;
									_session.PendingHuntSpawn = null;
									_session.PendingMapEntryLandmark = null;
									_session.LastHuntMap = "";
									SaveManager.Save(_session);
									_onTravel();
								}
								else
								{
									_session.HuntMap = dest.MapKey;
									_session.PendingHuntSpawn = pendingHuntSpawn;
									_session.PendingMapEntryLandmark = null;
									_session.LastHuntMap = "";
									SaveManager.Save(_session);
									_onHunt();
								}
							}
						}
					}
				};
				hFlowContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			}
			vBoxContainer2.AddChild(hFlowContainer, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(new ColorRect
			{
				Color = new Color(1f, 1f, 1f, 0.06f),
				CustomMinimumSize = new Vector2(620f, 1f)
			}, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private static (double X, double Y)? ResolveTownMapFixedLanding(MapDestination destination)
	{
		if (!destination.HasFixedLanding)
		{
			return null;
		}
		string text = "res://assets/maps/" + destination.MapKey;
		if (!FileAccess.FileExists(text + "/map.json"))
		{
			return null;
		}
		MapTopology mapTopology = MapTopology.Load(text);
		if (!mapTopology.ContainsGameCell(destination.LandingGameX.Value, destination.LandingGameY.Value))
		{
			return null;
		}
		var (localX, localY) = mapTopology.ToLocalCell(destination.LandingGameX.Value, destination.LandingGameY.Value);
		if (!mapTopology.IsWalkableCell(localX, localY))
		{
			return null;
		}
		return mapTopology.DisplayPixelCenter(localX, localY);
	}

	private static string MapFailText(MapAccessResult r)
	{
		return r.Failure switch
		{
			MapAccessFailure.ClassicHidden => "此地圖已停用", 
			MapAccessFailure.MissingQuest => "需完成前置任務", 
			MapAccessFailure.MissingHeldKey => "需持有：" + ItemName(r.RequirementKey), 
			MapAccessFailure.MissingConsumedKey => "需鑰匙：" + ItemName(r.RequirementKey), 
			MapAccessFailure.MissingPrideBoss => "需先擊敗前層頭目", 
			MapAccessFailure.MissingPrideAccessItem => $"需傲慢之塔 {r.RequiredValue} 樓通行證／傳送符", 
			MapAccessFailure.MissingItemDefinition => "缺物品定義", 
			MapAccessFailure.CorruptInventory => "背包狀態異常", 
			_ => "無法進入", 
		};
	}

	private static string EnhStr(int n)
	{
		if (n >= 0)
		{
			return $"+{n}";
		}
		return $"−{-n}";
	}

	private void OpenShop(string shopName)
	{
		if (L1jShopCatalog.TryResolveShopNpcId(GameDataProvider.Shared, shopName, out var npcId))
		{
			OpenShopByNpcId(npcId, shopName);
		}
	}

	private void OpenShopByNpcId(int npcId, string shopName)
	{
		CloseOverlay();
		Control overlay = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		ClassicShopView shop = new ClassicShopView(_panelViewport, "商店 · " + shopName, CloseOverlay);
		overlay.AddChild(shop, forceReadableName: false, InternalMode.Disabled);
		AddChild(overlay, forceReadableName: false, InternalMode.Disabled);
		_overlay = overlay;
		_overlayTitle = "商店";
		shop.BuyButton.Pressed += delegate
		{
			shop.SetBuying(buying: true);
			CloseSellBag();
			SetStatus(shop.Status, "商店 · " + shopName, CDim);
			RebuildShopBuy(shop, npcId, resetScroll: true);
		};
		shop.SellButton.Pressed += delegate
		{
			shop.SetBuying(buying: false);
			RebuildShopSell(shop, npcId, resetScroll: true);
			OpenSellBag(overlay, shop, npcId);
		};
		shop.SetBuying(buying: true);
		RebuildShopBuy(shop, npcId, resetScroll: true);
	}

	private void CloseSellBag()
	{
		_sellBag?.QueueFree();
		_sellBag = null;
		_sellBagRefresh = null;
	}

	private void OpenSellBag(Control overlay, ClassicShopView shop, int npcId)
	{
		CloseSellBag();
		_sellBagPage = 0;
		float num = shop.Position.X + shop.Size.X + 8f;
		if (num + 309f > _panelViewport.X)
		{
			num = Mathf.Max(0f, shop.Position.X - 309f - 8f);
		}
		ClassicWebWindows.BagShell shell = ClassicWebWindows.CreateBagShell(new Vector2(num, shop.Position.Y), CloseSellBag, "點物品賣出整疊");
		shell.Tabs.SelectFirstOccupied(GameDataProvider.Shared, _session.Player.InventoryStacks.Select((ItemStack stack) => stack.ItemKey));
		_sellBagRefresh = delegate
		{
			RebuildSellBagGrid(shell.Grid, shell.Status, shell.Page, shell.Previous, shell.Next, shop, npcId, shell.Tabs.Selected);
		};
		shell.Tabs.Changed += delegate
		{
			_sellBagPage = 0;
			_sellBagRefresh?.Invoke();
		};
		shell.Previous.Pressed += delegate
		{
			if (_sellBagPage > 0)
			{
				_sellBagPage--;
				_sellBagRefresh?.Invoke();
			}
		};
		shell.Next.Pressed += delegate
		{
			_sellBagPage++;
			_sellBagRefresh?.Invoke();
		};
		_sellBagRefresh();
		overlay.AddChild(shell.Panel, forceReadableName: false, InternalMode.Disabled);
		_sellBag = shell.Panel;
	}

	private void RebuildSellBagGrid(BagGrid grid, Label status, Label pageLabel, Button previous, Button next, ClassicShopView shop, int npcId, ClassicInventoryTab tab)
	{
		foreach (Node child in grid.GetChildren())
		{
			grid.RemoveChild(child);
			child.QueueFree();
		}
		GameData data = GameDataProvider.Shared;
		IReadOnlyList<ItemStack> readOnlyList = _session.Player.InventoryStacks.Where((ItemStack stack) => ClassicInventoryTabRules.Matches(data, stack.ItemKey, tab)).ToList();
		int num = 24;
		int num2 = Math.Max(1, (readOnlyList.Count + num - 1) / num);
		_sellBagPage = Math.Clamp(_sellBagPage, 0, num2 - 1);
		int num3 = _sellBagPage * num;
		for (int num4 = 0; num4 < num; num4++)
		{
			int num5 = num3 + num4;
			if (num5 >= readOnlyList.Count)
			{
				grid.AddChild(new InventoryGridSlot
				{
					MouseFilter = MouseFilterEnum.Ignore
				}, forceReadableName: false, InternalMode.Disabled);
				continue;
			}
			ItemStack itemStack = readOnlyList[num5];
			L1jShopFailure l1jShopFailure = L1jShopRules.SellRefusalOf(data, _session.Player, npcId, itemStack);
			long num6 = ((l1jShopFailure == L1jShopFailure.None) ? L1jShopRules.AssessedPriceFor(data, npcId, itemStack.ItemKey, itemStack.Blessing) : 0);
			ItemStack captured = itemStack;
			string tooltipText = L1jItemIdentityRules.DisplayName(data, itemStack) + ((itemStack.Quantity > 1) ? $" ×{itemStack.Quantity:N0}" : "") + ItemInstanceText.DetailTooltip(data, itemStack) + "\n" + l1jShopFailure switch
			{
				L1jShopFailure.None => $"賣出可得 {num6 * itemStack.Quantity:N0} 金\n點擊賣出整疊", 
				L1jShopFailure.Locked => "\ud83d\udd12 已鎖定，無法賣出", 
				_ => SellFailText(l1jShopFailure), 
			};
			InventoryGridSlot inventoryGridSlot = new InventoryGridSlot
			{
				ItemKey = itemStack.ItemKey,
				Draggable = false,
				SingleClick = true,
				Locked = itemStack.Locked,
				Quality = (ItemQualityColors.Highlighted(itemStack) ? new Color?(ItemQualityColors.Of(itemStack)) : ((Color?)null)),
				QualityFrame = ItemQualityColors.Framed(itemStack),
				FrameQuality = (ItemQualityColors.Framed(itemStack) ? new Color?(ItemQualityColors.FrameOf(itemStack)) : ((Color?)null)),
				BlessingState = (itemStack.IsIdentified ? itemStack.Blessing : ItemBlessing.Normal),
				BrokenBlade = (itemStack.IsIdentified && itemStack.BrokenBladeStacks > 0),
				TooltipText = tooltipText,
				OnActivate = ((l1jShopFailure != L1jShopFailure.None) ? null : ((Action)delegate
				{
					DoSell(captured, shop, npcId, status);
				}))
			};
			inventoryGridSlot.SetIcon(ItemIcons.For(itemStack.ItemKey));
			inventoryGridSlot.SetCorner(ItemInstanceText.StackCorner(itemStack));
			grid.AddChild(inventoryGridSlot, forceReadableName: false, InternalMode.Disabled);
		}
		pageLabel.Text = $"{_sellBagPage + 1} / {num2}";
		previous.Disabled = _sellBagPage == 0;
		next.Disabled = _sellBagPage >= num2 - 1;
	}

	private void RebuildShopBuy(ClassicShopView shop, int npcId, bool resetScroll = false)
	{
		GameData shared = GameDataProvider.Shared;
		Combatant player = _session.Player;
		bool flag = L1jNpcSkillLearningRules.IsMainMagicInstructor(npcId);
		IReadOnlyList<L1jShopItem> obj = (flag ? L1jNpcSkillLearningRules.ShopOffers(shared, player, npcId) : L1jShopCatalog.SellList(shared, npcId));
		List<ClassicShopEntry> list = new List<ClassicShopEntry>(obj.Count);
		foreach (L1jShopItem item in obj)
		{
			long num = (flag ? item.SellPrice : L1jShopRules.LayTax(item.SellPrice, L1jShopRules.TaxPercentOf(shared, npcId)));
			bool flag2 = player.Gold >= num;
			string text = ((item.PackCount > 1) ? $" ×{item.PackCount}" : "");
			L1jShopItem captured = item;
			list.Add(new ClassicShopEntry(item.ItemKey, BlessingShopItemName(item) + text, $"單價 {num:N0} 金", flag2, (flag2 ? "點擊後輸入數量再購買" : "金幣不足") + ItemStatText.Suffix(shared, item.ItemKey), delegate
			{
				OpenBuyQuantityPrompt(captured, shop, npcId);
			}, item.Blessing));
		}
		shop.SetGold(player.Gold);
		shop.SetEntries(list, resetScroll);
		if (list.Count == 0)
		{
			SetStatus(shop.Status, (!flag) ? "這裡的技能書都改由怪物掉落，架上已無販售品（仍可在此賣出物品）。" : (L1jNpcSkillLearningRules.IsGeren(npcId) ? "吉倫目前沒有可購買的魔法書（技能書資料缺漏）。" : "目前沒有可購買的技能書（可能尚未達到可學階級或技能資料缺漏）。"), CDim);
		}
	}

	private void CloseBuyPrompt()
	{
		if (_buyPrompt != null && GodotObject.IsInstanceValid(_buyPrompt))
		{
			_buyPrompt.QueueFree();
		}
		_buyPrompt = null;
	}

	private void OpenBuyQuantityPrompt(L1jShopItem offer, ClassicShopView shop, int npcId)
	{
		CloseBuyPrompt();
		long unit;
		long maximum;
		LineEdit amount;
		Label total;
		if (shop.GetParent() is Control control)
		{
			unit = L1jShopRules.LayTax(offer.SellPrice, L1jShopRules.TaxPercentOf(GameDataProvider.Shared, npcId));
			if (L1jNpcSkillLearningRules.IsMainMagicInstructor(npcId))
			{
				unit = offer.SellPrice;
			}
			long value = ((unit > 0) ? Math.Max(1L, _session.Player.Gold / unit) : 999999L);
			maximum = Math.Clamp(value, 1L, 999999L);
			var (control2, control3) = ClassicPromptFrame.Create(shop.Position + new Vector2((shop.Size.X - ClassicPromptFrame.FrameSize.X) * 0.5f, 96f), CloseBuyPrompt, 20);
			control.AddChild(control2, forceReadableName: false, InternalMode.Disabled);
			_buyPrompt = control2;
			float x = ClassicPromptFrame.BodyRect.Size.X;
			string text = ((offer.PackCount > 1) ? $" ×{offer.PackCount}" : "");
			Color promptTitleColor = offer.Blessing switch
			{
				ItemBlessing.Blessed => Color.FromHtml("#ffd700".AsSpan()),
				ItemBlessing.Cursed => Color.FromHtml("#ff5555".AsSpan()),
				_ => Color.FromHtml("#e7dfc8".AsSpan())
			};
			Label label = MakePromptLabel(BlessingShopItemName(offer) + text, new Vector2(0f, -2f), 11, promptTitleColor, x);
			label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			control3.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			control3.AddChild(MakePromptLabel($"單價 {unit:N0} 金", new Vector2(0f, 14f), 9, CDim, x), forceReadableName: false, InternalMode.Disabled);
			control3.AddChild(MakePromptLabel("數量", new Vector2(0f, 31f), 9, Color.FromHtml("#bda86a".AsSpan()), 24f), forceReadableName: false, InternalMode.Disabled);
			amount = new LineEdit
			{
				Text = "1",
				Position = new Vector2(26f, 29f),
				CustomMinimumSize = new Vector2(56f, 16f),
				Alignment = HorizontalAlignment.Right,
				MaxLength = 7,
				TooltipText = $"1 ~ {maximum}"
			};
			amount.AddThemeFontSizeOverride("font_size", 10);
			amount.AddThemeConstantOverride("minimum_character_width", 0);
			string[] array = new string[4] { "normal", "focus", "read_only", "hover" };
			foreach (string text2 in array)
			{
				amount.AddThemeStyleboxOverride(text2, PromptInputStyle());
			}
			control3.AddChild(amount, forceReadableName: false, InternalMode.Disabled);
			total = MakePromptLabel("", new Vector2(0f, 48f), 9, CDim, x);
			total.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			control3.AddChild(total, forceReadableName: false, InternalMode.Disabled);
			amount.TextChanged += delegate
			{
				RefreshTotal();
			};
			RefreshTotal();
			TextureButton textureButton = ClassicTradeButtons.Confirm(delegate
			{
				long quantity = Quantity();
				amount.Text = quantity.ToString();
				CloseBuyPrompt();
				DoBuy(offer, shop, npcId, quantity);
			}, "確認購買");
			textureButton.StretchMode = TextureButton.StretchModeEnum.Scale;
			Vector2 size = (textureButton.CustomMinimumSize = new Vector2(44f, 14f));
			textureButton.Size = size;
			textureButton.Position = new Vector2(75f, 62f);
			control3.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);
			amount.GrabFocus();
			amount.SelectAll();
		}
		long Quantity()
		{
			if (!long.TryParse(amount.Text, out var result))
			{
				return 1L;
			}
			return Math.Clamp(result, 1L, maximum);
		}
		void RefreshTotal()
		{
			long num = Quantity();
			long num2 = unit * num;
			bool flag = num2 <= _session.Player.Gold;
			total.Text = $"合計 {num2:N0} 金" + ((num >= maximum) ? $" (上限 {maximum})" : "");
			total.AddThemeColorOverride("font_color", flag ? CDim : CBad);
		}
	}

	private static StyleBoxFlat PromptInputStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#12100c".AsSpan()),
			BorderColor = Color.FromHtml("#4a4034".AsSpan()),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 2f,
			ContentMarginRight = 2f,
			ContentMarginTop = 0f,
			ContentMarginBottom = 0f
		};
	}

	private static Label MakePromptLabel(string text, Vector2 position, int fontSize, Color color, float width)
	{
		Label label = new Label();
		label.Text = text;
		label.Position = position;
		label.Size = new Vector2(width, fontSize + 5);
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private void DoBuy(L1jShopItem offer, ClassicShopView shop, int npcId, long quantity = 1L)
	{
		Combatant player = _session.Player;
		long num = Math.Clamp(quantity, 1L, 999999L);
		if (L1jNpcSkillLearningRules.IsMainMagicInstructor(npcId))
		{
			L1jNpcSkillLearningResult l1jNpcSkillLearningResult = L1jNpcSkillLearningRules.TryBuyBook(GameDataProvider.Shared, player, npcId, offer.ItemKey, num);
			if (!l1jNpcSkillLearningResult.Success)
			{
				SetStatus(shop.Status, "購買失敗：" + NpcSkillLearningFailureText(l1jNpcSkillLearningResult.Failure), CBad);
				return;
			}
			string value = string.Join("、", l1jNpcSkillLearningResult.LearnedSkillIds.Select((string skillId) => SkillInfo.Name(skillId)).Distinct());
			long num2 = Math.Max(1L, num);
			string value2 = ((num2 > 1) ? $" ×{num2}" : "");
			SetStatus(shop.Status, $"購買 {value}{value2}，支付 {l1jNpcSkillLearningResult.GoldSpent:N0} 金幣", CGood);
			RefreshHeaderGold();
			RebuildShopBuy(shop, npcId);
		}
		else
		{
			L1jBuyResult l1jBuyResult = L1jShopRules.TryBuy(GameDataProvider.Shared, player, npcId, offer.ItemKey, num, offer.L1jItemId);
			if (l1jBuyResult.Success)
			{
				string text = ((l1jBuyResult.Quantity > 1) ? $" ×{l1jBuyResult.Quantity}" : "");
				Color buyStatusColor = offer.Blessing switch
				{
					ItemBlessing.Blessed => Color.FromHtml("#ffd700".AsSpan()),
					ItemBlessing.Cursed => Color.FromHtml("#ff5555".AsSpan()),
					_ => CGood
				};
				SetStatus(shop.Status, "購買 " + BlessingShopItemName(offer) + text, buyStatusColor);
				RefreshHeaderGold();
				RebuildShopBuy(shop, npcId);
			}
			else
			{
				SetStatus(shop.Status, "購買失敗：" + BuyFailText(l1jBuyResult.Failure), CBad);
			}
		}
	}

	private static string BuyFailText(L1jShopFailure failure)
	{
		return failure switch
		{
			L1jShopFailure.InsufficientGold => "金幣不足", 
			L1jShopFailure.Overweight => "背包放不下（負重或格數）", 
			L1jShopFailure.NotSold => "這家沒有賣這件物品", 
			L1jShopFailure.UnknownShop => "這位不是商人", 
			L1jShopFailure.PriceOverflow => "金額超過上限", 
			_ => "無法購買", 
		};
	}

	private void RebuildShopSell(ClassicShopView shop, int npcId, bool resetScroll = false)
	{
		Combatant player = _session.Player;
		IReadOnlyList<ShopBuybackLedger.Entry> readOnlyList = _session.Buyback.Entries(BuybackKey(npcId));
		shop.SetGold(player.Gold);
		if (readOnlyList.Count == 0)
		{
			shop.SetEntries(Array.Empty<ClassicShopEntry>(), resetScroll);
			SetStatus(shop.Status, "點背包裡的物品賣出", CDim);
			return;
		}
		long num = CombatWallet.Balance(player);
		List<ClassicShopEntry> list = new List<ClassicShopEntry>(readOnlyList.Count);
		foreach (ShopBuybackLedger.Entry item in readOnlyList)
		{
			bool flag = num >= item.TotalPrice;
			long sequence = item.Sequence;
			list.Add(new ClassicShopEntry(item.ItemKey, L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, item.Stack) + ((item.Quantity > 1) ? $" ×{item.Quantity}" : ""), $"買回 {item.TotalPrice:N0} 金", flag, flag ? "點擊買回（原賣出價）" : "金幣不足", delegate
			{
				DoBuyBack(sequence, shop, npcId);
			}, item.Stack.Blessing));
		}
		shop.SetEntries(list, resetScroll);
		SetStatus(shop.Status, $"可買回 {readOnlyList.Count} 筆（最多保留 10 筆）", CDim);
	}

	private void DoSell(ItemStack st, ClassicShopView shop, int npcId, Label slotStatus)
	{
		GameData shared = GameDataProvider.Shared;
		Combatant player = _session.Player;
		L1jSellResult l1jSellResult = L1jShopRules.TrySell(shared, player, npcId, st.Uid, st.Quantity, _session.Party, LiveEngine, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
		if (!l1jSellResult.Success)
		{
			SetStatus(shop.Status, "無法賣出：" + SellFailText(l1jSellResult.Failure), CBad);
			slotStatus.Text = "無法賣出：" + SellFailText(l1jSellResult.Failure);
			return;
		}
		ItemStack itemStack = st.Copy();
		_session.Buyback.Record(BuybackKey(npcId), itemStack.Copy(null, l1jSellResult.Quantity), (l1jSellResult.Quantity > 0) ? (l1jSellResult.GoldGained / l1jSellResult.Quantity) : 0);
		SaveManager.Save(_session);
		string value = L1jItemIdentityRules.DisplayName(shared, itemStack);
		SetStatus(shop.Status, $"賣出 {value} ×{l1jSellResult.Quantity}（{l1jSellResult.GoldGained:N0} 金）", CGood);
		slotStatus.Text = $"賣出 {value} ×{l1jSellResult.Quantity}";
		RefreshHeaderGold();
		RebuildShopSell(shop, npcId);
		_sellBagRefresh?.Invoke();
	}

	private void DoBuyBack(long sequence, ClassicShopView shop, int npcId)
	{
		ShopBuybackLedger.Entry entry = _session.Buyback.Entries(BuybackKey(npcId)).FirstOrDefault((ShopBuybackLedger.Entry candidate) => candidate.Sequence == sequence);
		ShopBuybackResult shopBuybackResult = _session.Buyback.TryBuyBack(GameDataProvider.Shared, _session.Player, BuybackKey(npcId), sequence);
		if (!shopBuybackResult.Success)
		{
			SetStatus(shop.Status, "無法買回：" + BuybackFailText(shopBuybackResult.Failure), CBad);
			return;
		}
		SaveManager.Save(_session);
		string value = (((object)entry == null) ? ItemName(shopBuybackResult.ItemKey) : L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, entry.Stack));
		SetStatus(shop.Status, $"買回 {value} ×{shopBuybackResult.Quantity}（{shopBuybackResult.GoldSpent:N0} 金）", CGood);
		RefreshHeaderGold();
		RebuildShopSell(shop, npcId);
		RefreshSellBagIfOpen(shop, npcId);
	}

	private static string BuybackKey(int npcId)
	{
		return npcId.ToString();
	}

	private void RefreshSellBagIfOpen(ClassicShopView shop, int npcId)
	{
		_sellBagRefresh?.Invoke();
	}

	private static string BuybackFailText(ShopBuybackFailure failure)
	{
		return failure switch
		{
			ShopBuybackFailure.EntryNotFound => "這一筆已經不在清單上", 
			ShopBuybackFailure.InsufficientGold => "金幣不足", 
			ShopBuybackFailure.InventoryOverflow => "背包放不下", 
			_ => "無法買回", 
		};
	}

	private static string SellFailText(L1jShopFailure failure)
	{
		return failure switch
		{
			L1jShopFailure.Locked => "已鎖定", 
			L1jShopFailure.Enchanted => "超出安定值的強化品商店不收", 
			L1jShopFailure.Equipped => "裝備中的物品不能賣", 
			L1jShopFailure.Sealed => "已封印，無法賣出", 
			L1jShopFailure.NotPurchased => "物品資料不存在，無法估價", 
			L1jShopFailure.ItemNotFound => "找不到物品", 
			L1jShopFailure.UnknownShop => "這位不是商人", 
			_ => "無效數量", 
		};
	}

	private void OpenExchange(string npcKey, string displayName, Action onEmpty)
	{
		GameData shared = GameDataProvider.Shared;
		IReadOnlyList<ExchangeOption> options;
		try
		{
			options = ExchangeRules.ExchangeOptions(shared, npcKey);
		}
		catch
		{
			options = Array.Empty<ExchangeOption>();
		}
		if (options.Count == 0)
		{
			onEmpty();
			return;
		}
		VBoxContainer vBoxContainer = OpenPanel("兌換 · " + displayName, new Vector2(600f, 560f));
		Label label = Row("交出指定物品或金幣換取獎勵；背包不夠時會自動動用共用倉庫（鎖定的物品不計）。", CDim, 13);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(556f, 0f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(560f, 388f)
		};
		VBoxContainer list = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(540f, 0f)
		};
		list.AddThemeConstantOverride("separation", 6);
		scrollContainer.AddChild(list, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		Label status = Row("", CDim, 13);
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		status.CustomMinimumSize = new Vector2(556f, 0f);
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
		Rebuild();
		void Rebuild()
		{
			foreach (Node child in list.GetChildren())
			{
				child.QueueFree();
			}
			Combatant player = _session.Player;
			foreach (ExchangeOption item in options)
			{
				long num = ExchangeRules.AffordableQuantity(player, item, _session.Warehouse);
				HBoxContainer hBoxContainer = new HBoxContainer
				{
					CustomMinimumSize = new Vector2(536f, 0f)
				};
				hBoxContainer.AddThemeConstantOverride("separation", 8);
				VBoxContainer vBoxContainer2 = new VBoxContainer
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill
				};
				vBoxContainer2.AddChild(Row($"{ItemName(item.RewardItemKey)} ×{item.RewardQuantity}", CGold, 15), forceReadableName: false, InternalMode.Disabled);
				Label label2 = Row("代價：" + ExchangeCostText(item), CDim, 12);
				label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				label2.CustomMinimumSize = new Vector2(430f, 0f);
				vBoxContainer2.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
				vBoxContainer2.AddChild((num > 0) ? Row($"✓ 目前可換 {num} 次", CGood, 12) : Row("✗ 材料或金幣不足", CBad, 12), forceReadableName: false, InternalMode.Disabled);
				hBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
				Button button = new Button
				{
					Text = "兌換",
					CustomMinimumSize = new Vector2(84f, 40f)
				};
				button.Disabled = num <= 0;
				string optionId = item.Id;
				button.Pressed += delegate
				{
					DoExchange(npcKey, optionId, status, Rebuild);
				};
				hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
				list.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
				list.AddChild(new ColorRect
				{
					Color = new Color(1f, 1f, 1f, 0.06f),
					CustomMinimumSize = new Vector2(536f, 1f)
				}, forceReadableName: false, InternalMode.Disabled);
			}
		}
	}

	private static string ExchangeCostText(ExchangeOption option)
	{
		List<string> list = new List<string>();
		if (option.GoldCost > 0)
		{
			list.Add($"金 {option.GoldCost}");
		}
		foreach (var (key, value) in option.ItemCosts)
		{
			list.Add($"{ItemName(key)}×{value}");
		}
		if (list.Count != 0)
		{
			return string.Join("，", list);
		}
		return "無";
	}

	private void DoExchange(string npcKey, string optionId, Label status, Action rebuild)
	{
		ExchangeResult r = ExchangeRules.TryExchange(GameDataProvider.Shared, _session.Player, npcKey, optionId, 1L, _session.Warehouse);
		if (!r.Success)
		{
			SetStatus(status, "兌換失敗：" + ExchangeFailText(r, npcKey, optionId), CBad);
			rebuild();
			return;
		}
		SaveManager.Save(_session);
		string value = ((r.BlessedQuantity > 0) ? $"\u3000✨ 祝福 {r.BlessedQuantity} 件" : "");
		SetStatus(status, $"✓ 兌換完成：{ItemName(r.ItemKey)} ×{r.ProducedQuantity}{value}", (r.BlessedQuantity > 0) ? CGold : CGood);
		RefreshHeaderGold();
		rebuild();
	}

	private string ExchangeFailText(ExchangeResult r, string npcKey, string optionId)
	{
		if (r.Failure != ExchangeFailure.InsufficientItem)
		{
			return ExchangeFailText(r);
		}
		ExchangeOption exchangeOption = ExchangeRules.ExchangeOptions(GameDataProvider.Shared, npcKey).FirstOrDefault((ExchangeOption row) => string.Equals(row.Id, optionId, StringComparison.Ordinal));
		if ((object)exchangeOption == null)
		{
			return ExchangeFailText(r);
		}
		IReadOnlyList<ExchangeShortfall> readOnlyList = ExchangeRules.MissingCosts(_session.Player, exchangeOption, 1L, _session.Warehouse);
		if (readOnlyList.Count == 0)
		{
			return ExchangeFailText(r);
		}
		string text = string.Join("、", readOnlyList.Select((ExchangeShortfall row) => $"{ItemName(row.ItemKey)}（需 {row.Required}・有 {row.Held}・缺 {row.Short}）"));
		return "材料不足，還缺：" + text;
	}

	private static string ExchangeFailText(ExchangeResult r)
	{
		return r.Failure switch
		{
			ExchangeFailure.InsufficientGold => "金幣不足", 
			ExchangeFailure.InsufficientItem => "材料不足（背包＋倉庫都算過了）", 
			ExchangeFailure.MissingItemDefinition => "缺少物品資料", 
			ExchangeFailure.InventoryOverflow => "背包已滿", 
			ExchangeFailure.GoldOverflow => "金幣已達上限", 
			ExchangeFailure.InvalidNpc => "這位 NPC 沒有可兌換的項目", 
			ExchangeFailure.InvalidOption => "找不到這個兌換項目", 
			ExchangeFailure.InvalidQuantity => "數量不正確", 
			ExchangeFailure.AttemptSequenceExhausted => "取得次數已達上限", 
			ExchangeFailure.UidExhausted => "物品編號已用盡", 
			ExchangeFailure.CorruptState => "兌換資料異常", 
			_ => "無法兌換", 
		};
	}

	private void RefreshHeaderGold()
	{
		Combatant player = _session.Player;
		_hdrLine2.Text = $"EXP {(long)player.Experience}";
	}

	private static void SetStatus(Label status, string text, Color col)
	{
		status.Text = text;
		status.AddThemeColorOverride("font_color", col);
	}

	private static string ItemName(string key)
	{
		if (key == "gold")
		{
			return "金";
		}
		return GameDataProvider.Shared.Item(key)?["n"]?.GetValue<string>() ?? key;
	}

	private static string BlessingShopItemName(L1jShopItem item)
	{
		string baseName = ItemName(item.ItemKey);
		string bPrefix = item.Blessing switch
		{
			ItemBlessing.Blessed when !baseName.StartsWith("祝福") && !baseName.StartsWith("受祝福") => "祝福的 ",
			ItemBlessing.Cursed when !baseName.StartsWith("受詛咒") && !baseName.StartsWith("詛咒") => "受詛咒的 ",
			_ => ""
		};
		return bPrefix + baseName;
	}

	private static Label Row(string text, Color col, int size = 14)
	{
		Label label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", col);
		return label;
	}

	private void OpenElfElementDialog(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		Combatant player = _session.Player;
		ClassicNpcDialogHandle dialog = OpenNpcDialog(displayName, Array.Empty<string>());
		if (!ElfElementRules.IsElf(player))
		{
			Say("四大元素只回應妖精族的呼喚。");
			Say("你的血脈與森林無關，我無法為你立下契約。", "#c8b98a");
			return;
		}
		bool changing = ElfElementRules.HasChosen(player);
		if (changing)
		{
			string text = ElfElementRules.DisplayName(player.ElfElement);
			long value = CombatWallet.Balance(player);
			Say("你目前與「" + text + "」相繫。");
			Say($"契約可以重締——代價是 {100000L:n0} 金幣。");
			Say("但是要記住：改換之後，你為舊屬性學過的技能全部使不出來。書上的字還在，回應你的元素卻換了。", "#e2938f");
			Say($"（你身上有 {value:n0} 金幣）", "#c8b98a");
		}
		else
		{
			Say("你身上有森林的氣息，卻還沒有元素的形狀。");
			Say("四大元素之中，只有一種會回應你。第一次的立約不收你分文。");
			Say("選定之後，與它相繫的精靈水晶才會為你敞開。");
			Say($"日後若要改換，得付出 {100000L:n0} 金幣，" + "而且舊屬性的技能會就此沉默。", "#c8b98a");
		}
		foreach (var element in ElfElementRules.Elements)
		{
			string item = element.Key;
			string item2 = element.Name;
			string elementKey = item;
			string elementName = item2;
			bool flag = changing && string.Equals(item, player.ElfElement, StringComparison.Ordinal);
			string text2 = (flag ? ("◇ 「" + elementName + "」（現在的契約）") : (changing ? $"◆ 改換為「{elementName}」（{100000L:n0} 金幣）" : ("◆ 我選擇「" + elementName + "」")));
			dialog.AddOption(text2, delegate
			{
				ConfirmElfElement(displayName, elementKey, elementName, changing);
			}, flag);
		}
		void Say(string text3, string colorHex = "#e8e2d2")
		{
			dialog.AddLine(text3, colorHex);
		}
	}

	private void ConfirmElfElement(string displayName, string elementKey, string elementName, bool changing)
	{
		Combatant player = _session.Player;
		string value = (changing ? ElfElementRules.DisplayName(player.ElfElement) : "");
		string text = (changing ? ($"確定要把契約從「{value}」改成「{elementName}」嗎？\n這會扣掉 {100000L:n0} 金幣，而且你為「{value}」學過的技能全部使不出來。\n" + "（技能不會消失——改回去就能再用。）") : ("你確定要與「" + elementName + "」立下契約嗎？\n第一次立約免費；日後改換要 " + $"{100000L:n0} 金幣。"));
		ClassicNpcDialogHandle classicNpcDialogHandle = OpenNpcDialog(displayName, new string[1] { text });
		classicNpcDialogHandle.AddOption(changing ? "確認改換契約" : "確認立下契約", delegate
		{
			if (changing ? ElfElementRules.TryChange(player, elementKey, GameDataProvider.Shared, out var failure) : ElfElementRules.TryChoose(player, elementKey, GameDataProvider.Shared, out failure))
			{
				SaveManager.Save(_session);
				GameAudio.Instance?.PlayUi("skillAction", 40.0);
			}
			OpenElfElementDialog(displayName);
		});
		classicNpcDialogHandle.AddOption("讓我再想想", delegate
		{
			OpenElfElementDialog(displayName);
		});
	}

	public void InitEmbeddedNpcHost(AtlasBridge atlas, GameSession session, Vector2 panelViewport, Action onHunt, Action onTravel, Action onExit)
	{
		_atlas = atlas;
		_session = session;
		_onHunt = onHunt;
		_onTravel = onTravel;
		_onExit = onExit;
		_panelViewport = panelViewport;
		_townKey = session.TownKey;
		_townBg = ((WorldMapCatalog.TryGetDestination(GameDataProvider.Shared, _townKey, out MapDestination destination) && (object)destination != null) ? destination.Name : _townKey);
		_hdrLine2 = new Label
		{
			Visible = false
		};
		AddChild(_hdrLine2, forceReadableName: false, InternalMode.Disabled);
		base.Position = Vector2.Zero;
		base.Size = panelViewport;
		base.MouseFilter = MouseFilterEnum.Ignore;
		base.ZIndex = 2500;
		SetProcess(enable: false);
	}

	public void SetEmbeddedTownContext(string townKey)
	{
		if (!string.IsNullOrWhiteSpace(townKey) && !string.Equals(_townKey, townKey, StringComparison.Ordinal))
		{
			_townKey = townKey;
			_townBg = ((WorldMapCatalog.TryGetDestination(GameDataProvider.Shared, _townKey, out MapDestination destination) && (object)destination != null) ? destination.Name : _townKey);
		}
	}

	public bool HasActiveOverlay => _overlay != null || (_buyPrompt != null && GodotObject.IsInstanceValid(_buyPrompt));

	public bool CloseTopEmbeddedWindow()
	{
		if (_buyPrompt != null && GodotObject.IsInstanceValid(_buyPrompt))
		{
			CloseBuyPrompt();
			return true;
		}
		if (_overlay == null)
		{
			return false;
		}
		CloseOverlay();
		return true;
	}

	public void OpenL1jShop(int npcId, string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenShopByNpcId(npcId, displayName);
	}

	public void OpenL1jWarehouse(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenWarehouse(displayName);
	}

	public void OpenL1jClanWarehouse(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenClanWarehouse(displayName);
	}

	public void OpenL1jExchange(string portNpcKey, string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(portNpcKey, "portNpcKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenExchange(portNpcKey, displayName, CloseOverlay);
	}

	public void OpenL1jCrafting(string displayName, IReadOnlyList<NpcActionDefinition> recipes)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		ArgumentNullException.ThrowIfNull(recipes, "recipes");
		OpenNpcCrafting(displayName, recipes);
	}

	public void OpenL1jSkillLearning(int npcId, string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenNpcSkillLearning(npcId, displayName);
	}

	public void OpenL1jHarborFerry(string portNpcKey, string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(portNpcKey, "portNpcKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenHarborFerry(portNpcKey, displayName, CloseOverlay);
	}

	public void OpenL1jElfElement(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenElfElementDialog(displayName);
	}

	public void OpenL1jPetStore(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenPetStore(displayName);
	}

	public void OpenL1jForgottenIslandTravel(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenForgottenIslandTravel(displayName);
	}

	public void OpenL1jDufaDialog(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenDufaDialog(displayName);
	}

	public void OpenL1jZeusGolem(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenZeusGolem(displayName);
	}

	public void OpenL1jFlameLabConsul(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenFlameLabConsulDialog(displayName);
	}

	public void OpenL1jClanHousekeeper(int npcId, string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenClanHousekeeper(npcId, displayName);
	}

	public void OpenL1jClanPanel(string displayName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName, "displayName");
		OpenClanPanel(displayName);
	}

	private void OpenFlameLabConsulDialog(string displayName)
	{
		string[] lines = new string[4] { "實驗室的門是我開的，回去的路自然也由我來開。", "研究員、輔佐官與鐵匠都在這座廳裡——素材備齊了就去找他們。", "至於管理者大人，他不喜歡站在同一個地方，你得自己在廳裡找找。", "……你若真想見炎魔大人，就把元素之支配者帶在身上。" };
		ClassicNpcDialogHandle classicNpcDialogHandle = OpenNpcDialog(displayName, lines);
		Label status = classicNpcDialogHandle.Status;
		classicNpcDialogHandle.AddOption("回到暗影神殿1樓", delegate
		{
			CloseOverlay();
			_session.HuntMap = "shadow_temple";
			_session.PendingMapEntryLandmark = "shadow_temple_1f_arrival_from_flame_lab";
			_onHunt();
		});
		classicNpcDialogHandle.AddOption($"前往{"炎魔房間"}（需持有 元素之支配者 ×{1}）", delegate
		{
			TryEnterBalrogRoom(status);
		});
		classicNpcDialogHandle.AttachStatus();
	}

	private void TryEnterBalrogRoom(Label status)
	{
		if (!BalrogRoomRules.TryEnter(GameDataProvider.Shared, _session.Player, out var failure))
		{
			SetStatus(status, BalrogRoomFailText(failure), CBad);
			return;
		}
		try
		{
			_session.HuntMap = "balrog_room";
			_session.PendingMapEntryLandmark = "balrog_room_arrival";
			SaveManager.Save(_session);
			CloseOverlay();
			_onHunt();
		}
		catch (Exception ex)
		{
			_session.HuntMap = "flame_shadow_lab";
			_session.PendingMapEntryLandmark = null;
			SaveManager.Save(_session);
			GD.PushError("[BalrogRoom] 換場失敗：" + ex.Message);
			SetStatus(status, "裂隙沒有打開。", CBad);
		}
	}

	private static string BalrogRoomFailText(BalrogRoomEntryFailure failure)
	{
		return failure switch
		{
			BalrogRoomEntryFailure.MissingPass => "你身上沒有元素之支配者。", 
			BalrogRoomEntryFailure.InvalidCatalog => "通往炎魔房間的裂隙尚未成形。", 
			_ => "無法前往炎魔房間。", 
		};
	}

	private void OpenForgottenIslandTravel(string displayName)
	{
		ClassicNpcDialogHandle classicNpcDialogHandle = OpenNpcDialog(displayName, new string[1] { $"main 船票路線：等級 {45} 以上，" + "持遺忘之島船票（依斯巴商店販售）登船。" });
		Label status = classicNpcDialogHandle.Status;
		classicNpcDialogHandle.AddOption("驗票前往遺忘之島", delegate
		{
			if (!ForgottenIslandTravelRules.TryBoard(_session.Player, out var failure))
			{
				string text = failure switch
				{
					ForgottenIslandTravelFailure.LevelTooLow => $"等級 {45} 以上才能登船。", 
					ForgottenIslandTravelFailure.MissingTicket => "需要遺忘之島船票；可先向依斯巴購買。", 
					ForgottenIslandTravelFailure.Incapacitated => "目前的狀態無法登船。", 
					_ => "目前無法登船。", 
				};
				SetStatus(status, text, CBad);
			}
			else
			{
				_session.HuntMap = "oblivion_island";
				_session.PendingMapEntryLandmark = "forgotten_island_arrival";
				SaveManager.Save(_session);
				CloseOverlay();
				_onHunt();
			}
		});
		classicNpcDialogHandle.AttachStatus();
	}

	private void OpenHarborFerry(string npcKey, string displayName, Action onEmpty)
	{
		HarborFerryRoute harborFerryRoute = HarborFerryCatalog.FindByNpc(npcKey);
		if ((object)harborFerryRoute == null)
		{
			onEmpty();
			return;
		}
		ClassicNpcDialogHandle classicNpcDialogHandle = OpenNpcDialog(displayName, new string[1] { $"等級 {5} 以上可搭船，船資 {100L:N0} 金幣。" });
		Label status = classicNpcDialogHandle.Status;
		classicNpcDialogHandle.AddOption("搭船前往" + harborFerryRoute.DestinationName, delegate
		{
			TryUseHarborFerry(npcKey, status);
		});
		classicNpcDialogHandle.AttachStatus();
	}

	private void TryUseHarborFerry(string npcKey, Label status)
	{
		if (!HarborFerryRules.TryTravel(_session.Player, _session.TownKey, npcKey, out HarborFerryRoute destination, out HarborFerryFailure failure) || (object)destination == null)
		{
			SetStatus(status, HarborFerryFailText(failure), CBad);
			return;
		}
		_session.TownKey = destination.DestinationTownKey;
		_session.HuntMap = destination.DestinationMapKey;
		_session.PendingMapEntryLandmark = destination.DestinationLandmarkId;
		SaveManager.Save(_session);
		CloseOverlay();
		_onHunt();
	}

	private static string HarborFerryFailText(HarborFerryFailure failure)
	{
		return failure switch
		{
			HarborFerryFailure.LevelTooLow => $"等級 {5} 以上才能搭船。", 
			HarborFerryFailure.InsufficientGold => $"金幣不足（需要 {100L:N0} 金幣）。", 
			HarborFerryFailure.Incapacitated => "目前的狀態無法搭船。", 
			HarborFerryFailure.WrongDepartureTown => "目前不在這條航線的出發港口。", 
			_ => "航線資料異常，暫時無法搭船。", 
		};
	}

	public override void _Input(InputEvent @event)
	{
		if (EmbeddedNpcHost || !(@event is InputEventKey { Pressed: not false, Echo: false } inputEventKey))
		{
			return;
		}
		Control control = GetViewport().GuiGetFocusOwner();
		if ((control is LineEdit || control is TextEdit) ? true : false)
		{
			return;
		}
		Key key = ((inputEventKey.Keycode != Key.None) ? inputEventKey.Keycode : inputEventKey.PhysicalKeycode);
		if (inputEventKey.CtrlPressed)
		{
			switch (key)
			{
			default:
			{
				Key num = key - 80;
				if ((ulong)num > 3uL)
				{
					return;
				}
				switch ((int)num)
				{
				default:
					return;
				case 3:
					ToggleClassicSkills();
					break;
				case 1:
					QuitToCharacterSelect();
					break;
				case 0:
					TogglePvp();
					break;
				case 2:
					return;
				}
				break;
			}
			case Key.A:
				ToggleClassicEquipment();
				break;
			case Key.B:
				ToggleCollections();
				break;
			}
			GetViewport().SetInputAsHandled();
		}
		else if (key == Key.M && !inputEventKey.ShiftPressed && !inputEventKey.AltPressed)
		{
			OpenMapMenu();
			GetViewport().SetInputAsHandled();
		}
		else if (key == Key.Tab && !inputEventKey.ShiftPressed && !inputEventKey.AltPressed)
		{
			ToggleBagPanel();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TogglePvp()
	{
		_session.PvpEnabled = !_session.PvpEnabled;
		RefreshTownNameColor();
		SaveManager.Save(_session);
	}

	private void RefreshTownNameColor()
	{
		_townNameLabel?.AddThemeColorOverride("font_color", _session.PvpEnabled ? Color.FromHtml("#ff5a4a".AsSpan()) : CGold);
	}

	private void ToggleCollections()
	{
		if (_overlay != null && _overlayTitle == "收集冊")
		{
			CloseOverlay();
		}
		else
		{
			OpenCollections();
		}
	}

	private void ToggleBagPanel()
	{
		if (_classicRightPanel != null && _classicRightKind == "bag")
		{
			CloseClassicRight();
		}
		else
		{
			OpenBag();
		}
	}

	private void QuitToCharacterSelect()
	{
		SaveManager.Save(_session);
		_onExit();
	}

	private void OpenClanHousekeeper(int npcId, string displayName)
	{
		if (L1jHouseCatalog.Load(GameDataProvider.Shared).TryByKeeper(npcId, out L1jHouseDefinition house) && (object)house != null && house.Operational)
		{
			_houseMessage = "";
			ClanStore.SettleHouse();
			BuildClanHousePanel(displayName, house);
		}
	}

	private void BuildClanHousePanel(string displayName, L1jHouseDefinition house)
	{
		L1jHouseCatalog l1jHouseCatalog = L1jHouseCatalog.Load(GameDataProvider.Shared);
		ClanBook clan = ClanStore.Book;
		VBoxContainer vBoxContainer = OpenPanel(house.Name + "・" + displayName, new Vector2(620f, 590f));
		Label status = Row("", CDim, 13);
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		status.CustomMinimumSize = new Vector2(576f, 0f);
		if (_houseMessage.Length > 0)
		{
			SetStatus(status, _houseMessage, _houseMessageColor);
			_houseMessage = "";
		}
		vBoxContainer.AddChild(Row(house.Location, CGold, 17), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(Row($"房屋編號 {house.HouseId}\u3000面積 {house.Area}\u3000起標 {100000L:N0} 金幣", CText, 13), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(Row("L1J-TW 原版：拍賣 5 天、房屋稅 2,000 金幣／10 天、地下盟屋 5,000,000 金幣。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
		if (!clan.Exists)
		{
			vBoxContainer.AddChild(Row("必須先由王族建立血盟。", CBad), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
			return;
		}
		if (clan.HouseBidPending)
		{
			L1jHouseDefinition value2;
			string value = (l1jHouseCatalog.ById.TryGetValue(clan.PendingHouseId, out value2) ? value2.Name : clan.PendingHouseId.ToString());
			vBoxContainer.AddChild(Row($"競標中：{value}，出價 {clan.PendingHouseBid:N0}，剩餘 {HouseTime(clan.PendingHouseAuctionDeadlineUnixMilliseconds)}。", CGold, 13), forceReadableName: false, InternalMode.Disabled);
		}
		if (!clan.OwnsHouse)
		{
			BuildHouseBid(vBoxContainer, status, displayName, house, clan);
			vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
			return;
		}
		L1jHouseDefinition valueOrDefault = l1jHouseCatalog.ById.GetValueOrDefault(clan.HouseId);
		vBoxContainer.AddChild(Row(((object)valueOrDefault == null) ? $"血盟持有房屋 {clan.HouseId}。" : $"血盟持有：{valueOrDefault.Name}（{valueOrDefault.Location}）。", CGood), forceReadableName: false, InternalMode.Disabled);
		if (clan.HouseId != house.HouseId)
		{
			vBoxContainer.AddChild(Row("這位女僕不是本血盟小屋的管理人。", CDim, 13), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
			return;
		}
		if (clan.HouseOnSale)
		{
			vBoxContainer.AddChild(Row($"出售中：底價 {clan.HouseSalePrice:N0}，剩餘 {HouseTime(clan.HouseSaleDeadlineUnixMilliseconds)}。", CGold, 13), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(Row("本作只有一個帳號血盟，不製造虛構買家；無人投標時原版流程會取消出售並保留小屋。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
			return;
		}
		vBoxContainer.AddChild(Row($"房屋稅期限：{HouseDeadline(clan.HouseTaxDeadlineUnixMilliseconds)}（剩餘 {HouseTime(clan.HouseTaxDeadlineUnixMilliseconds)}）", CText, 13), forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = $"繳納房屋稅 {2000L:N0}",
			CustomMinimumSize = new Vector2(230f, 38f),
			Disabled = (_session.Player.Gold < 2000)
		};
		button.Pressed += delegate
		{
			ClanHouseResult result = clan.TryPayHouseTax(_session.Player, _session.Identity, ClanStore.NowUnixMilliseconds());
			FinishHouseAction(result, status, displayName, house, "已繳納房屋稅，期限延長 10 天。");
		};
		vBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		if ((object)house.Basement != null)
		{
			if (!clan.HouseBasementPurchased)
			{
				Button button2 = new Button
				{
					Text = $"購買地下盟屋 {5000000L:N0}",
					CustomMinimumSize = new Vector2(280f, 38f),
					Disabled = (_session.Player.Gold < 5000000)
				};
				button2.Pressed += delegate
				{
					ClanHouseResult result = clan.TryPurchaseBasement(_session.Player, _session.Identity, basementAvailable: true);
					FinishHouseAction(result, status, displayName, house, "地下盟屋購買完成。");
				};
				vBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
			}
			else
			{
				bool flag = FileAccess.FileExists("res://assets/maps/" + house.Basement.MapKey + "/map.json");
				Button button3 = new Button
				{
					Text = "進入地下盟屋",
					CustomMinimumSize = new Vector2(200f, 38f),
					Disabled = !flag
				};
				button3.Pressed += delegate
				{
					EnterHouseBasement(house, status);
				};
				vBoxContainer.AddChild(button3, forceReadableName: false, InternalMode.Disabled);
				if (!flag)
				{
					vBoxContainer.AddChild(Row("地下盟屋地圖尚未完成轉換，暫時不能進入。", CBad, 12), forceReadableName: false, InternalMode.Disabled);
				}
			}
		}
		else
		{
			vBoxContainer.AddChild(Row("古魯丁小屋在 main 沒有地下盟屋地圖。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
		}
		if (clan.IsLeader(_session.Identity))
		{
			BuildHouseSale(vBoxContainer, status, displayName, house, clan);
		}
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
	}

	private void BuildHouseBid(VBoxContainer body, Label status, string displayName, L1jHouseDefinition house, ClanBook clan)
	{
		bool flag = clan.IsLeader(_session.Identity) && _session.Player.ClassId == "royal" && _session.Player.Level >= 15 && !clan.HouseBidPending;
		SpinBox amount = new SpinBox
		{
			MinValue = 100000.0,
			MaxValue = Math.Max(100000L, Math.Min(2000000000L, _session.Player.Gold)),
			Value = 100000.0,
			Step = 1.0,
			CustomMinimumSize = new Vector2(230f, 38f),
			Editable = (flag && _session.Player.Gold >= 100000)
		};
		body.AddChild(amount, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "參加競標",
			CustomMinimumSize = new Vector2(180f, 38f),
			Disabled = !amount.Editable
		};
		button.Pressed += delegate
		{
			ClanHouseResult result = clan.TryBidHouse(_session.Player, _session.Identity, house.HouseId, (long)amount.Value, 100000L, ClanStore.NowUnixMilliseconds());
			FinishHouseAction(result, status, displayName, house, "競標已受理；5 天後結標，出價金已扣除。");
		};
		body.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		if (!flag)
		{
			body.AddChild(Row("僅限 15 級以上王族盟主，且同時只能競標一間小屋。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void BuildHouseSale(VBoxContainer body, Label status, string displayName, L1jHouseDefinition house, ClanBook clan)
	{
		body.AddChild(new Control
		{
			CustomMinimumSize = new Vector2(0f, 4f)
		}, forceReadableName: false, InternalMode.Disabled);
		SpinBox amount = new SpinBox
		{
			MinValue = 100000.0,
			MaxValue = 2000000000.0,
			Value = 100000.0,
			Step = 1.0,
			CustomMinimumSize = new Vector2(230f, 38f)
		};
		body.AddChild(amount, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "委託出售（5 天）",
			CustomMinimumSize = new Vector2(210f, 38f)
		};
		button.Pressed += delegate
		{
			ClanHouseResult result = clan.TryListHouseForSale(_session.Identity, (long)amount.Value, ClanStore.NowUnixMilliseconds());
			FinishHouseAction(result, status, displayName, house, "小屋已委託出售；成交時原版賣方實收 90%。");
		};
		body.AddChild(button, forceReadableName: false, InternalMode.Disabled);
	}

	private void FinishHouseAction(ClanHouseResult result, Label status, string displayName, L1jHouseDefinition house, string successText)
	{
		if (!result.Success)
		{
			SetStatus(status, ClanHouseRules.FailureText(result.Failure), CBad);
			return;
		}
		ClanStore.Save();
		SaveManager.Save(_session);
		RefreshHeaderGold();
		SetStatus(status, successText, CGood);
		_houseMessage = successText;
		_houseMessageColor = CGood;
		BuildClanHousePanel(displayName, house);
	}

	private void EnterHouseBasement(L1jHouseDefinition house, Label status)
	{
		L1jHouseBasement basement = house.Basement;
		if ((object)basement == null || ClanStore.Book.HouseId != house.HouseId || !ClanStore.Book.HouseBasementPurchased)
		{
			SetStatus(status, "血盟尚未取得這間地下盟屋。", CBad);
			return;
		}
		string text = "res://assets/maps/" + basement.MapKey;
		if (!FileAccess.FileExists(text + "/map.json"))
		{
			SetStatus(status, "地下盟屋地圖尚未完成轉換。", CBad);
			return;
		}
		MapTopology mapTopology = MapTopology.Load(text);
		if (!L1jHouseCatalog.TryResolveBasementArrival(mapTopology, basement, out var cellX, out var cellY))
		{
			SetStatus(status, "main 落點在 3.8c 地形中找不到可進入的室內格。", CBad);
			return;
		}
		var (item, item2) = mapTopology.DisplayPixelCenter(cellX, cellY);
		_session.HuntMap = basement.MapKey;
		_session.PendingHuntSpawn = (item, item2);
		SaveManager.Save(_session);
		CloseOverlay();
		_onHunt();
	}

	private static string HouseTime(long deadlineUnixMilliseconds)
	{
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(Math.Max(0L, deadlineUnixMilliseconds - ClanStore.NowUnixMilliseconds()));
		if (timeSpan.TotalDays >= 1.0)
		{
			return $"{(int)timeSpan.TotalDays} 天 {timeSpan.Hours} 小時";
		}
		return $"{timeSpan.Hours} 小時 {timeSpan.Minutes} 分";
	}

	private static string HouseDeadline(long deadlineUnixMilliseconds)
	{
		if (deadlineUnixMilliseconds > 0)
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(deadlineUnixMilliseconds).ToLocalTime().ToString("yyyy/MM/dd HH:mm");
		}
		return "尚未設定";
	}

	private VBoxContainer OpenTownIdentifyFrame(string title, Vector2 size)
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop
		};
		var (control2, control3) = ClassicMapFrame.Create((_panelViewport - size) * 0.5f, size, CloseOverlay);
		control2.AddChild(ClassicMapFrame.Title(title), forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			Position = new Vector2(8f, 28f),
			Size = new Vector2(Mathf.Max(0f, control3.Size.X - 16f), Mathf.Max(0f, control3.Size.Y - 36f))
		};
		vBoxContainer.AddThemeConstantOverride("separation", 8);
		control3.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		control.AddChild(control2, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = title;
		return vBoxContainer;
	}

	private HBoxContainer TownIdentifyTargetRow(ItemStack stack, Action choose)
	{
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(0f, 38f);
		hBoxContainer.AddThemeConstantOverride("separation", 8);
		hBoxContainer.AddChild(ItemIcons.Slot(stack.ItemKey), forceReadableName: false, InternalMode.Disabled);
		Label label = Row(EquippedMark(stack) + StackLabel(stack), CText);
		label.CustomMinimumSize = new Vector2(400f, 34f);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Button button = new Button
		{
			Text = "鑑定",
			CustomMinimumSize = new Vector2(72f, 32f)
		};
		button.Pressed += choose;
		hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private void OpenIdentifyScrollPanel(string scrollUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		if (itemStack == null || !L1jIdentifyRules.IsScroll(shared, itemStack.ItemKey))
		{
			return;
		}
		VBoxContainer vBoxContainer = OpenTownIdentifyFrame("鑑定卷軸", new Vector2(640f, 500f));
		IReadOnlyList<ItemStack> readOnlyList = L1jIdentifyRules.EligibleTargets(_session.Player, scrollUid);
		int unappraisedCount = readOnlyList.Count((ItemStack s) => !s.IsIdentified);
		int scrollTotal = _session.Player.InventoryStacks.Where((ItemStack s) => L1jIdentifyRules.IsScroll(shared, s.ItemKey) && !s.Locked).Sum((ItemStack s) => (int)s.Quantity);
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 10);
		Label label = Row("選擇要鑑定的物品。選擇後會顯示物品能力並消耗一張卷軸；已鑑定物品仍可再次查看，並會照原版消耗卷軸。", CText);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 44f);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Button btnAll = ClassicArtButtons.CreateIdentifyAllButton(unappraisedCount, scrollTotal, delegate
		{
			IdentifyAllTownItems(scrollUid);
		});
		headerRow.AddChild(btnAll, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(headerRow, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, CBad), forceReadableName: false, InternalMode.Disabled);
		}
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(Row("沒有可以鑑定的其他物品。", CDim), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 360f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			vBoxContainer2.AddChild(TownIdentifyTargetRow(captured, delegate
			{
				IdentifyTownItem(scrollUid, captured.Uid);
			}), forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void IdentifyAllTownItems(string scrollUid)
	{
		GameData shared = GameDataProvider.Shared;
		L1jIdentifyRules.L1jIdentifyAllResult res = L1jIdentifyRules.TryIdentifyAll(shared, _session.Player, scrollUid);
		if (res.IdentifiedCount > 0)
		{
			SaveManager.Save(_session);
			_townBagRefresh?.Invoke();
			string summary = $"✓ 成功鑑定 {res.IdentifiedCount} 件物品（消耗 {res.ScrollsConsumed} 張卷軸）";
			if (res.RemainingUnidentified > 0)
			{
				summary += $"，卷軸已用盡（尚有 {res.RemainingUnidentified} 件未鑑定）";
			}
			OpenIdentifyScrollPanel(scrollUid, summary);
		}
		else
		{
			OpenIdentifyScrollPanel(scrollUid, "沒有可以鑑定的未鑑定物品或卷軸不足。");
		}
	}

	private void IdentifyTownItem(string scrollUid, string targetUid)
	{
		GameData shared = GameDataProvider.Shared;
		L1jIdentifyResult result = L1jIdentifyRules.TryIdentify(shared, _session.Player, scrollUid, targetUid);
		if (!result.Attempted)
		{
			OpenIdentifyScrollPanel(scrollUid, IdentifyScrollText.Failure(result.Failure));
			return;
		}
		SaveManager.Save(_session);
		_townBagRefresh?.Invoke();
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == result.TargetUid) ?? _session.Player.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == result.TargetUid);
		string text = ((itemStack == null) ? "鑑定完成，但物品已不在身上。" : IdentifyScrollText.Describe(shared, itemStack, result.NewlyIdentified));
		VBoxContainer vBoxContainer = OpenTownIdentifyFrame("鑑定結果", new Vector2(600f, 400f));
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 10);
		if (itemStack != null)
		{
			hBoxContainer.AddChild(ItemIcons.Slot(itemStack.ItemKey), forceReadableName: false, InternalMode.Disabled);
		}
		Label label = Row(text, CGood);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 270f);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		bool flag = _session.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid);
		vBoxContainer.AddChild(ClassicArtButtons.Confirm(flag ? ((Action)delegate
		{
			OpenIdentifyScrollPanel(scrollUid);
		}) : new Action(CloseOverlay), flag ? "繼續鑑定" : "關閉"), forceReadableName: false, InternalMode.Disabled);
	}

	private void OpenMainTargetItemPanel(string sourceUid, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == sourceUid);
		if (itemStack == null || !L1jTargetItemUseRules.IsInventoryTargetItem(shared, itemStack.ItemKey))
		{
			return;
		}
		VBoxContainer vBoxContainer = OpenPanel(L1jTargetItemUseText.Title(shared, itemStack), new Vector2(580f, 500f));
		Label label = Row(L1jTargetItemUseText.Instruction(shared, itemStack), CText);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 58f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, message.StartsWith("無法", StringComparison.Ordinal) ? CBad : CGood), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jTargetItemUseRules.EligibleInventoryTargets(shared, _session.Player, sourceUid);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(Row("背包裡沒有符合的材料。", CDim), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 300f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			HBoxContainer hBoxContainer = new HBoxContainer();
			Label label2 = Row(StackLabel(captured), CText);
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "使用",
				CustomMinimumSize = new Vector2(72f, 30f)
			};
			button.Pressed += delegate
			{
				UseTownMainTargetItem(sourceUid, captured.Uid);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void UseTownMainTargetItem(string sourceUid, string targetUid)
	{
		L1jTargetItemUseResult result = L1jTargetItemUseRules.TryUseInventoryTargetItem(GameDataProvider.Shared, _session.Player, sourceUid, targetUid, _potionRng);
		if (result.Attempted)
		{
			SaveManager.Save(_session);
			_townBagRefresh?.Invoke();
		}
		if (_session.Player.InventoryStacks.Any((ItemStack item) => item.Uid == sourceUid))
		{
			OpenMainTargetItemPanel(sourceUid, L1jTargetItemUseText.Result(GameDataProvider.Shared, result));
			return;
		}
		VBoxContainer vBoxContainer = OpenPanel("使用結果", new Vector2(520f, 300f));
		Label label = Row(L1jTargetItemUseText.Result(GameDataProvider.Shared, result), L1jTargetItemUseText.IsSuccess(result) ? CGood : CBad);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 160f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(ClassicArtButtons.Confirm(CloseOverlay, "關閉"), forceReadableName: false, InternalMode.Disabled);
	}

	private void OpenNpcCrafting(string displayName, IReadOnlyList<NpcActionDefinition> recipes, string message = "")
	{
		IReadOnlyList<NpcActionDefinition> makeItems = recipes.Where((NpcActionDefinition recipe) => string.Equals(recipe.Kind, "MakeItem", StringComparison.Ordinal)).ToArray();
		VBoxContainer vBoxContainer = OpenPanel("製作 · " + displayName, new Vector2(700f, 590f), ornate: false, preserveRequestedFrame: true);
		Label label = Row("材料持有量＝背包＋個人倉庫（鎖定物品不計）；製作時會先消耗背包，再消耗倉庫，成品放入背包。", CDim, 12);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = Vector2.Zero;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			Label label2 = Row(message, message.StartsWith('✓') ? CGood : CBad, 12);
			label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label2.CustomMinimumSize = Vector2.Zero;
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			vBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		}
		CraftScrollShell craftScrollShell = CreateCraftScroll(410f, 76f);
		VBoxContainer list = craftScrollShell.List;
		vBoxContainer.AddChild(craftScrollShell.Root, forceReadableName: false, InternalMode.Disabled);
		if (makeItems.Count == 0)
		{
			list.AddChild(Row("這位 NPC 目前沒有可執行的製作配方。", CDim, 13), forceReadableName: false, InternalMode.Disabled);
		}
		foreach (NpcActionDefinition item in makeItems)
		{
			NpcActionDefinition captured = item;
			long num = NpcActionRules.CraftableSets(_session.Player, captured, _session.Warehouse);
			IReadOnlyList<NpcActionMaterialAvailability> readOnlyList = NpcActionRules.MaterialAvailability(GameDataProvider.Shared, _session.Player, captured, 1L, _session.Warehouse);
			NpcActionKillProgress npcActionKillProgress = NpcActionRules.KillProgress(_session.Player, captured);
			VBoxContainer vBoxContainer2 = new VBoxContainer
			{
				CustomMinimumSize = new Vector2(190f, 0f)
			};
			vBoxContainer2.AddThemeConstantOverride("separation", 3);
			vBoxContainer2.AddChild(CraftLine("成品", CDim, 11), forceReadableName: false, InternalMode.Disabled);
			if (captured.Outputs.Count == 0)
			{
				vBoxContainer2.AddChild(CraftLine(captured.Name, CGold, 15), forceReadableName: false, InternalMode.Disabled);
			}
			else
			{
				foreach (NpcActionItem output in captured.Outputs)
				{
					vBoxContainer2.AddChild(CraftItemLine(output, NpcActionItemText(output), CGold, 15), forceReadableName: false, InternalMode.Disabled);
				}
			}
			List<Control> list2 = new List<Control>();
			for (int num2 = 0; num2 < readOnlyList.Count; num2++)
			{
				NpcActionMaterialAvailability npcActionMaterialAvailability = readOnlyList[num2];
				list2.Add(CraftMaterialChip(captured.Materials[num2], npcActionMaterialAvailability.Name, npcActionMaterialAvailability.Held, npcActionMaterialAvailability.Required, npcActionMaterialAvailability.Enough));
			}
			TextureButton textureButton = ClassicTradeButtons.Confirm(delegate
			{
				CraftNpcRecipe(displayName, makeItems, captured);
			}, "確認製作 " + ProductText(captured));
			textureButton.Disabled = num <= 0;
			if (textureButton.Disabled)
			{
				textureButton.SelfModulate = new Color(0.45f, 0.45f, 0.45f);
			}
			list.AddChild(CraftRecipeCard(vBoxContainer2, list2, (num > 0) ? $"可製 {num:N0} 次" : (((object)npcActionKillProgress != null && !npcActionKillProgress.Complete) ? $"{npcActionKillProgress.TargetName} {npcActionKillProgress.Killed}/{npcActionKillProgress.Required}" : "材料不足"), (num > 0) ? CGood : CBad, textureButton), forceReadableName: false, InternalMode.Disabled);
		}
		CenterContainer centerContainer = new CenterContainer
		{
			CustomMinimumSize = new Vector2(0f, 30f)
		};
		centerContainer.AddChild(ClassicTradeButtons.Cancel(CloseOverlay, "取消並關閉製作"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(centerContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private void CraftNpcRecipe(string displayName, IReadOnlyList<NpcActionDefinition> recipes, NpcActionDefinition recipe)
	{
		NpcActionResult npcActionResult = NpcActionRules.ExecuteMakeItem(GameDataProvider.Shared, _session.Player, recipe, 1L, _session.Warehouse);
		if (npcActionResult.Success)
		{
			SaveManager.Save(_session);
			RefreshHeaderGold();
		}
		string text = ((npcActionResult.Lines.Count > 0) ? string.Join("\u3000", npcActionResult.Lines) : (npcActionResult.Success ? "製作完成。" : "無法製作。"));
		OpenNpcCrafting(displayName, recipes, (npcActionResult.Success ? "✓ " : "✗ ") + text);
	}

	private static Label CraftLine(string text, Color color, int fontSize)
	{
		Label label = Row(text, color, fontSize);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = Vector2.Zero;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return label;
	}

	private static HBoxContainer CraftItemLine(NpcActionItem item, string text, Color color, int fontSize)
	{
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(0f, 28f);
		hBoxContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hBoxContainer.AddThemeConstantOverride("separation", 7);
		hBoxContainer.AddChild(CraftItemIcon(item.ItemKey, item.IsAdena), forceReadableName: false, InternalMode.Disabled);
		Label label = CraftLine(text, color, fontSize);
		label.CustomMinimumSize = Vector2.Zero;
		label.VerticalAlignment = VerticalAlignment.Center;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		return hBoxContainer;
	}

	private static Control CraftMaterialChip(NpcActionItem item, string name, long held, long required, bool enough)
	{
		return CraftMaterialChip(item.ItemKey, item.IsAdena, name, held, required, enough);
	}

	private static Control CraftMaterialChip(string itemKey, string name, long held, long required, bool enough)
	{
		return CraftMaterialChip(itemKey, adena: false, name, held, required, enough);
	}

	private static Control CraftMaterialChip(string? itemKey, bool adena, string name, long held, long required, bool enough)
	{
		PanelContainer panelContainer = new PanelContainer();
		panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.05f, 0.07f, 0.78f),
			BorderColor = (enough ? new Color(0.32f, 0.55f, 0.34f, 0.65f) : new Color(0.62f, 0.25f, 0.25f, 0.75f)),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3
		});
		MarginContainer marginContainer = new MarginContainer();
		marginContainer.AddThemeConstantOverride("margin_left", 5);
		marginContainer.AddThemeConstantOverride("margin_right", 7);
		marginContainer.AddThemeConstantOverride("margin_top", 3);
		marginContainer.AddThemeConstantOverride("margin_bottom", 3);
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 5);
		hBoxContainer.AddChild(CraftItemIcon(itemKey, adena), forceReadableName: false, InternalMode.Disabled);
		Label label = CraftLine($"{held:N0}/{required:N0}  {name}", enough ? CGood : CBad, 12);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.VerticalAlignment = VerticalAlignment.Center;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		marginContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		panelContainer.AddChild(marginContainer, forceReadableName: false, InternalMode.Disabled);
		return panelContainer;
	}

	private static PanelContainer CraftRecipeCard(Control outputs, IReadOnlyList<Control> materialChips, string availability, Color availabilityColor, TextureButton craft)
	{
		PanelContainer panelContainer = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		panelContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.08f, 0.1f, 0.82f),
			BorderColor = new Color(0.38f, 0.35f, 0.28f, 0.72f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		});
		MarginContainer marginContainer = new MarginContainer();
		marginContainer.AddThemeConstantOverride("margin_left", 9);
		marginContainer.AddThemeConstantOverride("margin_right", 9);
		marginContainer.AddThemeConstantOverride("margin_top", 7);
		marginContainer.AddThemeConstantOverride("margin_bottom", 7);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		hBoxContainer.AddThemeConstantOverride("separation", 12);
		hBoxContainer.AddChild(outputs, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		vBoxContainer.AddChild(CraftLine("需求", CDim, 11), forceReadableName: false, InternalMode.Disabled);
		HFlowContainer hFlowContainer = new HFlowContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 36f)
		};
		hFlowContainer.AddThemeConstantOverride("h_separation", 6);
		hFlowContainer.AddThemeConstantOverride("v_separation", 5);
		foreach (Control materialChip in materialChips)
		{
			hFlowContainer.AddChild(materialChip, forceReadableName: false, InternalMode.Disabled);
		}
		vBoxContainer.AddChild(hFlowContainer, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(ClassicTradeButtons.NativeSize.X + 24f, 0f),
			Alignment = BoxContainer.AlignmentMode.Center
		};
		Label label = CraftLine(availability, availabilityColor, 12);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		vBoxContainer2.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		CenterContainer centerContainer = new CenterContainer();
		centerContainer.AddChild(craft, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer2.AddChild(centerContainer, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		marginContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		panelContainer.AddChild(marginContainer, forceReadableName: false, InternalMode.Disabled);
		return panelContainer;
	}

	private static HBoxContainer CraftItemLine(string itemKey, string text, Color color, int fontSize)
	{
		return CraftItemLine(new NpcActionItem(0, 1, itemKey), text, color, fontSize);
	}

	private static Control CraftItemIcon(string? itemKey, bool adena = false)
	{
		if (!string.IsNullOrWhiteSpace(itemKey))
		{
			return ItemIcons.Slot(itemKey);
		}
		if (!adena)
		{
			return new Control
			{
				CustomMinimumSize = new Vector2(28f, 28f)
			};
		}
		return ItemIcons.Slot("gold");
	}

	private static CraftScrollShell CreateCraftScroll(float height, float step)
	{
		Control obj = new Control
		{
			CustomMinimumSize = new Vector2(0f, height),
			ClipContents = true
		};
		ScrollContainer scrollContainer = new ScrollContainer
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetRight = -22f,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 7);
		scrollContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		obj.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicSideScrollBar classicSideScrollBar = ClassicSideScrollBar.ForScrollContainer(scrollContainer, height - 4f, step, hideWhenUnused: false);
		classicSideScrollBar.AnchorLeft = 1f;
		classicSideScrollBar.AnchorRight = 1f;
		classicSideScrollBar.OffsetLeft = -18f;
		classicSideScrollBar.OffsetRight = -6f;
		classicSideScrollBar.OffsetTop = 2f;
		classicSideScrollBar.OffsetBottom = height - 2f;
		obj.AddChild(classicSideScrollBar, forceReadableName: false, InternalMode.Disabled);
		return new CraftScrollShell(obj, vBoxContainer);
	}

	private static string NpcActionItemText(NpcActionItem item)
	{
		string text = (item.IsAdena ? "金幣" : ((item.ItemKey == null) ? $"main item {item.L1jItemId}" : ItemName(item.ItemKey)));
		if (item.Count <= 1)
		{
			return text;
		}
		return $"{text} ×{item.Count:N0}";
	}

	private static string ProductText(NpcActionDefinition recipe)
	{
		if (recipe.Outputs.Count == 0)
		{
			return recipe.Name;
		}
		return string.Join("、", recipe.Outputs.Select(NpcActionItemText));
	}

	private void OpenNpcSkillLearning(int npcId, string displayName, string message = "", bool messageIsError = false)
	{
		IReadOnlyList<L1jNpcSkillOffer> offers = L1jNpcSkillLearningRules.Offers(GameDataProvider.Shared, _session.Player);
		VBoxContainer vBoxContainer = OpenPanel("學習魔法 · " + displayName, new Vector2(570f, 590f));
		Label label = Row("依 L1J-TW main：只教授第 1～3 階一般魔法；價格＝魔法階級² × 100 金幣。可複選後一次確認。", CDim, 12);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(510f, 0f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			Label label2 = Row(message, messageIsError ? CBad : CGood, 13);
			label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label2.CustomMinimumSize = new Vector2(510f, 0f);
			vBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		}
		Label node = Row($"持有金幣\u3000{CombatWallet.Balance(_session.Player):N0}", CGold);
		vBoxContainer.AddChild(node, forceReadableName: false, InternalMode.Disabled);
		Control control = new Control
		{
			CustomMinimumSize = new Vector2(0f, 360f)
		};
		ScrollContainer scrollContainer = new ScrollContainer
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(490f, 0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 5);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);
		control.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		ClassicSideScrollBar classicSideScrollBar = ClassicSideScrollBar.ForScrollContainer(scrollContainer, 360f, 48.0, hideWhenUnused: false);
		classicSideScrollBar.AnchorLeft = 1f;
		classicSideScrollBar.AnchorRight = 1f;
		classicSideScrollBar.OffsetLeft = 9f;
		classicSideScrollBar.OffsetRight = 21f;
		classicSideScrollBar.OffsetBottom = 360f;
		control.AddChild(classicSideScrollBar, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(control, forceReadableName: false, InternalMode.Disabled);
		Dictionary<string, CheckBox> selected = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
		Label total = Row("已選 0 招\u3000合計 0 金幣", CText);
		foreach (L1jNpcSkillOffer item in offers)
		{
			CheckBox checkBox = new CheckBox
			{
				Text = $"第 {item.SkillLevel} 階\u3000{item.Name}\u3000{item.PriceAdena:N0} 金幣",
				CustomMinimumSize = new Vector2(486f, 30f),
				FocusMode = FocusModeEnum.None,
				TooltipText = $"main skill id {item.OfficialSkillId}；需求角色 Lv{item.RequiredPlayerLevel}"
			};
			checkBox.AddThemeFontSizeOverride("font_size", 14);
			checkBox.AddThemeColorOverride("font_color", CText);
			checkBox.Toggled += delegate
			{
				RefreshTotal();
			};
			selected[item.SkillId] = checkBox;
			vBoxContainer2.AddChild(checkBox, forceReadableName: false, InternalMode.Disabled);
		}
		if (offers.Count == 0)
		{
			bool flag;
			switch (ClassKitRegistry.NormalizeClassId(_session.Player.ClassId))
			{
			case "royal":
			case "knight":
			case "elf":
			case "mage":
			case "dark":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			Label label3 = Row(flag ? "目前沒有可向吉倫學習的魔法：可能尚未達到下一階門檻，或可學項目已全部學會。" : "依 L1J-TW main，這個職業不能向吉倫學習一般魔法。", CDim, 13);
			label3.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label3.CustomMinimumSize = new Vector2(486f, 0f);
			vBoxContainer2.AddChild(label3, forceReadableName: false, InternalMode.Disabled);
		}
		vBoxContainer.AddChild(total, forceReadableName: false, InternalMode.Disabled);
		RefreshTotal();
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0f, 24f)
		};
		hBoxContainer.AddThemeConstantOverride("separation", 16);
		TextureButton textureButton = ClassicTradeButtons.Confirm(delegate
		{
			LearnSelectedNpcSkills(npcId, displayName, offers, selected);
		}, "確認學習勾選的魔法");
		textureButton.Disabled = offers.Count == 0;
		if (textureButton.Disabled)
		{
			textureButton.SelfModulate = new Color(0.45f, 0.45f, 0.45f);
		}
		hBoxContainer.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(ClassicTradeButtons.Cancel(CloseOverlay, "取消並關閉技能學習"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		void RefreshTotal()
		{
			L1jNpcSkillOffer[] array = offers.Where((L1jNpcSkillOffer offer) => selected[offer.SkillId].ButtonPressed).ToArray();
			long num = array.Sum((L1jNpcSkillOffer offer) => offer.PriceAdena);
			total.Text = $"已選 {array.Length} 招\u3000合計 {num:N0} 金幣";
			total.AddThemeColorOverride("font_color", (num <= CombatWallet.Balance(_session.Player)) ? CText : CBad);
		}
	}

	private void LearnSelectedNpcSkills(int npcId, string displayName, IReadOnlyList<L1jNpcSkillOffer> offers, IReadOnlyDictionary<string, CheckBox> selected)
	{
		string[] selectedSkillIds = (from offer in offers
			where selected[offer.SkillId].ButtonPressed
			select offer.SkillId).ToArray();
		L1jNpcSkillLearningResult l1jNpcSkillLearningResult = L1jNpcSkillLearningRules.TryLearn(GameDataProvider.Shared, _session.Player, npcId, selectedSkillIds, allowRepeatPurchase: false, 1L);
		if (!l1jNpcSkillLearningResult.Success)
		{
			OpenNpcSkillLearning(npcId, displayName, NpcSkillLearningFailureText(l1jNpcSkillLearningResult.Failure), messageIsError: true);
			return;
		}
		SaveManager.Save(_session);
		RefreshHeaderGold();
		string value = string.Join("、", l1jNpcSkillLearningResult.LearnedSkillIds.Select(SkillInfo.Name));
		OpenNpcSkillLearning(npcId, displayName, $"學會 {value}；支付 {l1jNpcSkillLearningResult.GoldSpent:N0} 金幣。");
	}

	private static string NpcSkillLearningFailureText(L1jNpcSkillLearningFailure failure)
	{
		return failure switch
		{
			L1jNpcSkillLearningFailure.WrongNpc => "這位 NPC 不能教授一般魔法。", 
			L1jNpcSkillLearningFailure.UnsupportedClass => "依 main 規則，這個職業不能在此學習一般魔法。", 
			L1jNpcSkillLearningFailure.NoSelection => "請先勾選要學習的魔法。", 
			L1jNpcSkillLearningFailure.SkillUnavailable => "勾選內容已失效，請重新選擇。", 
			L1jNpcSkillLearningFailure.InsufficientGold => "金幣不足。", 
			_ => "無法學習魔法。", 
		};
	}

	private void OpenPetStore(string displayName)
	{
		_petMessage = "";
		BuildPetKeeperPanel(displayName);
	}

	private void PetStatus(Label status, string text, Color color)
	{
		SetStatus(status, text, color);
		_petMessage = text;
		_petMessageColor = color;
	}

	private void BuildPetKeeperPanel(string displayName)
	{
		VBoxContainer vBoxContainer = OpenPanel("寵物保管員 · " + displayName, new Vector2(660f, 600f));
		Combatant player = _session.Player;
		GameData data = GameDataProvider.Shared;
		PetInstance[] array = _session.Pets.Pets.Where((PetInstance pet) => PetCollarRules.FindCollar(data, player.InventoryStacks, pet.Uid) != null).ToArray();
		PetInstance[] array2 = _session.Pets.ActiveFor(player).ToArray();
		vBoxContainer.AddChild(Row($"出戰 {array2.Length} 隻 · 項圈可選 {array.Length} 隻", CGold, 15), forceReadableName: false, InternalMode.Disabled);
		Label label = Row("依 main：寄放會一次收回所有出戰寵物並卸下寵物裝備；領取時從背包內的項圈選擇，受魅力總成本限制，不消耗寵物哨子。", CDim, 12);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(616f, 0f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label status = Row("", CDim, 13);
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		status.CustomMinimumSize = new Vector2(616f, 0f);
		if (_petMessage.Length > 0)
		{
			SetStatus(status, _petMessage, _petMessageColor);
			_petMessage = "";
		}
		Button button = new Button
		{
			Text = "寄放所有出戰寵物",
			Disabled = (array2.Length == 0),
			CustomMinimumSize = new Vector2(220f, 32f)
		};
		button.Pressed += delegate
		{
			PetKeeperResult petKeeperResult = PetKeeperRules.DepositAll(data, _session.Pets, player);
			if (!petKeeperResult.Success)
			{
				PetStatus(status, PetKeeperFailText(petKeeperResult.Failure), CBad);
			}
			else
			{
				SaveManager.Save(_session);
				PetStatus(status, $"✓ 已寄放 {petKeeperResult.Affected} 隻寵物", CGood);
				BuildPetKeeperPanel(displayName);
			}
		};
		vBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(620f, 390f)
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(600f, 0f)
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 8);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		if (array.Length == 0)
		{
			vBoxContainer2.AddChild(Row("背包裡沒有已綁定的寵物項圈。", CDim), forceReadableName: false, InternalMode.Disabled);
		}
		PetInstance[] array3 = array;
		foreach (PetInstance petInstance in array3)
		{
			bool flag = string.Equals(petInstance.OwnerKey, player.Key, StringComparison.Ordinal);
			HBoxContainer hBoxContainer = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(590f, 34f)
			};
			Label label2 = Row($"{petInstance.DisplayName} · Lv{petInstance.Level} · HP {petInstance.Hp:0}/{petInstance.MaxHp:0}" + (flag ? " · 出戰中" : " · 保管中"), flag ? CGood : CText);
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
			Button button2 = new Button
			{
				Text = (flag ? "已領取" : "領取"),
				Disabled = flag,
				CustomMinimumSize = new Vector2(78f, 28f)
			};
			string uid = petInstance.Uid;
			button2.Pressed += delegate
			{
				PetKeeperResult petKeeperResult = PetKeeperRules.Withdraw(data, _session.Pets, player, uid);
				if (!petKeeperResult.Success)
				{
					PetStatus(status, PetKeeperFailText(petKeeperResult.Failure), CBad);
				}
				else
				{
					SaveManager.Save(_session);
					PetStatus(status, "✓ 已領取 " + petKeeperResult.Pet.DisplayName, CGood);
					BuildPetKeeperPanel(displayName);
				}
			};
			hBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
	}

	private static string PetKeeperFailText(PetKeeperFailure failure)
	{
		return failure switch
		{
			PetKeeperFailure.InvalidOwner => "✗ 目前無法使用寵物保管服務", 
			PetKeeperFailure.UnknownPet => "✗ 找不到項圈對應的寵物", 
			PetKeeperFailure.MissingCollar => "✗ 背包裡沒有這隻寵物的項圈", 
			PetKeeperFailure.AlreadyActive => "✗ 這隻寵物已經出戰", 
			PetKeeperFailure.ForeignPet => "✗ 這隻寵物目前由其他主人帶領", 
			PetKeeperFailure.InsufficientCharm => "✗ 魅力不足，無法再領取寵物", 
			PetKeeperFailure.InventoryOverflow => "✗ 背包沒有空間收回寵物裝備", 
			_ => "✗ 寵物保管操作失敗", 
		};
	}

	private void OpenDufaDialog(string displayName)
	{
		string[] lines = new string[4] { "先坐下來慢慢聽我說。", "很久以前，這個國家在各地開始出現盜賊與海賊，其中最有名的就是德雷克。他以膽識與殘酷聞名，甚至國家都沒辦法抵抗他的強大；以不斷威脅搶奪貨物，讓他們享有了很多財富。", "但是……他與自己的部下卻突然消失了！", "雖然他不見了，但人們對他的寶物更為關心。" };
		OpenNpcDialog(displayName, lines).AddOption("那些寶物在哪裡？", delegate
		{
			CloseOverlay();
			_session.HuntMap = "hidden_dock";
			_session.PendingMapEntryLandmark = "hidden_dock_arrival";
			_onHunt();
		});
	}

	private void UsePolymorphScroll(ItemStack stack, Action refreshBag, Label status)
	{
		if (PolymorphRules.IsShannaScroll(GameDataProvider.Shared, stack.ItemKey, out int scrollLevel))
		{
			OpenPolymorphSelection(stack.Uid, scrollLevel);
			return;
		}
		if (PolymorphRules.HasControlItem(_session.Player))
		{
			OpenPolymorphSelection(stack.Uid);
			return;
		}
		var (flag, text) = ItemActivation.UsePolymorph(GameDataProvider.Shared, _session.Player, stack.Uid, _potionRng);
		SetStatus(status, (flag ? "✓ " : "無法使用：") + text, flag ? CGood : CBad);
		if (flag)
		{
			SaveManager.Save(_session);
			refreshBag();
		}
	}

	private void OpenPolymorphSelection(string scrollUid, int? scrollMaxLevel = null)
	{
		IReadOnlyList<PolymorphForm> readOnlyList = PolymorphRules.SelectableForms(GameDataProvider.Shared, _session.Player, scrollMaxLevel);
		string title = scrollMaxLevel.HasValue ? $"夏納變身 · 選擇形態 (Lv.{scrollMaxLevel.Value})" : "變形控制 · 選擇形態";
		VBoxContainer vBoxContainer = OpenPanel(title, new Vector2(520f, 600f));
		vBoxContainer.AddChild(Row("選擇後會消耗 1 張變形卷軸；清單已依目前等級與武器過濾。", CDim, 12), forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(484f, 438f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(468f, 0f)
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		Label status = Row("", CDim, 13);
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		status.CustomMinimumSize = new Vector2(484f, 0f);
		foreach (PolymorphForm item in readOnlyList)
		{
			PolymorphForm form = item;
			Button button = new Button
			{
				Text = $"{form.Name}\u3000Lv{form.Level}",
				CustomMinimumSize = new Vector2(456f, 34f),
				Alignment = HorizontalAlignment.Left
			};
			button.Pressed += delegate
			{
				var (flag, text) = ItemActivation.UsePolymorph(GameDataProvider.Shared, _session.Player, scrollUid, _potionRng, form.Name);
				SetStatus(status, (flag ? "✓ " : "無法使用：") + text, flag ? CGood : CBad);
				if (flag)
				{
					SaveManager.Save(_session);
				}
			};
			vBoxContainer2.AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		if (readOnlyList.Count == 0)
		{
			vBoxContainer2.AddChild(Row("目前沒有符合等級與武器的變身形態。", CBad, 13), forceReadableName: false, InternalMode.Disabled);
		}
		Button button2 = new Button
		{
			Text = "返回背包",
			CustomMinimumSize = new Vector2(160f, 36f)
		};
		button2.Pressed += OpenBag;
		vBoxContainer.AddChild(button2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(status, forceReadableName: false, InternalMode.Disabled);
	}

	private void OpenResolventPanel(string solventUid, string message = "")
	{
		VBoxContainer vBoxContainer = OpenPanel("溶解劑", new Vector2(560f, 520f));
		Label label = Row("選擇要溶解的物品。確認後物品與溶解劑都會消失，失敗也不退還。", CText);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 44f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, message.StartsWith("✓") ? CGood : CBad), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<(ItemStack, L1jResolventDefinition)> readOnlyList = L1jResolventRules.EligibleTargets(GameDataProvider.Shared, _session.Player);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(Row("背包內沒有可溶解的未鎖定物品。", CDim), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 350f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer();
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (var item3 in readOnlyList)
		{
			ItemStack item = item3.Item1;
			L1jResolventDefinition item2 = item3.Item2;
			ItemStack captured = item;
			L1jResolventDefinition capturedDefinition = item2;
			HBoxContainer hBoxContainer = new HBoxContainer();
			string value = ((captured.IsIdentified && captured.Enhancement != 0) ? $"{captured.Enhancement:+#;-#} " : "");
			Label label2 = Row($"{value}{L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, captured)} ×{captured.Quantity}  基礎 {capturedDefinition.CrystalCount:N0}", CText);
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
			Button button = new Button
			{
				Text = "選擇",
				CustomMinimumSize = new Vector2(72f, 30f)
			};
			button.Pressed += delegate
			{
				OpenResolventConfirmation(solventUid, captured.Uid, capturedDefinition);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenResolventConfirmation(string solventUid, string targetUid, L1jResolventDefinition definition)
	{
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		if (itemStack == null)
		{
			OpenResolventPanel(solventUid, "物品已不在背包。未消耗任何物品。");
			return;
		}
		VBoxContainer vBoxContainer = OpenPanel("確認溶解", new Vector2(500f, 360f));
		string value = ((!itemStack.IsIdentified) ? "" : ((itemStack.Enhancement == 0) ? "+0 " : $"{itemStack.Enhancement:+#;-#} "));
		Label label = Row($"物品：{value}{L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, itemStack)}\n基礎產量：魔法結晶體 ×{definition.CrystalCount:N0}\n" + "結果：50% 無產物／40% 基礎量／10% 基礎量的 1.5 倍\n\n確認後會消耗此物品與溶解劑各 1 個，且不可復原。", CBad, 15);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 180f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		hBoxContainer.AddThemeConstantOverride("separation", 28);
		hBoxContainer.AddChild(ClassicArtButtons.Confirm(delegate
		{
			L1jResolventResult l1jResolventResult = L1jResolventRules.TryDissolve(GameDataProvider.Shared, _session.Player, solventUid, targetUid, confirmed: true, _potionRng);
			if (l1jResolventResult.Attempted)
			{
				SaveManager.Save(_session);
			}
			string text = ((!l1jResolventResult.Attempted) ? ResolventFailureText(l1jResolventResult.Failure) : ((l1jResolventResult.CrystalCount > 0) ? $"✓ 溶解完成：獲得魔法結晶體 ×{l1jResolventResult.CrystalCount:N0}" : "溶解失敗：物品與溶解劑已消耗，沒有獲得結晶。"));
			if (_session.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == solventUid && stack.ItemKey == "l1j_item_41245"))
			{
				OpenResolventPanel(solventUid, text);
			}
			else
			{
				VBoxContainer vBoxContainer2 = OpenPanel("溶解結果", new Vector2(460f, 250f));
				Label label2 = Row(text, (l1jResolventResult.CrystalCount > 0) ? CGood : CBad, 15);
				label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				label2.CustomMinimumSize = new Vector2(0f, 90f);
				vBoxContainer2.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
				vBoxContainer2.AddChild(ClassicArtButtons.Confirm(CloseOverlay, "關閉"), forceReadableName: false, InternalMode.Disabled);
			}
		}, "同意並溶解"), forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenResolventPanel(solventUid);
		}, "取消；不消耗物品"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private static string ResolventFailureText(L1jResolventFailure failure)
	{
		return failure switch
		{
			L1jResolventFailure.ConfirmationRequired => "必須先確認；未消耗任何物品。", 
			L1jResolventFailure.SolventMissing => "找不到可用的溶解劑；未消耗任何物品。", 
			L1jResolventFailure.TargetMissing => "物品已不在背包；未消耗任何物品。", 
			L1jResolventFailure.TargetEquipped => "請先卸下裝備；未消耗任何物品。", 
			L1jResolventFailure.TargetLocked => "物品已鎖定；未消耗任何物品。", 
			L1jResolventFailure.TargetNotResolvable => "此物品不在原版溶解表；未消耗任何物品。", 
			_ => "無法溶解；未消耗任何物品。", 
		};
	}

	private void OpenSealScrollPanel(string scrollUid, bool sealing, string message = "")
	{
		GameData data = GameDataProvider.Shared;
		ItemStack itemStack = _session.Player.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		if (itemStack == null || !(sealing ? L1jSealRules.IsSealScroll(data, itemStack.ItemKey) : L1jSealRules.IsUnsealScroll(data, itemStack.ItemKey)))
		{
			return;
		}
		string verb = (sealing ? "封印" : "解除封印");
		VBoxContainer vBoxContainer = OpenPanel(verb + "卷軸", new Vector2(560f, 520f));
		Label label = Row(SealScrollText.Intro(sealing), CText);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(0f, 44f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, message.StartsWith("✓") ? CGood : CBad), forceReadableName: false, InternalMode.Disabled);
		}
		IReadOnlyList<ItemStack> readOnlyList = L1jSealRules.EligibleTargets(data, _session.Player, sealing);
		if (readOnlyList.Count == 0)
		{
			vBoxContainer.AddChild(Row(SealScrollText.NoTargets(sealing), CDim), forceReadableName: false, InternalMode.Disabled);
			return;
		}
		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(520f, 320f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		foreach (ItemStack item in readOnlyList)
		{
			ItemStack captured = item;
			PanelContainer rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 34f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.11f, 0.95f),
				BorderColor = new Color(0.25f, 0.23f, 0.18f, 0.6f),
				BorderWidthBottom = 1,
				BorderWidthTop = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				CornerRadiusBottomLeft = 3,
				CornerRadiusBottomRight = 3,
				CornerRadiusTopLeft = 3,
				CornerRadiusTopRight = 3,
				ContentMarginLeft = 8f,
				ContentMarginRight = 8f,
				ContentMarginTop = 2f,
				ContentMarginBottom = 2f
			};
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

			HBoxContainer hBoxContainer = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Begin
			};
			hBoxContainer.AddThemeConstantOverride("separation", 8);
			rowPanel.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);

			Label label2 = Row(EquippedMark(captured) + StackLabel(captured), CText);
			label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			label2.AutowrapMode = TextServer.AutowrapMode.Off;
			label2.ClipText = true;
			hBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);

			Button button = new Button
			{
				Text = (sealing ? "選擇封印" : "選擇解封"),
				CustomMinimumSize = new Vector2(96f, 28f),
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			button.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.65f));
			button.AddThemeFontSizeOverride("font_size", 13);
			button.Pressed += delegate
			{
				L1jSealResult result = (sealing ? L1jSealRules.TrySeal(data, _session.Player, scrollUid, captured.Uid, confirmed: true) : L1jSealRules.TryUnseal(data, _session.Player, scrollUid, captured.Uid, confirmed: true));
				if (result.Attempted)
				{
					SaveManager.Save(_session);
				}
				string text = SealScrollText.Outcome(data, result, sealing);
				if (_session.Player.InventoryStacks.Any((ItemStack stack) => stack.Uid == scrollUid))
				{
					OpenSealScrollPanel(scrollUid, sealing, text);
				}
				else
				{
					VBoxContainer vBoxContainer3 = OpenPanel(verb + "結果", new Vector2(460f, 250f));
					Label label3 = Row(text, result.Attempted ? CGood : CBad, 15);
					label3.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					label3.CustomMinimumSize = new Vector2(0f, 90f);
					vBoxContainer3.AddChild(label3, forceReadableName: false, InternalMode.Disabled);
					vBoxContainer3.AddChild(ClassicArtButtons.Confirm(CloseOverlay, "關閉"), forceReadableName: false, InternalMode.Disabled);
				}
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenWarehouse(string whName)
	{
		OpenWarehousePanel(whName, _session.Warehouse, "倉庫", persistClanBook: false);
	}

	private void OpenClanWarehouse(string clanName)
	{
		ClanBook book = ClanStore.Book;
		if (book.Exists)
		{
			OpenWarehousePanel(clanName, book.Warehouse, "血盟倉庫", persistClanBook: true);
		}
	}

	private void OpenWarehousePanel(string whName, WarehouseState warehouse, string storeHeading, bool persistClanBook)
	{
		GameData data = GameDataProvider.Shared;
		Control root = OpenWarehouseOverlay();
		WarehouseSide bag = null!;
		WarehouseSide store = null!;
		bag = BuildWarehouseSide(root, 0f, isStore: false, HandleWarehouseDrop);
		store = BuildWarehouseSide(root, 343f, isStore: true, HandleWarehouseDrop);
		Label label = Row("⇄", CGold, 22);
		label.Position = new Vector2(309f, 180.6f);
		label.Size = new Vector2(34f, 30f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		root.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		WarehouseSide[] array = new WarehouseSide[2] { bag, store };
		foreach (WarehouseSide side in array)
		{
			side.Previous.Pressed += delegate
			{
				if (side.Page != 0)
				{
					side.Page--;
					Refresh();
				}
			};
			side.Next.Pressed += delegate
			{
				side.Page++;
				Refresh();
			};
			side.Category.ItemSelected += delegate(long index)
			{
				IReadOnlyList<ItemCategory> selectable = ItemCategories.Selectable;
				if (index >= 0 && index < selectable.Count)
				{
					side.Filter = selectable[(int)index];
					side.Page = 0;
					Refresh();
					SetStatus(side.Status, "", CDim);
				}
			};
		}
		Refresh();
		void DoMove(ItemStack stack, bool fromBag, long quantity)
		{
			WarehouseSide warehouseSide = (fromBag ? bag : store);
			WarehouseTransferResult warehouseTransferResult = (fromBag ? WarehouseRules.TryDeposit(data, _session.Player, warehouse, stack.Uid, quantity) : WarehouseRules.TryWithdraw(data, warehouse, _session.Player, stack.Uid, quantity));
			if (!warehouseTransferResult.Success)
			{
				SetStatus(warehouseSide.Status, (fromBag ? "無法存入：" : "無法取出：") + WhFailText(warehouseTransferResult.Failure), CBad);
			}
			else
			{
				if (persistClanBook)
				{
					ClanStore.Save();
				}
				SaveManager.Save(_session);
				Refresh();
				SetStatus(warehouseSide.Status, "✓ " + (fromBag ? "存入" : "取出") + " " + L1jItemIdentityRules.DisplayName(data, stack) + $" ×{warehouseTransferResult.Quantity}", CGood);
			}
		}
		void Move(ItemStack stack, bool fromBag)
		{
			if (stack.Quantity <= 1)
			{
				DoMove(stack, fromBag, 1L);
			}
			else
			{
				OpenWarehouseQuantityPrompt(root, stack, fromBag, delegate(long quantity)
				{
					DoMove(stack, fromBag, quantity);
				});
			}
		}
		void HandleWarehouseDrop(string itemKey, string stackUid, bool targetIsStore)
		{
			if (string.IsNullOrEmpty(itemKey) && string.IsNullOrEmpty(stackUid))
			{
				return;
			}
			bool fromBag = targetIsStore;
			IReadOnlyList<ItemStack> sourceList = fromBag ? _session.Player.InventoryStacks : warehouse.Items;
			ItemStack found = sourceList.FirstOrDefault(s => (!string.IsNullOrEmpty(stackUid) && s.Uid == stackUid) || s.ItemKey == itemKey);
			if (found != null)
			{
				Move(found, fromBag);
			}
		}
		void Refresh()
		{
			RefreshWarehouseSide(data, bag, _session.Player.InventoryStacks, fromBag: true, Move, HandleWarehouseDrop);
			RefreshWarehouseSide(data, store, warehouse.Items, fromBag: false, Move, HandleWarehouseDrop);
			bag.Heading.Text = $"身上道具（{_session.Player.InventoryStacks.Count}）";
			store.Heading.Text = $"{storeHeading}（{warehouse.Items.Count} / {warehouse.Capacity}）";
		}
	}

	private Control OpenWarehouseOverlay()
	{
		CloseOverlay();
		Control control = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 2200
		};
		Control control2 = new Control
		{
			Position = new Vector2(Mathf.Round((_panelViewport.X - 652f) * 0.5f), Math.Max(6f, Mathf.Round((_panelViewport.Y - 451.5f) * 0.5f))),
			Size = new Vector2(652f, 451.5f)
		};
		control.AddChild(control2, forceReadableName: false, InternalMode.Disabled);
		AddChild(control, forceReadableName: false, InternalMode.Disabled);
		_overlay = control;
		_overlayTitle = "倉庫";
		return control2;
	}

	private WarehouseSide BuildWarehouseSide(Control root, float x, bool isStore, Action<string, string, bool> onDrop)
	{
		WarehouseSide warehouseSide = new WarehouseSide();
		ClassicWebWindows.BagShell bagShell = ClassicWebWindows.CreateBagShell(new Vector2(x, 0f), CloseOverlay, "");
		root.AddChild(bagShell.Panel, forceReadableName: false, InternalMode.Disabled);
		bagShell.Tabs.Visible = false;
		warehouseSide.Grid = bagShell.Grid;
		warehouseSide.Status = bagShell.Status;
		warehouseSide.PageLabel = bagShell.Page;
		warehouseSide.Previous = bagShell.Previous;
		warehouseSide.Next = bagShell.Next;

		WarehouseDropZone dropZone = new WarehouseDropZone
		{
			Position = Vector2.Zero,
			Size = bagShell.Panel.Size,
			MouseFilter = MouseFilterEnum.Pass
		};
		dropZone.OnPayloadDropped = delegate(string payload)
		{
			var (itemKey, stackUid, _) = ItemDragPayload.Decode(payload);
			onDrop(itemKey, stackUid, isStore);
		};
		bagShell.Panel.AddChild(dropZone, forceReadableName: false, InternalMode.Disabled);
		bagShell.Panel.MoveChild(dropZone, 0);

		warehouseSide.Heading = Row("", CGold, 12);
		warehouseSide.Heading.ClipText = true;
		warehouseSide.Heading.Position = new Vector2(66f, 5f);
		warehouseSide.Heading.Size = new Vector2(86f, 23f);
		bagShell.Panel.AddChild(warehouseSide.Heading, forceReadableName: false, InternalMode.Disabled);
		OptionButton optionButton = new OptionButton
		{
			FocusMode = FocusModeEnum.None,
			ClipText = true,
			TooltipText = "選擇要顯示的物品分類"
		};
		foreach (ItemCategory item in ItemCategories.Selectable)
		{
			optionButton.AddItem(ItemCategories.Name(item));
		}
		optionButton.Selected = 0;
		optionButton.AddThemeFontSizeOverride("font_size", 10);
		WhCategoryStyle(optionButton);
		optionButton.Position = new Vector2(154f, 4f);
		optionButton.Size = new Vector2(116f, 24f);
		bagShell.Panel.AddChild(optionButton, forceReadableName: false, InternalMode.Disabled);
		warehouseSide.Category = optionButton;
		return warehouseSide;
	}

	private static void WhCategoryStyle(OptionButton button)
	{
		string[] array = new string[5] { "normal", "hover", "pressed", "focus", "disabled" };
		foreach (string text in array)
		{
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat
			{
				BgColor = new Color(0.04f, 0.04f, 0.05f, (text == "hover") ? 0.95f : 0.82f),
				BorderColor = new Color(0.78f, 0.68f, 0.42f, 0.32f),
				ContentMarginLeft = 2f,
				ContentMarginRight = 2f,
				ContentMarginTop = 0f,
				ContentMarginBottom = 0f
			};
			styleBoxFlat.SetBorderWidthAll(1);
			button.AddThemeStyleboxOverride(text, styleBoxFlat);
		}
	}

	private void OpenWarehouseQuantityPrompt(Control root, ItemStack stack, bool fromBag, Action<long> confirm)
	{
		Control blocker = new Control
		{
			Position = Vector2.Zero,
			Size = _panelViewport,
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 2500
		};
		if (_overlay != null)
		{
			_overlay.AddChild(blocker, forceReadableName: false, InternalMode.Disabled);
		}
		else
		{
			root.AddChild(blocker, forceReadableName: false, InternalMode.Disabled);
		}

		Vector2 boxSize = new Vector2(360f, 156f);
		Panel box = new Panel
		{
			Position = ((_panelViewport - boxSize) * 0.5f).Round(),
			Size = boxSize,
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.06f, 0.08f, 0.98f),
			BorderColor = new Color(0.78f, 0.68f, 0.42f, 0.95f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4
		};
		box.AddThemeStyleboxOverride("panel", styleBoxFlat);
		blocker.AddChild(box, forceReadableName: false, InternalMode.Disabled);

		long maxQuantity = stack.Quantity;
		long quantity = maxQuantity;
		string displayName = L1jItemIdentityRules.DisplayName(GameDataProvider.Shared, stack);

		Label title = Row((fromBag ? "存入「" : "取出「") + displayName + $"」（現有 {maxQuantity:N0}）", CGold, 13);
		title.Position = new Vector2(12f, 12f);
		title.Size = new Vector2(336f, 22f);
		title.ClipText = true;
		box.AddChild(title, forceReadableName: false, InternalMode.Disabled);

		LineEdit input = new LineEdit
		{
			Text = quantity.ToString(),
			Position = new Vector2(46f, 46f),
			Size = new Vector2(106f, 26f),
			Alignment = HorizontalAlignment.Center,
			TooltipText = $"請輸入數量 (1 ~ {maxQuantity})"
		};
		input.AddThemeFontSizeOverride("font_size", 12);
		StyleBoxFlat inputStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.02f, 0.04f, 0.95f),
			BorderColor = new Color(0.45f, 0.42f, 0.35f, 0.8f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2
		};
		input.AddThemeStyleboxOverride("normal", inputStyle);
		input.AddThemeStyleboxOverride("focus", inputStyle);
		box.AddChild(input, forceReadableName: false, InternalMode.Disabled);

		void SyncText()
		{
			input.Text = quantity.ToString();
		}

		void Adjust(long delta)
		{
			quantity = Math.Clamp(quantity + delta, 1L, maxQuantity);
			SyncText();
		}

		input.TextChanged += delegate(string newText)
		{
			if (long.TryParse(newText.Trim(), out long val))
			{
				quantity = Math.Clamp(val, 1L, maxQuantity);
			}
		};

		Button Mini(string text, float x, float y, float w, Action pressed)
		{
			Button button = new Button
			{
				Text = text,
				Position = new Vector2(x, y),
				Size = new Vector2(w, 26f),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			button.AddThemeFontSizeOverride("font_size", 11);
			button.Pressed += pressed;
			box.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			return button;
		}

		Mini("−", 14f, 46f, 28f, delegate { Adjust(-1L); });
		Mini("＋", 156f, 46f, 28f, delegate { Adjust(1L); });
		Mini("一半", 192f, 46f, 66f, delegate
		{
			quantity = Math.Max(1L, maxQuantity / 2);
			SyncText();
		});
		Mini("全部", 264f, 46f, 82f, delegate
		{
			quantity = maxQuantity;
			SyncText();
		});

		void DoConfirm()
		{
			if (long.TryParse(input.Text.Trim(), out long directVal))
			{
				quantity = Math.Clamp(directVal, 1L, maxQuantity);
			}
			long finalQty = quantity;
			blocker.QueueFree();
			confirm(finalQty);
		}

		input.TextSubmitted += delegate
		{
			DoConfirm();
		};

		TextureButton textureButton = ClassicArtButtons.Confirm(delegate
		{
			DoConfirm();
		});
		textureButton.Position = new Vector2(20f, 96f);
		box.AddChild(textureButton, forceReadableName: false, InternalMode.Disabled);

		TextureButton textureButton2 = ClassicArtButtons.Cancel(delegate
		{
			blocker.QueueFree();
		});
		textureButton2.Position = new Vector2(190f, 97f);
		box.AddChild(textureButton2, forceReadableName: false, InternalMode.Disabled);
	}

	private void RefreshWarehouseSide(GameData data, WarehouseSide side, IReadOnlyList<ItemStack> stacks, bool fromBag, Action<ItemStack, bool> onMove, Action<string, string, bool> onDrop)
	{
		foreach (Node child in side.Grid.GetChildren())
		{
			side.Grid.RemoveChild(child);
			child.QueueFree();
		}
		List<ItemStack> list = stacks.Where((ItemStack itemStack) => ItemCategories.Matches(data, itemStack.ItemKey, side.Filter)).ToList();
		int num = Math.Max(1, (list.Count + 24 - 1) / 24);
		side.Page = Math.Clamp(side.Page, 0, num - 1);
		int num2 = side.Page * 24;
		float num3 = side.Grid.Size.X / 4f;
		float num4 = side.Grid.Size.Y / 6f;
		int num5 = 0;
		while (num5 < 24 && num2 + num5 < list.Count)
		{
			ItemStack stack = list[num2 + num5];
			int num6;
			object obj;
			if (fromBag)
			{
				if (!stack.Locked)
				{
					num6 = ((!WarehouseRules.CanStore(data, stack.ItemKey)) ? 1 : 0);
					if (num6 == 0)
					{
						goto IL_0195;
					}
				}
				else
				{
					num6 = 1;
				}
				obj = (stack.Locked ? "\n🔒 已鎖定·無法寄存" : "\n此物品不可寄存");
				goto IL_019a;
			}
			num6 = 0;
			goto IL_0195;
			IL_0195:
			obj = "";
			goto IL_019a;
			IL_019a:
			string text = (string)obj;
			InventoryGridSlot inventoryGridSlot = new InventoryGridSlot
			{
				Position = new Vector2((float)(num5 % 4) * num3, (float)(num5 / 4) * num4),
				Size = new Vector2(num3, num4),
				ItemKey = stack.ItemKey,
				StackUid = stack.Uid,
				Draggable = true,
				Locked = stack.Locked,
				Quality = (ItemQualityColors.Highlighted(stack) ? new Color?(ItemQualityColors.Of(stack)) : ((Color?)null)),
				QualityFrame = ItemQualityColors.Framed(stack),
				FrameQuality = (ItemQualityColors.Framed(stack) ? new Color?(ItemQualityColors.FrameOf(stack)) : ((Color?)null)),
				BlessingState = (stack.IsIdentified ? stack.Blessing : ItemBlessing.Normal),
				BrokenBlade = (stack.IsIdentified && stack.BrokenBladeStacks > 0),
				SingleClick = true,
				TooltipText = StackLabel(stack) + ItemInstanceText.DetailTooltip(GameDataProvider.Shared, stack) + text + (fromBag ? "\n點選或拖曳＝存入" : "\n點選或拖曳＝取回"),
				OnActivate = delegate
				{
					onMove(stack, fromBag);
				}
			};
			inventoryGridSlot.CanDropItem = (k, uid) => true;
			inventoryGridSlot.OnDropSwap = (k, uid) => onDrop(k, uid, !fromBag);
			inventoryGridSlot.OnDropToEmpty = (k, uid) => onDrop(k, uid, !fromBag);
			inventoryGridSlot.SetIcon(ItemIcons.For(stack.ItemKey));
			inventoryGridSlot.SetCorner(ItemInstanceText.StackCorner(stack));
			if (num6 != 0)
			{
				inventoryGridSlot.Modulate = new Color(1f, 0.72f, 0.72f, 0.55f);
			}
			side.Grid.AddChild(inventoryGridSlot, forceReadableName: false, InternalMode.Disabled);
			num5++;
		}
		while (num5 < 24)
		{
			InventoryGridSlot emptySlot = new InventoryGridSlot
			{
				Position = new Vector2((float)(num5 % 4) * num3, (float)(num5 / 4) * num4),
				Size = new Vector2(num3, num4),
				MouseFilter = MouseFilterEnum.Stop
			};
			emptySlot.CanDropItem = (k, uid) => true;
			emptySlot.OnDropToEmpty = (k, uid) => onDrop(k, uid, !fromBag);
			side.Grid.AddChild(emptySlot, forceReadableName: false, InternalMode.Disabled);
			num5++;
		}
		side.PageLabel.Text = ((list.Count != 0) ? $"{side.Page + 1} / {num}" : ((side.Filter == ItemCategory.All) ? "（空）" : "（沒有這一類）"));
		side.Previous.Disabled = side.Page == 0;
		side.Next.Disabled = side.Page >= num - 1;
	}

	private static string WhFailText(WarehouseTransferFailure f)
	{
		return f switch
		{
			WarehouseTransferFailure.Locked => "已鎖定", 
			WarehouseTransferFailure.NotStorable => "此物品不可寄存", 
			WarehouseTransferFailure.WarehouseFull => "倉庫已滿", 
			WarehouseTransferFailure.ItemNotFound => "找不到物品", 
			WarehouseTransferFailure.MissingItemDefinition => "缺物品定義", 
			WarehouseTransferFailure.QuantityOverflow => "數量溢位", 
			WarehouseTransferFailure.DuplicateUid => "UID 重複", 
			WarehouseTransferFailure.CorruptState => "狀態異常", 
			WarehouseTransferFailure.UidExhausted => "UID 用盡", 
			_ => "無效數量", 
		};
	}

	private void OpenZeusGolem(string displayName, string message = "")
	{
		VBoxContainer vBoxContainer = OpenPanel("製作 · " + displayName, new Vector2(700f, 590f), ornate: false, preserveRequestedFrame: true);
		vBoxContainer.AddChild(CraftLine("L1J-TW 原版六組武器製作（材料武器必須剛好 +7）。", CText, 14), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(CraftLine(message, message.StartsWith("✓") ? CGood : CBad, 14), forceReadableName: false, InternalMode.Disabled);
		}
		CraftScrollShell craftScrollShell = CreateCraftScroll(410f, 76f);
		VBoxContainer list = craftScrollShell.List;
		vBoxContainer.AddChild(craftScrollShell.Root, forceReadableName: false, InternalMode.Disabled);
		foreach (ZeusGolemWeaponRecipe recipe in ZeusGolemWeaponRules.Recipes)
		{
			ZeusGolemWeaponRecipe captured = recipe;
			VBoxContainer vBoxContainer2 = new VBoxContainer
			{
				CustomMinimumSize = new Vector2(190f, 0f)
			};
			vBoxContainer2.AddThemeConstantOverride("separation", 3);
			vBoxContainer2.AddChild(CraftLine("成品", CDim, 11), forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(CraftItemLine(recipe.OutputItemKey, ItemName(recipe.OutputItemKey), CGold, 15), forceReadableName: false, InternalMode.Disabled);
			long num = _session.Player.InventoryStacks.LongCount((ItemStack stack) => stack.ItemKey == recipe.FirstItemKey && stack.Enhancement == 7 && !stack.Locked);
			long num2 = _session.Player.InventoryStacks.LongCount((ItemStack stack) => stack.ItemKey == recipe.SecondItemKey && stack.Enhancement == 7 && !stack.Locked);
			long num3 = CombatInventory.AvailableCount(_session.Player, "l1j_item_41246");
			long num4 = CombatInventory.AvailableCount(_session.Player, "l1j_item_49143");
			List<Control> materialChips = new List<Control>
			{
				CraftMaterialChip(recipe.FirstItemKey, "+7 " + ItemName(recipe.FirstItemKey), num, 1L, num >= 1),
				CraftMaterialChip(recipe.SecondItemKey, "+7 " + ItemName(recipe.SecondItemKey), num2, 1L, num2 >= 1),
				CraftMaterialChip("l1j_item_41246", "魔法結晶體", num3, 1000L, num3 >= 1000),
				CraftMaterialChip("l1j_item_49143", "勇氣結晶", num4, 10L, num4 >= 10)
			};
			bool flag = num >= 1 && num2 >= 1 && num3 >= 1000 && num4 >= 10;
			TextureButton textureButton = ClassicTradeButtons.Confirm(delegate
			{
				ZeusGolemWeaponResult zeusGolemWeaponResult = ZeusGolemWeaponRules.TryCraft(GameDataProvider.Shared, _session.Player, captured.Action);
				if (zeusGolemWeaponResult.Success)
				{
					SaveManager.Save(_session);
					OpenZeusGolem(displayName, "✓ 製作完成：" + ItemName(captured.OutputItemKey));
				}
				else
				{
					string text = string.Join("、", zeusGolemWeaponResult.Missing.Select(ZeusMaterialName));
					OpenZeusGolem(displayName, "材料不足：" + text);
				}
			}, "確認製作 " + ItemName(recipe.OutputItemKey));
			textureButton.Disabled = !flag;
			if (textureButton.Disabled)
			{
				textureButton.SelfModulate = new Color(0.45f, 0.45f, 0.45f);
			}
			list.AddChild(CraftRecipeCard(vBoxContainer2, materialChips, flag ? "可製作" : "材料不足", flag ? CGood : CBad, textureButton), forceReadableName: false, InternalMode.Disabled);
		}
		CenterContainer centerContainer = new CenterContainer
		{
			CustomMinimumSize = new Vector2(0f, 30f)
		};
		centerContainer.AddChild(ClassicTradeButtons.Cancel(CloseOverlay, "取消並關閉製作"), forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(centerContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private static string ZeusMaterialName(string value)
	{
		if (value.StartsWith("+7 ", StringComparison.Ordinal))
		{
			return "+7 " + ItemName(value.Substring(3));
		}
		int num = value.IndexOf(" ×", StringComparison.Ordinal);
		if (num <= 0)
		{
			return value;
		}
		return ItemName(value.Substring(0, num)) + value.Substring(num);
	}
}
