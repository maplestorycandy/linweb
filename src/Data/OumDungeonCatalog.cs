using System.Collections.Generic;

namespace IdleLineage.Data;

public static class OumDungeonCatalog
{
	public const int SourceMapId = 310;

	public const string MapKey = "oum_dungeon";

	public const string DisplayName = "歐姆地監";

	public const string VillageDisplayName = "歐姆村莊";

	public const string DiadMapKey = "diad_fortress";

	public const string DiadDisplayName = "地下大洞穴";

	public static IReadOnlyList<OumDungeonPortalLink> PortalLinks { get; } = new OumDungeonPortalLink[4]
	{
		new OumDungeonPortalLink("oum_dungeon", "oum_village_to_dungeon", "oum_dungeon", "歐姆地監", "oum_dungeon_arrival_from_village"),
		new OumDungeonPortalLink("oum_dungeon", "oum_dungeon_to_village", "oum_dungeon", "歐姆村莊", "oum_village_arrival_from_dungeon"),
		new OumDungeonPortalLink("oum_dungeon", "oum_to_diad_exit", "diad_fortress", "地下大洞穴", "diad_arrival_from_oum"),
		new OumDungeonPortalLink("diad_fortress", "diad_to_oum_entrance", "oum_dungeon", "歐姆地監", "oum_arrival_from_diad")
	};
}
