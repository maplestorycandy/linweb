using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jMobTeleportRules
{
	public const double MpCost = 10.0;

	public const int LandingAttempts = 2;

	public const int OffsetRadiusCells = 3;

	public const double InitialMinimumDistanceCellsExclusive = 3.0;

	public const double InitialMaximumDistanceCellsExclusive = 15.0;

	public const double RepeatMinimumDistanceCellsExclusive = 6.0;

	public const double RepeatMaximumDistanceCellsExclusive = 20.0;

	public const double RepeatChance = 0.19;

	public static bool Enabled(IGameData? data, Combatant mob)
	{
		JsonObject jsonObject = data?.Mob(mob.Avatar);
		if (jsonObject != null)
		{
			return CombatSkill.ReadBool(jsonObject, "teleport");
		}
		return false;
	}

	public static bool InInitialDistance(double cells)
	{
		if (cells > 3.0)
		{
			return cells < 15.0;
		}
		return false;
	}

	public static bool InRepeatDistance(double cells)
	{
		if (cells > 6.0)
		{
			return cells < 20.0;
		}
		return false;
	}
}
