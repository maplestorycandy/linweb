using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class DarkStealthRules
{
	public const string SkillId = "sk_dark_stealth";

	public const string CooldownBuff = "_dark_stealth_cooldown";

	public const double CooldownSeconds = 5.0;

	public static bool IsActive(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.Buffs.GetValueOrDefault("sk_dark_stealth") > 0.0;
	}

	public static bool CanCast(Combatant actor, string skillId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (string.Equals(skillId, "sk_dark_stealth", StringComparison.Ordinal))
		{
			return actor.Buffs.GetValueOrDefault("_dark_stealth_cooldown") <= 0.0;
		}
		return true;
	}
}
