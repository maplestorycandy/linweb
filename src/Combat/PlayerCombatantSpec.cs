using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleLineage.Combat;

public sealed record PlayerCombatantSpec(string Key, string DisplayName, string ClassId, int Level)
{
	public string Avatar { get; init; } = "";

	public int BornSeq { get; init; }

	public WorldPoint Position { get; init; } = WorldPoint.Zero;

	public IReadOnlyDictionary<string, int> Allocations { get; init; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

	public IReadOnlyDictionary<string, int> LevelStatBonuses { get; init; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

	public IReadOnlyDictionary<string, int> ElixirBonuses { get; init; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

	public int ElixirStatus { get; init; }

	public int UnspentElixirStatPoints { get; init; }

	public IReadOnlyDictionary<string, string> Equipment { get; init; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

	public IReadOnlyDictionary<string, ItemStack> EquippedItems { get; init; } = new ReadOnlyDictionary<string, ItemStack>(new Dictionary<string, ItemStack>());

	public IReadOnlyDictionary<string, long> Inventory { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());

	public IReadOnlyList<ItemStack> InventoryStacks { get; init; } = Array.Empty<ItemStack>();

	public long ItemUidSequence { get; init; }

	public double CurrentExperience { get; init; }

	public long CurrentGold { get; init; }

	public double? CurrentHp { get; init; }

	public double? CurrentMp { get; init; }
}
