using System.Collections.Generic;

namespace IdleLineage.Data;

public static class AdenSewerCatalog
{
	public const string MapKey = "aden_sewer";

	public const int SourceMapId = 301;

	public static AdenSewerSurfaceEntrance SurfaceEntrance { get; } = new AdenSewerSurfaceEntrance(34147, 33384, 34146, 33384, "aden_sewer", "aden_sewer_surface_arrival", "aden_sewer_surface_exit");

	public static IReadOnlyList<string> MobKeys { get; } = new string[3] { "l1j_45116", "l1j_45222", "l1j_45296" };
}
