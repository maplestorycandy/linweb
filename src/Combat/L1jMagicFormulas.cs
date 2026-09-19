using System;

namespace IdleLineage.Combat;

public static class L1jMagicFormulas
{
	public readonly record struct MagicDamageRoll(int Damage, bool Critical);

	public enum ProbabilityBranch
	{
		Generic,
		ElementalControl,
		StunShock,
		CounterBarrier,
		MrThreshold
	}

	public static int LevelDifference(int attackerLevel, int defenderLevel)
	{
		return attackerLevel - defenderLevel;
	}

	private static int GetInc(ICombatRandom random, int range, int increase)
	{
		return random.Roll(1, Math.Max(1, range)) + increase - 1;
	}

	public static int MagicBonus(int intelligence)
	{
		if (intelligence <= 5)
		{
			return -2;
		}
		if (intelligence <= 8)
		{
			return -1;
		}
		if (intelligence <= 11)
		{
			return 0;
		}
		if (intelligence <= 14)
		{
			return 1;
		}
		if (intelligence <= 17)
		{
			return 2;
		}
		if (intelligence <= 24)
		{
			return intelligence - 15;
		}
		if (intelligence <= 35)
		{
			return 10;
		}
		if (intelligence <= 42)
		{
			return 11;
		}
		if (intelligence <= 49)
		{
			return 12;
		}
		if (intelligence <= 50)
		{
			return 13;
		}
		return intelligence - 25;
	}

	public static int MagicLevel(int level)
	{
		return level / 4;
	}

	public static int MagicDiceDamage(ICombatRandom random, int diceCount, int dice, int value, int weaponMagicDamage, int intelligenceWithSp, double attributeResistance, bool canCritical, int magicCriticalBonus, int originalMagicDamage, int avatarBonus, double criticalMultiplier = 1.5)
	{
		return RollMagicDiceDamage(random, diceCount, dice, value, weaponMagicDamage, intelligenceWithSp, attributeResistance, canCritical, magicCriticalBonus, originalMagicDamage, avatarBonus, criticalMultiplier).Damage;
	}

	public static MagicDamageRoll RollMagicDiceDamage(ICombatRandom random, int diceCount, int dice, int value, int weaponMagicDamage, int intelligenceWithSp, double attributeResistance, bool canCritical, int magicCriticalBonus, int originalMagicDamage, int avatarBonus, double criticalMultiplier = 1.5)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = 0;
		for (int i = 0; i < diceCount; i++)
		{
			num += GetInc(random, dice, 1);
		}
		num += value;
		num += weaponMagicDamage;
		int num2 = intelligenceWithSp - 12;
		if (num2 < 1)
		{
			num2 = 1;
		}
		double num3 = 1.0 - attributeResistance + (double)num2 * 3.0 / 32.0;
		if (num3 < 0.0)
		{
			num3 = 0.0;
		}
		num = (int)((double)num * num3);
		int inc = GetInc(random, 100, 1);
		bool flag = canCritical && inc <= 10 + magicCriticalBonus;
		if (flag)
		{
			num = (int)((double)num * criticalMultiplier);
		}
		num += originalMagicDamage;
		num += avatarBonus;
		return new MagicDamageRoll(num, flag);
	}

	public static int Healing(ICombatRandom random, int dice, int value, int magicBonus, int lawful, int leverage)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		if (magicBonus > 10)
		{
			magicBonus = 10;
		}
		int num = value + magicBonus;
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			num2 += GetInc(random, dice, 1);
		}
		double num3 = 1.0;
		if (lawful > 0)
		{
			num3 += (double)lawful / 32768.0;
		}
		num2 = (int)((double)num2 * num3);
		return num2 * leverage / 10;
	}

	public static ProbabilityBranch BranchFor(int officialSkillId)
	{
		switch (officialSkillId)
		{
		case 133:
		case 145:
		case 152:
		case 153:
		case 157:
		case 161:
		case 167:
		case 173:
		case 174:
			return ProbabilityBranch.ElementalControl;
		case 87:
			return ProbabilityBranch.StunShock;
		case 91:
			return ProbabilityBranch.CounterBarrier;
		case 183:
		case 188:
		case 193:
			return ProbabilityBranch.MrThreshold;
		default:
			return ProbabilityBranch.Generic;
		}
	}

	public static bool ProbabilitySucceeds(ICombatRandom random, int probability)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		return Math.Min(90, probability) >= random.Roll(1, 100);
	}

	public static int Probability(ICombatRandom random, ProbabilityBranch branch, int probabilityDice, int probabilityValue, int attackerLevel, int defenderLevel, int magicBonus, int magicLevel, int targetMagicResistance, int originalMagicHit, int leverage, bool casterIsWizard, int statusResistance, int genericBonusDice = 0)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = LevelDifference(attackerLevel, defenderLevel);
		int num3;
		switch (branch)
		{
		case ProbabilityBranch.ElementalControl:
			num3 = (int)((double)probabilityDice / 10.0 * (double)num) + probabilityValue;
			num3 += 2 * originalMagicHit;
			break;
		case ProbabilityBranch.StunShock:
			num3 = probabilityValue + num * 2;
			num3 += 2 * originalMagicHit;
			break;
		case ProbabilityBranch.CounterBarrier:
			num3 = probabilityValue + num;
			num3 += 2 * originalMagicHit;
			break;
		case ProbabilityBranch.MrThreshold:
		{
			int num4 = magicBonus + magicLevel;
			if (num4 < 1)
			{
				num4 = 1;
			}
			num3 = 0;
			for (int j = 0; j < num4; j++)
			{
				num3 += GetInc(random, probabilityDice, probabilityValue);
			}
			num3 = num3 * leverage / 10;
			num3 += 2 * originalMagicHit;
			if (num3 < targetMagicResistance)
			{
				return 0;
			}
			return 100;
		}
		default:
		{
			int num2 = GenericProbabilityDiceCount(magicBonus, magicLevel, casterIsWizard, genericBonusDice);
			num3 = 0;
			for (int i = 0; i < num2; i++)
			{
				num3 += GetInc(random, probabilityDice, 1);
			}
			num3 = num3 * leverage / 10;
			num3 -= targetMagicResistance;
			break;
		}
		}
		return num3 - statusResistance;
	}

	public static int GenericProbabilityDiceCount(int magicBonus, int magicLevel, bool casterIsWizard, int bonusDice = 0)
	{
		int num = magicBonus + magicLevel + (casterIsWizard ? 1 : (-1));
		if (num < 1)
		{
			num = 1;
		}
		return num + Math.Max(0, bonusDice);
	}

	public static int NpcMagicResistanceDefense(ICombatRandom random, int damage, int magicResistance)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = random.Roll(1, 100);
		if (magicResistance < num)
		{
			return damage;
		}
		return damage / 2;
	}

	public static int MagicResistanceDefense(int damage, int magicResistance, int originalMagicHit)
	{
		double num2;
		if (magicResistance <= 100)
		{
			int num = (magicResistance - originalMagicHit) / 2;
			num2 = 1.0 - 0.01 * (double)num;
		}
		else
		{
			int num = (magicResistance - originalMagicHit) / 10;
			num2 = 0.6 - 0.01 * (double)num;
		}
		return (int)((double)damage * num2);
	}
}
