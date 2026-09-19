using System;

namespace IdleLineage.Combat;

public static class SolidBodyRules
{
	private static bool IsPlayerSide(CombatantKind kind)
	{
		if (kind == CombatantKind.Player || (uint)(kind - 2) <= 2u)
		{
			return true;
		}
		return false;
	}

	public static bool IsSolidPair(Combatant a, Combatant b)
	{
		ArgumentNullException.ThrowIfNull(a, "a");
		ArgumentNullException.ThrowIfNull(b, "b");
		if (a.Kind == CombatantKind.Mob)
		{
			if (!IsPlayerSide(b.Kind))
			{
				return b.Kind == CombatantKind.Mob;
			}
			return true;
		}
		if (b.Kind == CombatantKind.Mob)
		{
			return IsPlayerSide(a.Kind);
		}
		if (a.Kind != CombatantKind.Player || !IsPlayerSide(b.Kind))
		{
			if (b.Kind == CombatantKind.Player)
			{
				return IsPlayerSide(a.Kind);
			}
			return false;
		}
		return true;
	}

	public static bool StepBlocked(Combatant mover, WorldPoint candidate, Combatant blocker, WorldPoint latticeOrigin = default(WorldPoint))
	{
		ArgumentNullException.ThrowIfNull(mover, "mover");
		ArgumentNullException.ThrowIfNull(blocker, "blocker");
		return StepBlockedByPoint(candidate, blocker.Pos, latticeOrigin);
	}

	public static bool StepBlockedByPoint(WorldPoint candidate, WorldPoint blocker, WorldPoint latticeOrigin = default(WorldPoint))
	{
		return IsometricMovementRules.GridPointAt(candidate, latticeOrigin) == IsometricMovementRules.GridPointAt(blocker, latticeOrigin);
	}
}
