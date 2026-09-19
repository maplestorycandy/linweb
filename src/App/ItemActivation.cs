using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class ItemActivation
{
	public static ItemStack? FindFirstInventoryStack(GameData data, Combatant owner, ItemAction action)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return owner.InventoryStacks.FirstOrDefault((ItemStack stack) => !stack.Locked && Classify(data, owner, stack) == action);
	}

	public static string ResolveFallbackTeleportMap(int l1jId, string itemKey, string itemName)
	{
		if (l1jId == 40100 || itemKey == "scroll_teleport" || itemName.Contains("瞬間移動卷軸"))
			return "random";
		if (l1jId == 40079 || l1jId == 40095 || itemKey == "scroll_return" || itemName.Contains("回家的卷軸"))
			return "home";

		// Classic dungeon scrolls (42068..42080)
		if (l1jId == 42068 || itemName.Contains("亞丁地下墓穴")) return "aden_sewer";
		if (l1jId == 42069 || itemName.Contains("冒險洞窟")) return "l1j_map_1";
		if (l1jId == 42070 || itemName.Contains("海底隧道")) return "undersea_passage";
		if (l1jId == 42071 || itemName.Contains("眠龍洞穴")) return "l1j_map_12";
		if (l1jId == 42072 || itemName.Contains("奇岩地監")) return "l1j_map_54";
		if (l1jId == 42073 || itemName.Contains("龍之谷地監")) return "dragon_valley_dungeon_7f";
		if (l1jId == 42074 || itemName.Contains("說話之島地監")) return "l1j_map_2";
		if (l1jId == 42075 || itemName.Contains("古魯丁地監")) return "l1j_map_10";
		if (l1jId == 42076 || itemName.Contains("螞蟻洞穴") || itemName.Contains("巨蟻洞穴")) return "ant_queen_lair";
		if (l1jId == 42077 || itemName.Contains("象牙塔")) return "ivory_tower_1f";
		if (l1jId == 42078 || itemName.Contains("傲慢之塔")) return "l1j_map_101";
		if (l1jId == 42079 || itemName.Contains("影之神殿") || itemName.Contains("影子神殿")) return "shadow_temple";
		if (l1jId == 42080 || itemName.Contains("欲望洞穴")) return "desire_cave_entrance";

		// Classic town scrolls (40801..40857)
		if (itemName.Contains("說話之島") && itemName.Contains("卷軸")) return "town_talking_island";
		if (itemName.Contains("古魯丁") && itemName.Contains("卷軸")) return "town_gludio";
		if (itemName.Contains("肯特") && itemName.Contains("卷軸")) return "town_kent";
		if (itemName.Contains("風木") && itemName.Contains("卷軸")) return "town_woodbeck";
		if (itemName.Contains("銀騎士") && itemName.Contains("卷軸")) return "town_silver_knight";
		if (itemName.Contains("奇岩") && itemName.Contains("卷軸")) return "town_giran";
		if (itemName.Contains("海音") && itemName.Contains("卷軸")) return "town_heine";
		if (itemName.Contains("亞丁") && itemName.Contains("卷軸")) return "town_aden";
		if (itemName.Contains("威頓") && itemName.Contains("卷軸")) return "town_werldan";
		if (itemName.Contains("燃柳") && itemName.Contains("卷軸")) return "town_orc";
		if (itemName.Contains("歐瑞") && itemName.Contains("卷軸")) return "mainland_south";

		return "";
	}

	public static ItemAction Classify(GameData data, Combatant owner, ItemStack stack)
	{
		string itemKey = stack.ItemKey;
		var itemDef = data.Item(itemKey) ?? new JsonObject();
		int num = CombatSkill.ReadInt(itemDef, "l1jItemId");
		string teleMap = itemDef["teleportMap"]?.GetValue<string>() ?? itemDef["teleport_map"]?.GetValue<string>() ?? itemDef["targetMap"]?.GetValue<string>() ?? "";
		bool flag = string.Equals(itemKey, "scroll_return", StringComparison.Ordinal) || num == 40079 || num == 40095 || teleMap == "home";
		if (flag)
		{
			return ItemAction.ReturnScroll;
		}
		if (string.Equals(itemKey, "scroll_teleport", StringComparison.Ordinal) || !string.IsNullOrEmpty(teleMap) || (num >= 42068 && num <= 42080) || (num >= 40801 && num <= 40857) || num == 40100)
		{
			return ItemAction.TeleportScroll;
		}
		if (string.Equals(itemKey, "scroll_revive", StringComparison.Ordinal))
		{
			return ItemAction.ReviveScroll;
		}
		if (PetCollarRules.IsCollar(data, itemKey))
		{
			return ItemAction.PetCollar;
		}
		if (PetAcquisitionRules.IsHatchableEgg(data, itemKey))
		{
			return ItemAction.PetEgg;
		}
		if (PetAcquisitionRules.IsTamingItem(data, itemKey))
		{
			return ItemAction.PetTamingFood;
		}
		if (IsPetEvolutionFruit(data, itemKey))
		{
			return ItemAction.PetEvolutionFruit;
		}
		switch (num)
		{
		case 40315:
			return ItemAction.PetWhistle;
		case 40119:
			return ItemAction.UncurseScroll;
		default:
		{
			if (L1jIdentifyRules.IsScroll(data, itemKey))
			{
				return ItemAction.IdentifyScroll;
			}
			if (num == 49013)
			{
				return ItemAction.SoulOrbContainer;
			}
			if (RoiBagRules.IsDefinition(data.Item(itemKey)))
			{
				return ItemAction.RoiBag;
			}
			if (IvoryQuiverRules.IsDefinition(data.Item(itemKey)))
			{
				return ItemAction.IvoryQuiver;
			}
			if (L1jCookingRules.IsCookingItemId(num))
			{
				return ItemAction.Cooking;
			}
			if (MonsterCardRules.IsCardDefinition(data.Item(itemKey)))
			{
				return ItemAction.MonsterCard;
			}
			if (PainwandRules.IsDefinition(data.Item(itemKey)))
			{
				return ItemAction.Painwand;
			}
			if (LanternRules.IsLanternDefinition(data.Item(itemKey)))
			{
				return ItemAction.MainLight;
			}
			if (num == 40007)
			{
				return ItemAction.MainWand;
			}
			if (TowerOfInsolenceCatalog.TryResolveTravelItem(data, itemKey, out var _))
			{
				return ItemAction.PrideTravel;
			}
			if (IsPrideUnseal(data, itemKey))
			{
				return ItemAction.PrideUnseal;
			}
			if (OrcEmissaryPolymorphRules.IsDefinition(data.Item(itemKey)))
			{
				return ItemAction.OrcEmissaryPolymorph;
			}
			if (IsPolymorphScroll(data, itemKey))
			{
				return ItemAction.PolymorphScroll;
			}
			if (L1jTargetItemUseRules.IsDarkEntBark(data, itemKey))
			{
				return ItemAction.DarkEntBark;
			}
			if (L1jTargetItemUseRules.IsInventoryTargetItem(data, itemKey))
			{
				return ItemAction.MainTargetItem;
			}
			if (LanternRules.IsOilDefinition(data.Item(itemKey)))
			{
				return ItemAction.LampOil;
			}
			if (IsRespecCandle(data, itemKey))
			{
				return ItemAction.RespecCandle;
			}
			if (IsSkillBook(data, itemKey))
			{
				return ItemAction.LearnSkill;
			}
			if (L1jElixirRules.TryRead(data, itemKey, out var _))
			{
				return ItemAction.Elixir;
			}
			if (IsPotion(data, itemKey))
			{
				return ItemAction.Consumable;
			}
			if (IsMagicDollContainer(itemKey))
			{
				return ItemAction.MagicDollContainer;
			}
			if (IsMagicDollItem(data, itemKey))
			{
				return ItemAction.MagicDollSummon;
			}
			if (string.Equals(itemKey, "l1j_item_41245", StringComparison.Ordinal))
			{
				return ItemAction.Resolvent;
			}
			if (L1jAttrEnchantRules.IsScroll(data, itemKey))
			{
				return ItemAction.AttributeScroll;
			}
			if (IsWeaponEnchantScroll(data, itemKey, num))
			{
				return ItemAction.EnchantWeapon;
			}
			if (IsArmorEnchantScroll(data, itemKey, num))
			{
				return ItemAction.EnchantArmor;
			}
			if (L1jSealRules.IsSealScroll(data, itemKey))
			{
				return ItemAction.SealScroll;
			}
			if (L1jSealRules.IsUnsealScroll(data, itemKey))
			{
				return ItemAction.UnsealScroll;
			}
			if (PurifyStoneRules.TryReadRefinableStone(data, itemKey, out var _))
			{
				return ItemAction.PurifyStone;
			}
			if (IsMagicScroll(data, itemKey))
			{
				return ItemAction.MagicScroll;
			}
			if (!IsPlayerGear(data, owner, stack))
			{
				return ItemAction.None;
			}
			return ItemAction.Equip;
		}
		}
	}

	private static bool IsPotion(GameData data, string itemKey)
	{
		return ConsumableRules.IsConsumableItem(data, itemKey);
	}

	public static (bool Ok, string Text) UseUncurseScroll(Combatant owner, ItemStack scroll)
	{
		L1jUncurseResult l1jUncurseResult = L1jUncurseRules.TryUse(owner, scroll.Uid);
		if (!l1jUncurseResult.Success)
		{
			return (Ok: false, Text: l1jUncurseResult.Failure);
		}
		if (l1jUncurseResult.CleansedEquipmentCount <= 0)
		{
			return (Ok: true, Text: "沒有已裝備的詛咒物品；卷軸仍依原版規則消耗");
		}
		return (Ok: true, Text: $"已解除 {l1jUncurseResult.CleansedEquipmentCount} 件裝備的詛咒");
	}

	public static (bool Ok, string Text) UseSoulOrbContainer(GameData data, Combatant owner, ItemStack source)
	{
		if (!L1jSoulOrbRules.TryOpen(owner, source.Uid))
		{
			return (Ok: false, Text: "魔族的卷軸無法開啟");
		}
		string text = data.Item("item_soul_orb")?["n"]?.GetValue<string>() ?? "靈魂之球";
		return (Ok: true, Text: "取得 " + text);
	}

	public static (bool Ok, string Text) UseRoiBag(GameData data, Combatant owner, ItemStack source, ICombatRandom random)
	{
		RoiBagResult roiBagResult = RoiBagRules.TryOpen(data, owner, source.Uid, random);
		if (!roiBagResult.Success)
		{
			return (Ok: false, Text: roiBagResult.Failure switch
			{
				RoiBagFailure.BagLocked => "鎖定中的羅伊袋子無法開啟", 
				RoiBagFailure.InventoryFull => $"背包已達 {175} 格", 
				RoiBagFailure.Overweight => $"負重超過 {90}%", 
				RoiBagFailure.RewardMissing => "羅伊袋子的獎品資料缺失", 
				RoiBagFailure.QuantityOverflow => "獎品數量已達上限", 
				_ => "背包中找不到羅伊的袋子", 
			});
		}
		string value = ((roiBagResult.RewardItemId == 40308) ? "金幣" : (data.Item(roiBagResult.RewardItemKey)?["n"]?.GetValue<string>() ?? roiBagResult.RewardItemKey));
		return (Ok: true, Text: $"取得 {value} ×{roiBagResult.RewardQuantity:N0}");
	}

	public static (bool Ok, string Text) UseIvoryQuiver(GameData data, Combatant owner, ItemStack source, long nowUnixMilliseconds)
	{
		IvoryQuiverUseResult ivoryQuiverUseResult = IvoryQuiverRules.TryUse(data, owner, source.Uid, nowUnixMilliseconds);
		if (ivoryQuiverUseResult.Success)
		{
			string value = data.Items.FirstOrDefault<KeyValuePair<string, JsonNode>>((KeyValuePair<string, JsonNode> pair) => CombatSkill.ReadInt((pair.Value as JsonObject) ?? new JsonObject(), "l1jItemId") == 49551).Value?["n"]?.GetValue<string>() ?? "象牙塔的箭";
			return (Ok: true, Text: $"取得 {value} ×{ivoryQuiverUseResult.RewardQuantity}");
		}
		return (Ok: false, Text: ivoryQuiverUseResult.Failure switch
		{
			IvoryQuiverFailure.CooldownActive => "箭筒尚在冷卻（剩餘 " + FormatCooldown(ivoryQuiverUseResult.RemainingCooldownSeconds) + "）", 
			IvoryQuiverFailure.InventoryFull => $"背包已達 {175} 格，無法打開箭筒", 
			IvoryQuiverFailure.Overweight => $"負重超過 {90}%，無法打開箭筒", 
			IvoryQuiverFailure.RewardMissing => "找不到象牙塔的箭資料", 
			IvoryQuiverFailure.QuantityOverflow => "象牙塔的箭數量已達上限", 
			IvoryQuiverFailure.ItemNotFound => "箭筒已不在背包中", 
			_ => "這不是可使用的象牙塔箭筒", 
		});
	}

	private static string FormatCooldown(long seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0L, seconds));
		return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
	}

	public static (bool Ok, string Text) UseCooking(GameData data, Combatant owner, ItemStack source)
	{
		int itemId = CombatSkill.ReadInt(data.Item(source.ItemKey) ?? new JsonObject(), "l1jItemId");
		L1jCookingUseResult l1jCookingUseResult = L1jCookingRules.TryUse(owner, source.Uid, itemId);
		return (Ok: l1jCookingUseResult.Success, Text: l1jCookingUseResult.Text);
	}

	public static (bool Ok, string Text) UseElixir(GameData data, Combatant owner, ItemStack stack)
	{
		L1jElixirResult l1jElixirResult = L1jElixirRules.TryUse(data, owner, stack.Uid);
		if (l1jElixirResult.Success)
		{
			return (Ok: true, Text: $"{l1jElixirResult.AttributeName}永久增加 1（{l1jElixirResult.AttributeValue}）；萬能藥 {l1jElixirResult.ElixirStatus}/{5}");
		}
		string item;
		switch (l1jElixirResult.Failure)
		{
		case L1jElixirFailure.AttributeMaximum:
		case L1jElixirFailure.ElixirMaximum:
			item = "屬性最大值只能到35。 請重試一次。";
			break;
		case L1jElixirFailure.ItemLocked:
			item = "鎖定中的萬能藥無法使用";
			break;
		case L1jElixirFailure.InvalidElixirState:
			item = "角色的萬能藥資料不一致";
			break;
		default:
			item = "這瓶萬能藥無法使用";
			break;
		}
		return (Ok: false, Text: item);
	}

	public static (bool Ok, string Text) UseMainLight(GameData data, Combatant owner, ItemStack source)
	{
		if (!LanternRules.IsLanternDefinition(data.Item(source.ItemKey)))
		{
			return (Ok: false, Text: "不是可用照明物品");
		}
		if (owner.EquippedItems.TryGetValue("lantern", out ItemStack value))
		{
			owner.EquippedItems.Remove("lantern");
			CombatInventory.Add(owner, value.Copy());
			if (value.ItemKey == source.ItemKey)
			{
				CombatEquipment.SyncLegacyView(owner);
				return (Ok: true, Text: "熄滅照明");
			}
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == source.Uid);
		if (itemStack == null || itemStack.Quantity <= 0)
		{
			return (Ok: false, Text: "物品不在背包");
		}
		ItemStack value2 = new ItemStack($"light:{owner.Key}:{++owner.ItemUidSequence}", itemStack.ItemKey, 1L)
		{
			OilPercent = itemStack.OilPercent
		};
		itemStack.Quantity--;
		if (itemStack.Quantity == 0L)
		{
			owner.InventoryStacks.Remove(itemStack);
		}
		owner.EquippedItems["lantern"] = value2;
		CombatInventory.SyncLegacyView(owner);
		return (Ok: true, Text: "點亮照明");
	}

	public static bool IsSkillBook(GameData data, string itemKey)
	{
		return string.Equals(data.Item(itemKey)?["type"]?.GetValue<string>(), "skillbk", StringComparison.Ordinal);
	}

	public static (bool Ok, string Text) UseSkillBook(GameData data, Combatant owner, ItemStack stack)
	{
		SkillLearningResult result = SkillLearningRules.TryLearn(data, owner, stack.Uid);
		if (!result.Success)
		{
			return (Ok: false, Text: SkillLearningFailureText(result));
		}
		return (Ok: true, Text: SkillLearningSuccessText(result));
	}

	public static string SkillLearningSuccessText(SkillLearningResult result)
	{
		return "學會技能：" + SkillInfo.Name(result.SkillId);
	}

	public static bool IsMagicScroll(GameData data, string itemKey)
	{
		JsonObject? obj = data.Item(itemKey);
		if (obj == null) return false;
		string name = obj["n"]?.GetValue<string>() ?? "";
		if (name.Contains("魔法卷軸") || string.Equals(itemKey, "scroll_magicbarrier", StringComparison.Ordinal))
		{
			return true;
		}
		string useType = obj["useType"]?.GetValue<string>() ?? "";
		if (useType.StartsWith("spell_", StringComparison.Ordinal) || useType == "blank")
		{
			return true;
		}
		int l1jId = CombatSkill.ReadInt(obj, "l1jItemId");
		if (l1jId >= 50001 && l1jId <= 50300)
		{
			return true;
		}
		return false;
	}

	public readonly record struct MagicScrollInfo(
		string SkillKey,
		string SpellName,
		bool IsAttack,
		bool IsHeal,
		bool IsTeleport,
		bool IsAntidote,
		bool IsBuff
	);

	public static MagicScrollInfo ResolveMagicScrollInfo(GameData data, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(stack, "stack");

		JsonObject? itemDef = data.Item(stack.ItemKey);
		if (itemDef == null)
		{
			return default;
		}

		string name = itemDef["n"]?.GetValue<string>() ?? "";
		int l1jId = CombatSkill.ReadInt(itemDef, "l1jItemId");

		string spellName = "";
		int openIdx = name.IndexOf('(');
		int closeIdx = name.IndexOf(')');
		if (openIdx >= 0 && closeIdx > openIdx)
		{
			spellName = name.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
		}
		else if (name.Contains("（") && name.Contains("）"))
		{
			int o = name.IndexOf('（');
			int c = name.IndexOf('）');
			if (o >= 0 && c > o)
			{
				spellName = name.Substring(o + 1, c - o - 1).Trim();
			}
		}

		string resolvedSkillKey = "";
		if (l1jId >= 50001 && l1jId <= 50300)
		{
			int officialSkillId = l1jId - 50000;
			resolvedSkillKey = officialSkillId switch
			{
				1 => "sk_heal1",
				2 => "sk_sunlight",
				3 => "sk_shield",
				4 => "sk_lightarrow",
				5 => "sk_teleport",
				6 => "sk_icearrow",
				7 => "sk_windblade",
				8 => "sk_holy_wpn",
				9 => "sk_antidote",
				10 => "sk_cold_shiver",
				11 => "sk_curse_poison",
				12 => "sk_enchant_weapon",
				13 => "sk_detection",
				14 => "sk_decrease_weight",
				15 => "sk_firearrow",
				16 => "sk_hell_fang",
				17 => "sk_aurora",
				18 => "sk_undead_bane",
				19 => "sk_heal_mid",
				20 => "sk_dark_blind",
				21 => "sk_shield2",
				22 => "sk_chill",
				23 => "sk_energy_sense",
				25 => "sk_fireball",
				26 => "sk_dex_up",
				27 => "sk_break",
				28 => "sk_vampire",
				29 => "sk_slow",
				30 => "sk_rock_prison",
				31 => "sk_magic_shield",
				32 => "sk_meditation",
				33 => "sk_mummy_curse",
				34 => "sk_thunder",
				35 => "sk_heal2",
				36 => "sk_charm",
				37 => "sk_holy_light",
				38 => "sk_ice_spike",
				39 => "sk_mana_drain",
				40 => "sk_dark_shadow",
				41 => "sk_zombie",
				42 => "sk_str_up",
				43 => "sk_haste_spell",
				44 => "sk_cancel",
				45 => "sk_earthquake",
				46 => "sk_blaze",
				47 => "sk_weaken",
				48 => "sk_bless_wpn",
				49 => "sk_regen",
				50 => "sk_ice_lance",
				52 => "sk_holy_dash",
				53 => "sk_tornado",
				54 => "sk_greater_haste",
				55 => "sk_berserk",
				56 => "sk_disease",
				57 => "sk_full_heal",
				58 => "sk_fire_prison",
				59 => "sk_blizzard",
				60 => "sk_invisible",
				61 => "sk_resurrection",
				63 => "sk_heal_energy_storm",
				64 => "sk_seal",
				65 => "sk_thunder_storm",
				66 => "sk_sleep_mist",
				68 => "sk_holy_barrier",
				74 => "sk_meteor",
				77 => "sk_disintegrate",
				78 => "sk_abs_barrier",
				79 => "sk_soul_up",
				80 => "sk_blizzard_storm",
				158 => "sk_elf_lifespring",
				169 => "sk_elf_physboost",
				181 => "sk_dragon_armor",
				_ => ""
			};
		}
		if (string.IsNullOrEmpty(resolvedSkillKey) && !string.IsNullOrEmpty(spellName))
		{
			foreach (var kvp in data.Skills)
			{
				if (kvp.Value is JsonObject sObj)
				{
					string sName = sObj["n"]?.GetValue<string>() ?? "";
					if (string.Equals(sName, spellName, StringComparison.Ordinal) || sName.Contains(spellName))
					{
						resolvedSkillKey = kvp.Key;
						break;
					}
				}
			}
		}

		if (string.IsNullOrEmpty(spellName))
		{
			spellName = !string.IsNullOrEmpty(resolvedSkillKey) ? SkillInfo.Name(resolvedSkillKey) : name;
		}

		bool isAttack = L1jSkillTargetRules.IsAttackSkill(data, resolvedSkillKey);
		bool isHeal = resolvedSkillKey is "sk_heal1" or "sk_heal_mid" or "sk_heal2" or "sk_full_heal" || spellName.Contains("治癒");
		bool isTeleport = resolvedSkillKey == "sk_teleport" || spellName.Contains("傳送");
		bool isAntidote = resolvedSkillKey == "sk_antidote" || spellName.Contains("解毒");
		bool isBuff = !isAttack && !isHeal && !isTeleport && !isAntidote && !string.IsNullOrEmpty(resolvedSkillKey);

		return new MagicScrollInfo(resolvedSkillKey, spellName, isAttack, isHeal, isTeleport, isAntidote, isBuff);
	}

	public static (bool Ok, string Text) UseMagicScroll(GameData data, Combatant owner, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(stack, "stack");

		if (stack.Locked)
		{
			return (Ok: false, Text: "物品鎖定中，無法使用");
		}

		if (owner.CastCd > 0.05)
		{
			return (Ok: false, Text: "魔法卷軸冷卻中，請稍候");
		}

		JsonObject? itemDef = data.Item(stack.ItemKey);
		if (itemDef == null)
		{
			return (Ok: false, Text: "找不到物品資料");
		}

		string name = itemDef["n"]?.GetValue<string>() ?? "";
		string useType = itemDef["useType"]?.GetValue<string>() ?? "";

		if (name.Contains("空的") || useType == "blank")
		{
			return (Ok: false, Text: "這是一張空的魔法卷軸，需要抄寫魔法後才能使用");
		}

		MagicScrollInfo info = ResolveMagicScrollInfo(data, stack);
		if (string.IsNullOrEmpty(info.SkillKey))
		{
			return (Ok: false, Text: "無法辨識此魔法卷軸所記載的法術");
		}

		if (info.IsAttack)
		{
			return (Ok: false, Text: $"{info.SpellName} 是攻擊型魔法卷軸，請在戰鬥中選取怪物目標施放");
		}

		if (info.IsTeleport)
		{
			return (Ok: false, Text: "傳送魔法卷軸請在野外或快捷欄中使用");
		}

		double buffDuration = 0.0;
		JsonObject? skObj = data.Skill(info.SkillKey);
		if (skObj != null)
		{
			int durVal = CombatSkill.ReadInt(skObj, "dur");
			if (durVal > 0) buffDuration = durVal;
		}

		if (buffDuration <= 0.0)
		{
			buffDuration = info.SkillKey switch
			{
				"sk_holy_barrier" => 32.0,
				"sk_abs_barrier" => 12.0,
				"sk_haste_spell" => 300.0,
				"sk_greater_haste" => 1200.0,
				"sk_holy_dash" => 30.0,
				"sk_str_up" => 1200.0,
				"sk_dex_up" => 1200.0,
				"sk_shield" => 1200.0,
				"sk_shield2" => 1200.0,
				"sk_holy_wpn" => 1200.0,
				"sk_bless_wpn" => 1200.0,
				"sk_berserk" => 300.0,
				"sk_invisible" => 120.0,
				"sk_dragon_armor" => 1200.0,
				"sk_elf_lifespring" => 300.0,
				"sk_elf_physboost" => 960.0,
				"sk_magic_shield" => 1200.0,
				"sk_soul_up" => 1200.0,
				_ => 180.0
			};
		}

		if (info.IsHeal)
		{
			if (!CombatInventory.TryRemove(owner, stack.ItemKey, 1L))
			{
				return (Ok: false, Text: "背包中已沒有足夠的卷軸");
			}
			double healAmount = info.SkillKey switch
			{
				"sk_heal1" => 35.0,
				"sk_heal_mid" => 70.0,
				"sk_heal2" => 130.0,
				"sk_full_heal" => 300.0,
				_ => 50.0
			};
			double actualHeal = Math.Min(owner.MaxHp - owner.Hp, healAmount);
			if (actualHeal > 0)
			{
				owner.Hp = Math.Min(owner.MaxHp, owner.Hp + actualHeal);
				owner.CastCd = Math.Max(owner.CastCd, 0.8);
				return (Ok: true, Text: $"施放 {info.SpellName}！回復 HP +{(int)actualHeal}");
			}
			owner.CastCd = Math.Max(owner.CastCd, 0.8);
			return (Ok: true, Text: $"施放 {info.SpellName}！HP 已是全滿狀態");
		}

		if (info.IsAntidote)
		{
			if (!CombatInventory.TryRemove(owner, stack.ItemKey, 1L))
			{
				return (Ok: false, Text: "背包中已沒有足夠的卷軸");
			}
			owner.Buffs.Remove("poison");
			owner.Buffs.Remove("curse_poison");
			owner.CastCd = Math.Max(owner.CastCd, 0.8);
			return (Ok: true, Text: $"施放 {info.SpellName}！已解除中毒狀態");
		}

		if (info.IsBuff)
		{
			if (!CombatInventory.TryRemove(owner, stack.ItemKey, 1L))
			{
				return (Ok: false, Text: "背包中已沒有足夠的卷軸");
			}
			owner.Buffs[info.SkillKey] = Math.Max(owner.Buffs.GetValueOrDefault(info.SkillKey), buffDuration);
			owner.CastCd = Math.Max(owner.CastCd, 0.8);
			return (Ok: true, Text: $"施放 {info.SpellName}！獲得持續增益效果（{buffDuration:0} 秒）");
		}

		owner.CastCd = Math.Max(owner.CastCd, 0.8);
		return (Ok: true, Text: $"成功使用魔法卷軸（{info.SpellName}）！");
	}

	public static (bool Ok, string Text) UsePurifyStone(GameData data, Combatant owner, ItemStack stack, ICombatRandom random)
	{
		PurifyStoneResult purifyStoneResult = PurifyStoneRules.TryPurify(data, owner, stack.Uid, random);
		if (!purifyStoneResult.Attempted)
		{
			return (Ok: false, Text: purifyStoneResult.Failure switch
			{
				PurifyStoneFailure.SkillNotLearned => "要先學會「提煉魔石」（黑暗妖精）", 
				PurifyStoneFailure.HighestTier => "五級黑魔石已是最高等級", 
				PurifyStoneFailure.InsufficientMana => $"MP 不足（需要 {5.0:0}）", 
				_ => "無法提煉這件物品", 
			});
		}
		if (!purifyStoneResult.Upgraded)
		{
			return (Ok: true, Text: "提煉失敗，黑魔石碎裂了。");
		}
		string text = data.Item(purifyStoneResult.OutputItemKey)?["n"]?.GetValue<string>() ?? purifyStoneResult.OutputItemKey;
		return (Ok: true, Text: "提煉成功：" + text);
	}

	public static string SkillLearningFailureText(SkillLearningResult result)
	{
		return result.Failure switch
		{
			SkillLearningFailure.ClassMismatch => "你的職業學不了這本書上的技能", 
			SkillLearningFailure.LevelTooLow => $"等級不足（需要 {result.RequiredLevel} 級）", 
			SkillLearningFailure.ElementNotSelected => "要先找艾利溫選定妖精屬性（妖精森林／象牙塔 2 樓）", 
			SkillLearningFailure.ElementMismatch => "屬性不符（這本書要「" + ElfElementRules.DisplayName(result.RequiredElement) + "」）", 
			SkillLearningFailure.NotSkillBook => "這不是技能書", 
			SkillLearningFailure.SkillDefinitionMissing => "技能定義缺失（資料問題）", 
			SkillLearningFailure.SkillReferenceMissing => "這本書沒有指向任何技能（資料問題）", 
			SkillLearningFailure.ItemDefinitionMissing => "找不到這本書的定義（資料問題）", 
			SkillLearningFailure.ItemNotFound => "背包裡沒有這本書", 
			SkillLearningFailure.UnsupportedActor => "只有角色本人能學技能", 
			SkillLearningFailure.AlreadyLearned => "已經學會這項技能", 
			_ => "無法學習", 
		};
	}

	public static bool IsPetEvolutionFruit(IGameData data, string itemKey)
	{
		return L1jPetTypeCatalog.Load(data).ByForm.Values.Any((L1jPetTypeDefinition definition) => string.Equals(definition.EvolutionItemKey, itemKey, StringComparison.Ordinal));
	}

	public static bool IsMagicDollContainer(string itemKey)
	{
		return string.Equals(itemKey, "doll_bag", StringComparison.Ordinal);
	}

	public static bool IsMagicDollItem(GameData data, string itemKey)
	{
		if (data.Item(itemKey)?["eff"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return string.Equals(value, "magic_doll", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool IsPolymorphScroll(GameData data, string itemKey)
	{
		if (PolymorphRules.IsShannaScroll(data, itemKey, out _))
		{
			return true;
		}
		if (data.Item(itemKey)?["eff"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return string.Equals(value, "poly", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool IsPrideUnseal(GameData data, string itemKey)
	{
		if (data.Item(itemKey)?["eff"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return string.Equals(value, "pride_unseal", StringComparison.Ordinal);
		}
		return false;
	}

	public static (bool Ok, string Text) UsePrideUnseal(GameData data, Combatant owner, ItemStack stack)
	{
		if (!(data.Item(stack.ItemKey)?["prideTier"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value) || !TowerOfInsolenceCatalog.TryNormalizeTravelTier(value, out var _))
		{
			return (Ok: false, Text: "這張封印符的樓層資訊損毀了");
		}
		string text = $"item_pride_pass_{value}";
		if (data.Item(text) == null)
		{
			return (Ok: false, Text: "找不到對應的傳送符定義");
		}
		if (!ItemStackInventory.TryRemoveByItemKey(owner.InventoryStacks, stack.ItemKey, 1L))
		{
			return (Ok: false, Text: "封印符不見了");
		}
		CombatInventory.Add(owner, text, 1L);
		return (Ok: true, Text: $"解除封印，獲得 傲慢之塔傳送符({value}F)");
	}

	public static bool IsPlayerGear(GameData data, Combatant owner, ItemStack stack)
	{
		EquipmentEligibilityResult equipmentEligibilityResult = EquipmentRules.Evaluate(data, owner, stack);
		if (!equipmentEligibilityResult.Allowed)
		{
			if (equipmentEligibilityResult.Failure != EquipmentEligibilityFailure.NotPlayerEquipment)
			{
				return equipmentEligibilityResult.Failure != EquipmentEligibilityFailure.MissingItemDefinition;
			}
			return false;
		}
		return true;
	}

	public static (bool Ok, string Text) UseConsumable(GameData data, Combatant owner, string uid, ICombatRandom random)
	{
		ConsumableUseResult consumableUseResult = ConsumableRules.TryUse(data, owner, uid, random);
		if (!consumableUseResult.Success)
		{
			return (Ok: false, Text: UseFailText(consumableUseResult.Failure));
		}
		return (Ok: true, Text: consumableUseResult.Kind switch
		{
			ConsumableKind.Healing => (consumableUseResult.HpRestored > 0.0) ? $"回復 HP +{(int)consumableUseResult.HpRestored}" : "HP 已滿", 
			ConsumableKind.Food => $"{ItemName(data, consumableUseResult.ItemKey)}：飽食度 +{consumableUseResult.SatietyRestored:0}（{SatietyRules.Percent(owner):0.#}%）", 
			ConsumableKind.Whetstone => "磨刀石：武器損壞度 -1", 
			ConsumableKind.Cure => ItemName(data, consumableUseResult.ItemKey) + "：已解除" + string.Join("、", (consumableUseResult.CuredStatusKinds ?? Array.Empty<string>()).Select(StatusLabels.StatusDisplayName)), 
			_ => consumableUseResult.BuffApplied ? $"獲得增益（{consumableUseResult.BuffDurationSeconds:0} 秒）" : "增益已在", 
		});
	}

	public static bool IsRespecCandle(GameData data, string itemKey)
	{
		if (data.Item(itemKey)?["eff"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			return string.Equals(value, "reset", StringComparison.Ordinal);
		}
		return false;
	}

	public static (bool Ok, string Text) UseRespecCandle(GameData data, Combatant owner, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (owner.Kind != CombatantKind.Player)
		{
			return (Ok: false, Text: "只有玩家可以使用回憶蠟燭");
		}
		if (!IsRespecCandle(data, stack.ItemKey))
		{
			return (Ok: false, Text: "這不是回憶蠟燭");
		}
		if (!L1jLevelStatRules.HasValidState(owner) || !L1jElixirRules.HasValidState(owner))
		{
			return (Ok: false, Text: "角色六維配點資料不完整");
		}
		if (owner.Allocations.Values.Sum((int value) => Math.Max(0, value)) + L1jLevelStatRules.SpentLevelPoints(owner) <= 0)
		{
			return (Ok: false, Text: "目前沒有可重置的配點");
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == stack.Uid);
		if (itemStack == null || itemStack.Quantity <= 0)
		{
			return (Ok: false, Text: "背包裡找不到回憶蠟燭");
		}
		if (itemStack.Locked)
		{
			return (Ok: false, Text: "鎖定中的回憶蠟燭無法使用");
		}
		if (!ItemStackInventory.TryRemove(owner.InventoryStacks, itemStack.Uid, 1L, () => CombatInventory.NextUid(owner), out ItemStack _))
		{
			return (Ok: false, Text: "回憶蠟燭無法消耗");
		}
		L1jLevelStatRules.ResetAllocations(owner);
		CombatInventory.SyncLegacyView(owner);
		CombatantBuilder.RefreshPlayer(owner, data);
		CombatEquipment.Revalidate(data, owner);
		return (Ok: true, Text: $"六維配點已重置，可在能力資訊窗重新分配 {L1jLevelStatRules.RemainingPoints(owner)} 點" + ((owner.ElixirStatus > 0) ? $"；其中 {owner.ElixirStatus} 點來自萬能藥" : ""));
	}

	public static (bool Ok, string Text) UseLampOil(GameData data, Combatant owner, ItemStack oilStack)
	{
		if (!CombatInventory.TryRemove(owner, oilStack.ItemKey, 1L))
		{
			return (Ok: false, Text: "鎖定中的燈油無法使用");
		}
		ItemStack itemStack = LanternRules.Refill(data, owner);
		if (itemStack == null)
		{
			CombatInventory.Add(owner, oilStack.ItemKey, 1L);
			bool flag = LanternRules.EquippedLantern(owner) != null || owner.InventoryStacks.Any((ItemStack stack) => LanternRules.IsLanternDefinition(data.Item(stack.ItemKey)));
			return (Ok: false, Text: flag ? "燈籠的油已經是滿的" : "沒有可以補油的燈籠");
		}
		bool flag2 = itemStack == LanternRules.EquippedLantern(owner);
		return (Ok: true, Text: flag2 ? "已補滿裝備中燈籠的油量（100%）" : "已補滿背包裡燈籠的油量（100%）");
	}

	public static (bool Ok, string Text) Equip(GameData data, Combatant owner, string uid, ICombatRandom random)
	{
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == uid);
		EquipmentChangeResult r = CombatEquipment.TryEquip(data, owner, uid);
		if (!r.Success)
		{
			return (Ok: false, Text: "無法穿戴：" + EquipFailText(r));
		}
		string text = ((r.AutomaticallyUnequippedSlots.Count > 0) ? $"（自動卸下 {r.AutomaticallyUnequippedSlots.Count} 件）" : "");
		string text2 = ((itemStack == null) ? ItemName(data, r.ItemKey) : L1jItemIdentityRules.DisplayName(data, itemStack));
		return (Ok: true, Text: "已穿戴：" + text2 + text + PolySuppressedText(data, owner));
	}

	public static (bool Ok, string Text) Unequip(GameData data, Combatant owner, string slot, ICombatRandom random)
	{
		owner.EquippedItems.TryGetValue(slot, out ItemStack value);
		EquipmentChangeResult r = CombatEquipment.TryUnequip(data, owner, slot);
		if (!r.Success)
		{
			return (Ok: false, Text: "無法卸下：" + EquipFailText(r));
		}
		string text = ((value == null) ? ItemName(data, r.ItemKey) : L1jItemIdentityRules.DisplayName(data, value));
		return (Ok: true, Text: "已卸下：" + text + PolySuppressedText(data, owner));
	}

	private static string PolySuppressedText(GameData data, Combatant owner)
	{
		IReadOnlyList<string> readOnlyList = PolymorphRules.SuppressedSlots(data, owner);
		if (readOnlyList.Count != 0)
		{
			return $"；變身中有 {readOnlyList.Count} 件裝備無法發動效果";
		}
		return "";
	}

	public static (bool Ok, string Text) UsePolymorph(GameData data, Combatant owner, string uid, ICombatRandom random, string? requestedForm = null)
	{
		PolymorphResult polymorphResult = PolymorphRules.TryUseScroll(data, owner, uid, random, requestedForm);
		if (!polymorphResult.Success)
		{
			return (Ok: false, Text: PolymorphFailText(polymorphResult.Failure));
		}
		CombatantBuilder.RefreshPlayer(owner, data);
		return (Ok: true, Text: "變身為 " + polymorphResult.FormName);
	}

	public static (bool Ok, string Text) UseOrcEmissaryPolymorph(GameData data, Combatant owner, string uid)
	{
		PolymorphResult polymorphResult = OrcEmissaryPolymorphRules.TryUse(data, owner, uid);
		if (!polymorphResult.Success)
		{
			return (Ok: false, Text: PolymorphFailText(polymorphResult.Failure));
		}
		CombatantBuilder.RefreshPlayer(owner, data);
		return (Ok: true, Text: "變身為 " + polymorphResult.FormName + "（15 分鐘）");
	}

	public static string UseFailText(ConsumableUseFailure f)
	{
		return f switch
		{
			ConsumableUseFailure.ActorDead => "已死亡", 
			ConsumableUseFailure.PotionCooldown => "冷卻中", 
			ConsumableUseFailure.HealingBlocked => "治療被封鎖", 
			ConsumableUseFailure.ItemUseBlocked => "無法使用物品", 
			ConsumableUseFailure.ItemLocked => "鎖定中的物品無法使用", 
			ConsumableUseFailure.ClassMismatch => "職業不符", 
			ConsumableUseFailure.DirectUseDisabled => "不可直接使用", 
			ConsumableUseFailure.NotConsumable => "非消耗品", 
			ConsumableUseFailure.ManualOnly => "僅限手動", 
			ConsumableUseFailure.SatietyFull => "已經吃飽了", 
			ConsumableUseFailure.NothingToCure => "身上沒有可以解除的狀態", 
			ConsumableUseFailure.NothingToRepair => "沒有可修復的裝備", 
			ConsumableUseFailure.RequiresSpecialHandler => "尚未支援此消耗品", 
			ConsumableUseFailure.LevelTooLow => "等級不足，無法使用此道具", 
			ConsumableUseFailure.LevelTooHigh => "等級過高，無法使用此道具", 
			ConsumableUseFailure.ItemDelayActive => "使用間隔中", 
			ConsumableUseFailure.ItemReuseDelay => "還無法再次使用", 
			_ => "無法使用", 
		};
	}

	public static string PainwandFailureText(PainwandFailure failure)
	{
		return failure switch
		{
			PainwandFailure.ItemMissing => "找不到創造怪物魔杖", 
			PainwandFailure.ItemLocked => "鎖定中的創造怪物魔杖無法使用", 
			PainwandFailure.MapBlocked => "這張地圖禁止使用創造怪物魔杖", 
			PainwandFailure.InvalidActor => "目前無法使用創造怪物魔杖", 
			PainwandFailure.NotPainwand => "這不是創造怪物魔杖", 
			_ => "創造怪物魔杖資料不完整", 
		};
	}

	public static string TargetItemFailureText(L1jTargetItemUseFailure failure)
	{
		return failure switch
		{
			L1jTargetItemUseFailure.SourceMissing => "找不到要使用的物品", 
			L1jTargetItemUseFailure.SourceLocked => "鎖定中的物品無法使用", 
			L1jTargetItemUseFailure.TargetMissing => "選擇的材料已不在背包", 
			L1jTargetItemUseFailure.TargetLocked => "鎖定中的材料無法消耗", 
			L1jTargetItemUseFailure.InvalidTarget => "這件物品不能處理所選材料", 
			L1jTargetItemUseFailure.OutputMissing => "產物資料缺失", 
			L1jTargetItemUseFailure.QuantityOverflow => "產物數量已達上限", 
			_ => "無法使用這件物品", 
		};
	}

	public static string DarkEntBarkFailureText(DarkEntBarkFailure failure)
	{
		return failure switch
		{
			DarkEntBarkFailure.BarkMissing => "找不到黑暗安特的樹皮", 
			DarkEntBarkFailure.BarkLocked => "鎖定中的黑暗安特樹皮無法使用", 
			DarkEntBarkFailure.InvalidTarget => "這個目標不能被樹皮變形", 
			DarkEntBarkFailure.NoPolymorphForms => "原版隨機變形形態資料缺失", 
			_ => "無法使用黑暗安特的樹皮", 
		};
	}

	public static string PetCollarFailureText(PetCollarFailure failure)
	{
		return failure switch
		{
			PetCollarFailure.ItemNotFound => "背包裡找不到這條項圈", 
			PetCollarFailure.ItemLocked => "鎖定中的項圈無法使用", 
			PetCollarFailure.NotPetCollar => "這不是寵物項圈", 
			PetCollarFailure.UnboundCollar => "這條項圈沒有綁定寵物", 
			PetCollarFailure.UnknownPet => "項圈對應的寵物資料不存在", 
			PetCollarFailure.ForeignPet => "這隻寵物正由其他主人帶領", 
			PetCollarFailure.MissingWhistle => "需要一個寵物哨子（41160）", 
			PetCollarFailure.InsufficientCharm => "魅力不足，無法召喚這隻寵物", 
			PetCollarFailure.AlreadyActive => "這隻寵物已經出戰；請向寵物保管員寄放", 
			PetCollarFailure.InvalidOwner => "目前無法使用寵物項圈", 
			_ => "寵物項圈無法使用", 
		};
	}

	public static string EligFailText(EquipmentEligibilityFailure f)
	{
		return f switch
		{
			EquipmentEligibilityFailure.AvatarMismatch => "性別不符", 
			EquipmentEligibilityFailure.ClassMismatch => "職業不符", 
			EquipmentEligibilityFailure.SlotLockedByLevel => "等級不足", 
			EquipmentEligibilityFailure.UniqueItemAlreadyEquipped => "唯一裝備已穿戴", 
			EquipmentEligibilityFailure.DuplicateEarring => "耳環同名", 
			EquipmentEligibilityFailure.RingCopyLimit => "戒指數量上限", 
			EquipmentEligibilityFailure.CursedEquipmentConflict => "受詛咒衝突", 
			EquipmentEligibilityFailure.PetEquipmentOnly => "寵物專用", 
			EquipmentEligibilityFailure.LevelTooLow => "等級不足，無法使用此道具", 
			EquipmentEligibilityFailure.LevelTooHigh => "等級過高，無法使用此道具", 
			_ => "不可裝備", 
		};
	}

	public static string EquipFailText(EquipmentChangeResult r)
	{
		return r.Failure switch
		{
			EquipmentChangeFailure.EligibilityRejected => EligFailText(r.EligibilityFailure), 
			EquipmentChangeFailure.CursedEquipment => "受詛咒·無法卸下", 
			EquipmentChangeFailure.InventoryOverflow => "背包已滿", 
			EquipmentChangeFailure.ItemNotFound => "找不到物品", 
			EquipmentChangeFailure.SlotNotEquipped => "該欄位無裝備", 
			EquipmentChangeFailure.InvalidOwner => "無法操作", 
			_ => "無法變更裝備", 
		};
	}

	public static string PolymorphFailText(PolymorphFailure failure)
	{
		return failure switch
		{
			PolymorphFailure.ItemNotFound => "找不到變形卷軸", 
			PolymorphFailure.NotAPolymorphScroll => "這不是變形卷軸", 
			PolymorphFailure.ItemLocked => "鎖定中的變形卷軸無法使用", 
			PolymorphFailure.RequiresControlItem => "需要變形控制戒指或已裝備浣熊的變身葉", 
			PolymorphFailure.FormNotFound => "找不到指定的變身形態", 
			PolymorphFailure.LevelTooLow => "等級不足，尚無法使用此變身", 
			PolymorphFailure.WeaponMismatch => "目前武器無法使用此變身", 
			PolymorphFailure.NoCandidates => "目前沒有可用的變身形態", 
			_ => "無法變身", 
		};
	}

	private static string ItemName(GameData data, string itemKey)
	{
		return data.Item(itemKey)?["n"]?.GetValue<string>() ?? itemKey;
	}

	public static bool IsWeaponEnchantScroll(GameData data, string itemKey, int num = 0)
	{
		JsonObject? obj = data.Item(itemKey);
		if (obj == null)
		{
			return false;
		}
		string type = obj["type"]?.GetValue<string>() ?? "";
		if (string.Equals(type, "arm", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "wpn", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "acc", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (EquipmentRules.ResolveBaseSlot(obj).Length > 0)
		{
			return false;
		}
		if (itemKey.StartsWith("scroll_weapon", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (num == 0)
		{
			num = CombatSkill.ReadInt(obj, "l1jItemId");
		}
		if (num == 40087 || num == 140087 || num == 240087 || num == 40128)
		{
			return true;
		}
		string text = obj["n"]?.GetValue<string>() ?? "";
		if (text.Contains("卷軸") || text.Contains("卷") || itemKey.StartsWith("scroll_", StringComparison.OrdinalIgnoreCase))
		{
			if (text.Contains("魔法卷軸")) return false;
			if (L1jAttrEnchantRules.IsScroll(data, itemKey)) return false;
			if (text.Contains("對武器施法") || text.Contains("武器強化") || text.Contains("武器卷軸") || text.Contains("武卷"))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsArmorEnchantScroll(GameData data, string itemKey, int num = 0)
	{
		JsonObject? obj = data.Item(itemKey);
		if (obj == null)
		{
			return false;
		}
		string type = obj["type"]?.GetValue<string>() ?? "";
		if (string.Equals(type, "arm", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "wpn", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "acc", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (EquipmentRules.ResolveBaseSlot(obj).Length > 0)
		{
			return false;
		}
		if (itemKey.StartsWith("scroll_armor", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (num == 0)
		{
			num = CombatSkill.ReadInt(obj, "l1jItemId");
		}
		if (num == 40074 || num == 140074 || num == 240074 || num == 40127)
		{
			return true;
		}
		string text = obj["n"]?.GetValue<string>() ?? "";
		if (text.Contains("卷軸") || text.Contains("卷") || itemKey.StartsWith("scroll_", StringComparison.OrdinalIgnoreCase))
		{
			if (text.Contains("魔法卷軸")) return false;
			if (text.Contains("毀滅")) return false;
			if (text.Contains("對盔甲施法") || text.Contains("防具強化") || text.Contains("防具卷軸") || text.Contains("防卷"))
			{
				return true;
			}
		}
		return false;
	}
}
