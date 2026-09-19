using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class PreEnhancedLootRules
{
	private static readonly double[] WeaponBonusChances = new double[6] { 0.01, 0.001, 0.0001, 1E-05, 1E-06, 1E-07 };

	private static readonly double[] ArmorBonusChances = new double[4] { 0.01, 0.0001, 1E-06, 1E-07 };

	private static readonly HashSet<string> NonArmorSlots = new HashSet<string>(StringComparer.Ordinal) { "lantern", "petarm" };

	private const int HardCap = 15;

	public static bool IsEligible(JsonObject? definition)
	{
		if (definition == null)
		{
			return false;
		}
		if (CombatSkill.ReadBool(definition, "relic"))
		{
			return false;
		}
		if (CombatSkill.ReadBool(definition, "noEnhance"))
		{
			return false;
		}
		string text = CombatSkill.ReadString(definition, "type");
		if (text == "wpn")
		{
			return !CombatSkill.ReadBool(definition, "isArrow");
		}
		if (text == "arm")
		{
			return !NonArmorSlots.Contains(CombatSkill.ReadString(definition, "slot"));
		}
		return false;
	}

	public static int SafeEnchantOf(JsonObject? definition)
	{
		if (!IsEligible(definition))
		{
			return 0;
		}
		return Math.Max(0, (int)CombatSkill.ReadDouble(definition, "safe"));
	}

	public static int BaseEnhancement(JsonObject? definition)
	{
		return SafeEnchantOf(definition);
	}

	public static int RollBonus(JsonObject? definition, double roll)
	{
		if (!IsEligible(definition))
		{
			return 0;
		}
		double[] array = ((CombatSkill.ReadString(definition, "type") == "wpn") ? WeaponBonusChances : ArmorBonusChances);
		double num = 0.0;
		for (int num2 = array.Length - 1; num2 >= 0; num2--)
		{
			num += array[num2];
			if (roll < num)
			{
				return num2 + 1;
			}
		}
		return 0;
	}

	public static int RollEnhancement(JsonObject? definition, double roll)
	{
		if (!IsEligible(definition))
		{
			return 0;
		}
		return Math.Clamp(BaseEnhancement(definition) + RollBonus(definition, roll), 1, 15);
	}
}
