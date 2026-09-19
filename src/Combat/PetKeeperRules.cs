using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetKeeperRules
{
	public static PetKeeperResult DepositAll(IGameData data, PetRoster roster, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetKeeperResult.Failed(PetKeeperFailure.InvalidOwner);
		}
		PetInstance[] array = roster.ActiveFor(owner).ToArray();
		if (array.Length == 0)
		{
			return new PetKeeperResult(Success: true, PetKeeperFailure.None, 0, null);
		}
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		PetInstance[] array2 = array;
		foreach (PetInstance petInstance in array2)
		{
			foreach (ItemStack value in petInstance.Equipment.Values)
			{
				if (!ItemStackInventory.TryAddOrStack(list, value.Copy(), out ItemStack _))
				{
					return PetKeeperResult.Failed(PetKeeperFailure.InventoryOverflow, petInstance);
				}
			}
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		array2 = array;
		foreach (PetInstance petInstance2 in array2)
		{
			petInstance2.Equipment.Clear();
			roster.Recall(owner.Key, petInstance2.Uid);
		}
		return new PetKeeperResult(Success: true, PetKeeperFailure.None, array.Length, array[0]);
	}

	public static PetKeeperResult Withdraw(IGameData data, PetRoster roster, Combatant owner, string petUid, double otherPetCost = 0.0)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetKeeperResult.Failed(PetKeeperFailure.InvalidOwner);
		}
		PetInstance petInstance = roster.Find(petUid);
		if (petInstance == null)
		{
			return PetKeeperResult.Failed(PetKeeperFailure.UnknownPet);
		}
		if (PetCollarRules.FindCollar(data, owner.InventoryStacks, petUid) == null)
		{
			return PetKeeperResult.Failed(PetKeeperFailure.MissingCollar, petInstance);
		}
		if (string.Equals(petInstance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			return PetKeeperResult.Failed(PetKeeperFailure.AlreadyActive, petInstance);
		}
		if (petInstance.OwnerKey.Length > 0)
		{
			return PetKeeperResult.Failed(PetKeeperFailure.ForeignPet, petInstance);
		}
		PetRosterResult petRosterResult = roster.TryDeploy(data, owner, petUid, otherPetCost);
		if (!petRosterResult.Success)
		{
			return PetKeeperResult.Failed((petRosterResult.Failure == PetRosterFailure.InsufficientCharm) ? PetKeeperFailure.InsufficientCharm : PetKeeperFailure.UnknownPet, petInstance);
		}
		return new PetKeeperResult(Success: true, PetKeeperFailure.None, 1, petInstance);
	}
}
