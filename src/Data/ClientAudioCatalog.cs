using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class ClientAudioCatalog
{
	private const string TableName = "CLIENT_AUDIO_3_8C";

	public static int? ResolveEquipmentSound(IGameData data, string itemKey, string classId)
	{
		if (!string.Equals(classId, "warrior", StringComparison.Ordinal) && !string.IsNullOrEmpty(itemKey))
		{
			JsonObject jsonObject = data.Item(itemKey);
			if (jsonObject != null)
			{
				int num = jsonObject["l1jInvGfx"]?.GetValue<int>() ?? 0;
				if (num <= 0 || !(Root(data)["equipment"]?["byInvGfx"]?[num.ToString()] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
				{
					return null;
				}
				return value;
			}
		}
		return null;
	}

	public static ClientEnvironmentSoundRule? ResolveEnvironment(IGameData data, string mapKey, int gameX, int gameY, bool night, string weather = "")
	{
		if (mapKey.Length == 0 || !(Root(data)["environment"]?["rulesByMapKey"]?[mapKey] is JsonArray jsonArray))
		{
			return null;
		}
		string b = (night ? "night" : "day");
		string b2 = weather.Trim().ToLowerInvariant();
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonObject jsonObject)
			{
				string a = jsonObject["time"]?.GetValue<string>() ?? "any";
				string a2 = jsonObject["weather"]?.GetValue<string>() ?? "any";
				if ((string.Equals(a, "any", StringComparison.OrdinalIgnoreCase) || string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) && (string.Equals(a2, "any", StringComparison.OrdinalIgnoreCase) || string.Equals(a2, b2, StringComparison.OrdinalIgnoreCase)) && Contains(jsonObject, gameX, gameY))
				{
					return new ClientEnvironmentSoundRule(jsonObject["sourceOrder"]?.GetValue<int>() ?? 0, IntList(jsonObject["main"] as JsonArray), StringList(jsonObject["groups"] as JsonArray));
				}
			}
		}
		return null;
	}

	public static ClientEnvironmentSoundGroup? EnvironmentGroup(IGameData data, string name)
	{
		if (!(Root(data)["environment"]?["groups"]?[name] is JsonObject jsonObject))
		{
			return null;
		}
		return new ClientEnvironmentSoundGroup(name, jsonObject["interval"]?.GetValue<double>() ?? 0.0, IntList(jsonObject["sounds"] as JsonArray));
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

	private static IReadOnlyList<int> IntList(JsonArray? array)
	{
		if (array == null)
		{
			return Array.Empty<int>();
		}
		List<int> list = new List<int>(array.Count);
		foreach (JsonNode item in array)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value))
			{
				list.Add(value);
			}
		}
		return list;
	}

	private static IReadOnlyList<string> StringList(JsonArray? array)
	{
		if (array == null)
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>(array.Count);
		foreach (JsonNode item in array)
		{
			if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && value != null)
			{
				list.Add(value);
			}
		}
		return list;
	}

	private static JsonObject Root(IGameData data)
	{
		return (data.Table("CLIENT_AUDIO_3_8C") as JsonObject) ?? throw new InvalidOperationException("CLIENT_AUDIO_3_8C must be a JSON object.");
	}
}
