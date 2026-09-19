using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jPoisonAttackRules
{
	public const int TriggerPercent = 15;

	public const int DamageDurationTicks = 300;

	public const int DamageIntervalTicks = 30;

	public const int DamagePerTick = 5;

	public const int ParalysisDelayTicks = 200;

	public const int ParalysisDurationTicks = 450;

	public const string SilenceStatus = "poisonsilence";

	public const string ParalysisDelayStatus = "poisonparalyzing";

	public const string ParalysisStatus = "poisonparalyzed";

	public static IReadOnlyList<string> PoisonStatusKinds { get; } = new string[4] { "poison", "poisonsilence", "poisonparalyzing", "poisonparalyzed" };

	public static L1jPoisonAttackType AttackType(IGameData? data, Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (data == null || !attacker.UsesMonsterTemplate || HostilePlayerRules.UsesPlayerCombatRules(attacker))
		{
			return L1jPoisonAttackType.None;
		}
		string text = MobSkillRules.DefinitionKey(data, attacker);
		if (text.Length != 0)
		{
			JsonObject jsonObject = data.Mob(text);
			if (jsonObject != null)
			{
				return (int)Math.Floor(CombatSkill.ReadDouble(jsonObject, "poisonAtk")) switch
				{
					1 => L1jPoisonAttackType.Damage, 
					2 => L1jPoisonAttackType.Silence, 
					4 => L1jPoisonAttackType.Paralysis, 
					_ => L1jPoisonAttackType.None, 
				};
			}
		}
		return L1jPoisonAttackType.None;
	}

	public static bool IsPoisoned(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		foreach (string poisonStatusKind in PoisonStatusKinds)
		{
			if (target.HasStatus(poisonStatusKind))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CanInfect(IGameData? data, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!target.IsAlive || IsPoisoned(target))
		{
			return false;
		}
		if (target.Kind != CombatantKind.Player && !HostilePlayerRules.IsHostilePlayer(target))
		{
			return true;
		}
		if (target.Buffs.GetValueOrDefault("sk_dark_poisonres") > 0.0)
		{
			return false;
		}
		if (!HasEquippedItem(data, target, 20298))
		{
			return !HasEquippedItem(data, target, 20117);
		}
		return false;
	}

	public static bool IsPoisonStatus(string statusKind)
	{
		foreach (string poisonStatusKind in PoisonStatusKinds)
		{
			if (string.Equals(poisonStatusKind, statusKind, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public static bool Cure(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		bool flag = false;
		foreach (string poisonStatusKind in PoisonStatusKinds)
		{
			flag |= target.Statuses.Remove(poisonStatusKind);
			target.PeriodicEffects.Remove(poisonStatusKind);
			target.Counters.Remove(StatusRules.PotencyCounterKey(poisonStatusKind));
		}
		return flag;
	}

	private static bool HasEquippedItem(IGameData? data, Combatant target, int l1jItemId)
	{
		if (data == null)
		{
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack value in target.EquippedItems.Values)
		{
			if (hashSet.Add(value.Uid))
			{
				JsonObject jsonObject = data.Item(value.ItemKey);
				if (jsonObject != null && (int)Math.Floor(CombatSkill.ReadDouble(jsonObject, "l1jItemId")) == l1jItemId)
				{
					return true;
				}
			}
		}
		return false;
	}
}
