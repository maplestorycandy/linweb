using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CounterAttackRules
{
	public const double TriggerChance = 0.5;

	public const string TwoHandSwordTag = "雙手劍";

	public static bool IsShortDistance(Combatant attacker, Combatant defender)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(defender, "defender");
		bool num = CombatRangeRules.DiamondDistance(attacker.Pos, defender.Pos) / 48.0 > 1.0;
		bool flag = attacker.D.UsesRangedAttack || attacker.BasicProjectileKind.Length > 0;
		return !(num && flag);
	}

	public static bool CanCounter(IGameData? data, Combatant defender)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		if (data != null && BehaviorBuffRules.CounterBarrierActive(defender))
		{
			return HasTwoHandSword(data, defender);
		}
		return false;
	}

	public static bool HasTwoHandSword(IGameData? data, Combatant defender)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		if (data == null || defender.MainWeaponId.Length == 0)
		{
			return false;
		}
		if (!(data.Table("WEAPON_TAGS") is JsonObject jsonObject) || !(jsonObject[defender.MainWeaponId] is JsonArray jsonArray))
		{
			return false;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && string.Equals(value, "雙手劍", StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
