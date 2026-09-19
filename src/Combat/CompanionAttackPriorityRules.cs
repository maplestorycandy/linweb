using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class CompanionAttackPriorityRules
{
	public const int MaximumSelections = 3;

	public static IReadOnlyList<CompanionAttackPriorityOption> Options { get; } = new CompanionAttackPriorityOption[5]
	{
		new CompanionAttackPriorityOption(CompanionAttackPriority.AttackingSelf, "attacking_self", "正在攻擊自己的優先"),
		new CompanionAttackPriorityOption(CompanionAttackPriority.AttackingPlayer, "attacking_player", "正在攻擊玩家的優先"),
		new CompanionAttackPriorityOption(CompanionAttackPriority.Nearest, "nearest", "最近的敵人"),
		new CompanionAttackPriorityOption(CompanionAttackPriority.Boss, "boss", "頭目優先"),
		new CompanionAttackPriorityOption(CompanionAttackPriority.Aggressive, "aggressive", "主動怪優先")
	};

	public static string KeyOf(CompanionAttackPriority value)
	{
		return Options.First((CompanionAttackPriorityOption option) => option.Value == value).Key;
	}

	public static bool TryParseKey(string? key, out CompanionAttackPriority value)
	{
		foreach (CompanionAttackPriorityOption option in Options)
		{
			if (string.Equals(option.Key, key, StringComparison.Ordinal))
			{
				value = option.Value;
				return true;
			}
		}
		value = CompanionAttackPriority.AttackingSelf;
		return false;
	}

	public static CompanionAttackPriority[] Normalize(IEnumerable<string>? keys)
	{
		if (keys == null)
		{
			return Array.Empty<CompanionAttackPriority>();
		}
		List<CompanionAttackPriority> list = new List<CompanionAttackPriority>(3);
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

	public static string[] KeysOf(IEnumerable<CompanionAttackPriority> values)
	{
		return values.Distinct().Take(3).Select(KeyOf)
			.ToArray();
	}

	public static string Serialize(IEnumerable<CompanionAttackPriority> values)
	{
		return string.Join(',', KeysOf(values));
	}

	public static bool TryParseSaved(string? serialized, out CompanionAttackPriority[] priorities)
	{
		if (string.IsNullOrEmpty(serialized))
		{
			priorities = Array.Empty<CompanionAttackPriority>();
			return true;
		}
		string[] array = serialized.Split(',');
		if (array.Length > 3 || array.Any(string.IsNullOrWhiteSpace))
		{
			priorities = Array.Empty<CompanionAttackPriority>();
			return false;
		}
		List<CompanionAttackPriority> list = new List<CompanionAttackPriority>(array.Length);
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if (!TryParseKey(array2[i], out var value) || list.Contains(value))
			{
				priorities = Array.Empty<CompanionAttackPriority>();
				return false;
			}
			list.Add(value);
		}
		priorities = list.ToArray();
		return true;
	}
}
