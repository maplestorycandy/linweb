using Godot;

namespace IdleLineage.App;

public static class Depth
{
	private const int MaxDepth = 1400;

	private static double _scale = 1.0;

	public static void Configure(double worldHeight)
	{
		_scale = ((worldHeight > 1400.0) ? (1400.0 / worldHeight) : 1.0);
	}

	public static int Of(double worldY)
	{
		return Mathf.Clamp((int)(worldY * _scale), -1400, 1400);
	}

	public static int Of(double worldY, int layer)
	{
		return Mathf.Clamp(Of(worldY) + layer, -1400, 1400);
	}
}
