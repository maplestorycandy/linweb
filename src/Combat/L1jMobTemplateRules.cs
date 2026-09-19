using System;
using System.IO;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class L1jMobTemplateRules
{
	private static readonly int[] StrengthHit = new int[61]
	{
		-2, -2, -2, -2, -2, -2, -2, -2, -2, -1,
		-1, 0, 0, 1, 1, 2, 2, 3, 3, 4,
		4, 5, 5, 5, 6, 6, 6, 7, 7, 7,
		8, 8, 8, 9, 9, 9, 10, 10, 10, 11,
		11, 11, 12, 12, 12, 13, 13, 13, 14, 14,
		14, 15, 15, 15, 16, 16, 16, 17, 17, 17,
		18
	};

	private static readonly int[] DexterityHit = new int[61]
	{
		-2, -2, -2, -2, -2, -2, -2, -1, -1, 0,
		0, 1, 1, 2, 2, 3, 3, 4, 4, 5,
		6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
		16, 17, 18, 19, 19, 19, 20, 20, 20, 21,
		21, 21, 22, 22, 22, 23, 23, 23, 24, 24,
		24, 25, 25, 25, 26, 26, 26, 27, 27, 27,
		28
	};

	public static L1jMobInstanceStats Resolve(JsonObject definition, ICombatRandom? random = null)
	{
		ArgumentNullException.ThrowIfNull(definition, "definition");
		int num = Math.Max(1, ReadInt(definition, "lv", 1));
		int num2 = Math.Max(1, ReadInt(definition, "hp", 1));
		int num3 = Math.Max(0, ReadInt(definition, "mp"));
		int num4 = ReadInt(definition, "ac");
		int num5 = ReadInt(definition, "str");
		int num6 = ReadInt(definition, "con");
		int num7 = ReadInt(definition, "dex");
		int num8 = ReadInt(definition, "int");
		int num9 = ReadInt(definition, "wis");
		int num10 = ReadInt(definition, "mr");
		int num11 = Math.Max(0, ReadInt(definition, "exp"));
		int num12 = ReadInt(definition, "lawful");
		int num13 = ReadInt(definition, "randomLevel");
		int num14 = num;
		int val = num2;
		int val2 = num3;
		int armorClass = num4;
		int strength = num5;
		int constitution = num6;
		int dexterity = num7;
		int intelligence = num8;
		int wisdom = num9;
		int magicResistance = num10;
		int val3 = num11;
		int lawful = num12;
		int hitBonus = 0;
		int damageBonus = 0;
		if (random != null && num13 != 0)
		{
			int num15 = num13 - num;
			if (num15 == 0)
			{
				throw new InvalidDataException("L1J mob '" + ReadString(definition, "n") + "' has randomLevel equal to its base level.");
			}
			int num16 = (int)(Math.Clamp(random.NextDouble(), 0.0, Math.BitDecrement(1.0)) * (double)(num15 + 1));
			double rate = (double)num16 / (double)num15;
			num14 = num + num16;
			val = Interpolate(num2, ReadInt(definition, "randomHp"), rate);
			val2 = Interpolate(num3, ReadInt(definition, "randomMp"), rate);
			armorClass = Interpolate(num4, ReadInt(definition, "randomAc"), rate);
			lawful = Interpolate(num12, ReadInt(definition, "randomLawful"), rate);
			strength = Math.Min(num5 + num15, 127);
			constitution = Math.Min(num6 + num15, 127);
			dexterity = Math.Min(num7 + num15, 127);
			intelligence = Math.Min(num8 + num15, 127);
			wisdom = Math.Min(num9 + num15, 127);
			magicResistance = Math.Min(num10 + num15, 127);
			hitBonus = num15 * 2;
			damageBonus = num15 * 2;
			if (ReadInt(definition, "randomExp") != 0)
			{
				val3 = checked(num14 * num14 + 1);
			}
		}
		return new L1jMobInstanceStats(num14, Math.Max(1, val), Math.Max(0, val2), armorClass, strength, constitution, dexterity, intelligence, wisdom, magicResistance, Math.Max(0, val3), lawful, hitBonus, damageBonus);
	}

	public static int PhysicalHit(L1jMobInstanceStats stats)
	{
		return stats.Level + StrengthHit[Math.Clamp(stats.Strength, 0, 60)] + DexterityHit[Math.Clamp(stats.Dexterity, 0, 60)] + stats.HitBonus;
	}

	public static int PhysicalDamageBonus(L1jMobInstanceStats stats)
	{
		return stats.Strength / 2 + stats.DamageBonus;
	}

	private static int Interpolate(int baseline, int randomMaximum, double rate)
	{
		if (randomMaximum != 0)
		{
			return (int)((double)baseline + rate * (double)(randomMaximum - baseline));
		}
		return baseline;
	}

	private static int ReadInt(JsonObject source, string field, int fallback = 0)
	{
		if (source[field] != null)
		{
			return (int)CombatSkill.ReadDouble(source, field);
		}
		return fallback;
	}

	private static string ReadString(JsonObject source, string field)
	{
		return CombatSkill.ReadString(source, field);
	}
}
