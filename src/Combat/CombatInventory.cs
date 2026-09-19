using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CombatInventory
{
	private static bool IsGold(string itemKey)
	{
		return string.Equals(itemKey, "gold", StringComparison.Ordinal);
	}

	public static long Count(Combatant owner, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (IsGold(itemKey))
		{
			return CombatWallet.Balance(owner);
		}
		return ItemStackInventory.CountByItemKey(owner.InventoryStacks, itemKey);
	}

	public static long AvailableCount(Combatant owner, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (IsGold(itemKey))
		{
			return CombatWallet.Balance(owner);
		}
		return ItemStackInventory.CountByItemKey(owner.InventoryStacks, itemKey, includeLocked: false);
	}

	public static long Count(IReadOnlyDictionary<string, long> inventory, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return 0L;
		}
		if (!inventory.TryGetValue(itemKey, out var value))
		{
			return 0L;
		}
		return Math.Max(0L, value);
	}

	public static long Add(Combatant owner, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (IsGold(itemKey))
		{
			if (quantity > 0)
			{
				return CombatWallet.Add(owner, quantity);
			}
			return CombatWallet.Balance(owner);
		}
		long num = Count(owner, itemKey);
		if (quantity <= 0)
		{
			return num;
		}
		long num2 = Math.Min(quantity, long.MaxValue - num);
		if (num2 <= 0)
		{
			return long.MaxValue;
		}
		ItemStack incoming = new ItemStack(NextUid(owner), itemKey, num2);
		if (!ItemStackInventory.TryAddOrStack(owner.InventoryStacks, incoming, out ItemStack _))
		{
			throw new InvalidOperationException($"Unable to add item stack '{itemKey}' to '{owner.Key}'.");
		}
		SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItem(owner, itemKey);
		return num + num2;
	}

	public static long Add(IGameData data, Combatant owner, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (IsGold(itemKey))
		{
			if (quantity > 0)
			{
				return CombatWallet.Add(owner, quantity);
			}
			return CombatWallet.Balance(owner);
		}
		long num = Count(owner, itemKey);
		if (quantity <= 0)
		{
			return num;
		}
		long num2 = Math.Min(quantity, long.MaxValue - num);
		if (num2 <= 0)
		{
			return long.MaxValue;
		}
		ItemStack incoming = new ItemStack(NextUid(owner), itemKey, num2);
		if (!ItemStackInventory.TryAddOrStack(data, owner.InventoryStacks, incoming, out ItemStack _))
		{
			throw new InvalidOperationException($"Unable to add item stack '{itemKey}' to '{owner.Key}'.");
		}
		SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItem(owner, itemKey);
		return num + num2;
	}

	public static long Add(Combatant owner, ItemStack incoming)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(incoming, "incoming");
		if (IsGold(incoming.ItemKey))
		{
			return CombatWallet.Add(owner, incoming.Quantity);
		}
		long num = Count(owner, incoming.ItemKey);
		if (num > long.MaxValue - incoming.Quantity)
		{
			throw new InvalidOperationException($"Unable to add item stack '{incoming.ItemKey}' to '{owner.Key}'.");
		}
		if (!ItemStackInventory.TryAddOrStack(owner.InventoryStacks, incoming, out ItemStack _))
		{
			throw new InvalidOperationException($"Unable to add item stack '{incoming.ItemKey}' to '{owner.Key}'.");
		}
		SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItem(owner, incoming.ItemKey);
		return num + incoming.Quantity;
	}

	public static long Add(IGameData data, Combatant owner, ItemStack incoming)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(incoming, "incoming");
		if (IsGold(incoming.ItemKey))
		{
			return CombatWallet.Add(owner, incoming.Quantity);
		}
		long num = Count(owner, incoming.ItemKey);
		if (num > long.MaxValue - incoming.Quantity)
		{
			throw new InvalidOperationException($"Unable to add item stack '{incoming.ItemKey}' to '{owner.Key}'.");
		}
		if (!ItemStackInventory.TryAddOrStack(data, owner.InventoryStacks, incoming, out ItemStack _))
		{
			throw new InvalidOperationException($"Unable to add item stack '{incoming.ItemKey}' to '{owner.Key}'.");
		}
		SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItem(owner, incoming.ItemKey);
		return num + incoming.Quantity;
	}

	public static long Add(IDictionary<string, long> inventory, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		long num = ReadCount(inventory, itemKey);
		if (quantity <= 0)
		{
			return num;
		}
		return inventory[itemKey] = ((num > long.MaxValue - quantity) ? long.MaxValue : (num + quantity));
	}

	public static bool TryRemove(Combatant owner, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (IsGold(itemKey))
		{
			if (quantity > 0)
			{
				return CombatWallet.TrySpend(owner, quantity);
			}
			return false;
		}
		bool num = ItemStackInventory.TryRemoveByItemKey(owner.InventoryStacks, itemKey, quantity);
		if (num)
		{
			SyncLegacyView(owner);
		}
		return num;
	}

	public static bool TryRemove(IDictionary<string, long> inventory, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		long num = ReadCount(inventory, itemKey);
		if (num < quantity)
		{
			return false;
		}
		long num2 = num - quantity;
		if (num2 == 0L)
		{
			inventory.Remove(itemKey);
		}
		else
		{
			inventory[itemKey] = num2;
		}
		return true;
	}

	public static bool TryTransfer(Combatant source, Combatant destination, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		if (IsGold(itemKey))
		{
			return CombatWallet.TryTransfer(source, destination, quantity);
		}
		if (source == destination)
		{
			return true;
		}
		if (AvailableCount(source, itemKey) < quantity)
		{
			return false;
		}
		List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(source.InventoryStacks)
			select stack.Copy()).ToList();
		List<ItemStack> list2 = (from stack in ItemStackInventory.CopyAll(destination.InventoryStacks)
			select stack.Copy()).ToList();
		long nextSequence = source.ItemUidSequence;
		HashSet<string> usedUids = new HashSet<string>(list.Select((ItemStack item) => item.Uid).Concat(list2.Select((ItemStack item) => item.Uid)).Concat(source.EquippedItems.Values.Select((ItemStack item) => item.Uid))
			.Concat(destination.EquippedItems.Values.Select((ItemStack item) => item.Uid)), StringComparer.Ordinal);
		long num = quantity;
		ItemStack[] array = list.ToArray();
		foreach (ItemStack itemStack in array)
		{
			if (num == 0L)
			{
				break;
			}
			if (!(itemStack.ItemKey != itemKey) && !itemStack.Locked)
			{
				long num3 = Math.Min(itemStack.Quantity, num);
				if (!ItemStackInventory.TryTransfer(list, list2, itemStack.Uid, num3, NextTransferUid))
				{
					return false;
				}
				num -= num3;
			}
		}
		if (num != 0L)
		{
			return false;
		}
		source.InventoryStacks = list;
		destination.InventoryStacks = list2;
		source.ItemUidSequence = nextSequence;
		SyncLegacyView(source);
		SyncLegacyView(destination);
		return true;
		string NextTransferUid()
		{
			string text;
			do
			{
				if (nextSequence == long.MaxValue)
				{
					throw new OverflowException("The item UID sequence is exhausted.");
				}
				text = $"{source.Key}:item:{++nextSequence}";
			}
			while (!usedUids.Add(text));
			return text;
		}
	}

	public static IReadOnlyDictionary<string, long> Snapshot(Combatant owner, bool includeLocked = true)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		IEnumerable<ItemStack> stacks;
		if (!includeLocked)
		{
			stacks = owner.InventoryStacks.Where((ItemStack stack) => !stack.Locked);
		}
		else
		{
			IEnumerable<ItemStack> inventoryStacks = owner.InventoryStacks;
			stacks = inventoryStacks;
		}
		return ItemStackInventory.ToPlainCounts(stacks);
	}

	public static void LoadPlainCounts(Combatant owner, IReadOnlyDictionary<string, long> inventory)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(inventory, "inventory");
		owner.InventoryStacks.Clear();
		foreach (var (text2, num2) in inventory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(text2, "itemKey");
			if (num2 <= 0)
			{
				throw new InvalidDataException("Inventory quantity for '" + text2 + "' must be positive.");
			}
			if (IsGold(text2))
			{
				CombatWallet.Add(owner, num2);
			}
			else
			{
				owner.InventoryStacks.Add(new ItemStack(NextUid(owner), text2, num2));
			}
		}
		SyncLegacyView(owner);
	}

	public static void LoadStacks(Combatant owner, IEnumerable<ItemStack> stacks, long uidSequence = 0L)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(stacks, "stacks");
		if (uidSequence < 0)
		{
			throw new ArgumentOutOfRangeException("uidSequence");
		}
		IReadOnlyList<ItemStack> readOnlyList = ItemStackInventory.CopyAll(stacks);
		HashSet<string> hashSet = new HashSet<string>(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid), StringComparer.Ordinal);
		foreach (ItemStack item in readOnlyList)
		{
			if (!hashSet.Add(item.Uid))
			{
				throw new InvalidDataException("Item UID '" + item.Uid + "' is shared by inventory and equipment.");
			}
		}
		foreach (ItemStack item2 in readOnlyList)
		{
			if (IsGold(item2.ItemKey))
			{
				CombatWallet.Add(owner, item2.Quantity);
			}
		}
		owner.InventoryStacks = (from stack in readOnlyList
			where !IsGold(stack.ItemKey)
			select stack.Copy()).ToList();
		owner.ItemUidSequence = Math.Max(owner.ItemUidSequence, uidSequence);
		SyncLegacyView(owner);
	}

	public static void SyncLegacyView(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		IReadOnlyDictionary<string, long> readOnlyDictionary = ItemStackInventory.ToPlainCounts(owner.InventoryStacks);
		owner.Inventory.Clear();
		foreach (var (key, value) in readOnlyDictionary)
		{
			owner.Inventory[key] = value;
		}
	}

	internal static string NextUid(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		HashSet<string> hashSet = new HashSet<string>(owner.InventoryStacks.Select((ItemStack item) => item.Uid), StringComparer.Ordinal);
		hashSet.UnionWith(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid));
		string text;
		do
		{
			if (owner.ItemUidSequence == long.MaxValue)
			{
				throw new OverflowException("The item UID sequence is exhausted.");
			}
			text = $"{owner.Key}:item:{++owner.ItemUidSequence}";
		}
		while (!hashSet.Add(text));
		return text;
	}

	public static bool TryTransfer(IDictionary<string, long> source, IDictionary<string, long> destination, string itemKey, long quantity)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(destination, "destination");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		long num = ReadCount(source, itemKey);
		if (num < quantity)
		{
			return false;
		}
		if (source == destination)
		{
			return true;
		}
		long num2 = ReadCount(destination, itemKey);
		if (num2 > long.MaxValue - quantity)
		{
			return false;
		}
		long num3 = num - quantity;
		if (num3 == 0L)
		{
			source.Remove(itemKey);
		}
		else
		{
			source[itemKey] = num3;
		}
		destination[itemKey] = num2 + quantity;
		return true;
	}

	private static long ReadCount(IDictionary<string, long> inventory, string itemKey)
	{
		if (!inventory.TryGetValue(itemKey, out var value))
		{
			return 0L;
		}
		return Math.Max(0L, value);
	}
}
