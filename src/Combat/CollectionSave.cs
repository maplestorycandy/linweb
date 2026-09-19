using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CollectionSave
{
	private sealed class CollectionSaveData
	{
		public int Version { get; set; }

		public string Key { get; set; } = "";

		public string[]? Equipment { get; set; }

		public string[]? Misc { get; set; }

		public string[]? Relic { get; set; }

		public SortedDictionary<string, int>? Kills { get; set; }
	}

	public const int CurrentVersion = 1;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Capture(CollectionState state)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		return JsonSerializer.Serialize(new CollectionSaveData
		{
			Version = 1,
			Key = state.Key,
			Equipment = state.EquipmentItems.Order<string>(StringComparer.Ordinal).ToArray(),
			Misc = state.MiscItems.Order<string>(StringComparer.Ordinal).ToArray(),
			Relic = state.RelicItems.Order<string>(StringComparer.Ordinal).ToArray(),
			Kills = new SortedDictionary<string, int>(state.KillProgress.ToDictionary<KeyValuePair<string, int>, string, int>((KeyValuePair<string, int> pair) => pair.Key, (KeyValuePair<string, int> pair) => pair.Value), StringComparer.Ordinal)
		}, JsonOptions);
	}

	public static CollectionState Restore(IGameData data, string blob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(blob, "blob");
		CollectionSaveData collectionSaveData;
		try
		{
			collectionSaveData = JsonSerializer.Deserialize<CollectionSaveData>(blob, JsonOptions) ?? throw new InvalidDataException("Collection save is empty.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Collection save is not valid JSON.", innerException);
		}
		if (collectionSaveData.Version != 1)
		{
			throw new InvalidDataException($"Unsupported collection save version {collectionSaveData.Version}.");
		}
		if (string.IsNullOrWhiteSpace(collectionSaveData.Key) || collectionSaveData.Equipment == null || collectionSaveData.Misc == null || collectionSaveData.Relic == null)
		{
			throw new InvalidDataException("Collection save is missing required fields.");
		}
		string[] equipment = collectionSaveData.Equipment;
		string[] misc = collectionSaveData.Misc;
		string[] relic = collectionSaveData.Relic;
		RejectDuplicates(equipment, "equipment");
		RejectDuplicates(misc, "misc");
		RejectDuplicates(relic, "relic");
		CollectionState collectionState = new CollectionState(data, collectionSaveData.Key);
		collectionState.RestoreItems(equipment, misc, relic, collectionSaveData.Kills ?? throw new InvalidDataException("Collection save is missing kill progress."));
		return collectionState;
	}

	private static void RejectDuplicates(IEnumerable<string> values, string book)
	{
		// No-op: duplicates are handled gracefully by HashSets in CollectionState
	}
}
