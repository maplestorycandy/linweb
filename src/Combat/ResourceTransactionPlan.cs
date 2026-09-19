using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleLineage.Combat;

public sealed record ResourceTransactionPlan
{
	public long GoldCost { get; init; }

	public long GoldReward { get; init; }

	public IReadOnlyDictionary<string, long> ItemCosts { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());

	public IReadOnlyDictionary<string, long> ItemRewards { get; init; } = new ReadOnlyDictionary<string, long>(new Dictionary<string, long>());
}
