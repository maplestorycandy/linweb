using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class MagicDollCatalog
{
	public IReadOnlyDictionary<string, MagicDollDefinition> ByItemKey { get; }

	public string CrystalItemKey { get; }

	public IReadOnlyList<(string ItemKey, int Weight)> BagPool { get; }

	public IReadOnlyList<(string ItemKey, int Count)> ArkaMaterials { get; }

	public string ArkaOutputKey { get; }

	internal MagicDollCatalog(IReadOnlyDictionary<string, MagicDollDefinition> byItemKey, string crystalItemKey, IReadOnlyList<(string ItemKey, int Weight)> bagPool, IReadOnlyList<(string ItemKey, int Count)> arkaMaterials, string arkaOutputKey)
	{
		ByItemKey = byItemKey;
		CrystalItemKey = crystalItemKey;
		BagPool = bagPool;
		ArkaMaterials = arkaMaterials;
		ArkaOutputKey = arkaOutputKey;
	}
}
