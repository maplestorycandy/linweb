using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class SetSnapshot
{
	public IReadOnlyDictionary<string, int> PieceCounts { get; }

	public IReadOnlyList<SetTierState> Tiers { get; }

	internal SetSnapshot(IReadOnlyDictionary<string, int> pieceCounts, IReadOnlyList<SetTierState> tiers)
	{
		PieceCounts = pieceCounts;
		Tiers = tiers;
	}

	public int Count(string code)
	{
		return PieceCounts.GetValueOrDefault(code);
	}

	public bool Active(string code, int requiredPieces)
	{
		return Count(code) >= requiredPieces;
	}
}
