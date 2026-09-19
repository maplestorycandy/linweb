using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class AwakeningRules
{
	public const string Antharas = "sk_dragon_awaken_antares";

	public const string Fafurion = "sk_dragon_awaken_falion";

	public const string Valakas = "sk_dragon_awaken_baraka";

	public static readonly IReadOnlyList<string> BuffIds = new string[3] { "sk_dragon_awaken_antares", "sk_dragon_awaken_falion", "sk_dragon_awaken_baraka" };

	private static readonly IReadOnlyDictionary<string, JsonObject> ModifierSets = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
	{
		["sk_dragon_awaken_antares"] = JsonNode.Parse("{ \"ac\": 12 }").AsObject(),
		["sk_dragon_awaken_falion"] = JsonNode.Parse("{ \"mr\": 30, \"resFire\": 30, \"resWater\": 30, \"resEarth\": 30, \"resWind\": 30 }").AsObject(),
		["sk_dragon_awaken_baraka"] = JsonNode.Parse("{ \"str\": 5, \"dex\": 5, \"con\": 5, \"int\": 5, \"wis\": 5, \"cha\": 5 }").AsObject()
	};

	public static bool IsAwakening(string buffId)
	{
		return BuffIds.Contains<string>(buffId, StringComparer.Ordinal);
	}

	public static bool IsActive(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return BuffIds.Any((string buffId) => actor.Buffs.GetValueOrDefault(buffId) > 0.0);
	}

	public static void ApplyResourceBonuses(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Buffs.GetValueOrDefault("sk_dragon_awaken_antares") > 0.0)
		{
			actor.MaxHp += 127.0;
		}
	}

	public static JsonObject? Modifiers(string buffId)
	{
		return ModifierSets.GetValueOrDefault(buffId);
	}
}
