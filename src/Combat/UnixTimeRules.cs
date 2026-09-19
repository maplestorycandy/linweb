using System;

namespace IdleLineage.Combat;

public static class UnixTimeRules
{
	public const long HourMilliseconds = 3600000L;

	public static void Validate(long unixMilliseconds)
	{
		if (unixMilliseconds < 0)
		{
			throw new ArgumentOutOfRangeException("unixMilliseconds");
		}
	}

	public static long NextStrictHourlyBoundary(long unixMilliseconds)
	{
		Validate(unixMilliseconds);
		checked
		{
			return (unchecked(unixMilliseconds / 3600000) + 1) * 3600000;
		}
	}

	public static bool IsHourlyBoundary(long unixMilliseconds)
	{
		if (unixMilliseconds >= 3600000)
		{
			return unixMilliseconds % 3600000 == 0;
		}
		return false;
	}
}
