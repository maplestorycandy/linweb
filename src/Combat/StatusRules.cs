using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class StatusRules
{
	public const string PotencyCounterPrefix = "status:";

	public static bool IsImmune(Combatant target, string statusKind)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentException.ThrowIfNullOrWhiteSpace(statusKind, "statusKind");
		string text = NormalizeKind(statusKind);
		if ((target.Kind == CombatantKind.Player || HostilePlayerRules.IsHostilePlayer(target)) && text == "poison")
		{
			return target.Buffs.GetValueOrDefault("sk_dark_poisonres") > 0.0;
		}
		return false;
	}

	public static int L1jStatusResistance(IGameData? data, Combatant target, int officialSkillId)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (data == null || (target.Kind != CombatantKind.Player && !HostilePlayerRules.IsHostilePlayer(target)))
		{
			return 0;
		}
		(string, int) tuple;
		switch (officialSkillId)
		{
		case 157:
			tuple = ("registSustain", 1);
			break;
		case 87:
			tuple = ("registStun", 2);
			break;
		case 33:
			tuple = ("registStone", 1);
			break;
		case 66:
			tuple = ("registSleep", 1);
			break;
		case 50:
		case 80:
		case 194:
			tuple = ("registFreeze", 1);
			break;
		case 20:
		case 40:
		case 103:
			tuple = ("registBlind", 1);
			break;
		default:
			tuple = ("", 0);
			break;
		}
		var (text, num) = tuple;
		if (text.Length == 0)
		{
			return 0;
		}
		double num2 = 0.0;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack value in target.EquippedItems.Values)
		{
			if (hashSet.Add(value.Uid))
			{
				JsonObject jsonObject = data.Item(value.ItemKey);
				if (jsonObject != null)
				{
					num2 += ReadNumber(jsonObject, text);
				}
			}
		}
		return (int)Math.Floor(num2) * num;
	}

	public static double PeriodicDamageMultiplier(Combatant target, string statusKind)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentException.ThrowIfNullOrWhiteSpace(statusKind, "statusKind");
		return 1.0;
	}

	public static double PhysicalHitPenalty(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = 0.0;
		if (actor.HasStatus("blind"))
		{
			num += (double)Math.Max(0, actor.Counters.GetValueOrDefault(PotencyCounterKey("blind"), 4));
		}
		if (actor.HasStatus("weaken"))
		{
			num += 2.0;
		}
		if (actor.HasStatus("disease"))
		{
			num += 4.0;
		}
		return num;
	}

	public static double ArmorClassAdjustment(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = 0.0;
		if (target.HasStatus("disease"))
		{
			num += 8.0;
		}
		if (target.HasStatus("confuse") || target.HasStatus("panic"))
		{
			num += 5.0;
		}
		if (target.HasStatus("guardbreak"))
		{
			num += 10.0;
		}
		if (target.HasStatus("shatter"))
		{
			num -= 10.0;
		}
		return num;
	}

	public static double PhysicalDamageFlatPenalty(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = 0.0;
		if (actor.HasStatus("weaken"))
		{
			num += (double)(actor.UsesMonsterTemplate ? 4 : 5);
		}
		if (actor.UsesMonsterTemplate && actor.HasStatus("broken"))
		{
			num += 2.0;
		}
		if (actor.HasStatus("confuse") || actor.HasStatus("panic"))
		{
			num += 10.0;
		}
		if (actor.HasStatus("doom"))
		{
			num += 20.0;
		}
		return num;
	}

	public static double OutgoingPhysicalDamageMultiplier(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.UsesMonsterTemplate || !actor.HasStatus("broken"))
		{
			return 1.0;
		}
		return 0.8;
	}

	public static double IncomingDamageMultiplier(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = 1.0;
		if (target.HasStatus("fragile"))
		{
			num *= 1.1;
		}
		return num;
	}

	public static double BasicAttackIncomingDamageMultiplier(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!target.HasStatus("armorbreak"))
		{
			return 1.0;
		}
		double val = target.Counters.GetValueOrDefault(PotencyCounterKey("armorbreak"), 58);
		return 1.0 + Math.Max(0.0, val) / 100.0;
	}

	public static double EffectiveMagicResistance(Combatant target, double baseMagicResistance)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = Math.Max(0.0, baseMagicResistance);
		if (target.HasStatus("mrhalf"))
		{
			num /= 2.0;
		}
		if (target.HasStatus("confuse") || target.HasStatus("panic"))
		{
			num -= 10.0;
		}
		return Math.Max(0.0, num);
	}

	public static bool BlocksMobSkillCasting(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.UsesMonsterTemplate)
		{
			if (!actor.HasStatus("vacuum") && !actor.HasStatus("confuse"))
			{
				return actor.HasStatus("magicseal");
			}
			return true;
		}
		return false;
	}

	public static string PotencyCounterKey(string statusKind)
	{
		return "status:" + NormalizeKind(statusKind) + ":potency";
	}

	public static string NormalizeKind(string statusKind)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(statusKind, "statusKind");
		if (statusKind.Trim().ToLowerInvariant() == "slowatk")
		{
			return "slowAtk";
		}
		return statusKind.Trim().ToLowerInvariant();
	}

	private static string L1jRegistField(string statusKind)
	{
		return statusKind switch
		{
			"stun" => "registStun", 
			"stone" => "registStone", 
			"sleep" => "registSleep", 
			"freeze" => "registFreeze", 
			"bind" => "registSustain", 
			"blind" => "registBlind", 
			_ => "", 
		};
	}

	private static double ReadNumber(JsonObject source, string field)
	{
		if (!CombatSkill.TryReadDouble(source[field], out var value) || !double.IsFinite(value))
		{
			return 0.0;
		}
		return value;
	}
}
