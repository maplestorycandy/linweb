using System;
using System.Collections.Generic;

namespace IdleLineage.Data;

public static class ShadowTempleCatalog
{
	public const string EntranceMapKey = "shadow_temple";

	public const string Floor2MapKey = "shadow_temple_2f";

	public const string Floor3MapKey = "shadow_temple_3f";

	public const string Floor4MapKey = "shadow_temple_4f";

	public const string SnowfieldMapKey = "mainland_south";

	public const string SnowfieldPortalLandmarkId = "shadow_temple_portal";

	public const string SnowfieldPortalArrivalLandmarkId = "shadow_temple_portal_arrival";

	public const string PortalSpriteDirectory = "res://assets/props/portal";

	public const string PortalSpritePrefix = "twisted_space";

	public const int PortalFrameCount = 20;

	public static IReadOnlyList<ShadowTempleFloor> Floors { get; } = new ShadowTempleFloor[4]
	{
		new ShadowTempleFloor("shadow_temple", 521, "暗影神殿外圍"),
		new ShadowTempleFloor("shadow_temple_2f", 522, "暗影神殿1樓"),
		new ShadowTempleFloor("shadow_temple_3f", 523, "暗影神殿2樓"),
		new ShadowTempleFloor("shadow_temple_4f", 524, "暗影神殿3樓")
	};

	public static IReadOnlyList<ShadowTempleStairLink> StairLinks { get; } = new ShadowTempleStairLink[8]
	{
		new ShadowTempleStairLink("mainland_south", "shadow_temple_portal", "shadow_temple", "shadow_temple_1f_arrival"),
		new ShadowTempleStairLink("shadow_temple", "shadow_temple_1f_gate", "mainland_south", "shadow_temple_portal_arrival"),
		new ShadowTempleStairLink("shadow_temple", "shadow_temple_1f_stairs_down", "shadow_temple_2f", "shadow_temple_2f_arrival_from_1f"),
		new ShadowTempleStairLink("shadow_temple_2f", "shadow_temple_2f_stairs_up", "shadow_temple", "shadow_temple_1f_arrival_from_2f"),
		new ShadowTempleStairLink("shadow_temple_2f", "shadow_temple_2f_stairs_down", "shadow_temple_3f", "shadow_temple_3f_arrival_from_2f"),
		new ShadowTempleStairLink("shadow_temple_3f", "shadow_temple_3f_stairs_up", "shadow_temple_2f", "shadow_temple_2f_arrival_from_3f"),
		new ShadowTempleStairLink("shadow_temple_3f", "shadow_temple_3f_stairs_down", "shadow_temple_4f", "shadow_temple_4f_arrival_from_3f"),
		new ShadowTempleStairLink("shadow_temple_4f", "shadow_temple_4f_stairs_up", "shadow_temple_3f", "shadow_temple_3f_arrival_from_4f")
	};

	public static IReadOnlyList<string> WalkOnlyFloors { get; } = new string[3] { "shadow_temple_2f", "shadow_temple_3f", "shadow_temple_4f" };

	public static string DisplayName(string mapKey)
	{
		foreach (ShadowTempleFloor floor in Floors)
		{
			if (string.Equals(floor.MapKey, mapKey, StringComparison.Ordinal))
			{
				return floor.DisplayName;
			}
		}
		return mapKey;
	}
}
