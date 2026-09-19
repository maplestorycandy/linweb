using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CombatEquipment
{
	public static IReadOnlyDictionary<string, ItemStack> Snapshot(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return new ReadOnlyDictionary<string, ItemStack>(owner.EquippedItems.ToDictionary<KeyValuePair<string, ItemStack>, string, ItemStack>((KeyValuePair<string, ItemStack> pair) => pair.Key, (KeyValuePair<string, ItemStack> pair) => pair.Value.Copy(), StringComparer.Ordinal));
	}

	public static void Load(Combatant owner, IReadOnlyDictionary<string, ItemStack> equipment)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(equipment, "equipment");
		Dictionary<string, ItemStack> dictionary = new Dictionary<string, ItemStack>(StringComparer.Ordinal);
		HashSet<string> hashSet = new HashSet<string>(owner.InventoryStacks.Select((ItemStack item) => item.Uid), StringComparer.Ordinal);
		foreach (var (text2, itemStack2) in equipment)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(text2, "slot");
			ItemStackInventory.ValidateStack(itemStack2);
			if (itemStack2.Quantity != 1 && text2 != "arrow")
			{
				throw new InvalidDataException("Equipped item in slot '" + text2 + "' must have quantity one unless it is ammunition.");
			}
			if (!hashSet.Add(itemStack2.Uid))
			{
				throw new InvalidDataException("Item UID '" + itemStack2.Uid + "' appears more than once.");
			}
			dictionary[text2] = itemStack2.Copy();
		}
		owner.EquippedItems = dictionary;
		SyncLegacyView(owner);
	}

	public static EquipmentChangeResult TryEquip(IGameData data, Combatant owner, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		CombatantKind kind = owner.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.InvalidOwner);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.ItemNotFound);
		}
		EquipmentEligibilityResult equipmentEligibilityResult = EquipmentRules.Evaluate(data, owner, itemStack);
		if (!equipmentEligibilityResult.Allowed)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.EligibilityRejected, equipmentEligibilityResult.Slot, itemStack.ItemKey, equipmentEligibilityResult.Failure);
		}
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		Dictionary<string, ItemStack> dictionary = owner.EquippedItems.ToDictionary<KeyValuePair<string, ItemStack>, string, ItemStack>((KeyValuePair<string, ItemStack> pair) => pair.Key, (KeyValuePair<string, ItemStack> pair) => pair.Value.Copy(), StringComparer.Ordinal);
		long sequence = owner.ItemUidSequence;
		List<string> list2 = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal) { equipmentEligibilityResult.Slot };
		AddMutuallyExclusiveSlots(data, owner, itemStack.ItemKey, equipmentEligibilityResult.Slot, dictionary, hashSet);
		foreach (string item in hashSet)
		{
			if (dictionary.ContainsKey(item))
			{
				if (!TryReturnSlot(list, dictionary, item))
				{
					return EquipmentChangeResult.Failed(EquipmentChangeFailure.InventoryOverflow, equipmentEligibilityResult.Slot, itemStack.ItemKey);
				}
				if (item != equipmentEligibilityResult.Slot)
				{
					list2.Add(item);
				}
			}
		}
		int num = list.FindIndex((ItemStack item) => item.Uid == itemUid);
		if (num < 0)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.ItemNotFound, equipmentEligibilityResult.Slot, itemStack.ItemKey);
		}
		ItemStack itemStack2 = list[num];
		ItemStack value;
		if (equipmentEligibilityResult.Slot == "arrow")
		{
			value = itemStack2.Copy();
			list.RemoveAt(num);
		}
		else
		{
			string uid = NextUid(owner.Key, ref sequence, list, dictionary);
			value = itemStack2.Copy(uid, 1L);
			itemStack2.Quantity--;
			if (itemStack2.Quantity == 0L)
			{
				list.RemoveAt(num);
			}
		}
		dictionary[equipmentEligibilityResult.Slot] = value;
		if (!TrySynchronizeDependentEquipment(data, owner, list, dictionary, ref sequence, list2))
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.InventoryOverflow, equipmentEligibilityResult.Slot, itemStack.ItemKey);
		}
		Commit(data, owner, list, dictionary, sequence);
		return EquipmentChangeResult.Changed(equipmentEligibilityResult.Slot, itemStack.ItemKey, list2.Distinct<string>(StringComparer.Ordinal));
	}

	public static EquipmentChangeResult TryUnequip(IGameData data, Combatant owner, string slot)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(slot, "slot");
		CombatantKind kind = owner.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.InvalidOwner, slot);
		}
		if (!owner.EquippedItems.TryGetValue(slot, out ItemStack value))
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.SlotNotEquipped, slot);
		}
		if (value.Blessing == ItemBlessing.Cursed)
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.CursedEquipment, slot, value.ItemKey);
		}
		List<ItemStack> inventory = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		Dictionary<string, ItemStack> equipment = owner.EquippedItems.ToDictionary<KeyValuePair<string, ItemStack>, string, ItemStack>((KeyValuePair<string, ItemStack> pair) => pair.Key, (KeyValuePair<string, ItemStack> pair) => pair.Value.Copy(), StringComparer.Ordinal);
		long nextSequence = owner.ItemUidSequence;
		if (!TryReturnSlot(inventory, equipment, slot))
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.InventoryOverflow, slot, value.ItemKey);
		}
		List<string> list = new List<string>();
		if (!TrySynchronizeDependentEquipment(data, owner, inventory, equipment, ref nextSequence, list))
		{
			return EquipmentChangeResult.Failed(EquipmentChangeFailure.InventoryOverflow, slot, value.ItemKey);
		}
		Commit(data, owner, inventory, equipment, nextSequence);
		return EquipmentChangeResult.Changed(slot, value.ItemKey, list.Distinct<string>(StringComparer.Ordinal));
	}

	public static IReadOnlyList<string> Revalidate(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		List<ItemStack> inventory = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		Dictionary<string, ItemStack> equipment = owner.EquippedItems.ToDictionary<KeyValuePair<string, ItemStack>, string, ItemStack>((KeyValuePair<string, ItemStack> pair) => pair.Key, (KeyValuePair<string, ItemStack> pair) => pair.Value.Copy(), StringComparer.Ordinal);
		long nextSequence = owner.ItemUidSequence;
		List<string> list = new List<string>();
		if (!TrySynchronizeDependentEquipment(data, owner, inventory, equipment, ref nextSequence, list) || list.Count == 0)
		{
			return Array.Empty<string>();
		}
		Commit(data, owner, inventory, equipment, nextSequence);
		return list.Distinct<string>(StringComparer.Ordinal).ToArray();
	}

	public static void SyncLegacyView(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		owner.Equip.Clear();
		foreach (var (key, itemStack2) in owner.EquippedItems)
		{
			owner.Equip[key] = itemStack2.ItemKey;
		}
		owner.MainWeaponId = owner.EquippedItems.GetValueOrDefault("wpn")?.ItemKey ?? string.Empty;
		owner.OffhandWeaponId = owner.EquippedItems.GetValueOrDefault("offwpn")?.ItemKey ?? string.Empty;
	}

	private static void AddMutuallyExclusiveSlots(IGameData data, Combatant owner, string itemKey, string targetSlot, IReadOnlyDictionary<string, ItemStack> equipment, ISet<string> slots)
	{
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject != null)
		{
			ItemStack value2;
			if (targetSlot == "wpn" && EquipmentRules.IsTwoHandedWeapon(data, owner, itemKey) && equipment.TryGetValue("shield", out ItemStack value) && !HasTruthyProperty(data.Item(value.ItemKey), "armguard"))
			{
				slots.Add("shield");
			}
			else if (targetSlot == "shield" && !HasTruthyProperty(jsonObject, "armguard") && equipment.TryGetValue("wpn", out value2) && EquipmentRules.IsTwoHandedWeapon(data, owner, value2.ItemKey))
			{
				slots.Add("wpn");
			}
			if (targetSlot == "offwpn" && equipment.ContainsKey("shield"))
			{
				slots.Add("shield");
			}
			else if (targetSlot == "shield" && equipment.ContainsKey("offwpn"))
			{
				slots.Add("offwpn");
			}
		}
	}

	private static bool TryReturnSlot(IList<ItemStack> inventory, Dictionary<string, ItemStack> equipment, string slot)
	{
		if (!equipment.Remove(slot, out ItemStack value))
		{
			return true;
		}
		ItemStack stored;
		return ItemStackInventory.TryAddOrStack(inventory, value, out stored);
	}

	internal static bool TrySynchronizeDependentEquipment(IGameData data, Combatant owner, IList<ItemStack> inventory, Dictionary<string, ItemStack> equipment, ref long nextSequence, ICollection<string> automaticSlots)
	{
		if (equipment.TryGetValue("offwpn", out ItemStack value) && value.Blessing != ItemBlessing.Cursed && (!CanUseDualWieldOffhand(data, owner, equipment) || !IsDualWieldWeapon(data, owner, value.ItemKey)))
		{
			if (!TryReturnSlot(inventory, equipment, "offwpn"))
			{
				return false;
			}
			automaticSlots.Add("offwpn");
		}
		return true;
	}

	private static bool CanUseDualWieldOffhand(IGameData data, Combatant owner, IReadOnlyDictionary<string, ItemStack> equipment)
	{
		if (equipment.TryGetValue("wpn", out ItemStack value))
		{
			return IsDualWieldWeapon(data, owner, value.ItemKey);
		}
		return false;
	}

	private static bool IsDualWieldWeapon(IGameData data, Combatant owner, string itemKey)
	{
		WeaponFamily? weaponFamily = WeaponCombatProfile.ResolveFamily(itemKey, data);
		if (owner.ClassId == "warrior" && owner.LearnedSkills.Contains("sk_warrior_dualaxe") && weaponFamily == WeaponFamily.OneHandBlunt)
		{
			return true;
		}
		return false;
	}

	private static string NextUid(string ownerKey, ref long sequence, IEnumerable<ItemStack> inventory, IReadOnlyDictionary<string, ItemStack> equipment)
	{
		HashSet<string> hashSet = new HashSet<string>(inventory.Select((ItemStack item) => item.Uid).Concat(equipment.Values.Select((ItemStack item) => item.Uid)), StringComparer.Ordinal);
		string text;
		do
		{
			if (sequence == long.MaxValue)
			{
				throw new OverflowException("The item UID sequence is exhausted.");
			}
			text = $"{ownerKey}:item:{++sequence}";
		}
		while (!hashSet.Add(text));
		return text;
	}

	internal static void Commit(IGameData data, Combatant owner, List<ItemStack> inventory, Dictionary<string, ItemStack> equipment, long nextSequence)
	{
		owner.InventoryStacks = inventory;
		owner.EquippedItems = equipment;
		owner.ItemUidSequence = nextSequence;
		CombatInventory.SyncLegacyView(owner);
		SyncLegacyView(owner);
		CombatantBuilder.RefreshPlayer(owner, data);
	}

	private static bool HasTruthyProperty(JsonObject? source, string property)
	{
		JsonNode jsonNode = source?[property];
		if (jsonNode == null)
		{
			return false;
		}
		if ((jsonNode is JsonObject || jsonNode is JsonArray) ? true : false)
		{
			return true;
		}
		if (!(jsonNode is JsonValue jsonValue))
		{
			return false;
		}
		if (jsonValue.TryGetValue<bool>(out var value))
		{
			return value;
		}
		if (jsonValue.TryGetValue<double>(out var value2))
		{
			return value2 != 0.0;
		}
		if (jsonValue.TryGetValue<string>(out string value3))
		{
			return !string.IsNullOrEmpty(value3);
		}
		return false;
	}
}
