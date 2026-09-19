using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PartySave
{
	private sealed class PartySaveData
	{
		public int Version { get; set; }

		public string LeaderKey { get; set; } = string.Empty;

		public PartyMemberSaveData[]? Members { get; set; }
	}

	private sealed class PartyMemberSaveData
	{
		public string CharacterKey { get; set; } = string.Empty;

		public string CharacterBlob { get; set; } = string.Empty;
	}

	public const int CurrentVersion = 1;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Capture(MercenaryParty party)
	{
		ArgumentNullException.ThrowIfNull(party, "party");
		return JsonSerializer.Serialize(new PartySaveData
		{
			Version = 1,
			LeaderKey = party.LeaderKey,
			Members = party.Members.Select((MercenaryContract member) => new PartyMemberSaveData
			{
				CharacterKey = member.CharacterKey,
				CharacterBlob = member.CharacterBlob
			}).ToArray()
		}, JsonOptions);
	}

	public static MercenaryParty Restore(IGameData data, string blob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(blob, "blob");
		PartySaveData partySaveData;
		try
		{
			partySaveData = JsonSerializer.Deserialize<PartySaveData>(blob, JsonOptions) ?? throw new InvalidDataException("Monster-card party save is empty.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Monster-card party save is not valid JSON.", innerException);
		}
		if (partySaveData.Version != 1)
		{
			throw new InvalidDataException($"Unsupported monster-card party save version {partySaveData.Version}.");
		}
		if (string.IsNullOrWhiteSpace(partySaveData.LeaderKey) || partySaveData.Members == null)
		{
			throw new InvalidDataException("Monster-card party save is missing required data.");
		}
		if (partySaveData.Members.Length > 7)
		{
			throw new InvalidDataException("Monster-card party exceeds the maximum capacity.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		MercenaryParty mercenaryParty = new MercenaryParty(partySaveData.LeaderKey);
		PartyMemberSaveData[] members = partySaveData.Members;
		foreach (PartyMemberSaveData partyMemberSaveData in members)
		{
			if (!MonsterCompanionRules.TryReadMobKey(partyMemberSaveData.CharacterKey, out string mobKey) || data.Mob(mobKey) == null || !hashSet.Add(partyMemberSaveData.CharacterKey) || !MercenaryParty.ValidCompanionBlob(data, partyMemberSaveData.CharacterKey, mobKey, partyMemberSaveData.CharacterBlob))
			{
				throw new InvalidDataException("Monster-card party contains an invalid or duplicate member.");
			}
			mercenaryParty.RestoreMember(partyMemberSaveData.CharacterKey, partyMemberSaveData.CharacterBlob);
		}
		return mercenaryParty;
	}
}
