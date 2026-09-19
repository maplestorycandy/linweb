using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly record struct ItemGainResult(bool Success, ItemGainFailure Failure, string RequestedItemKey, string ResolvedItemKey, ItemBlessing Blessing, long Quantity, long AttemptSequence, int Enhancement = 0, int ItemLevel = 0, IReadOnlyList<EquipmentAffixRoll>? Affixes = null)
{
	public static ItemGainResult Failed(ItemGainFailure failure, string requestedItemKey, long attemptSequence)
	{
		return new ItemGainResult(Success: false, failure, requestedItemKey, requestedItemKey, ItemBlessing.Normal, 0L, attemptSequence);
	}
}
