using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class ClassGrowthRules
{
	public sealed record ClassGrowthProfile(int Str, int Dex, int Con, int Int, int Wis, int Cha, int FreePoints, int InitHp, int InitMr, int HpLevelConstant, int[] InitMpByWis, int[] RandomMpByWis, int MpNumerator, int MpDenominator, int MaxHpCap, int MaxMpCap);

	public const int HpRandomSpan = 5;

	public const double WarriorManaFactor = 0.95;

	private static readonly int[] BaseMpByWis = new int[36]
	{
		0, 0, 0, 0, 0, 0, 0, 1, 1, 1,
		2, 2, 2, 2, 2, 3, 3, 3, 3, 3,
		3, 4, 4, 4, 4, 5, 5, 5, 5, 6,
		6, 6, 6, 7, 7, 7
	};

	private static readonly int[] RandomMpVariantA = new int[36]
	{
		2, 2, 2, 2, 2, 2, 2, 2, 2, 3,
		2, 2, 3, 3, 3, 3, 3, 3, 4, 4,
		4, 4, 4, 4, 5, 4, 4, 5, 5, 4,
		4, 5, 5, 4, 4, 5
	};

	private static readonly int[] RandomMpVariantB = new int[36]
	{
		2, 2, 2, 2, 2, 2, 2, 2, 2, 3,
		2, 2, 2, 3, 3, 3, 3, 3, 4, 4,
		4, 4, 4, 4, 5, 4, 4, 5, 5, 4,
		4, 5, 5, 4, 4, 5
	};

	private static readonly int[] RoyalInitMp = new int[17]
	{
		2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
		2, 2, 2, 2, 2, 2, 4
	};

	private static readonly int[] KnightInitMp = new int[17]
	{
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 2, 2, 2, 2, 2
	};

	private static readonly int[] ElfInitMp = new int[17]
	{
		4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
		4, 4, 4, 4, 4, 4, 6
	};

	private static readonly int[] WizardInitMp = new int[17]
	{
		6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
		6, 6, 6, 6, 6, 6, 8
	};

	private static readonly int[] DarkElfInitMp = new int[17]
	{
		3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
		3, 3, 4, 4, 4, 4, 5
	};

	private static readonly IReadOnlyDictionary<string, ClassGrowthProfile> Profiles = new Dictionary<string, ClassGrowthProfile>(StringComparer.Ordinal)
	{
		["royal"] = new ClassGrowthProfile(13, 10, 10, 10, 11, 13, 8, 14, 10, 3, RoyalInitMp, RandomMpVariantA, 1, 1, 1400, 800),
		["knight"] = new ClassGrowthProfile(16, 12, 14, 8, 9, 12, 4, 16, 0, 6, KnightInitMp, RandomMpVariantA, 2, 3, 2200, 600),
		["elf"] = new ClassGrowthProfile(11, 12, 12, 12, 12, 9, 7, 15, 25, 2, ElfInitMp, RandomMpVariantB, 3, 2, 1600, 900),
		["mage"] = new ClassGrowthProfile(8, 7, 12, 12, 12, 8, 16, 12, 15, 1, WizardInitMp, RandomMpVariantB, 2, 1, 1300, 1300),
		["dark"] = new ClassGrowthProfile(12, 15, 8, 11, 10, 9, 10, 12, 10, 2, DarkElfInitMp, RandomMpVariantA, 3, 2, 1600, 900),
		["dragon"] = new ClassGrowthProfile(13, 11, 14, 11, 12, 8, 6, 16, 18, 5, RoyalInitMp, RandomMpVariantA, 2, 3, 2000, 600),
		["illusion"] = new ClassGrowthProfile(11, 10, 12, 12, 12, 8, 10, 15, 20, 1, ElfInitMp, RandomMpVariantB, 5, 3, 1200, 1200),
		["warrior"] = new ClassGrowthProfile(16, 12, 14, 8, 9, 12, 4, 17, 0, 7, KnightInitMp, RandomMpVariantA, 2, 3, 2310, 570)
	};

	private const int KnightMaxHpCap = 2200;

	private const int KnightMaxMpCap = 600;

	private static readonly int[] Dex2AcDivisor = new int[19]
	{
		8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
		7, 7, 7, 6, 6, 6, 5, 5, 4
	};

	private static readonly int[] StatMrByWis = new int[26]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 3, 3, 6, 10, 15,
		21, 28, 37, 47, 50, 50
	};

	public static IReadOnlyCollection<string> ClassIds => Profiles.Keys.ToArray();

	public static ClassGrowthProfile Profile(string? classId)
	{
		if (!Profiles.TryGetValue(ClassKitRegistry.NormalizeClassId(classId), out ClassGrowthProfile value))
		{
			return Profiles["mage"];
		}
		return value;
	}

	public static bool IsKnownClass(string? classId)
	{
		return Profiles.ContainsKey(ClassKitRegistry.NormalizeClassId(classId));
	}

	public static Attributes BaseAttributes(string? classId)
	{
		ClassGrowthProfile classGrowthProfile = Profile(classId);
		return new Attributes
		{
			Str = classGrowthProfile.Str,
			Dex = classGrowthProfile.Dex,
			Con = classGrowthProfile.Con,
			Int = classGrowthProfile.Int,
			Wis = classGrowthProfile.Wis,
			Cha = classGrowthProfile.Cha
		};
	}

	public static int InitialMana(string? classId, double wisdom)
	{
		int[] initMpByWis = Profile(classId).InitMpByWis;
		int num = Math.Clamp((int)Math.Floor(wisdom), 0, 16);
		return initMpByWis[num];
	}

	public static int BaseManaTerm(double wisdom)
	{
		return BaseMpByWis[Math.Clamp((int)Math.Floor(wisdom), 0, 35)];
	}

	public static int LevelHit(string? classId, int level)
	{
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "royal":
		case "elf":
		case "illusion":
			return level / 5;
		case "knight":
		case "dark":
			return level / 3;
		case "warrior":
			return level / 3 + level / 50;
		case "dragon":
			return level / 4;
		default:
			return 0;
		}
	}

	public static int LevelMeleeDamage(string? classId, int level)
	{
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "knight":
		case "elf":
		case "dark":
		case "dragon":
		case "illusion":
			return level / 10;
		case "warrior":
			return level / 9;
		default:
			return 0;
		}
	}

	public static int LevelRangedDamage(string? classId, int level)
	{
		if (!(ClassKitRegistry.NormalizeClassId(classId) == "elf"))
		{
			return 0;
		}
		return level / 10;
	}

	public static int LevelEvasion(string? classId, int level)
	{
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "royal":
			return level / 8;
		case "knight":
		case "dark":
			return level / 4;
		case "elf":
			return level / 6;
		case "mage":
			return level / 10;
		case "dragon":
			return level / 7;
		case "illusion":
			return level / 9;
		case "warrior":
			return level / 4 + level / 50;
		default:
			return 0;
		}
	}

	public static int MagicLevel(string? classId, int level)
	{
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "royal":
			return Math.Min(2, level / 10);
		case "knight":
		case "warrior":
			return level / 50;
		case "elf":
			return Math.Min(6, level / 8);
		case "mage":
			return Math.Min(10, level / 4);
		case "dark":
			return Math.Min(2, level / 12);
		case "dragon":
			return Math.Min(6, level / 9);
		case "illusion":
			return Math.Min(10, level / 6);
		default:
			return level / 4;
		}
	}

	public static int BaseArmorClass(string? classId, int level, double baseDex)
	{
		int num = Math.Clamp((int)Math.Floor(baseDex), 0, 18);
		int num2 = 10 - level / Dex2AcDivisor[num];
		if (!string.Equals(ClassKitRegistry.NormalizeClassId(classId), "warrior", StringComparison.Ordinal))
		{
			return num2;
		}
		return num2 + level / 50;
	}

	public static int StatMr(double wisdom)
	{
		return StatMrByWis[Math.Clamp((int)Math.Floor(wisdom), 0, 25)];
	}

	public static int BaseMagicResist(string? classId, int level, double wisdom)
	{
		int num = Profile(classId).InitMr + StatMr(wisdom) + level / 2;
		if (string.Equals(ClassKitRegistry.NormalizeClassId(classId), "warrior", StringComparison.Ordinal))
		{
			num = Math.Max(0, num - level / 25);
		}
		return num;
	}

	public static int AcDefenseMaximum(string? classId, double armorClass)
	{
		int num = Math.Max(0, 10 - (int)Math.Floor(armorClass));
		switch (ClassKitRegistry.NormalizeClassId(classId))
		{
		case "dragon":
		case "royal":
		case "elf":
			return num / 3;
		case "knight":
			return num / 2;
		case "mage":
			return num / 5;
		case "dark":
		case "illusion":
			return num / 4;
		case "warrior":
			return Math.Max(0, num / 2 - 1);
		default:
			return num / 5;
		}
	}

	public static int LevelUpHp(string? classId, double constitution, double roll)
	{
		int num = Math.Max(0, (int)Math.Floor(constitution));
		int num2 = (int)Math.Floor(Math.Clamp(roll, 0.0, 0.999999999) * 5.0) - 2;
		return num * 5 / 6 + num2 + Profile(classId).HpLevelConstant;
	}

	public static int LevelUpMp(string? classId, double wisdom, double roll)
	{
		ClassGrowthProfile classGrowthProfile = Profile(classId);
		int num = Math.Clamp((int)Math.Floor(wisdom), 0, 35);
		return ((int)Math.Floor(Math.Clamp(roll, 0.0, 0.999999999) * (double)classGrowthProfile.RandomMpByWis[num]) + BaseMpByWis[num]) * classGrowthProfile.MpNumerator / classGrowthProfile.MpDenominator;
	}
}
