using System;
using IdleLineage.Core;

namespace IdleLineage.Combat;

public static class AlignmentRules
{
	public const double MainLawfulRate = 1.0;

	public static int DistributedMonsterLawful(int monsterLawful, long sourceHate, long totalPlayerHate)
	{
		if (sourceHate <= 0 || totalPlayerHate <= 0)
		{
			return 0;
		}
		return (int)Math.Truncate((double)monsterLawful * (double)sourceHate / (double)totalPlayerHate);
	}

	public static int MonsterKillDelta(int distributedLawful)
	{
		return (int)((double)distributedLawful * 1.0) * -1;
	}

	public static double Change(Combatant character, double amount)
	{
		ArgumentNullException.ThrowIfNull(character, "character");
		CombatantKind kind = character.Kind;
		if ((kind != CombatantKind.Player && kind != CombatantKind.Ally) || 1 == 0)
		{
			return 0.0;
		}
		double alignment = character.Alignment;
		character.Alignment = CombatCurveMath.ChangeAlignment(alignment, amount);
		return character.Alignment - alignment;
	}
}
