using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class BuffSkill
{
	public required string Id { get; init; }

	public required double DurationSeconds { get; init; }

	public required bool NoRefresh { get; init; }

	public required bool Haste { get; init; }

	public required bool RequiresShield { get; init; }

	public required bool RequiresMeleeWeapon { get; init; }

	public required bool RequiresBluntWeapon { get; init; }

	public static bool TryRead(string id, JsonObject source, out BuffSkill? skill)
	{
		if (!SkillBuffRules.IsExecutable(id, source))
		{
			skill = null;
			return false;
		}
		skill = new BuffSkill
		{
			Id = id,
			DurationSeconds = (AwakeningRules.IsAwakening(id) ? double.PositiveInfinity : Math.Max(1.0 / 60.0, L1jSkillHandover.L1jBuffDurationSeconds(source) ?? CombatSkill.ReadDouble(source, "dur", 1.0))),
			NoRefresh = CombatSkill.ReadBool(source, "noRefresh"),
			Haste = CombatSkill.ReadBool(source, "haste"),
			RequiresShield = CombatSkill.ReadBool(source, "reqShield"),
			RequiresMeleeWeapon = CombatSkill.ReadBool(source, "reqWpnMelee"),
			RequiresBluntWeapon = CombatSkill.ReadBool(source, "reqWpnBlunt")
		};
		return true;
	}
}
