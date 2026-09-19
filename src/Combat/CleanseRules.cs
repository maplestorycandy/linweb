using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CleanseRules
{
	public const double RadiusCells = 15.0;

	private const string CleanseField = "cleanse";

	public static double Radius => 720.0;

	public static IReadOnlyList<string> CurableStatuses(IGameData? data, string skillId)
	{
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject == null)
		{
			return Array.Empty<string>();
		}
		return CurableStatuses(jsonObject);
	}

	public static IReadOnlyList<string> CurableStatuses(JsonObject? source)
	{
		switch ((source?["l1j"] is JsonObject source2) ? CombatSkill.ReadInt(source2, "officialId") : 0)
		{
		case 9:
			return new string[1] { "poison" };
		case 37:
			return new string[2] { "poison", "paralyze" };
		case 44:
			return Array.Empty<string>();
		default:
		{
			if (!(source?["cleanse"] is JsonArray { Count: not 0 } jsonArray))
			{
				return Array.Empty<string>();
			}
			List<string> list = new List<string>(jsonArray.Count);
			{
				foreach (JsonNode item in jsonArray)
				{
					if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
					{
						string text = StatusRules.NormalizeKind(value);
						if (!list.Contains<string>(text, StringComparer.Ordinal))
						{
							list.Add(text);
						}
					}
				}
				return list;
			}
		}
		}
	}

	public static bool IsCleanseSkill(JsonObject? source)
	{
		return CurableStatuses(source).Count > 0;
	}

	public static bool IsCleanseSkill(IGameData? data, string skillId)
	{
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject != null)
		{
			return IsCleanseSkill(jsonObject);
		}
		return false;
	}

	public static bool HasCurableStatus(Combatant actor, IReadOnlyList<string> curable)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(curable, "curable");
		if (actor.Statuses.Count == 0 || curable.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < curable.Count; i++)
		{
			if (string.Equals(curable[i], "poison", StringComparison.Ordinal) ? L1jPoisonAttackRules.IsPoisoned(actor) : actor.HasStatus(curable[i]))
			{
				return true;
			}
		}
		return false;
	}
}
