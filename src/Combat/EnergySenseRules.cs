using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class EnergySenseRules
{
	public const string SkillId = "sk_energy_sense";

	public static bool IsEnergySenseSkill(JsonObject source)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		if (string.Equals(CombatSkill.ReadString(source, "type"), "manual", StringComparison.Ordinal))
		{
			return string.Equals(CombatSkill.ReadString(source, "mEff"), "sense", StringComparison.Ordinal);
		}
		return false;
	}

	public static string ElementLabel(string? element)
	{
		return CombatSkill.NormalizeElement(element ?? string.Empty) switch
		{
			"fire" => "火", 
			"water" => "水", 
			"wind" => "風", 
			"earth" => "地", 
			_ => "無", 
		};
	}
}
