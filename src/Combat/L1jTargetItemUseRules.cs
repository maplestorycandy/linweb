using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jTargetItemUseRules
{
	public const int DarkEntBarkItemId = 40410;

	public const int BlackMagicPowderItemId = 40964;

	public const int GlueItemId = 41036;

	public const int SoulStoneItemId = 49188;

	public const int RustyFluteItemId = 49186;

	public const int SoulFluteItemId = 49189;

	public const int HistoryChancePercent = 50;

	public const int DiaryChancePercent = 67;

	public const double PolymorphDurationSeconds = 1800.0;

	private static readonly HashSet<int> DarkEntMobExclusions = new HashSet<int> { 45338, 45370, 45456, 45464, 45473, 45488, 45497, 45516, 45529, 45458 };

	private static readonly int[] DarkEntPolymorphGfx = new int[31]
	{
		29, 945, 947, 979, 1037, 1039, 3860, 3861, 3862, 3863,
		3864, 3865, 3904, 3906, 95, 146, 2374, 2376, 2377, 2378,
		3866, 3867, 3868, 3869, 3870, 3871, 3872, 3873, 3874, 3875,
		3876
	};

	public static bool IsDarkEntBark(IGameData data, string itemKey)
	{
		return MainItemId(data, itemKey) == 40410;
	}

	public static bool IsInventoryTargetItem(IGameData data, string itemKey)
	{
		int num = MainItemId(data, itemKey);
		if (num == 40964 || num == 41036 || num == 49188)
		{
			return true;
		}
		return false;
	}

	public static IReadOnlyList<ItemStack> EligibleInventoryTargets(IGameData data, Combatant owner, string sourceUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == sourceUid);
		if (itemStack == null)
		{
			return Array.Empty<ItemStack>();
		}
		int sourceId = MainItemId(data, itemStack.ItemKey);
		return (from item in owner.InventoryStacks
			where item.Uid != sourceUid
			where IsValidInventoryTarget(sourceId, MainItemId(data, item.ItemKey))
			orderby MainItemId(data, item.ItemKey)
			select item).ThenBy<ItemStack, string>((ItemStack item) => item.Uid, StringComparer.Ordinal).ToArray();
	}

	public static L1jTargetItemUseResult TryUseInventoryTargetItem(IGameData data, Combatant owner, string sourceUid, string targetUid, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(random, "random");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == sourceUid);
		if (itemStack == null)
		{
			return Fail(L1jTargetItemUseFailure.SourceMissing);
		}
		if (itemStack.Locked)
		{
			return Fail(L1jTargetItemUseFailure.SourceLocked);
		}
		int num = MainItemId(data, itemStack.ItemKey);
		if ((num != 40964 && num != 41036 && num != 49188) || 1 == 0)
		{
			return Fail(L1jTargetItemUseFailure.UnsupportedSource);
		}
		ItemStack itemStack2 = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == targetUid);
		if (itemStack2 == null)
		{
			return Fail(L1jTargetItemUseFailure.TargetMissing, num);
		}
		if (itemStack2.Locked)
		{
			return Fail(L1jTargetItemUseFailure.TargetLocked, num);
		}
		int num2 = MainItemId(data, itemStack2.ItemKey);
		if (!IsValidInventoryTarget(num, num2))
		{
			return Fail(L1jTargetItemUseFailure.InvalidTarget, num, num2);
		}
		int num3 = num switch
		{
			40964 => num2 + 8, 
			41036 => num2 + 10, 
			49188 => 49189, 
			_ => 0, 
		};
		string text = FindUniqueItemKey(data, num3);
		if (text == null)
		{
			return Fail(L1jTargetItemUseFailure.OutputMissing, num, num2, num3);
		}
		if (CombatInventory.Count(owner, text) == long.MaxValue)
		{
			return Fail(L1jTargetItemUseFailure.QuantityOverflow, num, num2, num3);
		}
		bool flag = num switch
		{
			40964 => random.NextDouble() * 100.0 < 50.0, 
			41036 => random.NextDouble() * 100.0 < 67.0, 
			49188 => true, 
			_ => false, 
		};
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		if (!ItemStackInventory.TryRemoveByUid(list, targetUid, 1L, out ItemStack removed) || !ItemStackInventory.TryRemoveByUid(list, sourceUid, 1L, out removed))
		{
			return Fail(L1jTargetItemUseFailure.TargetMissing, num, num2, num3);
		}
		long sequence;
		HashSet<string> used;
		if (flag)
		{
			sequence = owner.ItemUidSequence;
			used = list.Select((ItemStack item) => item.Uid).Concat(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid)).ToHashSet<string>(StringComparer.Ordinal);
			if (!ItemStackInventory.TryAddOrStack(data, list, new ItemStack(NextUid(), text, 1L), out removed))
			{
				return Fail(L1jTargetItemUseFailure.QuantityOverflow, num, num2, num3);
			}
			owner.ItemUidSequence = sequence;
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		if (flag)
		{
			CollectionRules.RegisterObtainedItem(owner, text);
		}
		return new L1jTargetItemUseResult(Attempted: true, flag, L1jTargetItemUseFailure.None, num, num2, flag ? num3 : 0, flag ? text : "");
		static L1jTargetItemUseResult Fail(L1jTargetItemUseFailure failure, int failedSourceId = 0, int failedTargetId = 0, int failedOutputId = 0)
		{
			return new L1jTargetItemUseResult(Attempted: false, Succeeded: false, failure, failedSourceId, failedTargetId, failedOutputId, "");
		}
		string NextUid()
		{
			string text2;
			do
			{
				if (sequence == long.MaxValue)
				{
					throw new OverflowException("Item UID exhausted.");
				}
				text2 = $"{owner.Key}:item:{++sequence}";
			}
			while (!used.Add(text2));
			return text2;
		}
	}

	public static DarkEntBarkResult TryUseDarkEntBark(IGameData data, Combatant attacker, Combatant target, string barkUid, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(random, "random");
		ItemStack itemStack = attacker.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == barkUid);
		if (itemStack == null || !IsDarkEntBark(data, itemStack.ItemKey))
		{
			return new DarkEntBarkResult(Attempted: false, Transformed: false, DarkEntBarkFailure.BarkMissing, "");
		}
		if (itemStack.Locked)
		{
			return new DarkEntBarkResult(Attempted: false, Transformed: false, DarkEntBarkFailure.BarkLocked, "");
		}
		CombatantKind kind = target.Kind;
		if ((uint)kind > 2u)
		{
			return new DarkEntBarkResult(Attempted: false, Transformed: false, DarkEntBarkFailure.InvalidTarget, "");
		}
		IReadOnlyList<PolymorphForm> readOnlyList = (from form in PolymorphRules.AllForms(data)
			where MemoryExtensions.Contains(DarkEntPolymorphGfx, form.Gfx)
			select form).DistinctBy((PolymorphForm form) => form.Gfx).ToArray();
		if (readOnlyList.Count == 0)
		{
			return new DarkEntBarkResult(Attempted: false, Transformed: false, DarkEntBarkFailure.NoPolymorphForms, "");
		}
		bool flag = attacker != target && random.NextDouble() * 100.0 >= (double)(3 * (attacker.Level - target.Level) + 100) - target.D.MagicResist;
		PolymorphForm polymorphForm = readOnlyList[Math.Clamp((int)(random.NextDouble() * (double)readOnlyList.Count), 0, readOnlyList.Count - 1)];
		bool flag2 = target.Kind == CombatantKind.Mob && (target.Level >= 50 || DarkEntMobExclusions.Contains(MainNpcId(data, target)));
		if (!ItemStackInventory.TryRemoveByUid(attacker.InventoryStacks, barkUid, 1L, out ItemStack _))
		{
			return new DarkEntBarkResult(Attempted: false, Transformed: false, DarkEntBarkFailure.BarkMissing, "");
		}
		CombatInventory.SyncLegacyView(attacker);
		if (flag || flag2)
		{
			return new DarkEntBarkResult(Attempted: true, Transformed: false, DarkEntBarkFailure.None, "");
		}
		target.PolymorphForm = polymorphForm.Name;
		target.Buffs["poly"] = 1800.0;
		if (target.Kind == CombatantKind.Player || (target.Kind == CombatantKind.Ally && !MonsterCompanionRules.IsCompanion(target)))
		{
			CombatantBuilder.RefreshPlayer(target, data);
		}
		return new DarkEntBarkResult(Attempted: true, Transformed: true, DarkEntBarkFailure.None, polymorphForm.Name);
	}

	private static bool IsValidInventoryTarget(int sourceId, int targetId)
	{
		return sourceId switch
		{
			40964 => targetId >= 41011 && targetId <= 41018, 
			41036 => targetId >= 41038 && targetId <= 41047, 
			49188 => targetId == 49186, 
			_ => false, 
		};
	}

	private static int MainItemId(IGameData data, string itemKey)
	{
		if (!(data.Item(itemKey)?["l1jItemId"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}

	private static int MainNpcId(IGameData data, Combatant target)
	{
		if (!(data.Mob(target.Key)?["npcid"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}

	private static string? FindUniqueItemKey(IGameData data, int itemId)
	{
		string text = null;
		foreach (var (text3, jsonNode2) in data.Items)
		{
			if (jsonNode2 is JsonObject jsonObject && jsonObject["l1jItemId"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value) && value == itemId)
			{
				if (text != null)
				{
					return null;
				}
				text = text3;
			}
		}
		return text;
	}
}
