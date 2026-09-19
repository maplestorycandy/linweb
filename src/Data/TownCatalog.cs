using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class TownCatalog
{
	public static TownDefinition GetTown(IGameData data, string townKey)
	{
		if (!TryGetTown(data, townKey, out TownDefinition town))
		{
			throw new KeyNotFoundException("Town '" + townKey + "' was not found in DB.towns.");
		}
		return town;
	}

	public static bool TryGetTown(IGameData data, string townKey, out TownDefinition? town)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(townKey, "townKey");
		if (data.Towns[townKey] == null)
		{
			town = null;
			return false;
		}
		town = ParseTown(townKey, data.Towns[townKey]);
		return true;
	}

	public static IReadOnlyList<TownDefinition> GetAllTowns(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		List<TownDefinition> list = new List<TownDefinition>(data.Towns.Count);
		foreach (var (townKey, node) in data.Towns)
		{
			list.Add(ParseTown(townKey, node));
		}
		return new ReadOnlyCollection<TownDefinition>(list);
	}

	private static TownDefinition ParseTown(string townKey, JsonNode? node)
	{
		if (!(node is JsonObject jsonObject))
		{
			throw InvalidTown(townKey, "must be a JSON object");
		}
		return new TownDefinition(townKey, RequiredString(jsonObject["n"], townKey, "n"));
	}

	private static string RequiredString(JsonNode? node, string townKey, string field)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		throw InvalidTown(townKey, "field '" + field + "' must be a non-empty string");
	}

	private static InvalidDataException InvalidTown(string townKey, string reason)
	{
		return new InvalidDataException($"DB.towns['{townKey}'] {reason}.");
	}
}
