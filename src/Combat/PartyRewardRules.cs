using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class PartyRewardRules
{
	public const int MaximumPartySize = 8;

	public static IReadOnlyList<Combatant> ExperienceRecipients(IEnumerable<Combatant> combatants)
	{
		ArgumentNullException.ThrowIfNull(combatants, "combatants");
		return (from candidate in combatants.Where(delegate(Combatant candidate)
			{
				bool flag = candidate.IsAlive;
				if (flag)
				{
					CombatantKind kind = candidate.Kind;
					bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
					flag = flag2;
				}
				return flag;
			})
			orderby (candidate.Kind != CombatantKind.Player) ? 1 : 0, candidate.BornSeq
			select candidate).Take(8).ToArray();
	}

	public static int ActiveMemberCount(IEnumerable<Combatant> combatants)
	{
		return Math.Max(1, ExperienceRecipients(combatants).Count);
	}

	public static double ExperienceBonusPercent(IEnumerable<Combatant> combatants)
	{
		IReadOnlyList<Combatant> readOnlyList = ExperienceRecipients(combatants);
		if (readOnlyList.Count == 0)
		{
			return 0.0;
		}
		double num = 4.0 * (double)Math.Max(0, readOnlyList.Count - 1);
		double num2 = ((readOnlyList[0].ClassId == "royal") ? 5.9 : 0.0);
		return num + num2;
	}

	public static double ExperienceMultiplier(IEnumerable<Combatant> combatants)
	{
		return 1.0 + ExperienceBonusPercent(combatants) / 100.0;
	}

	public static double ScaleExperience(double baseExperience, IEnumerable<Combatant> combatants)
	{
		if (!double.IsFinite(baseExperience) || baseExperience <= 0.0)
		{
			return 0.0;
		}
		return Math.Floor(baseExperience * ExperienceMultiplier(combatants));
	}

	public static long ScaleGold(long baseGold, int activeMemberCount)
	{
		return Math.Max(0L, baseGold);
	}

	public static double ScaleDropChance(double baseChance, int activeMemberCount)
	{
		return Math.Clamp(Math.Max(0.0, baseChance), 0.0, 1.0);
	}
}
