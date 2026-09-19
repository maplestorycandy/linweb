using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class GodotBossClassification
{
	public const string TableName = "GODOT_BOSS_CLASSIFICATION";

	private static readonly ConditionalWeakTable<IGameData, HashSet<string>> Cache = new ConditionalWeakTable<IGameData, HashSet<string>>();

	public static IReadOnlyCollection<string> Keys(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Load);
	}

	public static bool IsBoss(IGameData data, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!string.IsNullOrWhiteSpace(mobKey) && Keys(data).Contains(mobKey))
		{
			return data.Mob(mobKey) != null;
		}
		return false;
	}

	private static HashSet<string> Load(IGameData data)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (data.Table("GODOT_BOSS_CLASSIFICATION") is not JsonArray obj)
		{
			return hashSet;
		}
		foreach (JsonNode item in obj)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
			{
				if (data.Mob(value) != null)
				{
					hashSet.Add(value);
				}
			}
		}
		return hashSet;
	}
}
