using System;
using IdleLineage.Combat;

namespace IdleLineage.App;

public static class DayNight
{
	public const double PhaseHours = 2.0;

	public const double NightTiles = 1.0;

	public const double LampTiles = 5.0;

	public const double DarknessBandTiles = 1.0;

	public const double DarknessBandStep = 0.4;

	public const double MaxDarkness = 0.8;

	public const double OcclusionDarkness = 0.8;

	public const double HiddenDarknessBands = 2.0;

	public const double IsoAspectY = 2.0;

	private static readonly string[] CaveWords = new string[16]
	{
		"洞窟", "洞穴", "地下", "礦坑", "墓", "地牢", "巢穴", "祭壇", "神殿", "聖殿",
		"會議廳", "副本", "監獄", "倉庫", "遺跡", "塔"
	};

	private static double TilePixels => 48.0;

	public static double OcclusionFeatherPixels => TilePixels;

	public static double DarknessBandPixels => 1.0 * TilePixels;

	public static double VisionCutoffPixels(double clearRadiusPixels)
	{
		return clearRadiusPixels + 2.0 * DarknessBandPixels;
	}

	public static bool IsLit(Combatant? player, bool hasLamp)
	{
		if (!hasLamp)
		{
			return BehaviorBuffRules.IlluminatesDarkness(player);
		}
		return true;
	}

	public static bool IsDay(double unixSeconds)
	{
		return ((long)Math.Floor(unixSeconds / 3600.0 / 2.0) % 2 + 2) % 2 == 0;
	}

	public static bool IsCave(string mapName)
	{
		if (string.IsNullOrEmpty(mapName))
		{
			return false;
		}
		string[] caveWords = CaveWords;
		foreach (string value in caveWords)
		{
			if (mapName.Contains(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsDark(double unixSeconds, string mapName)
	{
		if (GameRateConfig.AlwaysDaylight)
		{
			return false;
		}
		if (!IsCave(mapName))
		{
			return !IsDay(unixSeconds);
		}
		return true;
	}

	public static double PhaseProgress(double unixSeconds)
	{
		double num = unixSeconds / 3600.0 / 2.0;
		return Math.Clamp(num - Math.Floor(num), 0.0, 1.0);
	}

	public static double VisionRadiusPixels(bool dark, bool lit)
	{
		if (!dark)
		{
			return double.PositiveInfinity;
		}
		return (lit ? 5.0 : 1.0) * TilePixels;
	}
}
