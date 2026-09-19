using System.Collections.Generic;

namespace IdleLineage.Data;

public static class ThebesMapCatalog
{
	public const string DesertMapKey = "thebes_desert";

	public const string PyramidMapKey = "thebes_pyramid";

	public const string TempleMapKey = "thebes_temple";

	public const string DesertRiftArrivalLandmarkId = "thebes_desert_rift_arrival";

	public const string DesertPyramidEntranceLandmarkId = "thebes_desert_pyramid_entrance";

	public const string PyramidSurfaceExitLandmarkId = "thebes_pyramid_surface_exit";

	public const string PyramidSurfaceArrivalLandmarkId = "thebes_pyramid_surface_arrival";

	public const string GateInteractionLandmarkId = "thebes_osiris_gate_interaction";

	public const string TempleArrivalLandmarkId = "thebes_temple_arrival";

	public const string TempleExitLandmarkId = "thebes_temple_exit";

	public const int GatekeeperSpriteGfx = 6992;

	public static IReadOnlyList<ThebesMapDefinition> Maps { get; } = new ThebesMapDefinition[3]
	{
		new ThebesMapDefinition("thebes_desert", 780, "底比斯 沙漠"),
		new ThebesMapDefinition("thebes_pyramid", 781, "底比斯 金字塔內部"),
		new ThebesMapDefinition("thebes_temple", 782, "底比斯 歐西里斯祭壇")
	};

	public static IReadOnlyList<ThebesPortalLink> PortalLinks { get; } = new ThebesPortalLink[4]
	{
		new ThebesPortalLink("thebes_desert", "thebes_desert_pyramid_entrance", "thebes_pyramid", "thebes_pyramid_surface_arrival"),
		new ThebesPortalLink("thebes_pyramid", "thebes_pyramid_surface_exit", "thebes_desert", "thebes_desert_pyramid_entrance"),
		new ThebesPortalLink("thebes_pyramid", "thebes_osiris_gate_interaction", "thebes_temple", "thebes_temple_arrival"),
		new ThebesPortalLink("thebes_temple", "thebes_temple_exit", "thebes_pyramid", "thebes_osiris_gate_interaction")
	};

	public static ThebesGatekeeperDefinition Gatekeeper { get; } = new ThebesGatekeeperDefinition("底比斯 歐西里斯祭壇守門人", 6992, 32997, 32747, "thebes_osiris_gate_interaction", "item_thebes_altar_key", "thebes_temple", "thebes_temple_arrival");
}
