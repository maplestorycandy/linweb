using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace IdleLineage.Data;

public sealed class L1jGetbackCatalog
{
	public const string TableName = "L1J_GETBACK";

	public const int ExpectedGetbackCount = 395;

	public const int ExpectedRestartCount = 194;

	public const int ExpectedTownLocationCount = 17;

	public IReadOnlyList<L1jGetbackRow> Getback { get; }

	public IReadOnlyList<L1jGetbackRestartRow> Restart { get; }

	public IReadOnlyDictionary<int, L1jTownGetbackLocation> TownLocations { get; }

	public L1jGetbackDestination Fallback { get; }

	private L1jGetbackCatalog(IReadOnlyList<L1jGetbackRow> getback, IReadOnlyList<L1jGetbackRestartRow> restart, IReadOnlyDictionary<int, L1jTownGetbackLocation> townLocations, L1jGetbackDestination fallback)
	{
		Getback = getback;
		Restart = restart;
		TownLocations = townLocations;
		Fallback = fallback;
	}

	public static L1jGetbackCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!(data.Table("L1J_GETBACK") is JsonObject owner))
		{
			throw new InvalidDataException("L1J_GETBACK must be a JSON object.");
		}
		Dictionary<int, L1jTownGetbackLocation> dictionary = new Dictionary<int, L1jTownGetbackLocation>();
		foreach (var (text2, jsonNode2) in RequiredObject(owner, "townLocations"))
		{
			if (!(jsonNode2 is JsonObject owner2) || !int.TryParse(text2, out var result))
			{
				throw Invalid("townLocations must be keyed by numeric town id");
			}
			int num = RequiredInt(owner2, "townId");
			if (result != num || !dictionary.TryAdd(result, new L1jTownGetbackLocation(result, OptionalString(owner2, "townKey"), RequiredInt(owner2, "mapId"), ReadDestinations(owner2, "destinations"))))
			{
				throw Invalid("invalid or duplicate town location " + text2);
			}
		}
		List<L1jGetbackRow> list = new List<L1jGetbackRow>();
		foreach (JsonObject item in RequiredRows(owner, "getback"))
		{
			JsonObject owner3 = RequiredObject(item, "area");
			JsonObject owner4 = RequiredObject(item, "townIds");
			list.Add(new L1jGetbackRow(new L1jGetbackArea(RequiredInt(owner3, "x1"), RequiredInt(owner3, "y1"), RequiredInt(owner3, "x2"), RequiredInt(owner3, "y2"), RequiredInt(owner3, "mapId"), ReadStrings(owner3, "mapKeys")), ReadDestinations(item, "destinations"), RequiredInt(owner4, "default"), RequiredInt(owner4, "elf"), RequiredInt(owner4, "darkElf"), RequiredBool(item, "scrollEscape"), RequiredString(item, "note")));
		}
		List<L1jGetbackRestartRow> list2 = new List<L1jGetbackRestartRow>();
		foreach (JsonObject item2 in RequiredRows(owner, "restart"))
		{
			list2.Add(new L1jGetbackRestartRow(RequiredInt(item2, "areaMapId"), ReadStrings(item2, "areaMapKeys"), RequiredString(item2, "note"), ReadDestination(RequiredObject(item2, "destination"))));
		}
		if (list.Count != 395 || list2.Count != 194 || dictionary.Count != 17)
		{
			throw Invalid($"row counts must be {395}/{194}/{17}, got {list.Count}/{list2.Count}/{dictionary.Count}");
		}
		for (int i = 1; i < list.Count; i++)
		{
			L1jGetbackArea area = list[i - 1].Area;
			L1jGetbackArea area2 = list[i].Area;
			if (area.MapId > area2.MapId || (area.MapId == area2.MapId && area.X1 < area2.X1))
			{
				throw Invalid("getback rows lost main's area_mapid/area_x1 ordering");
			}
		}
		return new L1jGetbackCatalog(new ReadOnlyCollection<L1jGetbackRow>(list), new ReadOnlyCollection<L1jGetbackRestartRow>(list2), new ReadOnlyDictionary<int, L1jTownGetbackLocation>(dictionary), ReadDestination(RequiredObject(owner, "fallback")));
	}

	public L1jGetbackRoute Resolve(string sourceMapKey, int gameX, int gameY, string? classId, int directRoll, int townRoll)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceMapKey, "sourceMapKey");
		L1jGetbackRow l1jGetbackRow = Getback.FirstOrDefault((L1jGetbackRow candidate) => candidate.Area.Contains(sourceMapKey, gameX, gameY));
		if ((object)l1jGetbackRow == null)
		{
			return new L1jGetbackRoute(InferTown(Fallback), "main fallback", ScrollEscape: true, UsedFallback: true, 0);
		}
		L1jGetbackDestination l1jGetbackDestination = l1jGetbackRow.Destinations[PositiveModulo(directRoll, l1jGetbackRow.Destinations.Count)];
		int num = TownIdFor(l1jGetbackRow, classId);
		if (num > 0)
		{
			if (!TownLocations.TryGetValue(num, out L1jTownGetbackLocation value) || value.Destinations.Count == 0)
			{
				throw Invalid($"getback row references unknown town id {num}");
			}
			l1jGetbackDestination = value.Destinations[PositiveModulo(townRoll, value.Destinations.Count)];
		}
		bool flag = !l1jGetbackDestination.IsRuntimeResolved;
		if (flag)
		{
			l1jGetbackDestination = InferTown(Fallback);
		}
		return new L1jGetbackRoute(InferTown(l1jGetbackDestination), flag ? (l1jGetbackRow.Note + "; unresolved main target -> runtime fallback") : l1jGetbackRow.Note, l1jGetbackRow.ScrollEscape, flag, num);
	}

	public bool TryResolveRestart(string sourceMapKey, out L1jGetbackRestartRow? route)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceMapKey, "sourceMapKey");
		route = Restart.FirstOrDefault((L1jGetbackRestartRow row) => row.AreaMapKeys.Contains<string>(sourceMapKey, StringComparer.Ordinal));
		if ((object)route == null)
		{
			return false;
		}
		L1jGetbackDestination destination = (route.Destination.IsRuntimeResolved ? route.Destination : Fallback);
		route = route with
		{
			Destination = InferTown(destination),
			Note = (route.Destination.IsRuntimeResolved ? route.Note : (route.Note + "; unresolved main target -> runtime fallback"))
		};
		return true;
	}

	private L1jGetbackDestination InferTown(L1jGetbackDestination destination)
	{
		if (!string.IsNullOrWhiteSpace(destination.TownKey))
		{
			return destination;
		}
		L1jGetbackDestination l1jGetbackDestination = (from candidate in TownLocations.Values.SelectMany((L1jTownGetbackLocation town) => town.Destinations)
			where candidate.MapId == destination.MapId && !string.IsNullOrWhiteSpace(candidate.TownKey)
			orderby Math.Abs(candidate.GameX - destination.GameX) + Math.Abs(candidate.GameY - destination.GameY)
			select candidate).FirstOrDefault();
		if ((object)l1jGetbackDestination != null)
		{
			return destination with
			{
				TownKey = l1jGetbackDestination.TownKey
			};
		}
		return destination;
	}

	private static int TownIdFor(L1jGetbackRow row, string? classId)
	{
		string text = classId?.Trim().ToLowerInvariant() ?? string.Empty;
		if (text == "elf" && row.ElfTownId > 0)
		{
			return row.ElfTownId;
		}
		bool flag = ((text == "dark" || text == "darkelf") ? true : false);
		if (flag && row.DarkElfTownId > 0)
		{
			return row.DarkElfTownId;
		}
		return row.DefaultTownId;
	}

	private static int PositiveModulo(int value, int count)
	{
		if (count <= 0)
		{
			throw Invalid("destination list cannot be empty");
		}
		int num = value % count;
		if (num >= 0)
		{
			return num;
		}
		return num + count;
	}

	private static IReadOnlyList<L1jGetbackDestination> ReadDestinations(JsonObject owner, string name)
	{
		List<L1jGetbackDestination> list = new List<L1jGetbackDestination>();
		foreach (JsonObject item in RequiredRows(owner, name))
		{
			list.Add(ReadDestination(item));
		}
		if (list.Count == 0)
		{
			throw Invalid(name + " cannot be empty");
		}
		return new ReadOnlyCollection<L1jGetbackDestination>(list);
	}

	private static L1jGetbackDestination ReadDestination(JsonObject row)
	{
		return new L1jGetbackDestination(RequiredInt(row, "gameX"), RequiredInt(row, "gameY"), RequiredInt(row, "mapId"), OptionalString(row, "mapKey"), OptionalInt(row, "localX"), OptionalInt(row, "localY"), OptionalDouble(row, "displayX"), OptionalDouble(row, "displayY"), OptionalString(row, "townKey"));
	}

	private static IReadOnlyList<JsonObject> RequiredRows(JsonObject owner, string name)
	{
		JsonArray obj = (owner[name] as JsonArray) ?? throw Invalid(name + " must be an array");
		List<JsonObject> list = new List<JsonObject>(obj.Count);
		foreach (JsonNode item in obj)
		{
			list.Add((item as JsonObject) ?? throw Invalid(name + " row must be an object"));
		}
		return list;
	}

	private static IReadOnlyList<string> ReadStrings(JsonObject owner, string name)
	{
		return new ReadOnlyCollection<string>(((owner[name] as JsonArray) ?? throw Invalid(name + " must be an array")).Select((JsonNode node) => node?.GetValue<string>() ?? throw Invalid(name + " contains null")).ToList());
	}

	private static JsonObject RequiredObject(JsonObject owner, string name)
	{
		return (owner[name] as JsonObject) ?? throw Invalid(name + " must be an object");
	}

	private static string RequiredString(JsonObject owner, string name)
	{
		return owner[name]?.GetValue<string>() ?? throw Invalid(name + " must be a string");
	}

	private static string? OptionalString(JsonObject owner, string name)
	{
		if (owner[name] != null)
		{
			return owner[name].GetValue<string>();
		}
		return null;
	}

	private static int RequiredInt(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be an integer")).GetValue<int>();
	}

	private static int? OptionalInt(JsonObject owner, string name)
	{
		if (owner[name] != null)
		{
			return owner[name].GetValue<int>();
		}
		return null;
	}

	private static double? OptionalDouble(JsonObject owner, string name)
	{
		if (owner[name] != null)
		{
			return owner[name].GetValue<double>();
		}
		return null;
	}

	private static bool RequiredBool(JsonObject owner, string name)
	{
		return (owner[name] ?? throw Invalid(name + " must be a boolean")).GetValue<bool>();
	}

	private static InvalidDataException Invalid(string message)
	{
		return new InvalidDataException("L1J_GETBACK: " + message);
	}
}
