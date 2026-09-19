using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class HarborFerryCatalog
{
	public const string Role = "harbor_ferry";

	public const string TalkingIslandNpcId = "npc_port_master_talking";

	public const string GludinNpcId = "npc_port_master_gludin";

	private static readonly HarborFerryRoute[] Routes = new HarborFerryRoute[2]
	{
		new HarborFerryRoute("npc_port_master_talking", "town_talking", "talking_island", "talking_island_port_bridge", "town_gludin", "mainland_south", "gludio_village", "古魯丁村莊"),
		new HarborFerryRoute("npc_port_master_gludin", "town_gludin", "mainland_south", "gludio_village", "town_talking", "talking_island", "talking_island_port_bridge", "說話之島")
	};

	public const string ShipTexturePath = "res://assets/props/ferry/ferry_ship.png";

	public static readonly (string MapKey, string LandmarkId, int OffsetX, int OffsetY)[] ShipBerths = new(string, string, int, int)[1] { ("talking_island", "talking_island_port_bridge", -12, 22) };

	private static readonly IReadOnlyDictionary<string, HarborFerryRoute> ByNpc = Routes.ToDictionary<HarborFerryRoute, string>((HarborFerryRoute route) => route.NpcId, StringComparer.Ordinal);

	public static IReadOnlyList<HarborFerryRoute> All => Routes;

	public static HarborFerryRoute? FindByNpc(string? npcId)
	{
		if (!string.IsNullOrWhiteSpace(npcId))
		{
			return ByNpc.GetValueOrDefault(npcId);
		}
		return null;
	}
}
