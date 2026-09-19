using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CounterDamageRules
{
	public const int CounterDiceSides = 6;

	public static int RollElementBonus(ICombatRandom random, string? attackElement, string? defenseElement, bool forcedCounter = false)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		if (!forcedCounter && !CombatMath.IsElementCounter(attackElement, defenseElement))
		{
			return 0;
		}
		return random.Roll(1, 6);
	}

	public static int RollWeaponAttackBonus(IGameData? data, ICombatRandom random, Combatant attacker, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		JsonObject weapon = data?.Item(attacker.MainWeaponId);
		int num = RollElementBonus(random, attacker.AttackElement, target.Element, WeaponCountersElement(weapon, target.Element));
		int num2 = UndeadType(data, target);
		bool flag = attacker.Buffs.GetValueOrDefault("sk_holy_wpn") > 0.0 && !attacker.D.UsesRangedAttack && HasMeleeWeapon(attacker);
		if (flag)
		{
			bool flag2 = ((num2 == 1 || num2 == 3) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			num++;
		}
		flag = HasBlessedWeapon(attacker);
		if (flag)
		{
			bool flag2 = (uint)(num2 - 1) <= 2u;
			flag = flag2;
		}
		if (flag)
		{
			num += random.Roll(1, 5);
		}
		return num + WeaponMaterialRules.RollBonus(data, random, attacker, target);
	}

	public static ItemBlessing AttackBlessing(Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		string key = (attacker.D.UsesRangedAttack ? "arrow" : "wpn");
		if (!attacker.EquippedItems.TryGetValue(key, out ItemStack value))
		{
			return ItemBlessing.Normal;
		}
		return value.Blessing;
	}

	public static bool HasBlessedWeapon(Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		return AttackBlessing(attacker) == ItemBlessing.Blessed;
	}

	private static bool HasMeleeWeapon(Combatant attacker)
	{
		if (attacker.MainWeaponId.Length <= 0)
		{
			return attacker.EquippedItems.ContainsKey("wpn");
		}
		return true;
	}

	public static bool HasTargetTag(IGameData? data, Combatant target, string? tag)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		string text = (tag ?? string.Empty).Trim().ToLowerInvariant();
		if (!(text == "undead"))
		{
			if (text == "demon")
			{
				return UndeadType(data, target) == 2;
			}
			return false;
		}
		int num = UndeadType(data, target);
		return (num == 1 || (uint)(num - 3) <= 1u) ? true : false;
	}

	public static int UndeadType(IGameData? data, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!((data?.Mob(target.Avatar))?["undeadType"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return Math.Clamp(value, 0, 5);
	}

	private static bool WeaponCountersElement(JsonObject? weapon, string? defenseElement)
	{
		string text = (defenseElement ?? string.Empty).Trim().ToLowerInvariant();
		if (((text != null && text.Length == 0) || text == "none" || text == "normal") ? true : false)
		{
			return false;
		}
		if (ReadBool(weapon, "counterAllEle"))
		{
			return true;
		}
		if (!(weapon?["counterEles"] is JsonArray jsonArray))
		{
			return false;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && string.Equals(value, text, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ReadBool(JsonObject? source, string property)
	{
		if (source != null)
		{
			return CombatSkill.ReadBool(source, property);
		}
		return false;
	}
}
