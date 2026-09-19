using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class BehaviorBuffRules
{
	public const string OutlawBuff = "sk_warrior_outlaw";

	public const string FlameSoulBuff = "sk_elf_flamesoul";

	public const string DarkDodgeBuff = "sk_dark_dodge";

	public const string MirrorImageBuff = "sk_illu_mirror";

	public const string TerrorStatus = "terror";

	public const string PreciseShotStatus = "preciseshot";

	public const string BurningWillBuff = "sk_dark_burn";

	public const string AttributeFireBuff = "sk_elf_attrfire";

	public const string DoubleBreakBuff = "sk_dark_double";

	public const string FlameSlashBuff = "sk_dragon_flameslash";

	public const string BraveWillBuff = "sk_royal_bravewill";

	public const string MagicShieldBuff = "sk_magic_shield";

	public const string MagicShieldCooldownBuff = "_magic_shield_cooldown";

	public const double MagicShieldCooldownSeconds = 3.0;

	public const string MirrorBuff = "sk_elf_mirror";

	public const string DeadlyBodyBuff = "sk_dragon_deadlybody";

	public const double DeadlyBodyReflectChance = 0.23;

	public const string EnduranceBuff = "sk_warrior_endurance";

	public const string BloodlustBuff = "sk_dragon_bloodlust";

	public const double BloodlustAttackSpeedMultiplier = 1.15;

	public const string SingleResistBuff = "sk_elf_singleres";

	public const double SingleResistBonus = 50.0;

	public const string WaterVitalBuff = "sk_elf_watervital";

	public const string WaterVitalCooldownBuff = "_water_vital_cooldown";

	public const double WaterVitalCooldownSeconds = 7.0;

	public const string SunlightBuff = "sk_sunlight";

	public const string CounterBarrierBuff = "sk_counter_barrier";

	public static bool CounterBarrierActive(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return Active(actor, "sk_counter_barrier");
	}

	private static bool Active(Combatant actor, string buffId)
	{
		return actor.Buffs.GetValueOrDefault(buffId) > 0.0;
	}

	public static int HitValueFloor(Combatant attacker)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (!Active(attacker, "sk_warrior_outlaw"))
		{
			return 0;
		}
		return 50;
	}

	public static bool MaximizesWeaponRoll(Combatant attacker, bool ranged)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (!ranged)
		{
			return Active(attacker, "sk_elf_flamesoul");
		}
		return false;
	}

	public static double TargetHitValueAdjustment(Combatant target, bool playerTypeTarget)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = (target.HasStatus("terror") ? 5 : 0);
		if (!playerTypeTarget)
		{
			return num;
		}
		if (Active(target, "sk_dark_dodge"))
		{
			num -= 5.0;
		}
		if (Active(target, "sk_illu_mirror"))
		{
			num -= 5.0;
		}
		return num;
	}

	public static int HitPercentLowerBound(Combatant target, bool playerTypeTarget)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!playerTypeTarget || !Active(target, "sk_dark_dodge"))
		{
			return 5;
		}
		return 0;
	}

	public static double BasicAttackDamageMultiplier(IGameData? data, Combatant attacker, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(random, "random");
		double num = 1.0;
		if (Active(attacker, "sk_dark_burn") && random.NextDouble() < 0.3)
		{
			num *= 1.5;
		}
		if (Active(attacker, "sk_elf_attrfire") && random.NextDouble() < 0.3)
		{
			num *= 1.5;
		}
		if (Active(attacker, "sk_dark_double") && HasDoubleBreakWeapon(data, attacker))
		{
			double num2 = 10.0 + ((attacker.Level >= 45) ? Math.Floor((double)(attacker.Level - 45) / 5.0) : 0.0);
			if (random.NextDouble() * 100.0 < num2)
			{
				num *= 2.0;
			}
		}
		if (Active(attacker, "sk_royal_bravewill") && random.NextDouble() < 0.1)
		{
			num *= 1.5;
		}
		return num;
	}

	private static bool HasDoubleBreakWeapon(IGameData? data, Combatant attacker)
	{
		if (data == null || attacker.MainWeaponId.Length == 0)
		{
			return false;
		}
		if (!(data.Table("WEAPON_TAGS") is JsonObject jsonObject) || !(jsonObject[attacker.MainWeaponId] is JsonArray jsonArray))
		{
			return false;
		}
		string value = default(string);
		foreach (JsonNode item in jsonArray)
		{
			bool flag = item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value);
			if (flag)
			{
				bool flag2 = ((value == "雙刀" || value == "鋼爪") ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	public static double ConsumeFlameSlashBonus(Combatant attacker, bool ranged)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		if (ranged || !Active(attacker, "sk_dragon_flameslash"))
		{
			return 0.0;
		}
		attacker.Buffs.Remove("sk_dragon_flameslash");
		return 7.0;
	}

	public static bool TryAbsorbMagicDamage(Combatant defender)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		if (!Active(defender, "sk_magic_shield"))
		{
			return false;
		}
		defender.Buffs.Remove("sk_magic_shield");
		defender.Buffs["_magic_shield_cooldown"] = 3.0;
		return true;
	}

	public static bool CanCast(Combatant actor, string skillId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (skillId == "sk_magic_shield")
		{
			return !Active(actor, "_magic_shield_cooldown");
		}
		return true;
	}

	public static bool MirrorReflects(Combatant defender, double appliedDamage, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (appliedDamage > 0.0 && Active(defender, "sk_elf_mirror"))
		{
			return random.NextDouble() * 100.0 < Math.Max(0.0, defender.D.Wis);
		}
		return false;
	}

	public static bool DeadlyBodyReflects(Combatant defender, double appliedDamage, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (appliedDamage > 0.0 && Active(defender, "sk_dragon_deadlybody"))
		{
			return random.NextDouble() < 0.23;
		}
		return false;
	}

	public static void ApplyResourceMultipliers(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (Active(actor, "sk_warrior_endurance"))
		{
			actor.MaxHp = Math.Floor(actor.MaxHp * (1.0 + (double)actor.Level / 2.0 / 100.0));
		}
	}

	public static double AttackIntervalMultiplier(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!Active(actor, "sk_dragon_bloodlust"))
		{
			return 1.0;
		}
		return 0.8695652173913044;
	}

	public static bool IlluminatesDarkness(Combatant? actor)
	{
		if (actor != null)
		{
			return Active(actor, "sk_sunlight");
		}
		return false;
	}

	public static void ApplyResistBonuses(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (Active(actor, "sk_elf_singleres"))
		{
			switch (actor.ElfElement)
			{
			case "fire":
				actor.D.ResistFire += 50.0;
				break;
			case "water":
				actor.D.ResistWater += 50.0;
				break;
			case "earth":
				actor.D.ResistEarth += 50.0;
				break;
			case "wind":
				actor.D.ResistWind += 50.0;
				break;
			}
		}
	}

	public static double ConsumeHealMultiplier(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!Active(target, "sk_elf_watervital") || Active(target, "_water_vital_cooldown"))
		{
			return 1.0;
		}
		target.Buffs["_water_vital_cooldown"] = 7.0;
		return 2.0;
	}
}
