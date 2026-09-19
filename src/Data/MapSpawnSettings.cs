using System.IO;

namespace IdleLineage.Data;

public sealed record MapSpawnSettings(int MinimumHiddenDistanceCells, int MaximumHiddenDistanceCells, int MaximumLivingNormalMobs, int NormalRetirementDistanceCells, int VisibleWorldWidthPixels, int VisibleWorldHeightPixels, int OffscreenMarginPixels)
{
	public static MapSpawnSettings Default { get; } = new MapSpawnSettings(16, 32, 48, 48, 1400, 600, 160);

	public const int DefaultVisibleWorldWidthPixels = 1400;

	public const int DefaultVisibleWorldHeightPixels = 600;

	public void Validate()
	{
		if (MinimumHiddenDistanceCells < 0 || MaximumHiddenDistanceCells < MinimumHiddenDistanceCells)
		{
			throw new InvalidDataException("Normal spawn distance range is invalid.");
		}
		if (MaximumLivingNormalMobs <= 0)
		{
			throw new InvalidDataException("Maximum living normal mob count must be positive.");
		}
		if (NormalRetirementDistanceCells <= MaximumHiddenDistanceCells)
		{
			throw new InvalidDataException("Normal mob retirement distance must exceed the maximum spawn distance.");
		}
		if (VisibleWorldWidthPixels <= 0 || VisibleWorldHeightPixels <= 0)
		{
			throw new InvalidDataException("Visible world size must be positive.");
		}
		if (OffscreenMarginPixels < 0)
		{
			throw new InvalidDataException("Offscreen spawn margin cannot be negative.");
		}
	}
}
