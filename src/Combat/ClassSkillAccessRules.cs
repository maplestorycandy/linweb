using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class ClassSkillAccessRules
{
	public const string TeleportSkillId = "sk_teleport";

	public const string SunlightSkillId = "sk_sunlight";

	private static readonly IReadOnlySet<string> RestrictedClasses = new HashSet<string>(StringComparer.Ordinal) { "illusion", "dragon" };

	private static readonly IReadOnlySet<string> RestrictedSkills = new HashSet<string>(StringComparer.Ordinal) { "sk_teleport", "sk_sunlight" };

	public static bool IsRestricted(string? classId, string skillId)
	{
		if (RestrictedSkills.Contains(skillId))
		{
			return RestrictedClasses.Contains(ClassKitRegistry.NormalizeClassId(classId));
		}
		return false;
	}

	public static bool Allows(Combatant actor, string skillId)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return !IsRestricted(actor.ClassId, skillId);
	}
}
