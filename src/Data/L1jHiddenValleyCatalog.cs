using System;

namespace IdleLineage.Data;

public static class L1jHiddenValleyCatalog
{
	public const int LevelExitMinimum = 13;

	public const int LevelExitMapId = 4;

	public const int LevelExitGameX = 33088;

	public const int LevelExitGameY = 33392;

	public const int LevelExitHeading = 4;

	public static int MapId => 2005;

	public static string MapKey => "l1j_map_2005";

	public static bool GrantsRestartRefill(int destinationMapId)
	{
		return destinationMapId == MapId;
	}

	public static bool TriggersLevelExit(string mapKey, int level)
	{
		if (string.Equals(mapKey, MapKey, StringComparison.Ordinal))
		{
			return level >= 13;
		}
		return false;
	}
}
