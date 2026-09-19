using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class WarehouseState
{
	public const int DefaultCapacity = 5000;

	private List<ItemStack> _items = new List<ItemStack>();

	public string Key { get; }

	public int Capacity { get; }

	public long Gold { get; internal set; }

	public long ItemUidSequence { get; internal set; }

	public IReadOnlyList<ItemStack> Items => _items;

	public WarehouseState(string key = "standard", int capacity = 5000)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key, "key");
		if (capacity <= 0)
		{
			throw new ArgumentOutOfRangeException("capacity");
		}
		Key = key;
		Capacity = capacity;
	}

	internal List<ItemStack> CopyItems()
	{
		return (from item in ItemStackInventory.CopyAll(_items)
			select item.Copy()).ToList();
	}

	internal void ReplaceItems(IEnumerable<ItemStack> items, long uidSequence)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		if (uidSequence < 0)
		{
			throw new ArgumentOutOfRangeException("uidSequence");
		}
		IReadOnlyList<ItemStack> readOnlyList = ItemStackInventory.CopyAll(items);
		if (readOnlyList.Count > Capacity)
		{
			throw new InvalidOperationException($"Warehouse '{Key}' exceeds its {Capacity}-slot capacity.");
		}
		_items = readOnlyList.Select((ItemStack item) => item.Copy()).ToList();
		ItemUidSequence = uidSequence;
	}
}
