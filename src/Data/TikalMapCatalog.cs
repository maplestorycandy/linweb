using System.Collections.Generic;

namespace IdleLineage.Data;

public static class TikalMapCatalog
{
	public const string AreaMapKey = "tikal_area";

	public const string AltarMapKey = "tikal_altar";

	public const string RiftArrivalLandmarkId = "tikal_rift_arrival";

	public const string AltarGateInteractionLandmarkId = "tikal_area_altar_gate_interaction";

	public const string AltarArrivalLandmarkId = "tikal_altar_arrival";

	public const string AltarExitLandmarkId = "tikal_altar_exit";

	public const int GatekeeperSpriteGfx = 7263;

	public static IReadOnlyList<TikalMapDefinition> Maps { get; } = new TikalMapDefinition[2]
	{
		new TikalMapDefinition("tikal_area", 783, "提卡爾神廟地區"),
		new TikalMapDefinition("tikal_altar", 784, "提卡爾 庫庫爾坎祭壇")
	};

	public static TikalGatekeeperDefinition Gatekeeper { get; } = new TikalGatekeeperDefinition("庫庫爾坎祭壇管理員", 7263, 33256, 32665, "tikal_area_altar_gate_interaction", "item_tikal_altar_key", "tikal_altar", "tikal_altar_arrival");
}
