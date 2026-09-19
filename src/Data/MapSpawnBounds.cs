namespace IdleLineage.Data;

public readonly record struct MapSpawnBounds(int MinimumX, int MinimumY, int MaximumX, int MaximumY)
{
	public int Width => checked(MaximumX - MinimumX + 1);

	public int Height => checked(MaximumY - MinimumY + 1);
}
