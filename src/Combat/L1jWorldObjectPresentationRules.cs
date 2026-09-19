using System;

namespace IdleLineage.Combat;

public static class L1jWorldObjectPresentationRules
{
	public const string FieldObjectImpl = "L1FieldObject";

	public const double ExpandedFootprintMinimumWidth = 96.0;

	public static double DepthAnchorY(double footY, bool staticSprite, L1jWorldObjectVisualBounds bounds)
	{
		if (!staticSprite)
		{
			return footY;
		}
		return footY + Math.Max(0.0, bounds.Bottom);
	}

	public static bool UsesExpandedSolidFootprint(string implementation, bool staticSprite, L1jWorldObjectVisualBounds bounds)
	{
		if (staticSprite && string.Equals(implementation, "L1FieldObject", StringComparison.Ordinal) && bounds.Width >= 96.0)
		{
			return bounds.Height > 0.0;
		}
		return false;
	}

	public static bool ContainsSolidPoint(L1jWorldObjectVisualBounds bounds, double relativeX, double relativeY)
	{
		if (bounds.Width <= 0.0 || bounds.Height <= 0.0)
		{
			return false;
		}
		double num = Math.Max(bounds.Top, bounds.Bottom - bounds.Width * 0.5);
		double num2 = bounds.Bottom - num;
		if (num2 <= 0.0)
		{
			return false;
		}
		double num3 = bounds.Width * 0.5;
		double num4 = num2 * 0.5;
		double num5 = (bounds.Left + bounds.Right) * 0.5;
		double num6 = (num + bounds.Bottom) * 0.5;
		double num7 = (relativeX - num5) / num3;
		double num8 = (relativeY - num6) / num4;
		return num7 * num7 + num8 * num8 <= 1.0;
	}

	public static int CandidateCellRadius(L1jWorldObjectVisualBounds bounds)
	{
		return Math.Max(1, (int)Math.Ceiling(bounds.Width / 48.0 + bounds.Height / 24.0) + 2);
	}
}
