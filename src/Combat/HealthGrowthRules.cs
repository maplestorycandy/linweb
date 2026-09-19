using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace IdleLineage.Combat;

public static class HealthGrowthRules
{
	public static int LevelRoll(string identity, string? classId, int level, double constitution)
	{
		if (level <= 1)
		{
			return 0;
		}
		return ClassGrowthRules.LevelUpHp(classId, constitution, CommittedRoll(identity, classId, level));
	}

	public static double RolledHealth(string identity, string? classId, int level, double constitution)
	{
		double num = 0.0;
		for (int i = 2; i <= level; i++)
		{
			num += (double)LevelRoll(identity, classId, i, constitution);
		}
		return num;
	}

	private static double CommittedRoll(string identity, string? classId, int level)
	{
		return (double)BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"IdleLineage.LevelHealth.l1j.v2|{identity}|{ClassKitRegistry.NormalizeClassId(classId)}|{level}"))) / 1.8446744073709552E+19;
	}
}
