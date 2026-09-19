using System;

namespace IdleLineage.Combat;

public readonly record struct L1jWorldObjectVisualBounds(double Left, double Top, double Right, double Bottom)
{
	public double Width => Math.Max(0.0, Right - Left);

	public double Height => Math.Max(0.0, Bottom - Top);
}
