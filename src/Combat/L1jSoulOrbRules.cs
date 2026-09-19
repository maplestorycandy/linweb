using System;
using System.Linq;

namespace IdleLineage.Combat;

public static class L1jSoulOrbRules
{
	public const int ContainerItemId = 49013;

	public const string SoulOrbItemKey = "item_soul_orb";

	public static bool TryOpen(Combatant owner, string sourceUid)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceUid, "sourceUid");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == sourceUid);
		if (itemStack == null || itemStack.Locked)
		{
			return false;
		}
		if (!ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, sourceUid, 1L, out ItemStack _))
		{
			return false;
		}
		CombatInventory.Add(owner, "item_soul_orb", 1L);
		return true;
	}
}
