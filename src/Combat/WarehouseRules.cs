using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WarehouseRules
{
	private static readonly HashSet<string> NonStorableItems = new HashSet<string>(StringComparer.Ordinal)
	{
		"item_mastery_proof", "item_pride_pass_11", "item_pride_pass_21", "item_pride_pass_31", "item_pride_pass_41", "item_pride_pass_51", "item_pride_pass_61", "item_pride_pass_71", "item_pride_pass_81", "item_pride_pass_91",
		"item_dantes_letter", "item_elf_whisper", "item_ancient_book", "item_sealed_intel", "item_spy_report", "item_chaos_key", "item_royal_order", "new_item_196", "new_item_198", "new_item_206",
		"new_item_208", "item_nightvision", "new_item_204", "new_item_205", "new_item_203", "new_item_214", "new_item_212", "new_item_240", "new_item_199", "new_item_200",
		"new_item_201", "new_item_202", "new_item_213", "item_blueflute", "item_death_oath", "item_orc_elder_head", "item_yeti_head", "item_fallen_key", "item_ant_fruit", "item_ant_branch",
		"item_ant_bark", "item_elmore_heart", "item_time_orb", "item_wyvern_blood", "new_item_207", "new_item_219", "item_demon_spy", "item_yeti_heart", "item_soulfire_ash", "new_item_197",
		"new_item_211", "item_lost_soul", "mat_flame_sword", "mat_flame_eye", "mat_flame_claw", "mat_flame_heart"
	};

	public static IReadOnlySet<string> NonStorableItemKeys => NonStorableItems;

	public static bool CanStore(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!string.IsNullOrWhiteSpace(itemKey) && !NonStorableItems.Contains(itemKey) && !MonsterCardRules.IsAnyCardDefinition(data.Item(itemKey)))
		{
			return !PetCollarRules.IsCollar(data, itemKey);
		}
		return false;
	}

	public static WarehouseTransferResult TryDeposit(IGameData data, Combatant owner, WarehouseState warehouse, string itemUid, long quantity)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(warehouse, "warehouse");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (quantity <= 0)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.InvalidQuantity);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null || itemStack.Quantity < quantity)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.ItemNotFound);
		}
		JsonObject jsonObject = data.Item(itemStack.ItemKey);
		if (jsonObject == null)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.MissingItemDefinition, itemStack.ItemKey);
		}
		if (itemStack.Locked)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.Locked, itemStack.ItemKey);
		}
		if (!CanStore(data, itemStack.ItemKey))
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.NotStorable, itemStack.ItemKey);
		}
		if (CombatSkill.ReadBool(jsonObject, "noTrade"))
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.NotTradable, itemStack.ItemKey);
		}
		if (itemStack.Sealed)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.Sealed, itemStack.ItemKey);
		}
		return Transfer(owner, warehouse, itemStack, quantity, deposit: true);
	}

	public static WarehouseTransferResult TryWithdraw(IGameData data, WarehouseState warehouse, Combatant owner, string itemUid, long quantity)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(warehouse, "warehouse");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (quantity <= 0)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.InvalidQuantity);
		}
		ItemStack itemStack = warehouse.Items.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null || itemStack.Quantity < quantity)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.ItemNotFound);
		}
		if (data.Item(itemStack.ItemKey) == null)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.MissingItemDefinition, itemStack.ItemKey);
		}
		WarehouseTransferResult result = Transfer(owner, warehouse, itemStack, quantity, deposit: false);
		if (result.Success)
		{
			CollectionRules.RegisterObtainedItem(owner, itemStack.ItemKey);
		}
		return result;
	}

	public static WarehouseGoldResult TryDepositGold(Combatant owner, WarehouseState warehouse, long amount)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(warehouse, "warehouse");
		if (amount <= 0)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.InvalidAmount);
		}
		if (owner.Gold < amount)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.InsufficientGold);
		}
		if (warehouse.Gold > long.MaxValue - amount)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.Overflow);
		}
		owner.Gold -= amount;
		warehouse.Gold += amount;
		return WarehouseGoldResult.Moved(amount);
	}

	public static WarehouseGoldResult TryWithdrawGold(WarehouseState warehouse, Combatant owner, long amount)
	{
		ArgumentNullException.ThrowIfNull(warehouse, "warehouse");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (amount <= 0)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.InvalidAmount);
		}
		if (warehouse.Gold < amount)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.InsufficientGold);
		}
		if (owner.Gold > long.MaxValue - amount)
		{
			return WarehouseGoldResult.Failed(WarehouseGoldFailure.Overflow);
		}
		warehouse.Gold -= amount;
		owner.Gold += amount;
		return WarehouseGoldResult.Moved(amount);
	}

	private static WarehouseTransferResult Transfer(Combatant owner, WarehouseState warehouse, ItemStack source, long quantity, bool deposit)
	{
		List<ItemStack> list;
		List<ItemStack> list2;
		try
		{
			list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
				select item.Copy()).ToList();
			list2 = warehouse.CopyItems();
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.CorruptState, source.ItemKey);
		}
		HashSet<string> usedUids = new HashSet<string>(StringComparer.Ordinal);
		foreach (string item in list.Select((ItemStack item) => item.Uid).Concat(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid)).Concat(list2.Select((ItemStack item) => item.Uid)))
		{
			if (!usedUids.Add(item))
			{
				return WarehouseTransferResult.Failed(WarehouseTransferFailure.DuplicateUid, source.ItemKey);
			}
		}
		IList<ItemStack> source2 = (deposit ? list : list2);
		IList<ItemStack> list3 = (deposit ? list2 : list);
		ItemStack copiedSource = source2.FirstOrDefault((ItemStack item) => item.Uid == source.Uid);
		if (copiedSource == null || copiedSource.Quantity < quantity)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.ItemNotFound, source.ItemKey);
		}
		ItemStack itemStack = (copiedSource.HasUniqueState ? null : list3.FirstOrDefault((ItemStack item) => ItemStackInventory.CanStack(item, copiedSource)));
		if (deposit && itemStack == null && list2.Count >= warehouse.Capacity)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.WarehouseFull, source.ItemKey);
		}
		if (itemStack != null && itemStack.Quantity > long.MaxValue - quantity)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.QuantityOverflow, source.ItemKey);
		}
		long nextSequence = (deposit ? warehouse.ItemUidSequence : owner.ItemUidSequence);
		string generatedUid = null;
		try
		{
			if (!ItemStackInventory.TryTransfer(source2, list3, source.Uid, quantity, NextUid))
			{
				return WarehouseTransferResult.Failed(WarehouseTransferFailure.CorruptState, source.ItemKey);
			}
		}
		catch (OverflowException)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.UidExhausted, source.ItemKey);
		}
		ItemStack itemStack2 = itemStack;
		if (itemStack2 == null)
		{
			string movedUid = generatedUid ?? source.Uid;
			itemStack2 = list3.FirstOrDefault((ItemStack item) => item.Uid == movedUid);
		}
		if (itemStack2 == null)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.CorruptState, source.ItemKey);
		}
		IReadOnlyDictionary<string, long> readOnlyDictionary;
		try
		{
			readOnlyDictionary = ItemStackInventory.ToPlainCounts(list);
		}
		catch (OverflowException)
		{
			return WarehouseTransferResult.Failed(WarehouseTransferFailure.QuantityOverflow, source.ItemKey);
		}
		if (deposit)
		{
			warehouse.ReplaceItems(list2, nextSequence);
		}
		else
		{
			warehouse.ReplaceItems(list2, warehouse.ItemUidSequence);
		}
		owner.InventoryStacks = list;
		if (!deposit)
		{
			owner.ItemUidSequence = nextSequence;
		}
		owner.Inventory.Clear();
		foreach (var (key, value) in readOnlyDictionary)
		{
			owner.Inventory[key] = value;
		}
		return WarehouseTransferResult.Moved(source.ItemKey, quantity);
		string NextUid()
		{
			string text2;
			do
			{
				if (nextSequence == long.MaxValue)
				{
					throw new OverflowException();
				}
				string value2 = (deposit ? warehouse.Key : owner.Key);
				text2 = $"{value2}:item:{++nextSequence}";
			}
			while (!usedUids.Add(text2));
			generatedUid = text2;
			return text2;
		}
	}
}
