using System;

namespace IdleLineage.Combat;

public static class ActionFrameRules
{
	public const double MillisecondsPerFrame = 40.0;

	public const double FramesPerSecond = 25.0;

	public static double SecondsForFrames(double frames)
	{
		return frames * 40.0 / 1000.0;
	}

	public static double OneShotSpeedScale(double nativeSeconds, double cycleSeconds, double speedRatio = 1.0)
	{
		double num = ((double.IsFinite(speedRatio) && speedRatio > 0.0) ? speedRatio : 1.0);
		if (cycleSeconds > 0.0 && nativeSeconds > cycleSeconds * num)
		{
			num = nativeSeconds / cycleSeconds;
		}
		if (!(num > 0.0))
		{
			return 1.0;
		}
		return num;
	}

	public static double AttacksPerMinuteForFrames(double frames)
	{
		if (!(frames > 0.0))
		{
			return 0.0;
		}
		return 60000.0 / (frames * 40.0);
	}

	public static int FixedStepsForFrames(double frames)
	{
		return Math.Max(1, (int)Math.Round(frames * 40.0 / 1000.0 / (1.0 / 60.0), MidpointRounding.AwayFromZero));
	}
}
