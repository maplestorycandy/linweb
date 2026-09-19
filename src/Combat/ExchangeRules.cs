using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ExchangeRules
{
	public const string ShimizheNpcId = "npc_shimizhe";

	public const string IsmaelNpcId = "npc_ismael";

	private static readonly IReadOnlyList<ExchangeOption> IsmaelOptions = Array.AsReadOnly(Array.Empty<ExchangeOption>());

	public static IReadOnlyList<ExchangeOption> ExchangeOptions(IGameData data, string npcId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(npcId, "npcId");
		if (!(npcId == "npc_shimizhe"))
		{
			if (npcId == "npc_ismael")
			{
				return IsmaelOptions;
			}
			return Array.Empty<ExchangeOption>();
		}
		return BuildShimizheOptions(data);
	}

	public static IReadOnlyList<ExchangeShortfall> MissingCosts(Combatant owner, ExchangeOption option, long quantity = 1L, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(option, "option");
		List<ExchangeShortfall> list = new List<ExchangeShortfall>();
		if (quantity <= 0)
		{
			return list;
		}
		foreach (var (itemKey, num2) in option.ItemCosts)
		{
			long num3;
			try
			{
				num3 = checked(num2 * quantity);
			}
			catch (OverflowException)
			{
				num3 = long.MaxValue;
			}
			long num4 = SaturatingAdd(CombatInventory.AvailableCount(owner, itemKey), WarehouseAvailableCount(warehouse, itemKey));
			if (num4 < num3)
			{
				list.Add(new ExchangeShortfall(itemKey, num3, num4));
			}
		}
		return list;
	}

	public static long AffordableQuantity(Combatant owner, ExchangeOption option, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(option, "option");
		long num = ((option.GoldCost > 0) ? (CombatWallet.Balance(owner) / option.GoldCost) : long.MaxValue);
		foreach (var (itemKey, num3) in option.ItemCosts)
		{
			if (num3 <= 0)
			{
				return 0L;
			}
			long num4 = SaturatingAdd(CombatInventory.AvailableCount(owner, itemKey), WarehouseAvailableCount(warehouse, itemKey));
			num = Math.Min(num, num4 / num3);
		}
		return Math.Max(0L, num);
	}

	public static ExchangeResult TryExchange(IGameData data, Combatant owner, string npcId, string optionId, long quantity = 1L, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (string.IsNullOrWhiteSpace(npcId))
		{
			return ExchangeResult.Failed(ExchangeFailure.InvalidNpc);
		}
		if (string.IsNullOrWhiteSpace(optionId))
		{
			return ExchangeResult.Failed(ExchangeFailure.InvalidOption);
		}
		if (quantity <= 0)
		{
			return ExchangeResult.Failed(ExchangeFailure.InvalidQuantity, optionId);
		}
		IReadOnlyList<ExchangeOption> readOnlyList;
		try
		{
			readOnlyList = ExchangeOptions(data, npcId);
		}
		catch (InvalidDataException)
		{
			return ExchangeResult.Failed(ExchangeFailure.CorruptState, optionId);
		}
		if (readOnlyList.Count == 0)
		{
			return ExchangeResult.Failed(ExchangeFailure.InvalidNpc, optionId);
		}
		ExchangeOption exchangeOption = readOnlyList.FirstOrDefault((ExchangeOption candidate) => candidate.Id == optionId);
		if ((object)exchangeOption == null)
		{
			return ExchangeResult.Failed(ExchangeFailure.InvalidOption, optionId);
		}
		long num;
		long producedQuantity;
		List<ItemStack> list;
		List<ItemStack> list2;
		HashSet<string> hashSet;
		long sequence;
		long itemGainAttemptSequence;
		long num4;
		long num5;
		HashSet<string> hashSet2;
		checked
		{
			Dictionary<string, long> dictionary;
			try
			{
				dictionary = exchangeOption.ItemCosts.ToDictionary<KeyValuePair<string, long>, string, long>((KeyValuePair<string, long> pair) => pair.Key, (KeyValuePair<string, long> pair) => pair.Value * quantity, StringComparer.Ordinal);
				num = exchangeOption.GoldCost * quantity;
				producedQuantity = exchangeOption.RewardQuantity * quantity;
			}
			catch (OverflowException)
			{
				return ExchangeResult.Failed(ExchangeFailure.InvalidQuantity, optionId);
			}
			if (data.Item(exchangeOption.RewardItemKey) == null)
			{
				return ExchangeResult.Failed(ExchangeFailure.MissingItemDefinition, optionId, exchangeOption.RewardItemKey);
			}
			foreach (string key2 in dictionary.Keys)
			{
				if (data.Item(key2) == null)
				{
					return ExchangeResult.Failed(ExchangeFailure.MissingItemDefinition, optionId, key2);
				}
			}
			if (CombatWallet.Balance(owner) < num)
			{
				return ExchangeResult.Failed(ExchangeFailure.InsufficientGold, optionId);
			}
			string key;
			long value;
			foreach (KeyValuePair<string, long> item in dictionary)
			{
				item.Deconstruct(out key, out value);
				string itemKey = key;
				long num2 = value;
				if (SaturatingAdd(CombatInventory.AvailableCount(owner, itemKey), WarehouseAvailableCount(warehouse, itemKey)) < num2)
				{
					return ExchangeResult.Failed(ExchangeFailure.InsufficientItem, optionId, itemKey);
				}
			}
			try
			{
				list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
					select item.Copy()).ToList();
				list2 = warehouse?.CopyItems();
			}
			catch (Exception ex3) when (((ex3 is ArgumentException || ex3 is InvalidDataException) ? 1 : 0) != 0)
			{
				return ExchangeResult.Failed(ExchangeFailure.CorruptState, optionId);
			}
			foreach (KeyValuePair<string, long> item2 in dictionary)
			{
				item2.Deconstruct(out key, out value);
				string itemKey2 = key;
				long quantity2 = value;
				long num3 = RemoveAvailable(list, itemKey2, quantity2);
				if (num3 > 0 && list2 != null)
				{
					num3 = RemoveAvailable(list2, itemKey2, num3);
				}
				if (num3 != 0L)
				{
					return ExchangeResult.Failed(ExchangeFailure.InsufficientItem, optionId, itemKey2);
				}
			}
			hashSet = new HashSet<string>(StringComparer.Ordinal);
			IEnumerable<string> enumerable = list.Select((ItemStack item) => item.Uid).Concat(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid));
			if (list2 != null)
			{
				enumerable = enumerable.Concat(list2.Select((ItemStack item) => item.Uid));
			}
			foreach (string item3 in enumerable)
			{
				if (!hashSet.Add(item3))
				{
					return ExchangeResult.Failed(ExchangeFailure.CorruptState, optionId);
				}
			}
			sequence = owner.ItemUidSequence;
			itemGainAttemptSequence = owner.Progress.ItemGainAttemptSequence;
			num4 = itemGainAttemptSequence;
			num5 = 0L;
			hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		}
		for (long num6 = 0L; num6 < quantity; num6++)
		{
			ItemGainOptions itemGainOptions = exchangeOption.GainOptions;
			if (itemGainOptions.Source == ItemGainSource.Crafting && itemGainOptions.ItemLevel <= 0)
			{
				itemGainOptions = itemGainOptions with
				{
					ItemLevel = Math.Clamp(owner.Level, 1, 99)
				};
			}
			ItemGainPreview itemGainPreview;
			try
			{
				itemGainPreview = ItemGainRules.Preview(data, owner.Key, num4, exchangeOption.RewardItemKey, itemGainOptions);
			}
			catch (KeyNotFoundException)
			{
				return ExchangeResult.Failed(ExchangeFailure.MissingItemDefinition, optionId, exchangeOption.RewardItemKey);
			}
			if (itemGainPreview.UsesCommittedRoll)
			{
				if (num4 == long.MaxValue)
				{
					return ExchangeResult.Failed(ExchangeFailure.AttemptSequenceExhausted, optionId, exchangeOption.RewardItemKey);
				}
				num4++;
			}
			long num7 = ((itemGainPreview.ItemLevel > 0) ? exchangeOption.RewardQuantity : 1);
			long quantity3 = ((itemGainPreview.ItemLevel > 0) ? 1 : exchangeOption.RewardQuantity);
			for (long num8 = 0L; num8 < num7; num8++)
			{
				string text = NextUid(owner.Key, hashSet, ref sequence);
				if (text == null)
				{
					return ExchangeResult.Failed(ExchangeFailure.UidExhausted, optionId, exchangeOption.RewardItemKey);
				}
				ItemStack incoming = new ItemStack(text, itemGainPreview.ResolvedItemKey, quantity3)
				{
					Blessing = itemGainPreview.Blessing,
					Enhancement = itemGainPreview.Enhancement,
					ItemLevel = itemGainPreview.ItemLevel,
					Affixes = (itemGainPreview.Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>())
				};
				if (!ItemStackInventory.TryAddOrStack(data, list, incoming, out ItemStack _))
				{
					return ExchangeResult.Failed(ExchangeFailure.InventoryOverflow, optionId, exchangeOption.RewardItemKey);
				}
			}
			hashSet2.Add(itemGainPreview.ResolvedItemKey);
			if (itemGainPreview.Blessing == ItemBlessing.Blessed)
			{
				num5 += exchangeOption.RewardQuantity;
			}
		}
		owner.Gold = CombatWallet.Balance(owner) - num;
		owner.InventoryStacks = list;
		owner.ItemUidSequence = sequence;
		owner.Progress.ItemGainAttemptSequence = num4;
		if (warehouse != null && list2 != null)
		{
			warehouse.ReplaceItems(list2, warehouse.ItemUidSequence);
		}
		CombatInventory.SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItems(owner, hashSet2);
		return ExchangeResult.Completed(optionId, exchangeOption.RewardItemKey, quantity, producedQuantity, num5, num4 - itemGainAttemptSequence);
	}

	private static IReadOnlyList<ExchangeOption> BuildShimizheOptions(IGameData data)
	{
		JsonArray jsonArray = RequiredArray(data, "SHIMIZHE_REWARDS");
		IReadOnlyDictionary<string, long> itemCosts = Costs(("item_son_letter", 1L), ("item_son_remains", 1L), ("item_son_portrait", 1L));
		List<ExchangeOption> list = new List<ExchangeOption>();
		foreach (JsonNode item in jsonArray)
		{
			string value;
			string text = ((item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? (value ?? "") : "");
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidDataException("SHIMIZHE_REWARDS contains an invalid item key.");
			}
			list.Add(new ExchangeOption
			{
				Id = "shimizhe_" + text,
				NpcId = "npc_shimizhe",
				RewardItemKey = text,
				ItemCosts = itemCosts
			});
		}
		return list.AsReadOnly();
	}

	private static JsonArray RequiredArray(IGameData data, string tableName)
	{
		return (data.Table(tableName) as JsonArray) ?? throw new InvalidDataException("Required exchange table '" + tableName + "' is unavailable.");
	}

	private static ExchangeOption Option(string id, string npcId, string rewardItemKey, string costItemKey, long cost, ItemGainOptions gainOptions = default(ItemGainOptions))
	{
		return new ExchangeOption
		{
			Id = id,
			NpcId = npcId,
			RewardItemKey = rewardItemKey,
			ItemCosts = Costs((costItemKey, cost)),
			GainOptions = gainOptions
		};
	}

	private static IReadOnlyDictionary<string, long> Costs(params (string ItemKey, long Quantity)[] entries)
	{
		return new ReadOnlyDictionary<string, long>(entries.ToDictionary<(string, long), string, long>(((string ItemKey, long Quantity) entry) => entry.ItemKey, ((string ItemKey, long Quantity) entry) => entry.Quantity, StringComparer.Ordinal));
	}

	private static long WarehouseAvailableCount(WarehouseState? warehouse, string itemKey)
	{
		if (warehouse == null)
		{
			return 0L;
		}
		long num = 0L;
		foreach (ItemStack item in warehouse.Items)
		{
			if (!(item.ItemKey != itemKey) && !item.Locked)
			{
				num = SaturatingAdd(num, item.Quantity);
			}
		}
		return num;
	}

	private static long RemoveAvailable(List<ItemStack> inventory, string itemKey, long quantity)
	{
		long num = quantity;
		ItemStack[] array = inventory.ToArray();
		foreach (ItemStack itemStack in array)
		{
			if (num == 0L)
			{
				break;
			}
			if (!(itemStack.ItemKey != itemKey) && !itemStack.Locked)
			{
				long num2 = Math.Min(itemStack.Quantity, num);
				itemStack.Quantity -= num2;
				num -= num2;
			}
		}
		inventory.RemoveAll((ItemStack stack) => stack.Quantity == 0);
		return num;
	}

	private static string? NextUid(string ownerKey, ISet<string> usedUids, ref long sequence)
	{
		while (sequence < long.MaxValue)
		{
			string text = $"{ownerKey}:item:{++sequence}";
			if (usedUids.Add(text))
			{
				return text;
			}
		}
		return null;
	}

	private static long SaturatingAdd(long left, long right)
	{
		if (left <= long.MaxValue - right)
		{
			return left + right;
		}
		return long.MaxValue;
	}
}
