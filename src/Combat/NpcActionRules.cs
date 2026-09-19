using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class NpcActionRules
{
	public const int AdenaItemId = 40308;

	public const string QuestSourceFile = "Quest.xml";

	public const string CustomWarriorTrialSourceFile = "custom-warrior-web-content.json";

	public static bool IsQuestSource(NpcActionDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(definition, "definition");
		if (!string.Equals(definition.Source, "Quest.xml", StringComparison.Ordinal))
		{
			if (string.Equals(definition.Source, "custom-warrior-web-content.json", StringComparison.Ordinal))
			{
				return definition.QuestId?.StartsWith("CustomWarrior", StringComparison.Ordinal) ?? false;
			}
			return false;
		}
		return true;
	}

	public static IReadOnlyList<NpcActionShortfall> MissingMaterials(IGameData data, Combatant actor, NpcActionDefinition definition, long amount = 1L, WarehouseState? warehouse = null)
	{
		return (from row in MaterialAvailability(data, actor, definition, amount, warehouse)
			where !row.Enough
			select new NpcActionShortfall(row.Name, row.Required, row.Held)).ToArray();
	}

	public static IReadOnlyList<NpcActionMaterialAvailability> MaterialAvailability(IGameData data, Combatant actor, NpcActionDefinition definition, long amount = 1L, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		List<NpcActionMaterialAvailability> list = new List<NpcActionMaterialAvailability>();
		if (amount <= 0)
		{
			return list;
		}
		foreach (NpcActionItem material in definition.Materials)
		{
			long required;
			try
			{
				required = checked(material.Count * amount);
			}
			catch (OverflowException)
			{
				required = long.MaxValue;
			}
			list.Add(new NpcActionMaterialAvailability(NameOf(data, material), required, CountOf(actor, material, warehouse)));
		}
		return list;
	}

	public static long CraftableSets(Combatant actor, NpcActionDefinition definition, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		NpcActionKillProgress npcActionKillProgress = KillProgress(actor, definition);
		if ((object)npcActionKillProgress != null && !npcActionKillProgress.Complete)
		{
			return 0L;
		}
		long num = long.MaxValue;
		foreach (NpcActionItem material in definition.Materials)
		{
			long num2 = CountOf(actor, material, warehouse);
			num = Math.Min(num, num2 / Math.Max(1, material.Count));
		}
		if (definition.Materials.Count == 0)
		{
			return ((object)definition.KillRequirement != null) ? 1 : 0;
		}
		return Math.Max(0L, num);
	}

	public static NpcActionKillProgress? KillProgress(Combatant actor, NpcActionDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		NpcActionKillRequirement killRequirement = definition.KillRequirement;
		if ((object)killRequirement != null)
		{
			return new NpcActionKillProgress(killRequirement.TargetName, killRequirement.RequiredCount, NpcActionCatalog.KillCountOf(actor, killRequirement.CounterId));
		}
		return null;
	}

	public static NpcActionResult ExecuteMakeItem(IGameData data, Combatant actor, NpcActionDefinition definition, long amount, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		if (definition.Kind != "MakeItem")
		{
			throw new ArgumentException("'" + definition.Name + "' 不是 MakeItem 列。");
		}
		List<string> list = new List<string>();
		if (amount <= 0)
		{
			return Finish(actor, definition, success: false, list);
		}
		NpcActionKillProgress npcActionKillProgress = KillProgress(actor, definition);
		if ((object)npcActionKillProgress != null && !npcActionKillProgress.Complete)
		{
			list.Add($"試煉進度不足：{npcActionKillProgress.TargetName}（需擊殺 {npcActionKillProgress.Required}・目前 {npcActionKillProgress.Killed}）");
			return Finish(actor, definition, success: false, list);
		}
		foreach (NpcActionItem output in definition.Outputs)
		{
			if (!output.IsAdena && output.ItemKey == null)
			{
				list.Add($"此配方的產物（main item {output.L1jItemId}）在本作沒有載體" + "——main 死資料或已裁決退役，流程保留但不可執行。");
				return Finish(actor, definition, success: false, list);
			}
		}
		IReadOnlyList<NpcActionShortfall> readOnlyList = MissingMaterials(data, actor, definition, amount, warehouse);
		if (readOnlyList.Count > 0)
		{
			foreach (NpcActionShortfall item in readOnlyList)
			{
				list.Add($"材料不足：{item.Name}（需 {item.Required}・有 {item.Held}・缺 {item.Short}）");
			}
			return Finish(actor, definition, success: false, list);
		}
		double num = 0.0;
		foreach (NpcActionItem output2 in definition.Outputs)
		{
			if (!output2.IsAdena)
			{
				string itemName = data.Item(output2.ItemKey)?["n"]?.GetValue<string>() ?? "";
				num += WeightRules.ItemWeight(data, itemName) * (double)output2.Count * (double)amount;
			}
		}
		WeightReport weightReport = WeightRules.Evaluate(data, actor);
		if (!GameRateConfig.DisableWeightPenalty && weightReport.CurrentWeight + num > weightReport.TotalCapacity)
		{
			list.Add("負重不足，無法製作。");
			return Finish(actor, definition, success: false, list);
		}
		List<ItemStack> list2;
		List<ItemStack> list3;
		try
		{
			list2 = (from item in ItemStackInventory.CopyAll(actor.InventoryStacks)
				select item.Copy()).ToList();
			list3 = warehouse?.CopyItems();
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			list.Add("背包或個人倉庫資料異常，無法製作。");
			return Finish(actor, definition, success: false, list);
		}
		long num2 = 0L;
		foreach (NpcActionItem material in definition.Materials)
		{
			long num3 = material.Count * amount;
			if (material.IsAdena)
			{
				num2 += num3;
				continue;
			}
			if (material.ItemKey == null)
			{
				list.Add("材料不足：" + NameOf(data, material) + "（本作沒有可用載體）");
				return Finish(actor, definition, success: false, list);
			}
			long num4 = RemoveAvailable(list2, material.ItemKey, material.Blessing, num3);
			if (num4 > 0 && list3 != null)
			{
				num4 = RemoveAvailable(list3, material.ItemKey, material.Blessing, num4);
			}
			if (num4 <= 0)
			{
				continue;
			}
			list.Add("材料不足：" + NameOf(data, material) + "（鎖定中的堆疊不可用）");
			return Finish(actor, definition, success: false, list);
		}
		if (num2 > CombatWallet.Balance(actor))
		{
			list.Add("金幣不足，無法製作。");
			return Finish(actor, definition, success: false, list);
		}
		bool flag = IsQuestSource(definition);
		long num5 = 0L;
		long nextUidSequence = actor.ItemUidSequence;
		long num6 = actor.Progress.ItemGainAttemptSequence;
		HashSet<string> usedUids = new HashSet<string>(StringComparer.Ordinal);
		IEnumerable<string> enumerable = list2.Select((ItemStack item) => item.Uid).Concat(actor.EquippedItems.Values.Select((ItemStack item) => item.Uid));
		if (list3 != null)
		{
			enumerable = enumerable.Concat(list3.Select((ItemStack item) => item.Uid));
		}
		foreach (string item2 in enumerable)
		{
			if (!usedUids.Add(item2))
			{
				list.Add("背包與個人倉庫出現重複物品編號，無法安全製作。");
				return Finish(actor, definition, success: false, list);
			}
		}
		foreach (NpcActionItem output3 in definition.Outputs)
		{
			long num7 = output3.Count * amount;
			if (output3.IsAdena)
			{
				num5 += num7;
			}
			else
			{
				ItemGainPreview itemGainPreview;
				try
				{
					itemGainPreview = ItemGainRules.Preview(data, actor.Key, num6, output3.ItemKey, new ItemGainOptions(flag ? ItemGainSource.QuestReward : ItemGainSource.Crafting, output3.Blessing, Blank: false, ForceBlessed: false, RollBeforeForceBlessed: false, actor.Level));
				}
				catch (KeyNotFoundException)
				{
					list.Add("背包放不下 " + NameOf(data, output3) + "，無法製作。");
					return Finish(actor, definition, success: false, list);
				}
				if (itemGainPreview.UsesCommittedRoll)
				{
					if (num6 == long.MaxValue)
					{
						list.Add("物品取得序號已用盡，無法製作。");
						return Finish(actor, definition, success: false, list);
					}
					num6++;
				}
				long num8 = ((itemGainPreview.ItemLevel > 0) ? num7 : 1);
				long quantity = ((itemGainPreview.ItemLevel > 0) ? 1 : num7);
				for (long num9 = 0L; num9 < num8; num9++)
				{
					if (!ItemStackInventory.TryAddOrStack(data, list2, new ItemStack(NextOutputUid(), itemGainPreview.ResolvedItemKey, quantity)
					{
						Blessing = itemGainPreview.Blessing,
						Enhancement = itemGainPreview.Enhancement,
						ItemLevel = itemGainPreview.ItemLevel,
						Affixes = (itemGainPreview.Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>())
					}, out ItemStack _))
					{
						list.Add("背包放不下 " + NameOf(data, output3) + "，無法製作。");
						return Finish(actor, definition, success: false, list);
					}
				}
			}
			list.Add("獲得 " + NameOf(data, output3) + ((num7 > 1) ? $" ({num7})" : ""));
		}
		if (num2 > 0 && !CombatWallet.TrySpend(actor, num2))
		{
			list.Add("金幣不足，無法製作。");
			return Finish(actor, definition, success: false, list);
		}
		actor.InventoryStacks = list2;
		actor.ItemUidSequence = nextUidSequence;
		actor.Progress.ItemGainAttemptSequence = num6;
		if (warehouse != null && list3 != null)
		{
			warehouse.ReplaceItems(list3, warehouse.ItemUidSequence);
		}
		if (num5 > 0)
		{
			CombatWallet.Add(actor, num5);
		}
		CombatInventory.SyncLegacyView(actor);
		return Finish(actor, definition, success: true, list);
		string NextOutputUid()
		{
			string text;
			do
			{
				if (nextUidSequence == long.MaxValue)
				{
					throw new OverflowException("The item UID sequence is exhausted.");
				}
				text = $"{actor.Key}:item:{++nextUidSequence}";
			}
			while (!usedUids.Add(text));
			return text;
		}
	}

	public static readonly string[] FullBuffList = new string[]
	{
		// 基礎藥水與速度 (全職業通用)
		"haste",
		"brave",
		"blue",
		"cautious",
		// 法師 / 全職業通用強效增益
		"sk_shield",
		"sk_shield2",
		"sk_dex_up",
		"sk_str_up",
		"sk_bless_wpn",
		"sk_holy_wpn",
		"sk_ench_wpn",
		"sk_magic_shield",
		"sk_soul_up",
		"sk_meditation",
		"sk_load_up",
		"sk_holy_barrier",
		"sk_reduction_armor",
		"sk_solid_shield",
		// 防禦抗性增益
		"sk_elf_mr",
		"sk_elf_eleres",
		"sk_dark_mrup"
	};

	public static readonly string[] DebuffsToClear = new string[]
	{
		"stun", "silence", "magicseal", "poison", "poisonsilence", "poisonparalyzing", "weaken", "disease", "blind", "slow"
	};

	public static NpcActionResult ExecuteAction(Combatant actor, NpcActionDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		if (definition.Kind != "Action")
		{
			throw new ArgumentException("'" + definition.Name + "' 不是純 Action 列。");
		}
		List<string> lines = new List<string>();
		List<string> htmlIds = new List<string>();
		List<NpcActionEffect> teleports = new List<NpcActionEffect>();
		bool success = ApplyEffects(actor, definition.Effects, htmlIds, teleports, lines);
		return new NpcActionResult(success, lines, htmlIds, teleports);
	}

	private static NpcActionResult Finish(Combatant actor, NpcActionDefinition definition, bool success, List<string> lines)
	{
		List<string> htmlIds = new List<string>();
		List<NpcActionEffect> teleports = new List<NpcActionEffect>();
		ApplyEffects(actor, success ? definition.Succeed : definition.Fail, htmlIds, teleports, lines);
		return new NpcActionResult(success, lines, htmlIds, teleports);
	}

	private static bool ApplyEffects(Combatant actor, IReadOnlyList<NpcActionEffect> effects, List<string> htmlIds, List<NpcActionEffect> teleports, List<string>? lines = null)
	{
		foreach (NpcActionEffect effect in effects)
		{
			switch (effect.Kind)
			{
			case "quest":
				if (effect.IfQuestId == null || NpcActionCatalog.QuestStepOf(actor, effect.IfQuestId) == effect.IfQuestStep)
				{
					actor.Progress.QuestSteps[effect.QuestId] = effect.QuestStep;
				}
				break;
			case "killCount":
				if (effect.QuestStep <= 0)
				{
					actor.Progress.QuestKillCounts.Remove(effect.QuestId);
				}
				else
				{
					actor.Progress.QuestKillCounts[effect.QuestId] = effect.QuestStep;
				}
				break;
			case "html":
				if (effect.HtmlId.Length > 0)
				{
					htmlIds.Add(effect.HtmlId);
				}
				break;
			case "teleport":
				teleports.Add(effect);
				break;
			case "buff":
				if (effect.Price > 0)
				{
					if (CombatWallet.Balance(actor) < effect.Price)
					{
						lines?.Add($"金幣不足：需要 {effect.Price:N0}，持有 {CombatWallet.Balance(actor):N0}");
						return false;
					}
					CombatWallet.TryCharge(actor, effect.Price);
				}
				if (effect.Cure)
				{
					foreach (string debuff in DebuffsToClear)
					{
						actor.Buffs.Remove(debuff);
					}
				}
				if (effect.Heal)
				{
					actor.Hp = actor.MaxHp;
					actor.Mp = actor.MaxMp;
				}
				double dur = (effect.Duration > 0.0) ? effect.Duration : 1800.0;
				if (string.IsNullOrEmpty(effect.BuffKey) || string.Equals(effect.BuffKey, "all", StringComparison.OrdinalIgnoreCase))
				{
					foreach (string b in FullBuffList)
					{
						actor.Buffs[b] = dur;
					}
					lines?.Add("【媽祖賜福】全套增益狀態加持完成，體力與魔力已完全恢復！");
				}
				else
				{
					actor.Buffs[effect.BuffKey] = dur;
					lines?.Add($"【賜福加持】已獲得狀態【{effect.BuffKey}】！");
				}
				break;
			}
		}
		return true;
	}

	private static long CountOf(Combatant actor, NpcActionItem item, WarehouseState? warehouse)
	{
		if (item.IsAdena)
		{
			return CombatWallet.Balance(actor);
		}
		if (item.ItemKey == null)
		{
			return 0L;
		}
		long num = ItemStackInventory.CountByItemKeyAndBlessing(actor.InventoryStacks, item.ItemKey, item.Blessing, includeLocked: false);
		long num2 = ((warehouse == null) ? 0 : ItemStackInventory.CountByItemKeyAndBlessing(warehouse.Items, item.ItemKey, item.Blessing, includeLocked: false));
		if (num <= long.MaxValue - num2)
		{
			return num + num2;
		}
		return long.MaxValue;
	}

	private static long RemoveAvailable(IList<ItemStack> inventory, string itemKey, ItemBlessing blessing, long required)
	{
		long val = ItemStackInventory.CountByItemKeyAndBlessing(inventory, itemKey, blessing, includeLocked: false);
		long num = Math.Min(required, val);
		if (num <= 0)
		{
			return required;
		}
		if (!ItemStackInventory.TryRemoveByItemKeyAndBlessing(inventory, itemKey, blessing, num))
		{
			return required;
		}
		return required - num;
	}

	private static string NameOf(IGameData data, NpcActionItem item)
	{
		if (item.IsAdena)
		{
			return "金幣";
		}
		if (item.ItemKey != null)
		{
			return data.Item(item.ItemKey)?["n"]?.GetValue<string>() ?? item.ItemKey;
		}
		return $"main item {item.L1jItemId}";
	}
}
