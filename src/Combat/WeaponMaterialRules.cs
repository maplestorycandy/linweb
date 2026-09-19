using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WeaponMaterialRules
{
	private static readonly HashSet<string> SilverMaterials = new HashSet<string>(StringComparer.Ordinal) { "silver", "mithril", "oriharukon" };

	private static readonly HashSet<string> DemonMaterials = new HashSet<string>(StringComparer.Ordinal) { "mithril", "oriharukon" };

	public static string MaterialOf(IGameData? data, string? itemKey)
	{
		JsonObject jsonObject = data?.Item(itemKey ?? string.Empty);
		if (jsonObject == null)
		{
			return string.Empty;
		}
		return CombatSkill.ReadString(jsonObject, "material");
	}

	public static string SourceItemKey(Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (attacker.D.UsesRangedAttack)
		{
			if (!attacker.EquippedItems.TryGetValue("arrow", out ItemStack value))
			{
				return string.Empty;
			}
			return value.ItemKey;
		}
		if (!attacker.EquippedItems.TryGetValue("wpn", out ItemStack value2))
		{
			return attacker.MainWeaponId;
		}
		return value2.ItemKey;
	}

	public static int RollBonus(IGameData? data, ICombatRandom random, Combatant attacker, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		string item = MaterialOf(data, SourceItemKey(attacker));
		int num = CounterDamageRules.UndeadType(data, target);
		bool flag = SilverMaterials.Contains(item);
		if (flag)
		{
			bool flag2;
			switch (num)
			{
			case 1:
			case 3:
			case 5:
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (flag)
		{
			return random.Roll(1, 20);
		}
		if (DemonMaterials.Contains(item) && num == 2)
		{
			return random.Roll(1, 3);
		}
		return 0;
	}
}
