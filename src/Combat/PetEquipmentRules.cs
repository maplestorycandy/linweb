using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetEquipmentRules
{
	public static PetEquipmentResult TryEquip(IGameData data, PetRoster roster, Combatant owner, string petUid, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		PetEquipmentResult result = ValidateOwner(data, roster, owner, petUid);
		if (!result.Success)
		{
			return result;
		}
		PetInstance pet = result.Pet;
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.ItemNotFound, pet);
		}
		if (itemStack.Locked)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.ItemLocked, pet);
		}
		if (!L1jPetItemCatalog.Load(data).TryGet(itemStack.ItemKey, out L1jPetItemDefinition definition))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.NotPetEquipment, pet, "", itemStack.ItemKey);
		}
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		int index = list.FindIndex((ItemStack item) => item.Uid == itemUid);
		ItemStack itemStack2 = list[index];
		long sequence = owner.ItemUidSequence;
		ItemStack value;
		if (itemStack2.Quantity == 1)
		{
			list.RemoveAt(index);
			value = itemStack2;
		}
		else
		{
			string uid = NextUid(owner, list, pet, ref sequence);
			itemStack2.Quantity--;
			value = itemStack2.Copy(uid, 1L);
		}
		if (pet.Equipment.TryGetValue(definition.Slot, out ItemStack value2) && !ItemStackInventory.TryAddOrStack(list, value2.Copy(), out ItemStack _))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.InventoryOverflow, pet, definition.Slot, definition.ItemKey);
		}
		owner.InventoryStacks = list;
		owner.ItemUidSequence = sequence;
		pet.Equipment[definition.Slot] = value;
		CombatInventory.SyncLegacyView(owner);
		return new PetEquipmentResult(Success: true, PetEquipmentFailure.None, pet, definition.Slot, definition.ItemKey);
	}

	public static PetEquipmentResult TryUnequip(IGameData data, PetRoster roster, Combatant owner, string petUid, string slot)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		if (!(slot == "petwpn") && !(slot == "petarm"))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.SlotEmpty, null, slot);
		}
		PetEquipmentResult result = ValidateOwner(data, roster, owner, petUid);
		if (!result.Success)
		{
			return result;
		}
		PetInstance pet = result.Pet;
		if (!pet.Equipment.TryGetValue(slot, out ItemStack value))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.SlotEmpty, pet, slot);
		}
		if (!L1jPetItemCatalog.Load(data).TryGet(value.ItemKey, out L1jPetItemDefinition definition) || definition.Slot != slot)
		{
			throw new InvalidDataException("A pet carries non-canonical equipment.");
		}
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		if (!ItemStackInventory.TryAddOrStack(list, value.Copy(), out ItemStack _))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.InventoryOverflow, pet, slot, value.ItemKey);
		}
		owner.InventoryStacks = list;
		pet.Equipment.Remove(slot);
		CombatInventory.SyncLegacyView(owner);
		return new PetEquipmentResult(Success: true, PetEquipmentFailure.None, pet, slot, value.ItemKey);
	}

	private static PetEquipmentResult ValidateOwner(IGameData data, PetRoster roster, Combatant owner, string petUid)
	{
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.InvalidOwner);
		}
		PetInstance petInstance = roster.Find(petUid);
		if (petInstance == null)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.UnknownPet);
		}
		if (petInstance.OwnerKey.Length == 0)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.PetNotActive, petInstance);
		}
		if (petInstance.OwnerKey != owner.Key)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.ForeignPet, petInstance);
		}
		if (PetCollarRules.FindCollar(data, owner.InventoryStacks, petUid) == null)
		{
			return PetEquipmentResult.Failed(PetEquipmentFailure.MissingCollar, petInstance);
		}
		return new PetEquipmentResult(Success: true, PetEquipmentFailure.None, petInstance, "", "");
	}

	private static string NextUid(Combatant owner, IReadOnlyList<ItemStack> inventory, PetInstance pet, ref long sequence)
	{
		HashSet<string> hashSet = new HashSet<string>(inventory.Select((ItemStack item) => item.Uid).Concat(owner.EquippedItems.Values.Select((ItemStack item) => item.Uid)).Concat(pet.Equipment.Values.Select((ItemStack item) => item.Uid)), StringComparer.Ordinal);
		do
		{
			if (sequence == long.MaxValue)
			{
				throw new OverflowException("The item UID sequence is exhausted.");
			}
			sequence++;
		}
		while (hashSet.Contains($"{owner.Key}:item:{sequence}"));
		return $"{owner.Key}:item:{sequence}";
	}
}
