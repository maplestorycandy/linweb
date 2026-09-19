using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class L1jDungeonRandomCatalog
{
	public const string TableName = "L1J_DUNGEON_RANDOM";

	public const int ExpectedRows = 10;

	public const int DestinationChoices = 5;

	private readonly IReadOnlyDictionary<L1jDungeonRandomCell, L1jDungeonRandomEntry> _bySource;

	public IReadOnlyCollection<L1jDungeonRandomEntry> Entries => new ReadOnlyCollection<L1jDungeonRandomEntry>(_bySource.Values.ToList());

	public double BarrierDurationSeconds { get; }

	private L1jDungeonRandomCatalog(IReadOnlyDictionary<L1jDungeonRandomCell, L1jDungeonRandomEntry> bySource, double barrierDurationSeconds)
	{
		_bySource = bySource;
		BarrierDurationSeconds = barrierDurationSeconds;
	}

	public bool TryChoose(int mapId, int gameX, int gameY, int choice, out L1jDungeonRandomCell destination, out int heading)
	{
		if ((choice < 0 || choice >= 5) ? true : false)
		{
			throw new ArgumentOutOfRangeException("choice");
		}
		if (_bySource.TryGetValue(new L1jDungeonRandomCell(mapId, gameX, gameY), out L1jDungeonRandomEntry value))
		{
			destination = value.Destinations[choice];
			heading = value.Heading;
			return true;
		}
		destination = default(L1jDungeonRandomCell);
		heading = 0;
		return false;
	}

	public static L1jDungeonRandomCatalog Load(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject jsonObject = (data.Table("L1J_DUNGEON_RANDOM") as JsonObject) ?? throw Invalid("table must be an object");
		JsonArray jsonArray = (jsonObject["rows"] as JsonArray) ?? throw Invalid("rows must be an array");
		double num = (double)RequiredInt(jsonObject, "barrierDurationMs") / 1000.0;
		if (num <= 0.0)
		{
			throw Invalid("barrier duration must be positive");
		}
		Dictionary<L1jDungeonRandomCell, L1jDungeonRandomEntry> dictionary = new Dictionary<L1jDungeonRandomCell, L1jDungeonRandomEntry>();
		int num2 = 0;
		foreach (JsonNode item in jsonArray)
		{
			JsonObject jsonObject2 = (item as JsonObject) ?? throw Invalid("every row must be an object");
			L1jDungeonRandomCell l1jDungeonRandomCell = ReadCell(RequiredObject(jsonObject2, "source"));
			JsonArray obj = (jsonObject2["destinations"] as JsonArray) ?? throw Invalid("destinations must be an array");
			if (obj.Count != 5)
			{
				throw Invalid($"source {l1jDungeonRandomCell} must have five destinations");
			}
			List<L1jDungeonRandomCell> list = obj.Select((JsonNode target) => ReadCell((target as JsonObject) ?? throw Invalid("every destination must be an object"))).ToList();
			L1jDungeonRandomEntry value = new L1jDungeonRandomEntry(l1jDungeonRandomCell, new ReadOnlyCollection<L1jDungeonRandomCell>(list), RequiredInt(jsonObject2, "heading"), RequiredString(jsonObject2, "note"));
			if (!dictionary.TryAdd(l1jDungeonRandomCell, value))
			{
				throw Invalid($"duplicate source {l1jDungeonRandomCell}");
			}
			num2 += list.Count;
		}
		JsonObject row = RequiredObject(jsonObject, "counts");
		if (jsonArray.Count != 10 || RequiredInt(row, "rows") != 10 || num2 != 50 || RequiredInt(row, "destinations") != num2)
		{
			throw Invalid("row or destination count does not match main");
		}
		return new L1jDungeonRandomCatalog(new ReadOnlyDictionary<L1jDungeonRandomCell, L1jDungeonRandomEntry>(dictionary), num);
	}

	private static L1jDungeonRandomCell ReadCell(JsonObject row)
	{
		return new L1jDungeonRandomCell(RequiredInt(row, "mapId"), RequiredInt(row, "gameX"), RequiredInt(row, "gameY"));
	}

	private static JsonObject RequiredObject(JsonObject row, string name)
	{
		return (row[name] as JsonObject) ?? throw Invalid(name + " must be an object");
	}

	private static int RequiredInt(JsonObject row, string name)
	{
		return (row[name] ?? throw Invalid(name + " must be an integer")).GetValue<int>();
	}

	private static string RequiredString(JsonObject row, string name)
	{
		return row[name]?.GetValue<string>() ?? throw Invalid(name + " must be a string");
	}

	private static InvalidDataException Invalid(string message)
	{
		return new InvalidDataException("L1J_DUNGEON_RANDOM: " + message);
	}
}
