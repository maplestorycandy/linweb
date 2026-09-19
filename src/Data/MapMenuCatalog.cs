using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace IdleLineage.Data;

public static class MapMenuCatalog
{
	private static readonly Regex ExplicitFloorPattern = new Regex("(?<floor>\\d+)\\s*(?:樓|層|F)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public const string FallbackGroupKey = "other";

	private static readonly (string Key, string Name)[] GroupOrder = new(string, string)[5]
	{
		("village", "村莊"),
		("wild", "野外"),
		("dungeon", "地監"),
		("pirate_island", "海賊島"),
		("rift", "時空裂痕")
	};

	private static readonly Dictionary<string, string> CategoryAliases = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["town"] = "village",
		["field"] = "wild"
	};

	public static IReadOnlyList<MapMenuRegion> Build(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		List<MapMenuRegion> list = new List<MapMenuRegion>();
		foreach (MapRegionDefinition region in WorldMapCatalog.GetRegions(data))
		{
			MapMenuRegion mapMenuRegion = Build(region);
			if ((object)mapMenuRegion != null)
			{
				list.Add(mapMenuRegion);
			}
		}
		return new ReadOnlyCollection<MapMenuRegion>(list);
	}

	public static MapMenuRegion? Build(MapRegionDefinition region)
	{
		ArgumentNullException.ThrowIfNull(region, "region");
		Dictionary<string, List<MapDestination>> dictionary = new Dictionary<string, List<MapDestination>>(StringComparer.Ordinal);
		foreach (MapDestination item in WorldMapDestinationRules.Effective(region).Where(IsSelectableDestination))
		{
			string key = GroupKeyOf(item);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = new List<MapDestination>());
			}
			value.Add(item);
		}
		if (dictionary.Count == 0)
		{
			return null;
		}
		List<MapMenuGroup> list2 = new List<MapMenuGroup>(dictionary.Count);
		(string, string)[] groupOrder = GroupOrder;
		for (int i = 0; i < groupOrder.Length; i++)
		{
			var (key2, name) = groupOrder[i];
			if (dictionary.TryGetValue(key2, out var value2))
			{
				list2.Add(Group(key2, name, value2));
			}
		}
		if (dictionary.TryGetValue("other", out var value3))
		{
			list2.Add(Group("other", "其他", value3));
		}
		return new MapMenuRegion(region.Key, region.Name, new ReadOnlyCollection<MapMenuGroup>(list2));
	}

	public static string GroupKeyOf(MapDestination destination)
	{
		ArgumentNullException.ThrowIfNull(destination, "destination");
		if (destination.Kind == MapDestinationKind.Town)
		{
			return "village";
		}
		string category = destination.Category;
		if (CategoryAliases.TryGetValue(category, out string value))
		{
			category = value;
		}
		if (!GroupOrder.Any(((string Key, string Name) group) => string.Equals(group.Key, category, StringComparison.Ordinal)))
		{
			return "other";
		}
		return category;
	}

	public static bool IsSelectableDestination(MapDestination destination)
	{
		ArgumentNullException.ThrowIfNull(destination, "destination");
		if (string.Equals(destination.Category, "special", StringComparison.Ordinal))
		{
			return false;
		}
		if (string.Equals(destination.Key, "eva_kingdom", StringComparison.Ordinal))
		{
			return false;
		}
		if (destination.Kind == MapDestinationKind.Town)
		{
			return true;
		}
		Match match = ExplicitFloorPattern.Match(destination.Name);
		if (match.Success)
		{
			if (int.TryParse(match.Groups["floor"].Value, out var result))
			{
				return result == 1;
			}
			return false;
		}
		return true;
	}

	private static MapMenuGroup Group(string key, string name, List<MapDestination> rows)
	{
		return new MapMenuGroup(key, name, new ReadOnlyCollection<MapDestination>(rows));
	}
}
