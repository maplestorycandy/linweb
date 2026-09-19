using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class StatusEffectSpec
{
	public required string Kind { get; init; }

	public required int DurationTicks { get; init; }

	public required int TickEvery { get; init; }

	public required double? FixedChancePercentage { get; init; }

	public required bool Force { get; init; }

	public required double HitOffset { get; init; }

	public required double Potency { get; init; }

	public DiceTerm? DamageDice { get; init; }

	public static StatusEffectSpec? TryRead(JsonObject? source)
	{
		if (source == null)
		{
			return null;
		}
		string text = CombatSkill.ReadString(source, "kind");
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		DiceTerm? damageDice = null;
		if (source["dmg"] is JsonArray { Count: >=2 } jsonArray && CombatSkill.TryReadDouble(jsonArray[0], out var value) && CombatSkill.TryReadDouble(jsonArray[1], out var value2) && value > 0.0 && value2 > 0.0)
		{
			damageDice = new DiceTerm((int)Math.Floor(value), (int)Math.Floor(value2));
		}
		int durationTicks = Math.Max(1, CombatSkill.ReadInt(source, "dur") * 10);
		int tickEvery = Math.Max(1, CombatSkill.ReadInt(source, "tick") * 10);
		double value3;
		return new StatusEffectSpec
		{
			Kind = text,
			DurationTicks = durationTicks,
			TickEvery = tickEvery,
			FixedChancePercentage = (CombatSkill.TryReadDouble(source["pct"], out value3) ? new double?(Math.Clamp(value3, 0.0, 100.0)) : ((double?)null)),
			Force = CombatSkill.ReadBool(source, "force"),
			HitOffset = CombatSkill.ReadDouble(source, "hitOff"),
			Potency = Math.Max(0.0, CombatSkill.ReadDouble(source, "hit")),
			DamageDice = damageDice
		};
	}

	public static StatusEffectSpec? TryReadFixed(JsonObject? source)
	{
		if (source == null)
		{
			return null;
		}
		string text = CombatSkill.ReadString(source, "kind");
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		int num = CombatSkill.ReadInt(source, "dur");
		return new StatusEffectSpec
		{
			Kind = text,
			DurationTicks = Math.Max(1, ((num > 0) ? num : 16) * 10),
			TickEvery = 1,
			FixedChancePercentage = Math.Clamp(CombatSkill.ReadDouble(source, "chance") * 100.0, 0.0, 100.0),
			Force = true,
			HitOffset = 0.0,
			Potency = 0.0
		};
	}
}
