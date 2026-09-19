using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class SkillPresentationRules
{
	public static bool IsProcOnly(IGameData data, string skillId)
	{
		return ReadBool(data, skillId, "procOnly");
	}

	public static bool IsCleanse(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		if (data.Skill(skillId)?["cleanse"] is JsonArray jsonArray)
		{
			return jsonArray.Count > 0;
		}
		return false;
	}

	public static bool UsesHealingSlot(IGameData data, string skillId)
	{
		if (!IsCleanse(data, skillId))
		{
			if (!string.Equals(ReadString(data, skillId, "type"), "heal", StringComparison.Ordinal))
			{
				return ReadBool(data, skillId, "healSlot");
			}
			return true;
		}
		return false;
	}

	private static bool ReadBool(IGameData data, string skillId, string field)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		bool value = default(bool);
		return data.Skill(skillId)?[field] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static string ReadString(IGameData data, string skillId, string field)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return "";
		}
		if (!(data.Skill(skillId)?[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}
}
