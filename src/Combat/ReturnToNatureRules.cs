using System;

namespace IdleLineage.Combat;

internal static class ReturnToNatureRules
{
	public const string SkillId = "sk_elf_release";

	public const int OfficialSkillId = 145;

	public static bool IsSkill(string skillId)
	{
		return string.Equals(skillId, "sk_elf_release", StringComparison.Ordinal);
	}
}
