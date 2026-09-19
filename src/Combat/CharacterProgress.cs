using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class CharacterProgress
{
	public long ItemGainAttemptSequence { get; set; }

	public Dictionary<string, int> QuestSteps { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

	public Dictionary<string, int> QuestKillCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

	public CollectionState? Collections { get; set; }

	public HashSet<string> QuestFlags { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	public HashSet<string> DefeatedBossKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	public List<TeleportMemoryLocation> TeleportMemories { get; set; } = new List<TeleportMemoryLocation>();

	public long TeleportMemorySequence { get; set; }

	public CharacterProgress Copy()
	{
		Validate();
		return new CharacterProgress
		{
			ItemGainAttemptSequence = ItemGainAttemptSequence,
			QuestSteps = new Dictionary<string, int>(QuestSteps, StringComparer.Ordinal),
			QuestKillCounts = new Dictionary<string, int>(QuestKillCounts, StringComparer.Ordinal),
			Collections = Collections,
			QuestFlags = new HashSet<string>(QuestFlags, StringComparer.Ordinal),
			DefeatedBossKeys = new HashSet<string>(DefeatedBossKeys, StringComparer.Ordinal),
			TeleportMemories = TeleportMemories.Select((TeleportMemoryLocation location) => location with {}).ToList(),
			TeleportMemorySequence = TeleportMemorySequence
		};
	}

	public void Validate()
	{
		if (ItemGainAttemptSequence < 0)
		{
			throw new InvalidDataException("Character item gain attempt sequence cannot be negative.");
		}
		if (QuestSteps == null)
		{
			throw new InvalidDataException("Character quest steps cannot be null.");
		}
		string key;
		int value;
		foreach (KeyValuePair<string, int> questStep in QuestSteps)
		{
			questStep.Deconstruct(out key, out value);
			string value2 = key;
			int num = value;
			bool flag = string.IsNullOrWhiteSpace(value2);
			if (!flag)
			{
				bool flag2 = ((num < 1 || num > 255) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Character quest steps contain an invalid entry.");
			}
		}
		if (QuestKillCounts == null)
		{
			throw new InvalidDataException("Character quest kill counts cannot be null.");
		}
		foreach (KeyValuePair<string, int> questKillCount in QuestKillCounts)
		{
			questKillCount.Deconstruct(out key, out value);
			string value3 = key;
			int num2 = value;
			if (string.IsNullOrWhiteSpace(value3) || num2 <= 0)
			{
				throw new InvalidDataException("Character quest kill counts contain an invalid entry.");
			}
		}
		ValidateFlags(QuestFlags, "QuestFlags");
		ValidateFlags(DefeatedBossKeys, "DefeatedBossKeys");
		if (TeleportMemories == null)
		{
			throw new InvalidDataException("Character teleport memories cannot be null.");
		}
		if (TeleportMemories.Count > 20)
		{
			throw new InvalidDataException("Character teleport memories exceed the permanent maximum.");
		}
		if (TeleportMemorySequence < 0)
		{
			throw new InvalidDataException("Character teleport memory sequence cannot be negative.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (TeleportMemoryLocation teleportMemory in TeleportMemories)
		{
			if ((object)teleportMemory == null || string.IsNullOrWhiteSpace(teleportMemory.Id) || string.IsNullOrWhiteSpace(teleportMemory.Name) || string.IsNullOrWhiteSpace(teleportMemory.MapKey) || !double.IsFinite(teleportMemory.WorldX) || !double.IsFinite(teleportMemory.WorldY) || !hashSet.Add(teleportMemory.Id) || !hashSet2.Add(teleportMemory.Name.Trim()))
			{
				throw new InvalidDataException("Character teleport memories contain an invalid entry.");
			}
		}
	}

	private static void ValidateFlags(IEnumerable<string>? flags, string name)
	{
		if (flags == null)
		{
			throw new InvalidDataException("Character progress " + name + " cannot be null.");
		}
		if (flags.Any(string.IsNullOrWhiteSpace))
		{
			throw new InvalidDataException("Character progress " + name + " cannot contain an empty key.");
		}
	}
}
