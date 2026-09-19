namespace IdleLineage.Data;

public static class MapOcclusionDepthRules
{
	public static bool ActorOverlapsVisual(in MapOcclusionGroup group, double displayScale, double footX, double footY, double visibleHalfWidth, double visibleHeight)
	{
		if (!ValidActorGeometry(displayScale, footX, footY, visibleHalfWidth, visibleHeight))
		{
			return false;
		}
		double num = (double)group.PixelX * displayScale;
		double num2 = (double)(group.PixelX + group.PixelWidth) * displayScale;
		double num3 = (double)group.PixelY * displayScale;
		double num4 = (double)(group.PixelY + group.PixelHeight) * displayScale;
		if (footX + visibleHalfWidth >= num && footX - visibleHalfWidth <= num2 && footY >= num3)
		{
			return footY - visibleHeight <= num4;
		}
		return false;
	}

	public static bool ActorCrossesBaseFromBelow(in MapOcclusionGroup group, double displayScale, double footX, double footY, double visibleHalfWidth, double visibleHeight)
	{
		if (!ValidActorGeometry(displayScale, footX, footY, visibleHalfWidth, visibleHeight))
		{
			return false;
		}
		double num = (double)group.PixelX * displayScale;
		double num2 = (double)(group.PixelX + group.PixelWidth) * displayScale;
		double num3 = (double)(group.PixelY + group.PixelHeight) * displayScale;
		if (footX + visibleHalfWidth >= num && footX - visibleHalfWidth <= num2 && footY > num3)
		{
			return footY - visibleHeight < num3;
		}
		return false;
	}

	private static bool ValidActorGeometry(double displayScale, double footX, double footY, double visibleHalfWidth, double visibleHeight)
	{
		if (double.IsFinite(displayScale) && double.IsFinite(footX) && double.IsFinite(footY) && double.IsFinite(visibleHalfWidth) && double.IsFinite(visibleHeight) && displayScale > 0.0 && visibleHalfWidth >= 0.0)
		{
			return visibleHeight > 0.0;
		}
		return false;
	}
}
