using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class TownScreen
{
	private void OpenEnchantScrollPanel(string scrollUid, bool isWeapon, string message = "")
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _session.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		if (scroll == null)
		{
			CloseOverlay();
			return;
		}

		string scrollName = L1jItemIdentityRules.DisplayName(shared, scroll);
		string typeStr = isWeapon ? "武器" : "防具";
		VBoxContainer vBoxContainer = OpenPanel(scrollName, new Vector2(560f, 520f));
		Label introLabel = Row($"選擇要施法的{typeStr}。安定值內強化機率 100%；超過安定值有機率損毀消失。", CText);
		introLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		introLabel.CustomMinimumSize = new Vector2(0f, 44f);
		vBoxContainer.AddChild(introLabel, forceReadableName: false, InternalMode.Disabled);

		if (!string.IsNullOrWhiteSpace(message))
		{
			vBoxContainer.AddChild(Row(message, message.StartsWith("✓") ? CGood : CBad), forceReadableName: false, InternalMode.Disabled);
		}

		string targetType = isWeapon ? "wpn" : "arm";
		List<ItemStack> candidates = _session.Player.InventoryStacks
			.Concat(_session.Player.EquippedItems.Values)
			.Where(s => !s.Sealed)
			.Where(s =>
			{
				JsonObject? def = shared.Item(s.ItemKey);
				if (def == null) return false;
				if (def["noEnhance"]?.GetValue<bool>() == true) return false;
				return string.Equals(def["type"]?.GetValue<string>(), targetType, StringComparison.OrdinalIgnoreCase);
			})
			.DistinctBy(s => s.Uid)
			.OrderByDescending(s => _session.Player.EquippedItems.Values.Any(eq => eq.Uid == s.Uid))
			.ThenByDescending(s => s.Enhancement)
			.ThenBy(s => L1jItemIdentityRules.DisplayName(shared, s))
			.ToList();

		if (candidates.Count == 0)
		{
			vBoxContainer.AddChild(Row($"背包與身上沒有符合的{typeStr}（封印中或不可強化的裝備無法選擇）。", CDim), forceReadableName: false, InternalMode.Disabled);
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

			bool isEquipped = _session.Player.EquippedItems.Values.Any(eq => eq.Uid == captured.Uid);

			PanelContainer rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0f, 34f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = isEquipped ? new Color(0.12f, 0.16f, 0.22f, 0.85f) : new Color(0.08f, 0.09f, 0.11f, 0.95f),
				BorderColor = isEquipped ? Color.FromHtml("#3d5475") : new Color(0.25f, 0.23f, 0.18f, 0.6f),
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

			Control iconControl = ItemIcons.Slot(captured.ItemKey);
			iconControl.CustomMinimumSize = new Vector2(28f, 28f);
			iconControl.Size = new Vector2(28f, 28f);
			hBoxContainer.AddChild(iconControl, forceReadableName: false, InternalMode.Disabled);

			string equipTag = isEquipped ? "[裝備中] " : "";
			string enhStr = captured.IsIdentified ? ((captured.Enhancement == 0) ? "+0 " : $"{captured.Enhancement:+#;-#} ") : "";
			string safeStr = $"（安定 +{safe}）";
			string baseName = L1jItemIdentityRules.DisplayName(shared, captured);

			Label label = Row($"{equipTag}{enhStr}{baseName}  {safeStr}", isEquipped ? Color.FromHtml("#8fd0ff") : CText);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			label.AutowrapMode = TextServer.AutowrapMode.Off;
			label.ClipText = true;
			hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);

			Button button = new Button
			{
				Text = "選擇強化",
				CustomMinimumSize = new Vector2(96f, 28f),
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			button.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.65f));
			button.AddThemeFontSizeOverride("font_size", 13);
			button.Pressed += delegate
			{
				OpenEnchantScrollConfirmation(scrollUid, captured.Uid, isWeapon);
			};
			hBoxContainer.AddChild(button, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer2.AddChild(rowPanel, forceReadableName: false, InternalMode.Disabled);
		}
	}

	private void OpenEnchantScrollConfirmation(string scrollUid, string targetUid, bool isWeapon)
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _session.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		ItemStack? target = _session.Player.InventoryStacks.FirstOrDefault(s => s.Uid == targetUid)
			?? _session.Player.EquippedItems.Values.FirstOrDefault(s => s.Uid == targetUid);

		if (scroll == null || target == null)
		{
			OpenEnchantScrollPanel(scrollUid, isWeapon, "物品已不在身上；未消耗任何物品。");
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

		bool isCursed = scroll.Blessing == ItemBlessing.Cursed || scroll.ItemKey.EndsWith("_c", StringComparison.OrdinalIgnoreCase) || scroll.ItemKey.Contains("cursed", StringComparison.OrdinalIgnoreCase);
		bool isBlessed = !isCursed && (scroll.Blessing == ItemBlessing.Blessed || scroll.ItemKey.EndsWith("_b", StringComparison.OrdinalIgnoreCase) || scroll.ItemKey.Contains("blessed", StringComparison.OrdinalIgnoreCase));
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
			double rate = ArpgEngineScreen.GetEnchantSuccessRate(isWeapon, target.Enhancement, safe, isBlessed, isCursed);
			string ratePercentStr = (rate * 100.0).ToString("0.##");
			successRateStr = $"{ratePercentStr}%（超過安定值；失敗時裝備損毀消失）";

			outcomeStr = isBlessed
				? $"+{target.Enhancement + 1}（小機率 +{target.Enhancement + 2}）"
				: $"+{target.Enhancement + 1}";
		}

		string targetName = L1jItemIdentityRules.DisplayName(shared, target);
		string scrollName = L1jItemIdentityRules.DisplayName(shared, scroll);

		VBoxContainer vBoxContainer = OpenPanel("確認強化裝備", new Vector2(520f, 380f));
		Color warningCol = isCursed ? CBad : (isSafe ? CText : CBad);
		Label label = Row(
			$"卷軸：{scrollName}\n" +
			$"目標：{EquippedMark(target)}{(target.Enhancement >= 0 ? $"+{target.Enhancement} " : $"{target.Enhancement} ")}{targetName}\n" +
			$"安定值：+{safe}\n" +
			$"成功後：{outcomeStr}\n" +
			$"成功率：{successRateStr}\n\n" +
			"確認後將消耗 1 張卷軸，不可復原。",
			warningCol, 15);
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
			ExecuteTownEnchant(scrollUid, targetUid, isWeapon, safe, isSafe, isBlessed, isCursed);
		}, "同意並強化"), forceReadableName: false, InternalMode.Disabled);

		hBoxContainer.AddChild(ClassicArtButtons.Cancel(delegate
		{
			OpenEnchantScrollPanel(scrollUid, isWeapon);
		}, "取消；不消耗卷軸"), forceReadableName: false, InternalMode.Disabled);

		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
	}

	private void ExecuteTownEnchant(string scrollUid, string targetUid, bool isWeapon, int safe, bool isSafe, bool isBlessed, bool isCursed)
	{
		GameData shared = GameDataProvider.Shared;
		ItemStack? scroll = _session.Player.InventoryStacks.FirstOrDefault(s => s.Uid == scrollUid);
		ItemStack? target = _session.Player.InventoryStacks.FirstOrDefault(s => s.Uid == targetUid)
			?? _session.Player.EquippedItems.Values.FirstOrDefault(s => s.Uid == targetUid);

		if (scroll == null || target == null)
		{
			CloseOverlay();
			return;
		}

		// Consume 1 scroll
		if (!CombatInventory.TryRemove(_session.Player, scroll.ItemKey, 1L))
		{
			if (scroll.Quantity > 1)
			{
				scroll.Quantity--;
			}
			else
			{
				_session.Player.InventoryStacks.Remove(scroll);
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
			FinishTownEnchant(scrollUid, isWeapon, resultMsg, shared);
			return;
		}

		if (isSafe)
		{
			success = true;
			if (isBlessed && _potionRng != null)
			{
				double roll = _potionRng.NextDouble();
				if (roll < 0.33) gain = 3;
				else if (roll < 0.66) gain = 2;
				else gain = 1;
			}
		}
		else
		{
			double rate = ArpgEngineScreen.GetEnchantSuccessRate(isWeapon, currentEnh, safe, isBlessed, isCursed);
			double roll = _potionRng?.NextDouble() ?? 0.5;
			success = (roll < rate);
			if (success && isBlessed && (_potionRng?.NextDouble() ?? 0.5) < 0.15)
			{
				gain = 2;
			}
		}

		if (success)
		{
			target.Enhancement += gain;
			target.IsIdentified = true;
			string resultMsg;
			if (isBlessed && gain > 1)
			{
				resultMsg = $"✓ 祝福強化成功！{targetName} 承受了殷海薩的祝福，大幅躍升至 +{target.Enhancement}，閃爍著耀眼的金色光芒！";
			}
			else
			{
				resultMsg = $"✓ 強化成功！{targetName} 變成 +{target.Enhancement}，閃爍著銀白色的光芒。";
			}
			FinishTownEnchant(scrollUid, isWeapon, resultMsg, shared);
		}
		else
		{
			// Destroy target
			bool removed = _session.Player.InventoryStacks.Remove(target);
			if (!removed)
			{
				foreach (var pair in _session.Player.EquippedItems.ToList())
				{
					if (pair.Value.Uid == target.Uid)
					{
						_session.Player.EquippedItems.Remove(pair.Key);
						break;
					}
				}
			}
			string resultMsg = $"強化失敗：{targetName} 承受不住強大的魔法力量而化為灰燼了。";
			FinishTownEnchant(scrollUid, isWeapon, resultMsg, shared);
		}
	}

	private void FinishTownEnchant(string scrollUid, bool isWeapon, string resultMsg, GameData shared)
	{
		CombatantBuilder.RefreshPlayer(_session.Player, shared);
		SaveManager.Save(_session);
		_townBagRefresh?.Invoke();

		if (_session.Player.InventoryStacks.Any(s => s.Uid == scrollUid))
		{
			OpenEnchantScrollPanel(scrollUid, isWeapon, resultMsg);
		}
		else
		{
			VBoxContainer vBoxContainer = OpenPanel("強化結果", new Vector2(460f, 250f));
			Label label = Row(resultMsg, resultMsg.StartsWith("✓") ? CGood : CBad, 15);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(0f, 90f);
			vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
			vBoxContainer.AddChild(ClassicArtButtons.Confirm(CloseOverlay, "關閉"), forceReadableName: false, InternalMode.Disabled);
		}
	}
}
