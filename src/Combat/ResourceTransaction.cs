using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class ResourceTransaction
{
	public static ResourceTransactionResult TryApply(Combatant owner, ResourceTransactionPlan plan)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(plan, "plan");
		if (plan.GoldCost < 0 || plan.GoldReward < 0)
		{
			return ResourceTransactionResult.Failed(ResourceTransactionFailure.InvalidPlan);
		}
		if (!TryCopyEntries(plan.ItemCosts, out Dictionary<string, long> copy) || !TryCopyEntries(plan.ItemRewards, out Dictionary<string, long> copy2))
		{
			return ResourceTransactionResult.Failed(ResourceTransactionFailure.InvalidPlan);
		}
		long num = CombatWallet.Balance(owner);
		if (num < plan.GoldCost)
		{
			return ResourceTransactionResult.Failed(ResourceTransactionFailure.InsufficientGold);
		}
		long num2 = num - plan.GoldCost;
		if (num2 > long.MaxValue - plan.GoldReward)
		{
			return ResourceTransactionResult.Failed(ResourceTransactionFailure.GoldOverflow);
		}
		long gold = num2 + plan.GoldReward;
		Dictionary<string, long> dictionary = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (string item in copy.Keys.Concat(copy2.Keys).Distinct<string>(StringComparer.Ordinal))
		{
			long num3 = CombatInventory.Count(owner, item);
			long num4 = CombatInventory.AvailableCount(owner, item);
			long valueOrDefault = copy.GetValueOrDefault(item);
			long valueOrDefault2 = copy2.GetValueOrDefault(item);
			if (num4 < valueOrDefault)
			{
				return ResourceTransactionResult.Failed(ResourceTransactionFailure.InsufficientItem, item);
			}
			long num5 = num3 - valueOrDefault;
			if (num5 > long.MaxValue - valueOrDefault2)
			{
				return ResourceTransactionResult.Failed(ResourceTransactionFailure.InventoryOverflow, item);
			}
			dictionary[item] = num5 + valueOrDefault2;
		}
		List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select stack.Copy()).ToList();
		string key;
		long value;
		foreach (KeyValuePair<string, long> item2 in copy)
		{
			item2.Deconstruct(out key, out value);
			string itemKey = key;
			long quantity = value;
			if (!ItemStackInventory.TryRemoveByItemKey(list, itemKey, quantity))
			{
				return ResourceTransactionResult.Failed(ResourceTransactionFailure.InsufficientItem, itemKey);
			}
		}
		long nextSequence = owner.ItemUidSequence;
		HashSet<string> usedUids = new HashSet<string>(list.Select((ItemStack item) => item.Uid).Concat(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid)), StringComparer.Ordinal);
		foreach (KeyValuePair<string, long> item3 in copy2)
		{
			item3.Deconstruct(out key, out value);
			string itemKey2 = key;
			long quantity2 = value;
			ItemStack incoming = new ItemStack(NextUid(), itemKey2, quantity2);
			if (!ItemStackInventory.TryAddOrStack(list, incoming, out ItemStack _))
			{
				return ResourceTransactionResult.Failed(ResourceTransactionFailure.InventoryOverflow, itemKey2);
			}
		}
		owner.Gold = gold;
		owner.InventoryStacks = list;
		owner.ItemUidSequence = nextSequence;
		CombatInventory.SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItems(owner, copy2.Keys);
		return ResourceTransactionResult.Ok();
		string NextUid()
		{
			string text;
			do
			{
				if (nextSequence == long.MaxValue)
				{
					throw new OverflowException("The item UID sequence is exhausted.");
				}
				text = $"{owner.Key}:item:{++nextSequence}";
			}
			while (!usedUids.Add(text));
			return text;
		}
	}

	private static bool TryCopyEntries(IReadOnlyDictionary<string, long>? source, out Dictionary<string, long> copy)
	{
		copy = new Dictionary<string, long>(StringComparer.Ordinal);
		if (source == null)
		{
			return false;
		}
		foreach (var (text2, num2) in source)
		{
			if (string.IsNullOrWhiteSpace(text2) || num2 <= 0)
			{
				return false;
			}
			copy[text2] = num2;
		}
		return true;
	}
}
