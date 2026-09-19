using System;

namespace IdleLineage.Combat;

public static class MercenaryRules
{
	public const int PartySizeIncludingLeader = 8;

	public const int ActiveMemberSlots = 7;

	public const double FollowDistance = 96.0;

	public const double CombatLeashDistance = 520.0;

	public const double WarpDistance = 900.0;

	public const double ReviveHealthRatio = 0.5;

	public const string ReviveScrollItemKey = "scroll_revive";

	public static int ActiveCapacity(Combatant leader)
	{
		ArgumentNullException.ThrowIfNull(leader, "leader");
		return 7;
	}

	public static WorldPoint FormationPoint(Combatant leader, int allyIndex, int allyCount)
	{
		ArgumentNullException.ThrowIfNull(leader, "leader");
		int num = Math.Max(1, allyCount);
		int num2 = Math.Clamp(allyIndex, 0, num - 1);
		double num3 = Math.PI / 2.0 + Math.PI * 2.0 * (double)num2 / (double)num;
		double num4 = ((num <= 3) ? 72 : 88);
		return new WorldPoint(leader.Pos.X + Math.Cos(num3) * num4, leader.Pos.Y + Math.Sin(num3) * num4);
	}
}
