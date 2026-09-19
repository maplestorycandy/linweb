using System;
using System.Linq;

namespace IdleLineage.Combat;

public static class L1jUncurseRules
{
	public const int ItemId = 40119;

	public static L1jUncurseResult TryUse(Combatant owner, string scrollUid)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(scrollUid, "scrollUid");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == scrollUid);
		if (itemStack == null || itemStack.Locked)
		{
			return new L1jUncurseResult(Success: false, 0, "背包中找不到可使用的解除詛咒卷軸");
		}
		if (!ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, scrollUid, 1L, out ItemStack _))
		{
			return new L1jUncurseResult(Success: false, 0, "解除詛咒卷軸無法消耗");
		}
		int num = 0;
		foreach (ItemStack value in owner.EquippedItems.Values)
		{
			if (value.Blessing == ItemBlessing.Cursed)
			{
				value.Blessing = ItemBlessing.Normal;
				num++;
			}
		}
		CombatInventory.SyncLegacyView(owner);
		return new L1jUncurseResult(Success: true, num);
	}
}
