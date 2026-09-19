using System;
using System.Linq;

namespace IdleLineage.Combat;

public static class TeleportMemoryRules
{
	public const string TeleportSkillKey = "sk_teleport";

	public const string TeleportScrollKey = "scroll_teleport";

	public const string ControlRingKey = "acc_116";

	public const int FixedMemoryCapacity = 20;

	public const int MaximumStoredLocations = 20;

	public static bool HasControlRing(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!actor.EquippedItems.Values.Any((ItemStack item) => string.Equals(item.ItemKey, "acc_116", StringComparison.Ordinal)))
		{
			return ItemStackInventory.CountByItemKey(actor.InventoryStacks, "acc_116") > 0;
		}
		return true;
	}

	public static int MemoryCapacity(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!HasControlRing(actor))
		{
			return 0;
		}
		return 20;
	}

	public static bool CanUseTeleportScroll(bool usableItem, bool teleportable)
	{
		return usableItem && teleportable;
	}

	public static TeleportMemoryResult TryRemember(Combatant actor, string name, string mapKey, WorldPoint position, bool teleportScrollAllowed)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!actor.IsAlive)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.Dead);
		}
		if (!teleportScrollAllowed)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.InvalidMap);
		}
		int num = MemoryCapacity(actor);
		if (num <= 0)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.MemoryUnavailable);
		}
		if (actor.Progress.TeleportMemories.Count >= num)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.CapacityReached);
		}
		string normalizedName = (name ?? "").Trim();
		if (normalizedName.Length == 0)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.InvalidName);
		}
		if (actor.Progress.TeleportMemories.Any((TeleportMemoryLocation entry) => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.DuplicateName);
		}
		string text = (mapKey ?? "").Trim();
		if (text.Length == 0)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.InvalidMap);
		}
		if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.InvalidPosition);
		}
		long num2 = actor.Progress.TeleportMemorySequence;
		string id;
		do
		{
			num2 = checked(num2 + 1);
			id = $"tp-{num2}";
		}
		while (actor.Progress.TeleportMemories.Any((TeleportMemoryLocation entry) => string.Equals(entry.Id, id, StringComparison.Ordinal)));
		actor.Progress.TeleportMemorySequence = num2;
		TeleportMemoryLocation teleportMemoryLocation = new TeleportMemoryLocation(id, normalizedName, text, position.X, position.Y);
		actor.Progress.TeleportMemories.Add(teleportMemoryLocation);
		return new TeleportMemoryResult(Success: true, TeleportMemoryFailure.None, teleportMemoryLocation);
	}

	public static TeleportMemoryResult TryDelete(Combatant actor, string locationId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		string id = (locationId ?? "").Trim();
		int num = actor.Progress.TeleportMemories.FindIndex((TeleportMemoryLocation entry) => string.Equals(entry.Id, id, StringComparison.Ordinal));
		if (num < 0)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.UnknownLocation);
		}
		TeleportMemoryLocation location = actor.Progress.TeleportMemories[num];
		actor.Progress.TeleportMemories.RemoveAt(num);
		return new TeleportMemoryResult(Success: true, TeleportMemoryFailure.None, location);
	}

	public static TeleportMemoryResult TryPayForRememberedTeleport(Combatant actor, TeleportMemoryLocation location, int teleportSpellMpCost, bool spellAllowed = true, bool scrollAllowed = true)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(location, "location");
		if (!actor.IsAlive)
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.Dead);
		}
		if (!HasControlRing(actor))
		{
			return TeleportMemoryResult.Failed(TeleportMemoryFailure.ControlRingRequired);
		}
		int num = Math.Max(0, teleportSpellMpCost);
		bool flag = actor.LearnedSkills.Contains("sk_teleport") || actor.GrantedSkills.Contains("sk_teleport");
		if (spellAllowed && flag && actor.CanCast && actor.Mp >= (double)num)
		{
			actor.Mp -= num;
			return new TeleportMemoryResult(Success: true, TeleportMemoryFailure.None, location, TeleportPaymentSource.Spell);
		}
		if (scrollAllowed && ItemStackInventory.TryRemoveByItemKey(actor.InventoryStacks, "scroll_teleport", 1L))
		{
			return new TeleportMemoryResult(Success: true, TeleportMemoryFailure.None, location, TeleportPaymentSource.Scroll);
		}
		return TeleportMemoryResult.Failed(TeleportMemoryFailure.NoTeleportResource);
	}
}
