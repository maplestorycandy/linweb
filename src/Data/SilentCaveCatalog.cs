namespace IdleLineage.Data;

public static class SilentCaveCatalog
{
	public const string MapKey = "silent_outer";

	public const string TownKey = "town_silent";

	public const int SourceMapId = 304;

	public const string VillageLandmarkId = "silent_cave_village";

	public const string OuterBridgeLandmarkId = "silent_cave_outer_bridge";

	public const string TownBridgeLandmarkId = "silent_cave_town_bridge";

	public static SilentCaveSurfaceEntrance SurfaceEntrance { get; } = new SilentCaveSurfaceEntrance(33476, 32347, 33476, 32348, "silent_outer", "silent_cave_surface_arrival", "silent_cave_surface_exit");
}
