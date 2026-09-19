using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class PlayerAttackPriorityRules
{
	public const int MaximumSelections = 3;

	public static IReadOnlyList<PlayerAttackPriorityOption> Options { get; } = new PlayerAttackPriorityOption[4]
	{
		new PlayerAttackPriorityOption(PlayerAttackPriority.Nearest, "nearest", "最近的敵人"),
		new PlayerAttackPriorityOption(PlayerAttackPriority.Boss, "boss", "頭目優先"),
		new PlayerAttackPriorityOption(PlayerAttackPriority.Aggressive, "aggressive", "主動怪優先"),
		new PlayerAttackPriorityOption(PlayerAttackPriority.AttackingPlayer, "attacking_player", "正在攻擊自己的怪物優先")
	};

	public static string KeyOf(PlayerAttackPriority value)
	{
		return Options.First((PlayerAttackPriorityOption option) => option.Value == value).Key;
	}

	public static string LabelOf(PlayerAttackPriority value)
	{
		return Options.First((PlayerAttackPriorityOption option) => option.Value == value).Label;
	}

	public static bool TryParseKey(string? key, out PlayerAttackPriority value)
	{
		foreach (PlayerAttackPriorityOption option in Options)
		{
			if (string.Equals(option.Key, key, StringComparison.Ordinal))
			{
				value = option.Value;
				return true;
			}
		}
		value = PlayerAttackPriority.Nearest;
		return false;
	}

	public static PlayerAttackPriority[] Normalize(IEnumerable<string>? keys)
	{
		if (keys == null)
		{
			return Array.Empty<PlayerAttackPriority>();
		}
		List<PlayerAttackPriority> list = new List<PlayerAttackPriority>(3);
		foreach (string key in keys)
		{
			if (TryParseKey(key, out var value) && !list.Contains(value))
			{
				list.Add(value);
				if (list.Count == 3)
				{
					break;
				}
			}
		}
		return list.ToArray();
	}

	public static string[] KeysOf(IEnumerable<PlayerAttackPriority> values)
	{
		return values.Distinct().Take(3).Select(KeyOf)
			.ToArray();
	}

	public static string Serialize(IEnumerable<PlayerAttackPriority> values)
	{
		return string.Join(',', KeysOf(values));
	}

	public static bool TryParseSaved(string? serialized, out PlayerAttackPriority[] priorities)
	{
		if (string.IsNullOrEmpty(serialized))
		{
			priorities = Array.Empty<PlayerAttackPriority>();
			return true;
		}
		string[] array = serialized.Split(',');
		if (array.Length > 3 || array.Any(string.IsNullOrWhiteSpace))
		{
			priorities = Array.Empty<PlayerAttackPriority>();
			return false;
		}
		List<PlayerAttackPriority> list = new List<PlayerAttackPriority>(array.Length);
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if (!TryParseKey(array2[i], out var value) || list.Contains(value))
			{
				priorities = Array.Empty<PlayerAttackPriority>();
				return false;
			}
			list.Add(value);
		}
		priorities = list.ToArray();
		return true;
	}
}
