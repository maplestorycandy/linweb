using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IdleLineage.Data;

public sealed class L1jClassicMapCatalog
{
	public const int ExpectedMapCount = 62;

	public const string MapFileName = "l1j-classic-maps.json";

	public const string LinkFileName = "l1j-classic-map-links.json";

	public IReadOnlyList<L1jClassicMapDefinition> Maps { get; }

	public IReadOnlyList<L1jClassicMapLink> Links { get; }

	public int UnresolvedLinkCount { get; }

	private L1jClassicMapCatalog(IReadOnlyList<L1jClassicMapDefinition> maps, IReadOnlyList<L1jClassicMapLink> links, int unresolvedLinkCount)
	{
		Maps = maps;
		Links = links;
		UnresolvedLinkCount = unresolvedLinkCount;
	}

	public static L1jClassicMapCatalog Load(string dataDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory, "dataDirectory");
		using JsonDocument jsonDocument = JsonDocument.Parse(DataFileSystem.ReadAllText(DataFileSystem.Combine(dataDirectory, "l1j-classic-maps.json")));
		using JsonDocument jsonDocument2 = JsonDocument.Parse(DataFileSystem.ReadAllText(DataFileSystem.Combine(dataDirectory, "l1j-classic-map-links.json")));
		List<L1jClassicMapDefinition> list = new List<L1jClassicMapDefinition>();
		foreach (JsonElement item in RequireArray(jsonDocument.RootElement, "maps").EnumerateArray())
		{
			list.Add(new L1jClassicMapDefinition(RequireInt(item, "mapId"), RequireString(item, "mapKey"), RequireString(item, "displayName"), RequireInt(item, "musicSourceMapId")));
		}
		List<L1jClassicMapLink> list2 = new List<L1jClassicMapLink>();
		foreach (JsonElement item2 in RequireArray(jsonDocument2.RootElement, "links").EnumerateArray())
		{
			list2.Add(new L1jClassicMapLink(RequireString(item2, "id"), RequireInt(item2, "sourceMapId"), RequireString(item2, "sourceMapKey"), ReadCell(item2, "sourceGameCell"), RequireInt(item2, "destinationMapId"), RequireString(item2, "destinationMapKey"), RequireString(item2, "destinationName"), ReadCell(item2, "destinationGameCell"), RequireInt(item2, "heading")));
		}
		int @int = RequireObject(jsonDocument2.RootElement, "counts").GetProperty("unresolved").GetInt32();
		Validate(list, list2);
		return new L1jClassicMapCatalog(new ReadOnlyCollection<L1jClassicMapDefinition>(list), new ReadOnlyCollection<L1jClassicMapLink>(list2), @int);
	}

	private static void Validate(IReadOnlyList<L1jClassicMapDefinition> maps, IReadOnlyList<L1jClassicMapLink> links)
	{
		if (maps.Count < 62)
		{
			throw new InvalidDataException($"Classic map catalog must contain at least {62} maps, got {maps.Count}.");
		}
		if (maps.Select((L1jClassicMapDefinition map) => map.MapId).Distinct().Count() != maps.Count || maps.Select((L1jClassicMapDefinition map) => map.MapKey).Distinct<string>(StringComparer.Ordinal).Count() != maps.Count)
		{
			throw new InvalidDataException("Classic map ids and keys must be unique.");
		}
		if (links.Select((L1jClassicMapLink link) => link.Id).Distinct<string>(StringComparer.Ordinal).Count() != links.Count)
		{
			throw new InvalidDataException("Classic dungeon link ids must be unique.");
		}
	}

	private static (int X, int Y) ReadCell(JsonElement parent, string name)
	{
		JsonElement property = parent.GetProperty(name);
		if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() != 2)
		{
			throw new InvalidDataException(name + " must be a two-value array.");
		}
		return (X: property[0].GetInt32(), Y: property[1].GetInt32());
	}

	private static JsonElement RequireArray(JsonElement parent, string name)
	{
		JsonElement property = parent.GetProperty(name);
		if (property.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidDataException(name + " must be an array.");
		}
		return property;
	}

	private static JsonElement RequireObject(JsonElement parent, string name)
	{
		JsonElement property = parent.GetProperty(name);
		if (property.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException(name + " must be an object.");
		}
		return property;
	}

	private static int RequireInt(JsonElement parent, string name)
	{
		return parent.GetProperty(name).GetInt32();
	}

	private static string RequireString(JsonElement parent, string name)
	{
		return parent.GetProperty(name).GetString() ?? throw new InvalidDataException(name + " must be a string.");
	}
}
