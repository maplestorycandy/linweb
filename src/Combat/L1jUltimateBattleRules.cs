using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdleLineage.Combat;

public static class L1jUltimateBattleRules
{
	public const int MinutesPerDay = 1440;

	public static int MinuteOfDay(int hhmm)
	{
		return hhmm / 100 * 60 + hhmm % 100;
	}

	public static bool IsOpenAt(L1jUbArena arena, int minuteOfDay)
	{
		int num = MinutesUntilStart(arena, minuteOfDay);
		if (num >= 0)
		{
			return num <= 5;
		}
		return false;
	}

	public static int MinutesUntilStart(L1jUbArena arena, int minuteOfDay)
	{
		ArgumentNullException.ThrowIfNull(arena, "arena");
		if (arena.OpenTimes.Count == 0)
		{
			return -1;
		}
		int num = int.MaxValue;
		foreach (int openTime in arena.OpenTimes)
		{
			int num2 = MinuteOfDay(openTime) - minuteOfDay;
			if (num2 < 0)
			{
				num2 += 1440;
			}
			num = Math.Min(num, num2);
		}
		return num;
	}

	public static L1jUbEntryFailure CheckEntry(L1jUbArena? arena, int level, string? classId, bool male, int minuteOfDay)
	{
		if ((object)arena == null)
		{
			return L1jUbEntryFailure.UnknownArena;
		}
		if (!IsOpenAt(arena, minuteOfDay))
		{
			return L1jUbEntryFailure.NotOpen;
		}
		if (level < arena.MinLevel)
		{
			return L1jUbEntryFailure.LevelTooLow;
		}
		if (level > arena.MaxLevel)
		{
			return L1jUbEntryFailure.LevelTooHigh;
		}
		string text = (classId ?? string.Empty).Trim();
		if (IsMainClass(text) && !arena.AllowedClasses.Contains(text))
		{
			return L1jUbEntryFailure.ClassNotAllowed;
		}
		if (male ? (!arena.AllowsMale) : (!arena.AllowsFemale))
		{
			return L1jUbEntryFailure.GenderNotAllowed;
		}
		return L1jUbEntryFailure.None;
	}

	private static bool IsMainClass(string classId)
	{
		switch (classId)
		{
		case "dragon":
		case "knight":
		case "dark":
		case "mage":
		case "royal":
		case "elf":
		case "illusion":
			return true;
		default:
			return false;
		}
	}

	public static int PickPattern(L1jUbArena arena, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(arena, "arena");
		ArgumentNullException.ThrowIfNull(random, "random");
		int[] array = arena.Patterns.Keys.Order().ToArray();
		if (array.Length == 0)
		{
			throw new InvalidDataException($"Arena {arena.UbId} has no spawn pattern.");
		}
		int num = Math.Clamp((int)(random.NextDouble() * (double)array.Length), 0, array.Length - 1);
		return array[num];
	}

	public static IReadOnlyList<L1jUbWaveGroup> Wave(L1jUbArena arena, int pattern, int round)
	{
		ArgumentNullException.ThrowIfNull(arena, "arena");
		if (!arena.Patterns.TryGetValue(pattern, out IReadOnlyDictionary<int, IReadOnlyList<L1jUbWaveGroup>> value) || !value.TryGetValue(round, out var value2))
		{
			return Array.Empty<L1jUbWaveGroup>();
		}
		return value2;
	}

	public static string FailureText(L1jUbEntryFailure failure)
	{
		return failure switch
		{
			L1jUbEntryFailure.NotOpen => "現在不是入場時間（開賽前 5 分鐘才開門）。", 
			L1jUbEntryFailure.LevelTooLow => "等級不足，這座競技場不收。", 
			L1jUbEntryFailure.LevelTooHigh => "等級過高，這座競技場不收。", 
			L1jUbEntryFailure.ClassNotAllowed => "這座競技場不開放你的職業。", 
			L1jUbEntryFailure.GenderNotAllowed => "這座競技場不開放你的性別。", 
			L1jUbEntryFailure.UnknownArena => "找不到這座競技場。", 
			_ => "無法入場。", 
		};
	}
}
