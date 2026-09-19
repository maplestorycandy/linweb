using System;
using System.Collections.Generic;

namespace IdleLineage.Data;

public static class MapEntryRequirementRetirement
{
	private static readonly IReadOnlySet<string> QuestRetiredMaps = new HashSet<string>(StringComparer.Ordinal) { "demon_temple", "town_flame_audience" };

	public static bool DropsQuestRequirement(string mapKey)
	{
		if (!string.IsNullOrEmpty(mapKey))
		{
			return QuestRetiredMaps.Contains(mapKey);
		}
		return false;
	}
}
