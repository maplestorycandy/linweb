using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public readonly record struct EquipmentChangeResult(bool Success, EquipmentChangeFailure Failure, string Slot, string ItemKey, EquipmentEligibilityFailure EligibilityFailure, IReadOnlyList<string> AutomaticallyUnequippedSlots)
{
	public static EquipmentChangeResult Changed(string slot, string itemKey, IEnumerable<string>? automaticallyUnequippedSlots = null)
	{
		return new EquipmentChangeResult(Success: true, EquipmentChangeFailure.None, slot, itemKey, EquipmentEligibilityFailure.None, automaticallyUnequippedSlots?.ToArray() ?? Array.Empty<string>());
	}

	public static EquipmentChangeResult Failed(EquipmentChangeFailure failure, string slot = "", string itemKey = "", EquipmentEligibilityFailure eligibilityFailure = EquipmentEligibilityFailure.None)
	{
		return new EquipmentChangeResult(Success: false, failure, slot, itemKey, eligibilityFailure, Array.Empty<string>());
	}
}
