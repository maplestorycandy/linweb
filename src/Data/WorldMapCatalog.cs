using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public static class WorldMapCatalog
{
	private sealed record CategoryEntry(string Category, string Name, string? Color, bool ClassicHide, string? QuestRequirement, string? HeldKeyRequirement, string? ConsumedKeyRequirement, string? PrideBossRequirement, int? PrideFloorRequirement);

	public const string RegionTableName = "MAP_REGIONS";

	public const string CategoryTableName = "MAP_CATEGORIES";

	private static string? EffectiveQuestRequirement(string mapKey, string? exportedRequirement)
	{
		if (!MapEntryRequirementRetirement.DropsQuestRequirement(mapKey))
		{
			return exportedRequirement;
		}
		return null;
	}

	public static IReadOnlyList<MapRegionDefinition> GetRegions(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonArray jsonArray = (data.Table("MAP_REGIONS") as JsonArray) ?? throw new InvalidDataException("MAP_REGIONS must be a JSON array.");
		IReadOnlyDictionary<string, CategoryEntry> readOnlyDictionary = ParseCategories((data.Table("MAP_CATEGORIES") as JsonObject) ?? throw new InvalidDataException("MAP_CATEGORIES must be a JSON object."));
		List<MapRegionDefinition> list = new List<MapRegionDefinition>(jsonArray.Count);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> destinationKeys = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < jsonArray.Count; i++)
		{
			JsonObject obj = (jsonArray[i] as JsonObject) ?? throw InvalidRegion(i, "must be a JSON object");
			string text = RequiredString(obj["key"], $"{"MAP_REGIONS"}[{i}].key");
			if (!hashSet.Add(text))
			{
				throw InvalidRegion(i, "duplicates region key '" + text + "'");
			}
			string text2 = RequiredString(obj["label"], $"{"MAP_REGIONS"}[{i}].label");
			if (!(obj["maps"] is JsonArray jsonArray2))
			{
				throw InvalidRegion(i, "field 'maps' must be an array");
			}
			string text3 = OptionalString(obj, "castleCity", $"{"MAP_REGIONS"}[{i}]");
			int? num = OptionalNonNegativeInteger(obj, "castleAt", $"{"MAP_REGIONS"}[{i}]");
			if (text3 == null != !num.HasValue)
			{
				throw InvalidRegion(i, "castleCity and castleAt must either both be present or both be absent");
			}
			if (num > jsonArray2.Count)
			{
				throw InvalidRegion(i, "castleAt cannot be greater than the destination count");
			}
			List<MapDestination> list2 = new List<MapDestination>(jsonArray2.Count);
			for (int j = 0; j < jsonArray2.Count; j++)
			{
				if (!(jsonArray2[j] is JsonObject jsonObject))
				{
					throw InvalidDestination(i, j, "must be a JSON object");
				}
				string text4 = RequiredString(jsonObject["v"], $"{"MAP_REGIONS"}[{i}].maps[{j}].v");
				if (!destinationKeys.Add(text4))
				{
					throw InvalidDestination(i, j, "duplicates destination key '" + text4 + "'");
				}
				string name = WorldMapDestinationRules.DisplayName(text4, RequiredString(jsonObject["t"], $"{"MAP_REGIONS"}[{i}].maps[{j}].t"));
				if (!readOnlyDictionary.TryGetValue(text4, out var value))
				{
					throw InvalidDestination(i, j, "is missing from MAP_CATEGORIES");
				}
				MapDestinationKind kind;
				if (data.Towns[text4] != null && data.Maps[text4] == null)
				{
					kind = MapDestinationKind.Town;
				}
				else
				{
					if (data.Maps[text4] == null || data.Towns[text4] != null)
					{
						throw InvalidDestination(i, j, "'" + text4 + "' must resolve to exactly one DB.towns or DB.maps entry");
					}
					kind = MapDestinationKind.Hunt;
				}
				list2.Add(new MapDestination(text, text2, i, j, text4, name, value.Category, value.Name, value.Color, kind, value.ClassicHide, EffectiveQuestRequirement(text4, value.QuestRequirement), value.HeldKeyRequirement, value.ConsumedKeyRequirement, value.PrideBossRequirement, value.PrideFloorRequirement));
			}
			list.Add(new MapRegionDefinition(text, text2, i, new ReadOnlyCollection<MapDestination>(list2), text3, num));
		}
		if (destinationKeys.Count != readOnlyDictionary.Count)
		{
			string value2 = readOnlyDictionary.Keys.First((string key) => !destinationKeys.Contains(key));
			throw new InvalidDataException($"{"MAP_CATEGORIES"} destination '{value2}' is missing from {"MAP_REGIONS"}.");
		}
		return new ReadOnlyCollection<MapRegionDefinition>(list);
	}

	public static IReadOnlyList<MapDestination> GetDestinations(IGameData data)
	{
		IReadOnlyList<MapRegionDefinition> regions = GetRegions(data);
		List<MapDestination> list = new List<MapDestination>(regions.Sum((MapRegionDefinition region) => region.Destinations.Count));
		foreach (MapRegionDefinition item in regions)
		{
			list.AddRange(item.Destinations);
		}
		return new ReadOnlyCollection<MapDestination>(list);
	}

	public static MapDestination GetDestination(IGameData data, string mapKey)
	{
		if (!TryGetDestination(data, mapKey, out MapDestination destination))
		{
			throw new KeyNotFoundException($"Map destination '{mapKey}' was not found in {"MAP_REGIONS"}.");
		}
		return destination;
	}

	public static bool TryGetDestination(IGameData data, string mapKey, out MapDestination? destination)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		destination = GetDestinations(data).FirstOrDefault((MapDestination candidate) => candidate.Key == mapKey);
		return (object)destination != null;
	}

	private static IReadOnlyDictionary<string, CategoryEntry> ParseCategories(JsonObject categories)
	{
		Dictionary<string, CategoryEntry> dictionary = new Dictionary<string, CategoryEntry>(StringComparer.Ordinal);
		foreach (var (text2, jsonNode2) in categories)
		{
			if (!(jsonNode2 is JsonArray jsonArray))
			{
				throw new InvalidDataException("MAP_CATEGORIES['" + text2 + "'] must be an array.");
			}
			for (int i = 0; i < jsonArray.Count; i++)
			{
				if (!(jsonArray[i] is JsonObject jsonObject))
				{
					throw new InvalidDataException($"{"MAP_CATEGORIES"}['{text2}'][{i}] must be a JSON object.");
				}
				string text3 = $"{"MAP_CATEGORIES"}['{text2}'][{i}]";
				string text4 = RequiredString(jsonObject["v"], text3 + ".v");
				if (dictionary.ContainsKey(text4))
				{
					throw new InvalidDataException(text3 + " duplicates destination key '" + text4 + "'.");
				}
				(string? Boss, int? Floor) tuple = ParsePrideRequirement(jsonObject, text3);
				string item = tuple.Boss;
				int? item2 = tuple.Floor;
				string heldKeyRequirement = OptionalString(jsonObject, "keyHoldReq", text3);
				dictionary.Add(text4, new CategoryEntry(text2, RequiredString(jsonObject["t"], text3 + ".t"), OptionalString(jsonObject, "c", text3), OptionalBoolean(jsonObject, "classicHide", text3), OptionalString(jsonObject, "questReq", text3), heldKeyRequirement, OptionalString(jsonObject, "needKey", text3), item, item2));
			}
		}
		return new ReadOnlyDictionary<string, CategoryEntry>(dictionary);
	}

	private static (string? Boss, int? Floor) ParsePrideRequirement(JsonObject source, string location)
	{
		if (!source.TryGetPropertyValue("prideReq", out JsonNode jsonNode) || jsonNode == null)
		{
			return (Boss: null, Floor: null);
		}
		if (jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return (Boss: value, Floor: null);
		}
		if (TryNonNegativeInteger(jsonNode, out var result))
		{
			return (Boss: null, Floor: result);
		}
		throw new InvalidDataException(location + ".prideReq must be a non-empty boss key or non-negative floor.");
	}

	private static string RequiredString(JsonNode? node, string location)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		throw new InvalidDataException(location + " must be a non-empty string.");
	}

	private static string? OptionalString(JsonObject source, string field, string location)
	{
		if (!source.TryGetPropertyValue(field, out JsonNode jsonNode) || jsonNode == null)
		{
			return null;
		}
		if (jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		throw new InvalidDataException(location + "." + field + " must be a non-empty string when present.");
	}

	private static bool OptionalBoolean(JsonObject source, string field, string location)
	{
		if (!source.TryGetPropertyValue(field, out JsonNode jsonNode) || jsonNode == null)
		{
			return false;
		}
		if (jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var value))
		{
			return value;
		}
		throw new InvalidDataException(location + "." + field + " must be a boolean when present.");
	}

	private static int? OptionalNonNegativeInteger(JsonObject source, string field, string location)
	{
		if (!source.TryGetPropertyValue(field, out JsonNode jsonNode) || jsonNode == null)
		{
			return null;
		}
		if (TryNonNegativeInteger(jsonNode, out var result))
		{
			return result;
		}
		throw new InvalidDataException(location + "." + field + " must be a non-negative integer when present.");
	}

	private static bool TryNonNegativeInteger(JsonNode? node, out int result)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<long>(out var value) && value >= 0 && value <= int.MaxValue)
		{
			result = (int)value;
			return true;
		}
		result = 0;
		return false;
	}

	private static InvalidDataException InvalidRegion(int regionIndex, string reason)
	{
		return new InvalidDataException($"{"MAP_REGIONS"}[{regionIndex}] {reason}.");
	}

	private static InvalidDataException InvalidDestination(int regionIndex, int destinationIndex, string reason)
	{
		return new InvalidDataException($"{"MAP_REGIONS"}[{regionIndex}].maps[{destinationIndex}] {reason}.");
	}
}
