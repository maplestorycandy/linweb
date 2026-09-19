using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IdleLineage.Data;

public sealed class RastabadMapCatalog
{
	public const string LayoutFileName = "rastabad-layout.json";

	public const string RouteStartMapKey = "elf_grave";

	public const string UnderLakeMapKey = "under_lake";

	public const string GreatCaveMapKey = "giant_tomb";

	public const string DiadFortressMapKey = "diad_fortress";

	public const string FrontGateMapKey = "rastabad_gate";

	public IReadOnlyList<RastabadMap> Maps { get; }

	public IReadOnlyList<RastabadPortalLink> PortalLinks { get; }

	public RastabadRemoteEntrance RemoteEntrance { get; }

	private RastabadMapCatalog(IReadOnlyList<RastabadMap> maps, IReadOnlyList<RastabadPortalLink> portalLinks, RastabadRemoteEntrance remoteEntrance)
	{
		Maps = maps;
		PortalLinks = portalLinks;
		RemoteEntrance = remoteEntrance;
	}

	public static RastabadMapCatalog Load(string dataDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory, "dataDirectory");
		using JsonDocument jsonDocument = JsonDocument.Parse(DataFileSystem.ReadAllText(DataFileSystem.Combine(dataDirectory, "rastabad-layout.json")));
		JsonElement rootElement = jsonDocument.RootElement;
		List<RastabadMap> list = new List<RastabadMap>();
		foreach (JsonElement item in RequireArray(rootElement, "maps").EnumerateArray())
		{
			list.Add(new RastabadMap(RequireString(item, "mapKey"), RequireInt(item, "sourceMapId"), RequireString(item, "displayName")));
		}
		List<RastabadPortalLink> list2 = new List<RastabadPortalLink>();
		foreach (JsonElement item2 in RequireArray(rootElement, "portalLinks").EnumerateArray())
		{
			list2.Add(new RastabadPortalLink(RequireString(item2, "from"), RequireString(item2, "portal"), RequireString(item2, "to"), RequireString(item2, "arrival"), item2.TryGetProperty("syntheticReturn", out var value) && value.ValueKind == JsonValueKind.True));
		}
		JsonElement parent = RequireObject(rootElement, "remoteEntrance");
		RastabadRemoteEntrance remoteEntrance = new RastabadRemoteEntrance(RequireString(parent, "sourceMapKey"), RequireString(parent, "portalLandmarkId"), RequireString(parent, "destinationMapKey"), RequireString(parent, "arrivalLandmarkId"));
		Validate(list, list2, remoteEntrance);
		return new RastabadMapCatalog(new ReadOnlyCollection<RastabadMap>(list), new ReadOnlyCollection<RastabadPortalLink>(list2), remoteEntrance);
	}

	public string DisplayName(string mapKey)
	{
		return Maps.FirstOrDefault((RastabadMap map) => string.Equals(map.MapKey, mapKey, StringComparison.Ordinal))?.DisplayName ?? string.Empty;
	}

	public bool Contains(string mapKey)
	{
		return DisplayName(mapKey).Length > 0;
	}

	private static void Validate(IReadOnlyList<RastabadMap> maps, IReadOnlyList<RastabadPortalLink> links, RastabadRemoteEntrance remoteEntrance)
	{
		HashSet<string> hashSet = maps.Select((RastabadMap map) => map.MapKey).ToHashSet<string>(StringComparer.Ordinal);
		if (hashSet.Count != maps.Count)
		{
			throw new InvalidDataException("Rastabad map keys must be unique.");
		}
		if (!hashSet.Contains("elf_grave") || !hashSet.Contains("under_lake") || !hashSet.Contains("giant_tomb") || !hashSet.Contains("diad_fortress") || !hashSet.Contains("rastabad_gate"))
		{
			throw new InvalidDataException("Rastabad layout is missing a required route map.");
		}
		if (!hashSet.Contains(remoteEntrance.DestinationMapKey))
		{
			throw new InvalidDataException("Rastabad remote entrance has an unknown destination.");
		}
		foreach (RastabadPortalLink link in links)
		{
			if (!hashSet.Contains(link.SourceMapKey))
			{
				throw new InvalidDataException("Rastabad link source '" + link.SourceMapKey + "' does not exist.");
			}
			if (!hashSet.Contains(link.DestinationMapKey) && !string.Equals(link.DestinationMapKey, remoteEntrance.SourceMapKey, StringComparison.Ordinal))
			{
				throw new InvalidDataException("Rastabad link destination '" + link.DestinationMapKey + "' does not exist.");
			}
		}
	}

	private static JsonElement RequireArray(JsonElement parent, string name)
	{
		if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			throw new InvalidDataException("'" + name + "' must be an array.");
		}
		return value;
	}

	private static JsonElement RequireObject(JsonElement parent, string name)
	{
		if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
		{
			throw new InvalidDataException("'" + name + "' must be an object.");
		}
		return value;
	}

	private static string RequireString(JsonElement parent, string name)
	{
		if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
		{
			throw new InvalidDataException("'" + name + "' must be a non-empty string.");
		}
		return value.GetString();
	}

	private static int RequireInt(JsonElement parent, string name)
	{
		if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var value2))
		{
			throw new InvalidDataException("'" + name + "' must be an integer.");
		}
		return value2;
	}
}
