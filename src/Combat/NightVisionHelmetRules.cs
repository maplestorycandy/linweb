using System;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class NightVisionHelmetRules
{
	public const int HelmetItemId = 20036;

	public static bool IsActive(IGameData data, Combatant owner)
	{
		if (data == null || owner == null)
		{
			return false;
		}
		return owner.EquippedItems.Values.Any(delegate(ItemStack stack)
		{
			JsonObject jsonObject = data.Item(stack.ItemKey);
			return jsonObject != null && CombatSkill.ReadInt(jsonObject, "l1jItemId") == 20036;
		});
	}
}

public static class ElfNightVisionRules
{
	public static bool IsElf(Combatant? player)
	{
		return player != null && string.Equals(player.ClassId, "elf", StringComparison.OrdinalIgnoreCase);
	}

	public static bool HasNightVision(IGameData data, Combatant player, bool elfNightVisionEnabled)
	{
		if (player == null)
		{
			return false;
		}
		if (elfNightVisionEnabled && IsElf(player))
		{
			return true;
		}
		return NightVisionHelmetRules.IsActive(data, player);
	}
}
