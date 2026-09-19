using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record HostilePlayerTemplate(string RosterId, string DisplayName, string ClassId, int Level)
{
	public string Avatar { get; init; } = string.Empty;

	public IReadOnlyDictionary<string, int> Allocations { get; init; } = new Dictionary<string, int>();

	public IReadOnlyDictionary<string, int> LevelStatBonuses { get; init; } = new Dictionary<string, int>();

	public IReadOnlyDictionary<string, ItemStack> EquippedItems { get; init; } = new Dictionary<string, ItemStack>();

	public IReadOnlyList<string> LearnedSkills { get; init; } = Array.Empty<string>();
}
