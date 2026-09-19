using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

internal static class SkillBuffRules
{
	private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> EquivalentBuffs = BuildEquivalentBuffs(new string[1][] { new string[2] { "sk_haste_spell", "sk_greater_haste" } });

	private static readonly HashSet<string> FullySupportedFlagBuffs = new HashSet<string>(StringComparer.Ordinal)
	{
		"sk_haste_spell", "sk_greater_haste", "sk_holy_dash", "sk_elf_winddash", "sk_dark_walkhaste", "sk_dark_poison", "sk_dark_poisonres", "sk_heal_energy_storm", "sk_royal_precise", "sk_royal_bravewill",
		"sk_invisible", "sk_reveal", "sk_soul_up", "sk_reduction_armor", "sk_holy_barrier", "sk_elf_earthshield", "sk_illu_avatar", "sk_illu_mirror", "sk_illu_pain", "sk_dark_stealth",
		"sk_fire_prison", "sk_blizzard_storm", "sk_warrior_outlaw", "sk_elf_flamesoul", "sk_dark_burn", "sk_elf_attrfire", "sk_dark_double", "sk_dragon_flameslash", "sk_magic_shield", "sk_dark_dodge",
		"sk_elf_mirror", "sk_dragon_deadlybody", "sk_warrior_endurance", "sk_dragon_bloodlust", "sk_elf_singleres", "sk_elf_watervital", "sk_sunlight", "sk_counter_barrier", "sk_load_up", "sk_illu_loaddown",
		"sk_elf_physboost", "sk_elf_energyboost"
	};

	private static readonly string[] UnsupportedBehaviorFields = new string[1] { "summon" };

	public const string SoulUpBuff = "sk_soul_up";

	public const string ReductionArmorBuff = "sk_reduction_armor";

	public const string HolyBarrierBuff = "sk_holy_barrier";

	public const string EarthShieldBuff = "sk_elf_earthshield";

	public const string AvatarBuff = "sk_illu_avatar";

	public static bool IsExecutable(string skillId, JsonObject source)
	{
		if (!string.Equals(CombatSkill.ReadString(source, "type"), "buff", StringComparison.Ordinal))
		{
			return false;
		}
		if (UnsupportedBehaviorFields.Any((string field) => source[field] != null))
		{
			return false;
		}
		if (!(source["d"] is JsonObject) && L1jSkillHandover.L1jBuffModifiers(source) == null && !CubeBuffRules.IsCubeBuff(skillId))
		{
			return FullySupportedFlagBuffs.Contains(skillId);
		}
		return true;
	}

	public static bool AffectsDerivedStats(IGameData? data, string buffName)
	{
		if (!string.Equals(buffName, "_spellblade", StringComparison.Ordinal) && !string.Equals(buffName, "poly", StringComparison.Ordinal))
		{
			JsonObject jsonObject = data?.Skill(buffName);
			if (jsonObject != null && string.Equals(CombatSkill.ReadString(jsonObject, "type"), "buff", StringComparison.Ordinal))
			{
				if (!(jsonObject["d"] is JsonObject))
				{
					return L1jSkillHandover.L1jBuffModifiers(jsonObject) != null;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public static bool HasEquivalentActive(Combatant actor, string skillId)
	{
		if (EquivalentBuffs.TryGetValue(skillId, out IReadOnlySet<string> value))
		{
			return value.Any((string equivalent) => actor.Buffs.GetValueOrDefault(equivalent) > 0.0);
		}
		return false;
	}

	public static void ApplyAttributeBonuses(Attributes target, Combatant actor, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		foreach (JsonObject item in ActiveModifierSets(actor, data))
		{
			target.Str += CombatSkill.ReadDouble(item, "str");
			target.Dex += CombatSkill.ReadDouble(item, "dex");
			target.Con += CombatSkill.ReadDouble(item, "con");
			target.Int += CombatSkill.ReadDouble(item, "int");
			target.Wis += CombatSkill.ReadDouble(item, "wis");
			target.Cha += CombatSkill.ReadDouble(item, "cha");
		}
	}

	public static void ApplyDerivedBonuses(Combatant actor, IGameData data)
	{
		DerivedStats d = actor.D;
		foreach (JsonObject item in ActiveModifierSets(actor, data))
		{
			d.MeleeDamage += CombatSkill.ReadDouble(item, "meleeDmg");
			d.MeleeHit += CombatSkill.ReadDouble(item, "meleeHit");
			d.RangedDamage += CombatSkill.ReadDouble(item, "rangedDmg");
			d.RangedHit += CombatSkill.ReadDouble(item, "rangedHit");
			d.ExtraDamage += CombatSkill.ReadDouble(item, "extraDmg");
			d.ExtraHit += CombatSkill.ReadDouble(item, "extraHit");
			d.MagicDamage += CombatSkill.ReadDouble(item, "magicDmg");
			d.ArmorClass -= CombatSkill.ReadDouble(item, "ac");
			d.EvasionRating += CombatSkill.ReadDouble(item, "er");
			d.ManaRegen += CombatSkill.ReadDouble(item, "mpR");
			d.DamageReduction += CombatSkill.ReadDouble(item, "dr");
			d.MagicResist += CombatSkill.ReadDouble(item, "mr");
			d.ResistFire += CombatSkill.ReadDouble(item, "resFire");
			d.ResistWater += CombatSkill.ReadDouble(item, "resWater");
			d.ResistEarth += CombatSkill.ReadDouble(item, "resEarth");
			d.ResistWind += CombatSkill.ReadDouble(item, "resWind");
		}
		if (actor.Buffs.GetValueOrDefault("sk_reduction_armor") > 0.0)
		{
			d.DamageReduction += Math.Floor((double)actor.Level / 10.0);
		}
	}

	internal static (double Hit, double Damage, double ExtraDamage) MonsterCompanionPhysicalBonuses(Combatant actor, IGameData? data, bool ranged)
	{
		if (data == null || !MonsterCompanionRules.IsCompanion(actor))
		{
			return default((double, double, double));
		}
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		foreach (JsonObject item in ActiveModifierSets(actor, data))
		{
			num += CombatSkill.ReadDouble(item, "str");
			num2 += CombatSkill.ReadDouble(item, "dex");
			num3 += CombatSkill.ReadDouble(item, ranged ? "rangedHit" : "meleeHit");
			num3 += CombatSkill.ReadDouble(item, "extraHit");
			num4 += CombatSkill.ReadDouble(item, ranged ? "rangedDmg" : "meleeDmg");
			num5 += CombatSkill.ReadDouble(item, "extraDmg");
		}
		num3 += (double)(L1jAttackTables.StrHit(actor.D.Str + num) - L1jAttackTables.StrHit(actor.D.Str));
		num3 += (double)(L1jAttackTables.DexHit(actor.D.Dex + num2) - L1jAttackTables.DexHit(actor.D.Dex));
		num4 += (double)(ranged ? (L1jAttackTables.DexDmg(actor.D.Dex + num2) - L1jAttackTables.DexDmg(actor.D.Dex)) : (L1jAttackTables.StrDmg(actor.D.Str + num) - L1jAttackTables.StrDmg(actor.D.Str)));
		return (Hit: num3, Damage: num4, ExtraDamage: num5);
	}

	public static double IncomingDamageMultiplier(IGameData? data, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		double num = ((target.Buffs.GetValueOrDefault("sk_holy_barrier") > 0.0) ? 0.7 : 1.0);
		if (target.Buffs.GetValueOrDefault("sk_illu_avatar") > 0.0)
		{
			JsonObject jsonObject = data?.Skill("sk_illu_avatar");
			double value = ((jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "dmgTakenReduce", 3.0) : 3.0);
			num *= 1.0 - Math.Clamp(value, 0.0, 100.0) / 100.0;
		}
		return num;
	}

	public static bool BlocksBasicAttack(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		return target.Buffs.GetValueOrDefault("sk_elf_earthshield") > 0.0;
	}

	public static void ApplyResourceMultipliers(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Buffs.GetValueOrDefault("sk_soul_up") > 0.0)
		{
			actor.MaxHp = Math.Floor(actor.MaxHp * 1.2);
			actor.MaxMp = Math.Floor(actor.MaxMp * 1.2);
		}
		AwakeningRules.ApplyResourceBonuses(actor);
	}

	private static IEnumerable<JsonObject> ActiveModifierSets(Combatant actor, IGameData data)
	{
		foreach (var (text2, num2) in actor.Buffs)
		{
			if (num2 <= 0.0)
			{
				continue;
			}
			JsonObject jsonObject = data.Skill(text2);
			if (jsonObject != null)
			{
				JsonObject jsonObject2 = AwakeningRules.Modifiers(text2) ?? L1jSkillHandover.L1jBuffModifiers(jsonObject) ?? (jsonObject["d"] as JsonObject);
				if (jsonObject2 != null)
				{
					yield return jsonObject2;
				}
			}
		}
	}

	private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildEquivalentBuffs(IEnumerable<IReadOnlyList<string>> groups)
	{
		Dictionary<string, IReadOnlySet<string>> dictionary = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
		foreach (IReadOnlyList<string> group in groups)
		{
			foreach (string skillId in group)
			{
				dictionary[skillId] = group.Where((string other) => !string.Equals(other, skillId, StringComparison.Ordinal)).ToHashSet<string>(StringComparer.Ordinal);
			}
		}
		return dictionary;
	}
}
