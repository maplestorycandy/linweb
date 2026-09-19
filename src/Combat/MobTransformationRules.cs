using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MobTransformationRules
{
	public const int MaximumChainLength = 16;

	public static bool TryBuildChain(IGameData data, string initialMobKey, out IReadOnlyList<MobTransformationTransition> chain, out string error)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(initialMobKey))
		{
			chain = Array.Empty<MobTransformationTransition>();
			error = "The initial mob key is required.";
			return false;
		}
		List<MobTransformationTransition> list = new List<MobTransformationTransition>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		string text = initialMobKey;
		for (int i = 0; i <= 16; i++)
		{
			if (!hashSet.Add(text))
			{
				chain = Array.Empty<MobTransformationTransition>();
				error = $"Mob transform chain '{initialMobKey}' contains a cycle at '{text}'.";
				return false;
			}
			JsonObject jsonObject = data.Mob(text);
			if (jsonObject == null)
			{
				chain = Array.Empty<MobTransformationTransition>();
				error = $"Mob transform chain '{initialMobKey}' references missing mob '{text}'.";
				return false;
			}
			string text2 = ReadString(jsonObject["transformTo"]);
			if (text2.Length == 0)
			{
				chain = list;
				error = string.Empty;
				return true;
			}
			if (list.Count >= 16)
			{
				chain = Array.Empty<MobTransformationTransition>();
				error = $"Mob transform chain '{initialMobKey}' exceeds {16} transitions.";
				return false;
			}
			if (data.Mob(text2) == null)
			{
				chain = Array.Empty<MobTransformationTransition>();
				error = $"Mob '{text}' transforms into missing mob '{text2}'.";
				return false;
			}
			list.Add(new MobTransformationTransition(text, text2, Math.Max(0, ReadInt(jsonObject["transformGfx"]))));
			text = text2;
		}
		chain = Array.Empty<MobTransformationTransition>();
		error = "Mob transform chain '" + initialMobKey + "' could not be resolved.";
		return false;
	}

	public static bool TryResolveNext(IGameData data, string mobKey, out MobTransformationTransition? transition)
	{
		if (TryBuildChain(data, mobKey, out IReadOnlyList<MobTransformationTransition> chain, out string _) && chain.Count > 0)
		{
			transition = chain[0];
			return true;
		}
		transition = null;
		return false;
	}

	public static string ResolveRootMobKey(IGameData data, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(mobKey))
		{
			return mobKey;
		}
		string text = mobKey;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal) { text };
		for (int i = 0; i <= 16; i++)
		{
			string text2 = null;
			foreach (var (text4, jsonNode2) in data.Mobs)
			{
				if (jsonNode2 is JsonObject jsonObject && string.Equals(ReadString(jsonObject["transformTo"]), text, StringComparison.Ordinal))
				{
					text2 = text4;
					break;
				}
			}
			if (text2 == null || !hashSet.Add(text2))
			{
				return text;
			}
			text = text2;
		}
		return text;
	}

	private static string ReadString(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return string.Empty;
		}
		return value?.Trim() ?? string.Empty;
	}

	private static int ReadInt(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
