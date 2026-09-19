using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetCollarRules
{
	public const int CollarItemId = 40314;

	public const int HighCollarItemId = 40316;

	public const int WhistleItemId = 41160;

	public const int InventorySlotMaximum = int.MaxValue;

	public const string CollarItemKey = "l1j_item_40314";

	public const string HighCollarItemKey = "l1j_item_40316";

	public const string WhistleItemKey = "l1j_item_41160";

	public static bool IsCollar(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return false;
		}
		int num = ReadInt(data.Item(itemKey)?["l1jItemId"]);
		if (num == 40314 || num == 40316)
		{
			return true;
		}
		return false;
	}

	public static bool IsBoundCollar(IGameData data, ItemStack stack, string? petUid = null)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (IsCollar(data, stack.ItemKey) && stack.PetUid.Length > 0)
		{
			if (petUid != null)
			{
				return string.Equals(stack.PetUid, petUid, StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	public static ItemStack? FindCollar(IGameData data, IEnumerable<ItemStack> items, string petUid)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		return items.FirstOrDefault((ItemStack item) => IsBoundCollar(data, item, petUid));
	}

	public static bool CanGrantCollar(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return owner.Kind == CombatantKind.Player && !string.IsNullOrWhiteSpace(owner.Key);
	}

	public static ItemStack GrantCollar(IGameData data, Combatant owner, PetInstance pet)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(pet, "pet");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			throw new InvalidOperationException("A pet collar can only be granted to a player.");
		}
		ItemStack itemStack = FindCollar(data, owner.InventoryStacks, pet.Uid);
		if (itemStack != null)
		{
			return itemStack;
		}
		if (!CanGrantCollar(owner))
		{
			throw new InvalidOperationException("The player inventory has no free slot for a pet collar.");
		}
		ItemStack collar = new ItemStack(CombatInventory.NextUid(owner), "l1j_item_40314", 1L)
		{
			PetUid = pet.Uid
		};
		CombatInventory.Add(owner, collar);
		return owner.InventoryStacks.Single((ItemStack item) => item.Uid == collar.Uid);
	}

	public static bool UpgradeCollar(IGameData data, Combatant owner, string petUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		ItemStack itemStack = FindCollar(data, owner.InventoryStacks, petUid);
		if (itemStack == null)
		{
			return false;
		}
		int index = owner.InventoryStacks.IndexOf(itemStack);
		owner.InventoryStacks[index] = new ItemStack(CombatInventory.NextUid(owner), "l1j_item_40316", 1L)
		{
			PetUid = petUid,
			Locked = itemStack.Locked
		};
		CombatInventory.SyncLegacyView(owner);
		return true;
	}

	public static int EnsureCollars(IGameData data, PetRoster roster, Combatant owner, IEnumerable<ItemStack>? externalItems = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ItemStack[] items = externalItems?.ToArray() ?? Array.Empty<ItemStack>();
		int num = 0;
		foreach (PetInstance pet in roster.Pets)
		{
			if (FindCollar(data, owner.InventoryStacks, pet.Uid) == null && FindCollar(data, items, pet.Uid) == null)
			{
				ItemStack incoming = new ItemStack(CombatInventory.NextUid(owner), "l1j_item_40314", 1L)
				{
					PetUid = pet.Uid
				};
				CombatInventory.Add(owner, incoming);
				num++;
			}
		}
		return num;
	}

	public static PetCollarResult Toggle(IGameData data, PetRoster roster, Combatant owner, string collarUid, double otherPetCost = 0.0)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(collarUid, "collarUid");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetCollarResult.Failed(PetCollarFailure.InvalidOwner);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, collarUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return PetCollarResult.Failed(PetCollarFailure.ItemNotFound);
		}
		if (itemStack.Locked)
		{
			return PetCollarResult.Failed(PetCollarFailure.ItemLocked, null, itemStack.ItemKey);
		}
		if (!IsCollar(data, itemStack.ItemKey))
		{
			return PetCollarResult.Failed(PetCollarFailure.NotPetCollar, null, itemStack.ItemKey);
		}
		if (string.IsNullOrWhiteSpace(itemStack.PetUid))
		{
			return PetCollarResult.Failed(PetCollarFailure.UnboundCollar, null, itemStack.ItemKey);
		}
		PetInstance petInstance = roster.Find(itemStack.PetUid);
		if (petInstance == null)
		{
			return PetCollarResult.Failed(PetCollarFailure.UnknownPet, null, itemStack.ItemKey);
		}
		if (string.Equals(petInstance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			return PetCollarResult.Failed(PetCollarFailure.AlreadyActive, petInstance, itemStack.ItemKey);
		}
		if (petInstance.OwnerKey.Length > 0)
		{
			return PetCollarResult.Failed(PetCollarFailure.ForeignPet, petInstance, itemStack.ItemKey);
		}
		if (CombatInventory.AvailableCount(owner, "l1j_item_41160") < 1)
		{
			return PetCollarResult.Failed(PetCollarFailure.MissingWhistle, petInstance, itemStack.ItemKey);
		}
		PetRosterResult petRosterResult = roster.TryDeploy(data, owner, petInstance.Uid, otherPetCost);
		if (!petRosterResult.Success)
		{
			return PetCollarResult.Failed(petRosterResult.Failure switch
			{
				PetRosterFailure.InsufficientCharm => PetCollarFailure.InsufficientCharm, 
				PetRosterFailure.AssignedToAnotherOwner => PetCollarFailure.ForeignPet, 
				_ => PetCollarFailure.UnknownPet, 
			}, petInstance, itemStack.ItemKey);
		}
		if (!CombatInventory.TryRemove(owner, "l1j_item_41160", 1L))
		{
			roster.Recall(owner.Key, petInstance.Uid);
			return PetCollarResult.Failed(PetCollarFailure.MissingWhistle, petInstance, itemStack.ItemKey);
		}
		return new PetCollarResult(Success: true, PetCollarFailure.None, PetCollarAction.Summoned, petInstance, itemStack.ItemKey, "l1j_item_41160");
	}

	private static int ReadInt(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
