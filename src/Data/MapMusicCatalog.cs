using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class MapMusicCatalog
{
	private const string TableName = "CLIENT_AUDIO_3_8C";

	public static string LoginTrack(IGameData data)
	{
		return SystemTrack(data, "login") ?? "music82";
	}

	public static string? ResolveZone(IGameData data, string mapKey, int gameX, int gameY, long? unixSeconds = null)
	{
		foreach (JsonObject item in Rules(data, mapKey))
		{
			if (item["bounds"] is JsonArray && Contains(item, gameX, gameY))
			{
				string text = SelectTrack(item, "music", unixSeconds);
				if (text != null)
				{
					return text;
				}
			}
		}
		return null;
	}

	public static string? ResolveMapTrack(IGameData data, string mapKey, long? unixSeconds = null)
	{
		foreach (JsonObject item in Rules(data, mapKey))
		{
			if (!(item["bounds"] is JsonArray))
			{
				string text = SelectTrack(item, "music", unixSeconds);
				if (text != null)
				{
					return text;
				}
			}
		}
		return null;
	}

	public static string? ResolveAmbient(IGameData data, string mapKey, int gameX, int gameY, long? unixSeconds = null)
	{
		return ResolveZone(data, mapKey, gameX, gameY, unixSeconds) ?? ResolveMapTrack(data, mapKey, unixSeconds);
	}

	public static string? ResolveBoss(IGameData data, string mapKey, int gameX, int gameY, long? unixSeconds = null)
	{
		return ResolveEventTrack(data, mapKey, gameX, gameY, "battle", unixSeconds);
	}

	public static string? ResolveVictorySting(IGameData data, string mapKey, int gameX, int gameY, long? unixSeconds = null)
	{
		return ResolveEventTrack(data, mapKey, gameX, gameY, "death", unixSeconds);
	}

	public static IReadOnlyList<MapMusicZone> Zones(IGameData data, string mapKey)
	{
		List<MapMusicZone> list = new List<MapMusicZone>();
		foreach (JsonObject item in Rules(data, mapKey))
		{
			if (item["bounds"] is JsonArray { Count: 4 } jsonArray)
			{
				string text = SelectTrack(item, "music", 0L);
				if (text != null)
				{
					list.Add(new MapMusicZone(text, jsonArray[0].GetValue<int>(), jsonArray[1].GetValue<int>(), jsonArray[2].GetValue<int>(), jsonArray[3].GetValue<int>()));
				}
			}
		}
		return list;
	}

	public static IReadOnlyCollection<string> MappedKeys(IGameData data)
	{
		return (from pair in RulesByMap(data)
			select pair.Key).ToHashSet<string>(StringComparer.Ordinal);
	}

	public static IReadOnlyCollection<string> ReferencedTracks(IGameData data)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (Root(data)["referenced"]?["musicIds"] is JsonArray jsonArray)
		{
			foreach (JsonNode item in jsonArray)
			{
				if (item is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value))
				{
					hashSet.Add($"music{value}");
				}
			}
		}
		hashSet.Add(LoginTrack(data));
		return hashSet;
	}

	private static string? ResolveEventTrack(IGameData data, string mapKey, int gameX, int gameY, string field, long? unixSeconds)
	{
		foreach (JsonObject item in Rules(data, mapKey))
		{
			if (!(item["bounds"] is JsonArray) || Contains(item, gameX, gameY))
			{
				string text = SelectTrack(item, field, unixSeconds);
				if (text != null)
				{
					return text;
				}
			}
		}
		return null;
	}

	private static string? SystemTrack(IGameData data, string key)
	{
		if (!(Root(data)["system"]?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value) || value < 0)
		{
			return null;
		}
		return $"music{value}";
	}

	private static string? SelectTrack(JsonObject rule, string field, long? unixSeconds)
	{
		if (!(rule[field] is JsonArray { Count: not 0 } jsonArray))
		{
			return null;
		}
		int index = 0;
		int num = rule["interval"]?.GetValue<int>() ?? 0;
		if (jsonArray.Count > 1 && num > 0)
		{
			index = (int)(((unixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / num % jsonArray.Count + jsonArray.Count) % jsonArray.Count);
		}
		if (!(jsonArray[index] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value) || value < 0)
		{
			return null;
		}
		return $"music{value}";
	}

	private static bool Contains(JsonObject rule, int gameX, int gameY)
	{
		if (!(rule["bounds"] is JsonArray { Count: 4 } jsonArray))
		{
			return true;
		}
		if (gameX >= jsonArray[0].GetValue<int>() && gameY >= jsonArray[1].GetValue<int>() && gameX <= jsonArray[2].GetValue<int>())
		{
			return gameY <= jsonArray[3].GetValue<int>();
		}
		return false;
	}

	private static IEnumerable<JsonObject> Rules(IGameData data, string mapKey)
	{
		if (mapKey.Length == 0 || !(RulesByMap(data)[mapKey] is JsonArray jsonArray))
		{
			yield break;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject)
			{
				yield return jsonObject;
			}
		}
	}

	private static JsonObject RulesByMap(IGameData data)
	{
		return (Root(data)["music"]?["rulesByMapKey"] as JsonObject) ?? throw new InvalidOperationException("CLIENT_AUDIO_3_8C.music.rulesByMapKey is missing.");
	}

	private static JsonObject Root(IGameData data)
	{
		return (data.Table("CLIENT_AUDIO_3_8C") as JsonObject) ?? throw new InvalidOperationException("CLIENT_AUDIO_3_8C must be a JSON object.");
	}
}
