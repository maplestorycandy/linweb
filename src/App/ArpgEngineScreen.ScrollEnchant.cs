using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class ArpgEngineScreen
{
	private void OpenHuntEnchantScrollPanel(string scrollUid, bool isWeapon, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _engine.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		if (scroll == null)
		{
			CloseItemTargetOverlay();
			return;
		}

		string scrollName = L1jItemIdentityRules.DisplayName(shared, scroll);
		string typeStr = isWeapon ? "武器" : "防具";
		VBoxContainer frame = CreateItemTargetFrame(scrollName, new Vector2(540f, 430f));
		frame.AddChild(ItemPanelLabel($"選擇要施法的{typeStr}。安定值內強化機率 100%；超過安定值有機率損毀消失。", "#c9d1de", 14, 44f), forceReadableName: false, InternalMode.Disabled);
		if (!string.IsNullOrWhiteSpace(message))
		{
			frame.AddChild(ItemPanelLabel(message, message.StartsWith("✓") ? "#8fdd8f" : "#e2938f", 14, 36f), forceReadableName: false, InternalMode.Disabled);
		}

		string targetType = isWeapon ? "wpn" : "arm";
		List<ItemStack> candidates = _engine.Player.InventoryStacks
			.Concat(_engine.Player.EquippedItems.Values)
			.Where(s => !s.Sealed)
			.Where(s =>
			{
				JsonObject? def = shared.Item(s.ItemKey);
				if (def == null) return false;
				if (def["noEnhance"]?.GetValue<bool>() == true) return false;
				return string.Equals(def["type"]?.GetValue<string>(), targetType, StringComparison.OrdinalIgnoreCase);
			})
			.DistinctBy(s => s.Uid)
			.OrderByDescending(s => _engine.Player.EquippedItems.Values.Any(eq => eq.Uid == s.Uid))
			.ThenByDescending(s => s.Enhancement)
			.ThenBy(s => L1jItemIdentityRules.DisplayName(shared, s))
			.ToList();

		if (candidates.Count == 0)
		{
			frame.AddChild(ItemPanelLabel($"背包與身上沒有符合的{typeStr}（封印中或不可強化的裝備無法選擇）。", "#8b95a6", 14, 44f), forceReadableName: false, InternalMode.Disabled);
			return;
		}

		ScrollContainer scrollContainer = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(520f, 320f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		ClassicMapFrame.MakeScrollbarsTransparent(scrollContainer);

		VBoxContainer listContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		listContainer.AddThemeConstantOverride("separation", 4);
		scrollContainer.AddChild(listContainer, forceReadableName: false, InternalMode.Disabled);
		frame.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);

		foreach (ItemStack item in candidates)
		{
			ItemStack captured = item;

			JsonObject? itemDef = shared.Item(captured.ItemKey);
			int safe = CombatSkill.ReadInt(itemDef ?? new JsonObject(), "safe");
			if (safe == 0 && itemDef != null && itemDef.ContainsKey("safe"))
			{
				safe = 0;
			}
			else if (safe == 0)
			{
				safe = isWeapon ? 6 : 4;
			}

			bool isEquipped = _engine.Player.EquippedItems.Values.Any(eq => eq.Uid == captured.Uid);

			PanelContainer rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 34f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};

			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = isEquipped ? new Color(0.12f, 0.16f, 0.22f, 0.85f) : new Color(0.09f, 0.11f, 0.14f, 0.8f),
				BorderColor = isEquipped ? Color.FromHtml("#3d5475") : Color.FromHtml("#262c37"),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2
			};
			rowStyle.SetContentMarginAll(2f);
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

			HBoxContainer row = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Begin
			};
			row.AddThemeConstantOverride("separation", 8);

			Control iconControl = ItemIcons.Slot(captured.ItemKey);
			iconControl.CustomMinimumSize = new Vector2(28f, 28f);
			iconControl.Size = new Vector2(28f, 28f);
			row.AddChild(iconControl, forceReadableName: false, InternalMode.Disabled);

			string equipTag = isEquipped ? "[裝備中] " : "";
			string enhStr = captured.IsIdentified ? ((captured.Enhancement == 0) ? "+0 " : $"{captured.Enhancement:+#;-#} ") : "";
			string safeStr = $"（安定 +{safe}）";
			string baseName = L1jItemIdentityRules.DisplayName(shared, captured);

			Label nameLabel = new Label
			{
				Text = $"{equipTag}{enhStr}{baseName}  {safeStr}",
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
				ClipText = true,
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				CustomMinimumSize = new Vector2(280f, 28f)
			};
			nameLabel.AddThemeFontSizeOverride("font_size", 13);
			nameLabel.AddThemeColorOverride("font_color", isEquipped ? Color.FromHtml("#8fd0ff") : Color.FromHtml("#d2d8e4"));
			nameLabel.TooltipText = $"{equipTag}{enhStr}{baseName} {safeStr}\n當前強化：{enhStr.Trim()}\n安全上限：+{safe}";
			row.AddChild(nameLabel, forceReadableName: false, InternalMode.Disabled);

			Button btn = new Button
			{
				Text = "選擇強化",
				CustomMinimumSize = new Vector2(96f, 28f),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			btn.AddThemeFontSizeOverride("font_size", 12);

			StyleBoxFlat btnNormal = new StyleBoxFlat
			{
				BgColor = new Color(0.18f, 0.22f, 0.28f, 0.9f),
				BorderColor = Color.FromHtml("#4f5d75"),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2
			};
			btnNormal.SetContentMarginAll(2f);

			StyleBoxFlat btnHover = new StyleBoxFlat
			{
				BgColor = new Color(0.28f, 0.35f, 0.46f, 0.95f),
				BorderColor = Color.FromHtml("#8aa4cc"),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2
			};
			btnHover.SetContentMarginAll(2f);

			btn.AddThemeStyleboxOverride("normal", btnNormal);
			btn.AddThemeStyleboxOverride("hover", btnHover);
			btn.AddThemeStyleboxOverride("pressed", btnHover);
			btn.AddThemeColorOverride("font_color", Color.FromHtml("#e8c07a"));
			btn.AddThemeColorOverride("font_hover_color", Color.FromHtml("#ffffff"));

			btn.Pressed += delegate
			{
				OpenHuntEnchantScrollConfirmation(scrollUid, captured.Uid, isWeapon);
			};
			row.AddChild(btn, forceReadableName: false, InternalMode.Disabled);

			rowPanel.AddChild(row, forceReadableName: false, InternalMode.Disabled);
			listContainer.AddChild(rowPanel, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenHuntEnchantScrollConfirmation(string scrollUid, string targetUid, bool isWeapon)
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _engine.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		ItemStack? target = _engine.Player.InventoryStacks.FirstOrDefault(s => s.Uid == targetUid)
			?? _engine.Player.EquippedItems.Values.FirstOrDefault(s => s.Uid == targetUid);

		if (scroll == null || target == null)
		{
			OpenHuntEnchantScrollPanel(scrollUid, isWeapon, "物品已不在身上；未消耗任何物品。");
			return;
		}

		JsonObject? targetDef = shared.Item(target.ItemKey);
		int safe = CombatSkill.ReadInt(targetDef ?? new JsonObject(), "safe");
		if (safe == 0 && targetDef != null && targetDef.ContainsKey("safe"))
		{
			safe = 0;
		}
		else if (safe == 0)
		{
			safe = isWeapon ? 6 : 4;
		}

		bool isCursed = IsCursedScroll(scroll, shared);
		bool isBlessed = !isCursed && IsBlessedScroll(scroll, shared);
		bool isSafe = target.Enhancement < safe;

		string outcomeStr;
		string successRateStr;

		if (isCursed)
		{
			int nextVal = Math.Max(0, target.Enhancement - 1);
			successRateStr = "100%（詛咒安全施法；絕不損毀消失）";
			outcomeStr = $"+{nextVal}（強化值扣減 1）";
		}
		else if (isSafe)
		{
			successRateStr = "100%（安全強化值以內）";
			if (isBlessed)
			{
				outcomeStr = $"+{target.Enhancement + 1} ~ +{target.Enhancement + 3}（祝福隨機跳 +1~+3）";
			}
			else
			{
				outcomeStr = $"+{target.Enhancement + 1}";
			}
		}
		else
		{
			double rate = GetEnchantSuccessRate(isWeapon, target.Enhancement, safe, isBlessed, isCursed);
			string ratePercentStr = (rate * 100.0).ToString("0.##");
			successRateStr = $"{ratePercentStr}%（超過安定值；失敗時裝備損毀消失）";

			outcomeStr = isBlessed
				? $"+{target.Enhancement + 1}（小機率 +{target.Enhancement + 2}）"
				: $"+{target.Enhancement + 1}";
		}

		string targetName = L1jItemIdentityRules.DisplayName(shared, target);
		string scrollName = L1jItemIdentityRules.DisplayName(shared, scroll);

		VBoxContainer frame = CreateItemTargetFrame("確認強化裝備", new Vector2(500f, 340f));
		string warningCol = isCursed ? "#e8b4b8" : (isSafe ? "#c9d1de" : "#e2938f");
		frame.AddChild(ItemPanelLabel(
			$"卷軸：{scrollName}\n" +
			$"目標：{HuntEquippedMark(target)}{(target.Enhancement >= 0 ? $"+{target.Enhancement} " : $"{target.Enhancement} ")}{targetName}\n" +
			$"安定值：+{safe}\n" +
			$"成功後：{outcomeStr}\n" +
			$"成功率：{successRateStr}\n\n" +
			"確認後將消耗 1 張卷軸，不可復原。",
			warningCol, 15, 180f), forceReadableName: false, InternalMode.Disabled);

		HBoxContainer buttons = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttons.AddThemeConstantOverride("separation", 28);
		buttons.AddChild(ClassicArtButtons.Confirm(delegate
		{
			ExecuteHuntEnchant(scrollUid, targetUid, isWeapon, safe, isSafe, isBlessed, isCursed);
		}, "同意並強化"), forceReadableName: false, InternalMode.Disabled);

		buttons.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenHuntEnchantScrollPanel(scrollUid, isWeapon);
		}, "取消；不消耗卷軸"), forceReadableName: false, InternalMode.Disabled);

		frame.AddChild(buttons, forceReadableName: false, InternalMode.Disabled);
	}

	private void ExecuteHuntEnchant(string scrollUid, string targetUid, bool isWeapon, int safe, bool isSafe, bool isBlessed, bool isCursed)
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _engine.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		ItemStack? target = _engine.Player.InventoryStacks.FirstOrDefault(s => s.Uid == targetUid)
			?? _engine.Player.EquippedItems.Values.FirstOrDefault(s => s.Uid == targetUid);

		if (scroll == null || target == null)
		{
			CloseItemTargetOverlay();
			return;
		}

		// Consume 1 scroll
		if (!CombatInventory.TryRemove(_engine.Player, scroll.ItemKey, 1L))
		{
			if (scroll.Quantity > 1)
			{
				scroll.Quantity--;
			}
			else
			{
				_engine.Player.InventoryStacks.Remove(scroll);
			}
		}

		string targetName = L1jItemIdentityRules.DisplayName(shared, target);
		int currentEnh = target.Enhancement;
		bool success = false;
		int gain = 1;

		if (isCursed)
		{
			target.Enhancement = Math.Max(0, currentEnh - 1);
			target.IsIdentified = true;
			string resultMsg = $"✓ 詛咒施法成功！{targetName} 閃爍著赤紅色的光芒，強化值降為 +{target.Enhancement}。";
			SlabLog($"[color=#ff7777]{resultMsg}[/color]");
			FinishEnchant(scrollUid, isWeapon, resultMsg, shared);
			return;
		}

		if (isSafe)
		{
			success = true;
			if (isBlessed && _rng != null)
			{
				int roll = _rng.Next(100);
				if (roll < 33) gain = 3;
				else if (roll < 66) gain = 2;
				else gain = 1;
			}
		}
		else
		{
			double rate = GetEnchantSuccessRate(isWeapon, currentEnh, safe, isBlessed, isCursed);
			double roll = _rng?.NextDouble() ?? 0.5;
			success = (roll < rate);
			if (success && isBlessed && (_rng?.Next(100) ?? 50) < 15)
			{
				gain = 2;
			}
		}

		if (success)
		{
			target.Enhancement += gain;
			target.IsIdentified = true;
			string glowCol = isBlessed ? "#ffe877" : "#8fdd8f";
			string resultMsg;
			if (isBlessed && gain > 1)
			{
				resultMsg = $"✓ 祝福強化成功！{targetName} 承受了殷海薩的祝福，大幅躍升至 +{target.Enhancement}，閃爍著耀眼的金色光芒！";
			}
			else
			{
				resultMsg = $"✓ 強化成功！{targetName} 變成 +{target.Enhancement}，閃爍著銀白色的光芒。";
			}
			SlabLog($"[color={glowCol}]{resultMsg}[/color]");
			FinishEnchant(scrollUid, isWeapon, resultMsg, shared);
		}
		else
		{
			// Destroy target
			bool removed = _engine.Player.InventoryStacks.Remove(target);
			if (!removed)
			{
				foreach (var pair in _engine.Player.EquippedItems.ToList())
				{
					if (pair.Value.Uid == target.Uid)
					{
						_engine.Player.EquippedItems.Remove(pair.Key);
						break;
					}
				}
			}
			string resultMsg = $"強化失敗：{targetName} 承受不住強大的魔法力量而化為灰燼了。";
			SlabLog($"[color=#e2938f]{resultMsg}[/color]");
			FinishEnchant(scrollUid, isWeapon, resultMsg, shared);
		}
	}

	private void FinishEnchant(string scrollUid, bool isWeapon, string resultMsg, GameData shared)
	{
		CombatantBuilder.RefreshPlayer(_engine.Player, shared);
		SaveManager.Save(_session);
		SyncEquipmentAndBagUi();
		RefreshHud();

		if (_engine.Player.InventoryStacks.Any(s => s.Uid == scrollUid))
		{
			OpenHuntEnchantScrollPanel(scrollUid, isWeapon, resultMsg);
		}
		else
		{
			CloseItemTargetOverlay();
		}
	}

	public static double GetEnchantSuccessRate(bool isWeapon, int currentEnh, int safe, bool isBlessed, bool isCursed)
	{
		if (isCursed || currentEnh < safe)
		{
			return 1.0;
		}

		if (isWeapon)
		{
			// Safe 6: +6, +7, +8 are 33%
			if (currentEnh < safe + 3)
			{
				return 0.33;
			}
			// +9 -> +10
			return isBlessed ? 0.015 : 0.009;
		}
		else
		{
			// Armor:
			// +9 -> +10: Normal 0.3%, Blessed 1.11%
			if (currentEnh >= 9)
			{
				return isBlessed ? 0.0111 : 0.003;
			}
			// +4..+8: 1 / currentEnh (e.g. +4: 25%, +5: 20%, +6: 16.66%, +7: 14.28%, +8: 12.5%)
			if (currentEnh > 0)
			{
				return 1.0 / currentEnh;
			}
			return 0.33;
		}
	}

	private static bool IsCursedScroll(ItemStack scroll, GameData shared)
	{
		if (scroll.Blessing == ItemBlessing.Cursed) return true;
		if (scroll.ItemKey.EndsWith("_c", StringComparison.OrdinalIgnoreCase) || scroll.ItemKey.Contains("cursed", StringComparison.OrdinalIgnoreCase)) return true;
		return scrollDefName(shared, scroll).Contains("詛咒");
	}

	private static bool IsBlessedScroll(ItemStack scroll, GameData shared)
	{
		if (scroll.Blessing == ItemBlessing.Blessed) return true;
		if (scroll.ItemKey.EndsWith("_b", StringComparison.OrdinalIgnoreCase) || scroll.ItemKey.Contains("blessed", StringComparison.OrdinalIgnoreCase)) return true;
		return scrollDefName(shared, scroll).Contains("祝福");
	}

	private static string scrollDefName(GameData data, ItemStack scroll)
	{
		return data.Item(scroll.ItemKey)?["n"]?.GetValue<string>() ?? "";
	}
}
