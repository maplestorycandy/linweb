using System;
using System.Collections.Generic;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class EvilDeathLossRules
{
	public sealed record Loss(ItemStack Stack, string DisplayName);

	public const double LossChance = 0.01;

	public static Loss? ApplyLoss(IGameData data, Combatant player, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(player, "player");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (CombatCurveMath.GetAlignmentTier(player.Alignment) != AlignmentTier.Evil)
		{
			return null;
		}
		if (random.NextDouble() >= 0.01)
		{
			return null;
		}
		List<(string, ItemStack)> list = new List<(string, ItemStack)>();
		foreach (var (item, itemStack2) in player.EquippedItems)
		{
			if (DeathLossItemRules.CanDrop(data, itemStack2))
			{
				list.Add((item, itemStack2));
			}
		}
		foreach (ItemStack inventoryStack in player.InventoryStacks)
		{
			if (DeathLossItemRules.CanDrop(data, inventoryStack))
			{
				list.Add(("", inventoryStack));
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		var (text2, itemStack3) = list[Math.Clamp((int)(random.NextDouble() * (double)list.Count), 0, list.Count - 1)];
		ItemStack stack;
		if (text2.Length > 0 && itemStack3.Quantity > 1)
		{
			itemStack3.Quantity--;
			stack = itemStack3.Copy(itemStack3.Uid + ":death", 1L);
		}
		else if (text2.Length > 0)
		{
			player.EquippedItems.Remove(text2);
			stack = itemStack3;
		}
		else if (itemStack3.Quantity > 1)
		{
			itemStack3.Quantity--;
			stack = itemStack3.Copy(itemStack3.Uid + ":death", 1L);
		}
		else
		{
			player.InventoryStacks.Remove(itemStack3);
			stack = itemStack3;
		}
		CombatInventory.SyncLegacyView(player);
		CombatEquipment.SyncLegacyView(player);
		CombatantBuilder.RefreshPlayer(player, data);
		string displayName = data.Item(itemStack3.ItemKey)?["n"]?.GetValue<string>() ?? itemStack3.ItemKey;
		return new Loss(stack, displayName);
	}
}
