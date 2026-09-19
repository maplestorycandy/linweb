using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class SpellbladeRules
{
	public const string ActiveBuff = "_spellblade";

	public const string BonusCounter = "spellbladeBonus";

	public const string ElementCounter = "spellbladeElement";

	public const double DurationSeconds = 10.0;

	public static SpellbladeProfile? Profile(IGameData? data, Combatant actor, int skillTier, string skillElement, bool isMagicDamage, int consumedMana)
	{
		if (consumedMana > 0 && isMagicDamage)
		{
			JsonObject jsonObject = data?.Item(actor.MainWeaponId);
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "spellbladeBuff"))
			{
				return new SpellbladeProfile(10.0, TierBonus(skillTier), CombatSkill.NormalizeElement(skillElement));
			}
		}
		return null;
	}

	public static void Store(Combatant actor, SpellbladeProfile profile)
	{
		actor.Counters["spellbladeBonus"] = profile.MeleeDamageAndHit;
		actor.Counters["spellbladeElement"] = EncodeElement(profile.AttackElement);
		actor.Buffs["_spellblade"] = profile.DurationSeconds;
	}

	public static void ApplyDerivedBonuses(IGameData? data, Combatant actor)
	{
		if (!(actor.Buffs.GetValueOrDefault("_spellblade") <= 0.0))
		{
			JsonObject jsonObject = data?.Item(actor.MainWeaponId);
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "spellbladeBuff"))
			{
				double num = Math.Max(0, actor.Counters.GetValueOrDefault("spellbladeBonus"));
				actor.D.MeleeDamage += num;
				actor.D.MeleeHit += num;
				actor.AttackElement = DecodeElement(actor.Counters.GetValueOrDefault("spellbladeElement"));
			}
		}
	}

	public static int TierBonus(int tier)
	{
		return Math.Max(1, tier) switch
		{
			1 => 1, 
			2 => 2, 
			3 => 3, 
			4 => 6, 
			5 => 9, 
			6 => 12, 
			7 => 15, 
			8 => 18, 
			9 => 21, 
			_ => 25, 
		};
	}

	private static int EncodeElement(string element)
	{
		return element switch
		{
			"fire" => 1, 
			"water" => 2, 
			"wind" => 3, 
			"earth" => 4, 
			_ => 0, 
		};
	}

	private static string DecodeElement(int encoded)
	{
		return encoded switch
		{
			1 => "fire", 
			2 => "water", 
			3 => "wind", 
			4 => "earth", 
			_ => "none", 
		};
	}
}
