using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace IdleLineage.Combat;

public static class ManaGrowthRules
{
	public static int LevelRoll(string identity, string? classId, int level, double wisdom)
	{
		if (level <= 1)
		{
			return 0;
		}
		return ClassGrowthRules.LevelUpMp(classId, wisdom, CommittedRoll(identity, classId, level));
	}

	public static double RolledMana(string identity, string? classId, int level, double wisdom)
	{
		double num = 0.0;
		for (int i = 2; i <= level; i++)
		{
			num += (double)LevelRoll(identity, classId, i, wisdom);
		}
		if (!string.Equals(ClassKitRegistry.NormalizeClassId(classId), "warrior", StringComparison.Ordinal))
		{
			return num;
		}
		return Math.Floor(num * 0.95);
	}

	private static double CommittedRoll(string identity, string? classId, int level)
	{
		return (double)BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"IdleLineage.LevelMana.l1j.v2|{identity}|{ClassKitRegistry.NormalizeClassId(classId)}|{level}"))) / 1.8446744073709552E+19;
	}
}
