using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class ShopBuybackLedger
{
	public sealed record Entry(string ShopKey, long Sequence, ItemStack Stack, long UnitPrice)
	{
		public string ItemKey => Stack.ItemKey;

		public long Quantity => Stack.Quantity;

		public long TotalPrice => UnitPrice * Stack.Quantity;
	}

	public const int CapacityPerShop = 10;

	private readonly Dictionary<string, List<Entry>> _byShop = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);

	private long _sequence;

	public IReadOnlyList<Entry> Entries(string shopKey)
	{
		if (!string.IsNullOrWhiteSpace(shopKey) && _byShop.TryGetValue(shopKey, out List<Entry> value))
		{
			return value.ToArray();
		}
		return Array.Empty<Entry>();
	}

	public Entry Record(string shopKey, ItemStack stack, long unitPrice)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(shopKey, "shopKey");
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (unitPrice < 0)
		{
			throw new ArgumentOutOfRangeException("unitPrice");
		}
		if (!_byShop.TryGetValue(shopKey, out List<Entry> value))
		{
			value = (_byShop[shopKey] = new List<Entry>(10));
		}
		Entry entry = new Entry(shopKey, ++_sequence, stack.Copy(), unitPrice);
		value.Insert(0, entry);
		while (value.Count > 10)
		{
			value.RemoveAt(value.Count - 1);
		}
		return entry;
	}

	public ShopBuybackResult TryBuyBack(IGameData data, Combatant buyer, string shopKey, long sequence)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(buyer, "buyer");
		if (string.IsNullOrWhiteSpace(shopKey) || !_byShop.TryGetValue(shopKey, out List<Entry> value))
		{
			return ShopBuybackResult.Failed(ShopBuybackFailure.EntryNotFound);
		}
		int num = value.FindIndex((Entry candidate) => candidate.Sequence == sequence);
		if (num < 0)
		{
			return ShopBuybackResult.Failed(ShopBuybackFailure.EntryNotFound);
		}
		Entry entry = value[num];
		if (CombatWallet.Balance(buyer) < entry.TotalPrice)
		{
			return ShopBuybackResult.Failed(ShopBuybackFailure.InsufficientGold);
		}
		ItemStack itemStack = entry.Stack.Copy(CombatInventory.NextUid(buyer));
		List<ItemStack> list = buyer.InventoryStacks.Select((ItemStack item) => item.Copy()).ToList();
		if (!ItemStackInventory.TryAddOrStack(list, itemStack, out ItemStack _))
		{
			return ShopBuybackResult.Failed(ShopBuybackFailure.InventoryOverflow);
		}
		if (!CombatWallet.TryCharge(buyer, entry.TotalPrice))
		{
			return ShopBuybackResult.Failed(ShopBuybackFailure.InsufficientGold);
		}
		buyer.InventoryStacks = list;
		CombatInventory.SyncLegacyView(buyer);
		CollectionRules.RegisterObtainedItem(buyer, itemStack.ItemKey);
		value.RemoveAt(num);
		return new ShopBuybackResult(Success: true, ShopBuybackFailure.None, entry.ItemKey, entry.Quantity, entry.TotalPrice);
	}

	public void Clear(string? shopKey = null)
	{
		if (string.IsNullOrWhiteSpace(shopKey))
		{
			_byShop.Clear();
		}
		else
		{
			_byShop.Remove(shopKey);
		}
	}
}
