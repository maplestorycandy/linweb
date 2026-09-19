using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MonsterCompanionRules
{
	public const string CardKeyPrefix = "card_";

	public const string CharacterKeyPrefix = "mon-comp:";

	public const string CompanionClassId = "monster";

	public static bool IsCompanionSave(string? classId)
	{
		return string.Equals(classId, "monster", StringComparison.Ordinal);
	}

	public static bool IsCompanion(Combatant? actor)
	{
		if (actor != null)
		{
			return IsCompanionSave(actor.ClassId);
		}
		return false;
	}

	public static bool TriggersForegroundOcclusionFade(Combatant? actor)
	{
		if (actor != null && actor.Kind == CombatantKind.Ally && actor.IsAlive)
		{
			return IsCompanion(actor);
		}
		return false;
	}

	public static bool IsRecruitable(string mobKey, JsonObject? mob)
	{
		if (mob == null)
		{
			return false;
		}
		if (CombatSkill.ReadInt(mob, "npcid") >= 80000)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(CombatSkill.ReadString(mob, "n")))
		{
			return false;
		}
		if (CombatSkill.ReadBool(mob, "noAttack"))
		{
			return false;
		}
		if (CombatSkill.ReadBool(mob, "noArt"))
		{
			return false;
		}
		return HasMoveSpeed(mob);
	}

	public static bool HasMoveSpeed(JsonObject mob)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		if (mob["moveSpd"] != null)
		{
			return CombatSkill.ReadDouble(mob, "moveSpd") > 0.0;
		}
		return true;
	}

	public static string CardKey(string mobKey)
	{
		return "card_" + mobKey;
	}

	public static IReadOnlyList<string> EligibleMobKeys(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		List<string> list = new List<string>();
		foreach (var (text2, jsonNode2) in data.Mobs)
		{
			if (jsonNode2 is JsonObject mob && IsRecruitable(text2, mob))
			{
				list.Add(text2);
			}
		}
		list.Sort(StringComparer.Ordinal);
		return list;
	}

	public static JsonObject ScaleDefinition(JsonObject mob, int targetLevel)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		JsonObject jsonObject = mob.DeepClone().AsObject();
		int num = Math.Max(1, CombatSkill.ReadInt(mob, "lv"));
		int num2 = Math.Clamp(targetLevel, 1, 99);
		int num3 = Math.Max(0, num2 - num);
		jsonObject["lv"] = JsonValue.Create((double)num2, (JsonNodeOptions?)null);
		if (num3 == 0)
		{
			return jsonObject;
		}
		double num4 = Math.Max(1.0, CombatSkill.ReadDouble(mob, "hp"));
		double num5 = 8.0 + 0.5 * num4 / (double)num;
		jsonObject["hp"] = JsonValue.Create(Math.Max(1.0, Math.Round(num4 + (double)num3 * num5)));
		double num6 = Math.Max(0.0, CombatSkill.ReadDouble(mob, "mp"));
		if (num6 > 0.0)
		{
			double num7 = Math.Max(1.0, 0.25 * num6 / (double)num);
			jsonObject["mp"] = JsonValue.Create(Math.Round(num6 + (double)num3 * num7));
		}
		double num8 = CombatSkill.ReadDouble(mob, "ac");
		double val = Math.Min(-100.0, num8);
		jsonObject["ac"] = JsonValue.Create(Math.Max(val, num8 - (double)num3));
		double num9 = Math.Max(0.0, CombatSkill.ReadDouble(mob, "mr"));
		jsonObject["mr"] = JsonValue.Create(Math.Min(100.0, Math.Round(num9 + (double)num3 * 0.75)));
		GrowHitField(jsonObject, mob, "hit", num3, required: true);
		GrowHitField(jsonObject, mob, "rangedHit", num3, required: false);
		if (mob["dmg"] is JsonArray { Count: >=2 } jsonArray && jsonArray[0] is JsonValue jsonValue && jsonValue.TryGetValue<decimal>(out var value) && jsonArray[1] is JsonValue jsonValue2 && jsonValue2.TryGetValue<decimal>(out var value2))
		{
			double a = (double)value;
			double num10 = (double)value2;
			double num11 = Math.Max(1.0, Math.Round(a));
			double value3 = Math.Max(num11, Math.Round(num10 + (double)num3));
			jsonObject["dmg"] = new JsonArray(JsonValue.Create(num11), JsonValue.Create(value3));
		}
		return jsonObject;
	}

	private static void GrowHitField(JsonObject scaled, JsonObject source, string field, int levelDelta, bool required)
	{
		if (required || source[field] != null)
		{
			scaled[field] = JsonValue.Create(CombatSkill.ReadDouble(source, field) + (double)levelDelta);
		}
	}

	public static Combatant Create(IGameData data, string mobKey, int level, string? characterKey = null, int bornSeq = 0)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mobKey, "mobKey");
		JsonObject mob = data.Mob(mobKey) ?? throw new KeyNotFoundException("Mob '" + mobKey + "' was not found.");
		if (!IsRecruitable(mobKey, mob))
		{
			throw new InvalidOperationException("Mob '" + mobKey + "' is not recruitable as a companion (noAttack/noArt/moveSpd=0 are excluded).");
		}
		Combatant combatant = CombatantBuilder.CreateMobFromDefinition(data, mobKey, ScaleDefinition(mob, level), characterKey ?? CharacterKey(mobKey, bornSeq), bornSeq, null, null, useDefinitionCombatStats: true);
		combatant.Kind = CombatantKind.Ally;
		combatant.ClassId = "monster";
		combatant.ExperienceReward = 0.0;
		combatant.GoldMin = 0;
		combatant.GoldMax = 0;
		combatant.DropMultiplier = 0.0;
		return combatant;
	}

	public static string CharacterKey(string mobKey, int bornSeq)
	{
		return $"{"mon-comp:"}{mobKey}#{Math.Max(0, bornSeq)}";
	}

	public static string CardCharacterKey(string mobKey)
	{
		return "mon-comp:" + mobKey;
	}

	public static bool IsCompanionKey(string? characterKey)
	{
		return characterKey?.StartsWith("mon-comp:", StringComparison.Ordinal) ?? false;
	}

	public static bool TryReadMobKey(string? characterKey, out string mobKey)
	{
		mobKey = "";
		if (!IsCompanionKey(characterKey))
		{
			return false;
		}
		string text = characterKey.Substring("mon-comp:".Length);
		int num = text.LastIndexOf('#');
		mobKey = ((num > 0) ? text.Substring(0, num) : text);
		return mobKey.Length > 0;
	}
}
