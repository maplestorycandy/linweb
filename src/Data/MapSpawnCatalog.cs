using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Combat;

namespace IdleLineage.Data;

public static class MapSpawnCatalog
{
	private static readonly string[] L1jSpawnCatalogPathCandidates = new string[2]
	{
		"res://data/l1j-spawns.json",
		Path.Combine("data", "l1j-spawns.json")
	};

	private static JsonObject? _l1jSpawns;

	public static IReadOnlyList<string> GetMobKeys(IGameData data, string mapKey)
	{
		if (!TryGetMobKeys(data, mapKey, out IReadOnlyList<string> mobKeys))
		{
			throw new KeyNotFoundException("Map '" + mapKey + "' was not found in DB.maps.");
		}
		return mobKeys;
	}

	public static bool TryGetMobKeys(IGameData data, string mapKey, out IReadOnlyList<string> mobKeys)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		if (data.Maps[mapKey] == null)
		{
			mobKeys = Array.Empty<string>();
			return false;
		}
		if (!(data.Maps[mapKey] is JsonArray jsonArray))
		{
			throw new InvalidDataException("DB.maps['" + mapKey + "'] must be an array of mob keys.");
		}
		List<string> list = new List<string>(jsonArray.Count);
		for (int i = 0; i < jsonArray.Count; i++)
		{
			if (!(jsonArray[i] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidDataException($"DB.maps['{mapKey}'][{i}] must be a non-empty mob key.");
			}
			if (data.Mob(value) == null)
			{
				Godot.GD.PushWarning($"[MapSpawnCatalog] DB.maps['{mapKey}'][{i}] references missing mob '{value}'; skipping.");
				continue;
			}
			list.Add(value);
		}
		mobKeys = new ReadOnlyCollection<string>(list);
		return true;
	}

	public static int GetInitialSpawnDelay(IGameData data, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		try
		{
			if (LoadL1jSpawns(data)["initialSpawnDelays"] is JsonObject jsonObject && jsonObject[mapKey] != null)
			{
				if (jsonObject[mapKey] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out int delay))
				{
					return Math.Max(0, delay);
				}
			}
		}
		catch { }
		return 0;
	}

	public static int? GetSpawnTriggerRange(IGameData data, string mapKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		try
		{
			if (LoadL1jSpawns(data)["spawnTriggerRanges"] is JsonObject jsonObject && jsonObject[mapKey] != null)
			{
				if (jsonObject[mapKey] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out int range))
				{
					return Math.Max(0, range);
				}
			}
		}
		catch { }
		return null;
	}

	public static IReadOnlyList<MapSpawnPoint> GetFixedSpawnPoints(IGameData data, string mapKey)
	{
		if (!TryGetFixedSpawnPoints(data, mapKey, out IReadOnlyList<MapSpawnPoint> fixedSpawnPoints))
		{
			throw new KeyNotFoundException("Map '" + mapKey + "' has no fixed spawn points.");
		}
		return fixedSpawnPoints;
	}

	public static bool TryGetFixedSpawnPoints(IGameData data, string mapKey, out IReadOnlyList<MapSpawnPoint> fixedSpawnPoints)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		if (!(LoadL1jSpawns(data)["points"] is JsonObject jsonObject) || !(jsonObject[mapKey] is JsonArray jsonArray))
		{
			fixedSpawnPoints = Array.Empty<MapSpawnPoint>();
			return false;
		}
		List<MapSpawnPoint> list = new List<MapSpawnPoint>(jsonArray.Count);
		foreach (JsonNode item3 in jsonArray)
		{
			if (!(item3 is JsonObject jsonObject2))
			{
				throw new InvalidDataException("l1j-spawns points[" + mapKey + "] row must be an object.");
			}
			string text = jsonObject2["kind"]?.GetValue<string>() ?? throw new InvalidDataException("l1j-spawns points[" + mapKey + "] must contain kind (normal|boss).");
			if ((!(text == "normal") && !(text == "boss")) || 1 == 0)
			{
				continue;
			}
			string value = jsonObject2["source"]?.GetValue<string>() ?? throw new InvalidDataException("l1j-spawns points[" + mapKey + "] row is missing source.");
			int value2 = ReadRequiredInt(jsonObject2["spawnId"], "l1j-spawns points[" + mapKey + "] row is missing spawnId.");
			string text2 = jsonObject2["mob"]?.GetValue<string>() ?? throw new InvalidDataException("l1j-spawns points[" + mapKey + "] row is missing mob.");
			if (!(jsonObject2["cell"] is JsonArray { Count: >=2 } jsonArray2))
			{
				throw new InvalidDataException($"l1j-spawns points[{mapKey}] row for '{text2}' has invalid cell.");
			}
			int x = ReadRequiredInt(jsonArray2[0], $"l1j-spawns points[{mapKey}] row for '{text2}' has invalid cell X.");
			int y = ReadRequiredInt(jsonArray2[1], $"l1j-spawns points[{mapKey}] row for '{text2}' has invalid cell Y.");
			int num = 1;
			JsonNode jsonNode = jsonObject2["count"];
			if (jsonNode != null)
			{
				num = ReadRequiredInt(jsonNode, $"l1j-spawns points[{mapKey}] row for '{text2}' has invalid count.");
			}
			if (num > 0)
			{
				int randomX = ReadOptionalNonNegativeInt(jsonObject2["rx"], "rx", mapKey, text2);
				int randomY = ReadOptionalNonNegativeInt(jsonObject2["ry"], "ry", mapKey, text2);
				(int Minimum, int Maximum) tuple = ReadRespawn(jsonObject2, mapKey, text2);
				int item = tuple.Minimum;
				int item2 = tuple.Maximum;
				MapSpawnBounds? area = ReadArea(jsonObject2, mapKey, text2);
				MapSpawnCell? targetCell = null;
				if (jsonObject2["targetCell"] is JsonArray { Count: >= 2 } targetArr)
				{
					int tx = targetArr[0]?.GetValue<int>() ?? 0;
					int ty = targetArr[1]?.GetValue<int>() ?? 0;
					if (tx > 0 || ty > 0)
					{
						targetCell = new MapSpawnCell(tx, ty);
					}
				}
				if (data.Mob(text2) == null)
				{
					Godot.GD.PushWarning($"[MapSpawnCatalog] l1j-spawns points[{mapKey}] references missing mob '{text2}'; skipping.");
					continue;
				}
				for (int i = 0; i < num; i++)
				{
					list.Add(new MapSpawnPoint($"{mapKey}:{value}:{value2}:{i}", text2, new MapSpawnCell(x, y), text == "boss", item, item2, area, randomX, randomY, targetCell));
				}
			}
		}
		fixedSpawnPoints = new ReadOnlyCollection<MapSpawnPoint>(list);
		return true;
	}

	private static int ReadRequiredInt(JsonNode? node, string error)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var value))
		{
			return value;
		}
		throw new InvalidDataException(error);
	}

	private static int ReadOptionalNonNegativeInt(JsonNode? node, string field, string mapKey, string mobKey)
	{
		if (node == null)
		{
			return 0;
		}
		int num = ReadRequiredInt(node, $"l1j-spawns points[{mapKey}] row for '{mobKey}' has invalid {field}.");
		if (num < 0)
		{
			throw new InvalidDataException($"l1j-spawns points[{mapKey}] row for '{mobKey}' has negative {field}.");
		}
		return num;
	}

	private static (int Minimum, int Maximum) ReadRespawn(JsonObject spawn, string mapKey, string mobKey)
	{
		if (!(spawn["respawn"] is JsonArray { Count: >=2 } jsonArray))
		{
			return (Minimum: 0, Maximum: 0);
		}
		int num = ReadRequiredInt(jsonArray[0], $"l1j-spawns points[{mapKey}] row for '{mobKey}' has invalid respawn minimum.");
		int num2 = ReadRequiredInt(jsonArray[1], $"l1j-spawns points[{mapKey}] row for '{mobKey}' has invalid respawn maximum.");
		if (num < 0 || num2 < num)
		{
			throw new InvalidDataException($"l1j-spawns points[{mapKey}] row for '{mobKey}' has invalid respawn range.");
		}
		return (Minimum: num, Maximum: num2);
	}

	private static MapSpawnBounds? ReadArea(JsonObject spawn, string mapKey, string mobKey)
	{
		if (!(spawn["area"] is JsonArray jsonArray))
		{
			return null;
		}
		if (jsonArray.Count < 4)
		{
			throw new InvalidDataException($"l1j-spawns points[{mapKey}] row for '{mobKey}' has invalid area.");
		}
		int val = ReadRequiredInt(jsonArray[0], $"Invalid area X1 for '{mobKey}' on '{mapKey}'.");
		int val2 = ReadRequiredInt(jsonArray[1], $"Invalid area Y1 for '{mobKey}' on '{mapKey}'.");
		int val3 = ReadRequiredInt(jsonArray[2], $"Invalid area X2 for '{mobKey}' on '{mapKey}'.");
		int val4 = ReadRequiredInt(jsonArray[3], $"Invalid area Y2 for '{mobKey}' on '{mapKey}'.");
		return new MapSpawnBounds(Math.Min(val, val3), Math.Min(val2, val4), Math.Max(val, val3), Math.Max(val2, val4));
	}

	private static JsonObject LoadL1jSpawns(IGameData data)
	{
		if (_l1jSpawns != null)
		{
			return _l1jSpawns;
		}
		string[] l1jSpawnCatalogPathCandidates = L1jSpawnCatalogPathCandidates;
		foreach (string path in l1jSpawnCatalogPathCandidates)
		{
			if (DataFileSystem.Exists(path))
			{
				_l1jSpawns = JsonNode.Parse(DataFileSystem.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("l1j-spawns.json must be a JSON object.");
				return _l1jSpawns;
			}
		}
		string text = FindSpawnCatalogFallbackPath(AppContext.BaseDirectory);
		if (text != null)
		{
			_l1jSpawns = JsonNode.Parse(DataFileSystem.ReadAllText(text))?.AsObject() ?? throw new InvalidDataException("l1j-spawns.json must be a JSON object.");
			return _l1jSpawns;
		}
		string text2 = string.Join(", ", L1jSpawnCatalogPathCandidates);
		throw new FileNotFoundException("Could not find l1j-spawns.json. Tried: " + text2);
	}

	private static string? FindSpawnCatalogFallbackPath(string from)
	{
		string text = Path.Combine("data", "l1j-spawns.json");
		if (text != null && DataFileSystem.Exists(text))
		{
			return text;
		}
		string text2 = Path.GetFullPath(from);
		for (int i = 0; i < 8; i++)
		{
			string text3 = Path.Combine(text2, "data", "l1j-spawns.json");
			if (DataFileSystem.Exists(text3))
			{
				return text3;
			}
			string directoryName = Path.GetDirectoryName(text2);
			if (string.IsNullOrEmpty(directoryName) || directoryName == text2)
			{
				break;
			}
			text2 = directoryName;
		}
		return null;
	}
}
