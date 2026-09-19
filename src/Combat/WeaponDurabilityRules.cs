using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WeaponDurabilityRules
{
	public const string WhetstoneItemKey = "item_whetstone";

	public const string WhetstoneEffect = "whetstone";

	public const double NormalBreakChance = 0.1;

	public const double BlessedBreakChance = 0.03;

	public static ItemStack? EquippedMainWeapon(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.EquippedItems.GetValueOrDefault("wpn");
	}

	public static int DamagePenalty(IGameData? data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		bool flag = data == null;
		if (!flag)
		{
			CombatantKind kind = attacker.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			return 0;
		}
		ItemStack itemStack = EquippedMainWeapon(attacker);
		if (itemStack != null)
		{
			JsonObject jsonObject = data.Item(itemStack.ItemKey);
			if (jsonObject != null && !(CombatSkill.ReadString(jsonObject, "type") != "wpn"))
			{
				return Math.Max(0, itemStack.BrokenBladeStacks);
			}
		}
		return 0;
	}

	public static bool CanBeDamaged(IGameData data, ItemStack weapon)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(weapon, "weapon");
		JsonObject jsonObject = data.Item(weapon.ItemKey);
		if (jsonObject != null && CombatSkill.ReadString(jsonObject, "type") == "wpn")
		{
			return CombatSkill.ReadBool(jsonObject, "canBeDamaged");
		}
		return false;
	}

	public static bool TryAccumulateBrokenBlade(IGameData? data, Combatant attacker, Combatant target, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(random, "random");
		bool flag = data == null || !target.Hard;
		if (!flag)
		{
			CombatantKind kind = attacker.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2;
		}
		if (flag || attacker.Buffs.GetValueOrDefault("sk_elf_flamesoul") > 0.0)
		{
			return false;
		}
		ItemStack itemStack = EquippedMainWeapon(attacker);
		if (itemStack == null || !CanBeDamaged(data, itemStack))
		{
			return false;
		}
		double num = ((CounterDamageRules.AttackBlessing(attacker) == ItemBlessing.Blessed) ? 0.03 : 0.1);
		if (random.NextDouble() >= num)
		{
			return false;
		}
		if (itemStack.BrokenBladeStacks < int.MaxValue)
		{
			itemStack.BrokenBladeStacks++;
		}
		return true;
	}

	public static int RepairOnePoint(Combatant actor)
	{
		ItemStack itemStack = EquippedMainWeapon(actor);
		if (itemStack == null || itemStack.BrokenBladeStacks <= 0)
		{
			return 0;
		}
		itemStack.BrokenBladeStacks--;
		return 1;
	}

	public static int RepairEquippedWeaponFully(Combatant actor)
	{
		ItemStack itemStack = EquippedMainWeapon(actor);
		if (itemStack == null || itemStack.BrokenBladeStacks <= 0)
		{
			return 0;
		}
		int brokenBladeStacks = itemStack.BrokenBladeStacks;
		itemStack.BrokenBladeStacks = 0;
		return brokenBladeStacks;
	}
}
