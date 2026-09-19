using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class RelicConditionalCombatRules
{
	public const string FireVulnerabilityBuff = "_relicFireVulnerability";

	public const string WetBuff = "_relicWet";

	public static double ApplyBasicAttackDamage(IGameData? data, Combatant attacker, Combatant target, double damage, double? effectiveAttackIntervalSeconds = null)
	{
		if (damage <= 0.0)
		{
			return 0.0;
		}
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject == null)
		{
			return damage;
		}
		double num = damage;
		if (jsonObject["raceBonus"] is JsonObject source && RaceMatches(target, CombatSkill.ReadString(source, "race")))
		{
			num = Math.Max(1.0, Math.Floor(num * Math.Max(0.0, CombatSkill.ReadDouble(source, "mult", 1.0))));
		}
		if (jsonObject["raceFlat"] is JsonObject source2 && RaceMatches(target, CombatSkill.ReadString(source2, "race")))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(source2, "add"));
		}
		if (jsonObject["eleBonusDmg"] is JsonObject source3 && string.Equals(CombatSkill.NormalizeElement(CombatSkill.ReadString(source3, "ele")), CombatSkill.NormalizeElement(target.Element), StringComparison.Ordinal))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(source3, "add", CombatSkill.ReadDouble(source3, "dmg")));
		}
		if (target.HasStatus("magicseal") || target.HasStatus("silence"))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "silencedBonusDmg"));
		}
		if (target.HasStatus("poison"))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "poisonedBonusDmg"));
		}
		if (target.HasStatus("slow"))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "slowedBonusDmg"));
		}
		if (IsParalyzeImmuneTarget(data, target))
		{
			num += Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "immParalyzeBonusDmg"));
		}
		num = ApplyTargetProfileMultiplier(jsonObject, target, num);
		num = ApplyElementWeaponMultiplier(data, attacker, num);
		if (CombatSkill.ReadBool(jsonObject, "slowScaleDmg"))
		{
			double num2 = Math.Max(0.0, effectiveAttackIntervalSeconds ?? attacker.D.AttackInterval);
			num += Math.Max(0.0, Math.Floor((num2 - 0.1) / 0.05 + 1E-09));
		}
		return Math.Max(1.0, num);
	}

	public static double ApplyBasicAttackDoubleStrike(IGameData? data, Combatant attacker, double damage, double rollPercent)
	{
		if (damage <= 0.0)
		{
			return 0.0;
		}
		double num = BasicAttackDoubleStrikeChancePercent(data, attacker);
		if (!(rollPercent < num))
		{
			return damage;
		}
		return Math.Max(1.0, damage * 2.0);
	}

	public static double BasicAttackDoubleStrikeChancePercent(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Clamp((jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "dblStrikeRate") : 0.0, 0.0, 100.0);
	}

	public static double FullHealthTripleArrowMultiplier(IGameData? data, Combatant attacker, Combatant target, string skillId, int hitIndex)
	{
		if (hitIndex == 0 && !(target.Hp <= 0.0) && !(target.Hp < target.MaxHp) && string.Equals(skillId, "sk_elf_triple", StringComparison.Ordinal))
		{
			JsonObject jsonObject = MainWeapon(data, attacker);
			if (jsonObject != null)
			{
				return Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "fullHpMultTriple", 1.0));
			}
		}
		return 1.0;
	}

	public static double IncomingHeavyDamageMultiplier(IGameData? data, Combatant defender, bool heavy)
	{
		if (!heavy)
		{
			return 1.0;
		}
		double value = EquippedDefinitions(data, defender).Sum((JsonObject definition) => Math.Max(0.0, CombatSkill.ReadDouble(definition, "crushDr")));
		return 1.0 - Math.Clamp(value, 0.0, 80.0) / 100.0;
	}

	public static double GatedPhysicalReductionPercent(IGameData? data, Combatant defender)
	{
		return Math.Clamp(EquippedDefinitions(data, defender).Sum((JsonObject definition) => Math.Max(0.0, CombatSkill.ReadDouble(definition, "physDrGated"))), 0.0, 90.0);
	}

	public static double PhysicalCriticalDamageBonus(IGameData? data, Combatant attacker, bool ranged)
	{
		if (ranged || attacker.Hp <= 0.0)
		{
			return 0.0;
		}
		double num = 0.0;
		foreach (JsonObject item in EquippedDefinitions(data, attacker))
		{
			if (item["critDmgLowHp"] is JsonObject source && !(attacker.Hp >= Math.Max(0.0, CombatSkill.ReadDouble(source, "hp"))))
			{
				num += Math.Max(0.0, CombatSkill.ReadDouble(source, "add"));
			}
		}
		return num;
	}

	public static int HeavyRollThreshold(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject == null || !string.Equals(WeaponCombatProfile.WeaponEffect(jsonObject), "crush", StringComparison.Ordinal))
		{
			return 20;
		}
		int num = (int)Math.Round(Math.Max(0.0, CombatSkill.ReadDouble(jsonObject, "heavyRatePct")) / 5.0, MidpointRounding.AwayFromZero);
		return Math.Clamp(19 - num, 2, 19);
	}

	public static double HeavyDamageMultiplier(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "heavyMult", 1.0) : 1.0);
	}

	public static double HeavyBonusDamage(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "heavyBonusDmg") : 0.0);
	}

	public static double MissGrazeChancePercent(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Clamp((jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "missGrazeRate") : 0.0, 0.0, 100.0);
	}

	public static double GrazeDamageMultiplier(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Clamp(((jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "grazeDmgPct", 50.0) : 50.0) / 100.0, 0.0, 1.0);
	}

	public static bool CannotEvade(IGameData? data, Combatant defender)
	{
		return EquippedDefinitions(data, defender).Any((JsonObject definition) => CombatSkill.ReadBool(definition, "noEvade"));
	}

	public static bool WeaponPreventsBleed(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "noBleed");
		}
		return false;
	}

	public static bool HasFireNullify(IGameData? data, Combatant defender)
	{
		return EquippedDefinitions(data, defender).Any((JsonObject definition) => CombatSkill.ReadBool(definition, "fireNullify"));
	}

	public static double GroupHealMultiplier(IGameData? data, Combatant caster)
	{
		JsonObject jsonObject = MainWeapon(data, caster);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "groupHealMult", 1.0) : 1.0);
	}

	public static bool IgnoresSpellMagicResistance(IGameData? data, Combatant caster)
	{
		JsonObject jsonObject = MainWeapon(data, caster);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "spellIgnoreMr");
		}
		return false;
	}

	public static double AutoCastManaMultiplier(IGameData? data, Combatant caster)
	{
		JsonObject jsonObject = MainWeapon(data, caster);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "autoCastMpMult", 1.0) : 1.0);
	}

	public static int SkillManaCost(IGameData? data, Combatant caster, int cost)
	{
		if (cost <= 0)
		{
			return 0;
		}
		int originalManaCostReduction = caster.D.OriginalManaCostReduction;
		if (originalManaCostReduction > 0)
		{
			cost -= originalManaCostReduction;
			if (cost <= 0)
			{
				cost = 1;
			}
		}
		JsonObject jsonObject = MainWeapon(data, caster);
		if (jsonObject == null || !CombatSkill.ReadBool(jsonObject, "fullHpMpHalf") || caster.Hp <= 0.0 || caster.Hp < caster.MaxHp)
		{
			return cost;
		}
		return Math.Max(1, (int)Math.Ceiling((double)cost / 2.0));
	}

	private static int L1jIntThresholdReduction(int officialSkillId, int intelligence)
	{
		if (officialSkillId >= 87 && officialSkillId <= 91)
		{
			if (intelligence <= 12)
			{
				return 0;
			}
			return intelligence - 12;
		}
		if ((officialSkillId <= 8 || officialSkillId > 80) ? true : false)
		{
			return 0;
		}
		int num = 0;
		if (intelligence > 12)
		{
			num++;
		}
		if (intelligence > 13 && officialSkillId > 16)
		{
			num++;
		}
		if (intelligence > 14 && officialSkillId > 23)
		{
			num++;
		}
		if (intelligence > 15 && officialSkillId > 32)
		{
			num++;
		}
		if (intelligence > 16 && officialSkillId > 40)
		{
			num++;
		}
		if (intelligence > 17 && officialSkillId > 48)
		{
			num++;
		}
		if (intelligence > 18 && officialSkillId > 56)
		{
			num++;
		}
		return num;
	}

	private static bool HelmetHalvesManaCost(IGameData? data, Combatant caster, int officialSkillId)
	{
		JsonObject jsonObject = EquippedDefinition(data, caster, "helm");
		if (jsonObject == null)
		{
			return false;
		}
		int num = CombatSkill.ReadInt(jsonObject, "l1jItemId");
		switch (officialSkillId)
		{
		case 1:
		case 19:
			return num == 20014;
		case 12:
		case 13:
		case 42:
			return num == 20015;
		case 26:
			return num == 20013;
		case 43:
			return (num == 20008 || num == 20013) ? true : false;
		case 54:
			return num == 20023;
		default:
			return false;
		}
	}

	public static int SkillManaCost(IGameData? data, Combatant caster, string skillId, int cost)
	{
		if (string.Equals(skillId, "sk_chill", StringComparison.Ordinal))
		{
			JsonObject jsonObject = MainWeapon(data, caster);
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "freeChill"))
			{
				return 0;
			}
		}
		if (cost > 0 && data?.Skill(skillId)?["l1j"] is JsonObject source)
		{
			int officialSkillId = CombatSkill.ReadInt(source, "officialId");
			cost -= L1jIntThresholdReduction(officialSkillId, (int)Math.Floor(caster.D.Int));
			if (HelmetHalvesManaCost(data, caster, officialSkillId))
			{
				cost /= 2;
			}
			cost = Math.Max(cost, 1);
		}
		return SkillManaCost(data, caster, cost);
	}

	public static double HasteStrikeBonus(IGameData? data, Combatant attacker)
	{
		int num;
		if (attacker.Buffs.GetValueOrDefault("haste") > 0.0)
		{
			JsonObject jsonObject = MainWeapon(data, attacker);
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "hasteStrike"))
			{
				num = 30;
				goto IL_0038;
			}
		}
		num = 0;
		goto IL_0038;
		IL_0038:
		return num;
	}

	public static double PoisonDamageMultiplier(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "poisonMult", 1.0) : 1.0);
	}

	public static double PoisonHealingMultiplier(IGameData? data, Combatant defender)
	{
		return (from definition in EquippedDefinitions(data, defender)
			select Math.Max(0.0, CombatSkill.ReadDouble(definition, "poisonHealMult"))).DefaultIfEmpty(0.0).Max();
	}

	public static string OnHitElementVulnerability(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		return CombatSkill.NormalizeElement((jsonObject != null) ? CombatSkill.ReadString(jsonObject, "onHitEleVuln") : string.Empty);
	}

	public static bool AppliesWetOnHit(IGameData? data, Combatant attacker)
	{
		JsonObject jsonObject = MainWeapon(data, attacker);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "onHitWet");
		}
		return false;
	}

	public static double IncomingElementExposureMultiplier(Combatant defender, string element)
	{
		string text = CombatSkill.NormalizeElement(element);
		if (text == "fire" && defender.Buffs.GetValueOrDefault("_relicFireVulnerability") > 0.0)
		{
			return 1.3;
		}
		if (text == "wind" && defender.Buffs.GetValueOrDefault("_relicWet") > 0.0)
		{
			return 2.0;
		}
		return 1.0;
	}

	public static double AutoCastDamageMultiplier(IGameData? data, Combatant caster)
	{
		JsonObject jsonObject = MainWeapon(data, caster);
		return Math.Max(0.0, (jsonObject != null) ? CombatSkill.ReadDouble(jsonObject, "autoCastDmgMult", 1.0) : 1.0);
	}

	public static string EffectiveSkillId(IGameData? data, Combatant caster, string requestedSkillId)
	{
		if (string.Equals(requestedSkillId, "sk_fireball", StringComparison.Ordinal))
		{
			JsonObject jsonObject = EquippedDefinition(data, caster, "armor");
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "fireballBurst"))
			{
				return "sk_fireball_burst";
			}
		}
		return requestedSkillId;
	}

	public static bool HasAutoCastBacklash(IGameData? data, Combatant caster)
	{
		JsonObject jsonObject = MainWeapon(data, caster);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "autocastBacklash");
		}
		return false;
	}

	public static double WeakPointInsightDamage(IGameData? data, Combatant attacker, Combatant target)
	{
		if (!CombatMath.IsElementCounter(attacker.AttackElement, target.Element))
		{
			return 0.0;
		}
		return EquippedDefinitions(data, attacker, includeOffhandWeapon: false).Sum((JsonObject definition) => Math.Max(0.0, CombatSkill.ReadDouble(definition, "weakHitBonus")));
	}

	public static double IncomingRaceDamageMultiplier(IGameData? data, Combatant defender, Combatant attacker)
	{
		if (attacker.Race.Length == 0)
		{
			return 1.0;
		}
		double num = 1.0;
		foreach (JsonObject item in EquippedDefinitions(data, defender))
		{
			if (item["raceDr"] is JsonObject source && RaceMatches(attacker, CombatSkill.ReadString(source, "race")))
			{
				double num2 = Math.Clamp(CombatSkill.ReadDouble(source, "pct"), 0.0, 100.0);
				num *= 1.0 - num2 / 100.0;
			}
		}
		return Math.Clamp(num, 0.0, 1.0);
	}

	public static RelicUndeadImmunity? UndeadImmunity(IGameData? data, Combatant defender, Combatant attacker)
	{
		CombatantKind kind = defender.Kind;
		if ((kind != CombatantKind.Player && kind != CombatantKind.Ally && !HostilePlayerRules.IsHostilePlayer(defender)) || !CounterDamageRules.HasTargetTag(data, attacker, "undead"))
		{
			return null;
		}
		if (data == null)
		{
			return null;
		}
		foreach (ItemStack value in defender.EquippedItems.Values)
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null && jsonObject["undeadImmune"] is JsonObject source)
			{
				string text = CombatSkill.ReadString(jsonObject, "n");
				return new RelicUndeadImmunity(value.ItemKey, (text.Length > 0) ? text : value.ItemKey, Math.Max(1.0 / 60.0, CombatSkill.ReadDouble(source, "cdSec", 5.0)));
			}
		}
		return null;
	}

	private static bool RaceMatches(Combatant actor, string expectedRace)
	{
		if (expectedRace.Length > 0)
		{
			return string.Equals(actor.Race.Trim(), expectedRace.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsParalyzeImmuneTarget(IGameData? data, Combatant target)
	{
		if (target.IsBoss || StatusRules.IsImmune(target, "paralyze"))
		{
			return true;
		}
		if (data == null || target.Avatar.Length == 0)
		{
			return false;
		}
		JsonObject jsonObject = data.Mob(target.Avatar);
		if (jsonObject != null)
		{
			if (!CombatSkill.ReadBool(jsonObject, "immParalyze"))
			{
				return CombatSkill.ReadBool(jsonObject, "immStun");
			}
			return true;
		}
		return false;
	}

	private static double ApplyTargetProfileMultiplier(JsonObject weapon, Combatant target, double damage)
	{
		double num = 1.0;
		num = ((!target.Hard) ? (num * Math.Max(0.0, CombatSkill.ReadDouble(weapon, "softMult", 1.0))) : (num * Math.Max(0.0, CombatSkill.ReadDouble(weapon, "hardSkinMult", 1.0))));
		if (target.Hp > 0.0 && target.Hp >= target.MaxHp)
		{
			num *= Math.Max(0.0, CombatSkill.ReadDouble(weapon, "fullHpMult", 1.0));
		}
		return Math.Max(1.0, Math.Floor(damage * num));
	}

	private static double ApplyElementWeaponMultiplier(IGameData? data, Combatant attacker, double damage)
	{
		string text = CombatSkill.NormalizeElement(attacker.AttackElement);
		if (text.Length == 0)
		{
			return damage;
		}
		double num = 1.0;
		foreach (JsonObject item in EquippedDefinitions(data, attacker))
		{
			if (item["eleWpnMult"] is JsonObject source && string.Equals(CombatSkill.NormalizeElement(CombatSkill.ReadString(source, "ele")), text, StringComparison.Ordinal))
			{
				num *= Math.Max(0.0, CombatSkill.ReadDouble(source, "mult", 1.0));
			}
		}
		return Math.Max(1.0, Math.Floor(damage * num));
	}

	private static JsonObject? MainWeapon(IGameData? data, Combatant actor)
	{
		if (data == null)
		{
			return null;
		}
		ItemStack value;
		string text = (actor.EquippedItems.TryGetValue("wpn", out value) ? value.ItemKey : actor.MainWeaponId);
		if (text.Length <= 0)
		{
			return null;
		}
		return data.Item(text);
	}

	private static JsonObject? EquippedDefinition(IGameData? data, Combatant actor, string slot)
	{
		if (data == null || !actor.EquippedItems.TryGetValue(slot, out ItemStack value))
		{
			return null;
		}
		return data.Item(value.ItemKey);
	}

	private static IEnumerable<JsonObject> EquippedDefinitions(IGameData? data, Combatant actor, bool includeOffhandWeapon = true)
	{
		if (data == null)
		{
			yield break;
		}
		foreach (var (text2, itemStack2) in actor.EquippedItems)
		{
			if (includeOffhandWeapon || !(text2 == "offwpn"))
			{
				JsonObject jsonObject = data.Item(itemStack2.ItemKey);
				if (jsonObject != null)
				{
					yield return jsonObject;
				}
			}
		}
	}
}
