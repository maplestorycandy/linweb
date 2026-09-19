using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class InstantKillSpec
{
	public required double? Chance { get; init; }

	public required int MaxHitValue { get; init; }

	public required string RequiredTag { get; init; }

	public static InstantKillSpec? TryRead(JsonObject? source)
	{
		if (source == null)
		{
			return null;
		}
		double value;
		double? chance = (CombatSkill.TryReadDouble(source["p"], out value) ? new double?(Math.Clamp(value, 0.0, 1.0)) : ((double?)null));
		int num = CombatSkill.ReadInt(source, "cap");
		return new InstantKillSpec
		{
			Chance = chance,
			MaxHitValue = Math.Clamp((num > 0) ? num : 20, 1, 20),
			RequiredTag = CombatSkill.ReadString(source, "tag")
		};
	}
}
