using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class AbsoluteBarrierRules
{
	public const string SkillId = "sk_abs_barrier";

	public const string CooldownKey = "_barrier_cd:sk_abs_barrier";

	public static bool IsBarrierSkill(JsonObject source)
	{
		if (string.Equals(CombatSkill.ReadString(source, "type"), "manual", StringComparison.Ordinal))
		{
			return string.Equals(CombatSkill.ReadString(source, "mEff"), "barrier", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool IsActive(Combatant combatant)
	{
		return combatant.Buffs.GetValueOrDefault("sk_abs_barrier") > 0.0;
	}

	public static double CooldownSeconds(double durationSeconds)
	{
		return durationSeconds + 12.0;
	}
}
