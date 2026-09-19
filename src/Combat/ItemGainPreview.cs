using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly record struct ItemGainPreview(string RequestedItemKey, string ResolvedItemKey, ItemBlessing Blessing, double BlessingChance, bool BlessingEligible, bool UsesCommittedRoll, int Enhancement = 0, int ItemLevel = 0, IReadOnlyList<EquipmentAffixRoll>? Affixes = null);
