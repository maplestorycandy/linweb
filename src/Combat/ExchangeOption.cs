using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace IdleLineage.Combat;

public sealed record ExchangeOption
{
	public required string Id { get; init; }

	public required string NpcId { get; init; }

	public required string RewardItemKey { get; init; }

	public long RewardQuantity { get; init; } = 1L;

	public long GoldCost { get; init; }

	public IReadOnlyDictionary<string, long> ItemCosts { get; init; } = EmptyCosts;

	public ItemGainOptions GainOptions { get; init; } = new ItemGainOptions(ItemGainSource.Generic, null, false, false, false, 0, EquipmentAffixDropGrade.Normal);

	private static readonly IReadOnlyDictionary<string, long> EmptyCosts = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>(StringComparer.Ordinal));

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ExchangeOption(ExchangeOption original)
	{
		Id = original.Id;
		NpcId = original.NpcId;
		RewardItemKey = original.RewardItemKey;
		RewardQuantity = original.RewardQuantity;
		GoldCost = original.GoldCost;
		ItemCosts = original.ItemCosts;
		GainOptions = original.GainOptions;
	}
}
