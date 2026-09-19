using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class IntegratedTownCatalog
{
	private static readonly IntegratedTownDefinition[] Definitions = new IntegratedTownDefinition[20]
	{
		new IntegratedTownDefinition("talking_island", "town_talking", "說話之島村莊", "說話之島", "talking_island", null, (32565, 32953)),
		new IntegratedTownDefinition("mainland_south", "town_elf", "妖精森林村莊", "妖精森林周邊", "mainland_south", "elf_forest_village"),
		new IntegratedTownDefinition("mainland_south", "town_gludin", "古魯丁村莊", "古魯丁周邊", "mainland_south", "gludio_village"),
		new IntegratedTownDefinition("mainland_south", "town_windwood_castle", "風木村莊", "風木周邊", "mainland_south", "windwood_village"),
		new IntegratedTownDefinition("mainland_south", "town_silver_knight", "銀騎士村莊", "銀騎士地區", "mainland_south", "silver_knight_village", (33095, 33371)),
		new IntegratedTownDefinition("mainland_south", "town_kent_castle", "肯特村莊", "肯特周邊", "mainland_south", "kent_village"),
		new IntegratedTownDefinition("mainland_south", "town_giran", "奇岩城鎮", "奇岩周邊", "mainland_south", "giran_village"),
		new IntegratedTownDefinition("mainland_south", "town_heine", "海音村莊", "海音周邊", "mainland_south", "heine_village"),
		new IntegratedTownDefinition("mainland_south", "town_aden", "亞丁城鎮", "亞丁周邊", "mainland_south", "aden_city"),
		new IntegratedTownDefinition("mainland_south", "town_gludio", "燃柳村莊", "妖魔森林", "mainland_south", "burning_willow_village"),
		new IntegratedTownDefinition("mainland_south", "town_witon", "威頓村莊", "火龍窟", "mainland_south", "witon_village"),
		new IntegratedTownDefinition("silent_outer", "town_silent", "沉默洞穴", "沉默洞穴周邊", "silent_outer", "silent_cave_village"),
		new IntegratedTownDefinition("pirate_wild", "town_pirate_village", "海賊島村莊", "海賊島前半部", "pirate_wild", "pirate_front_village"),
		new IntegratedTownDefinition("mainland_south", "town_oren", "歐瑞村莊", "歐瑞雪原", "mainland_south", "oren_village"),
		new IntegratedTownDefinition("ivory_tower_3f", "town_ivory_tower", "象牙塔3樓", "象牙塔3樓", "ivory_tower_3f", "ivory_tower_3f_arrival_from_2f"),
		new IntegratedTownDefinition("rastabad_4f_council_kassandra_dantes", "town_elder_council", "長老會議廳", "長老會議廳周邊", "rastabad_4f_council_kassandra_dantes", "elder_council_hall"),
		new IntegratedTownDefinition("hyperia", "town_hyperia", "希培利亞", "希培利亞", "hyperia", "hyperia_arrival"),
		new IntegratedTownDefinition("behemoth", "town_behemoth", "貝希摩斯", "貝希摩斯", "behemoth", "behemoth_arrival"),
		new IntegratedTownDefinition("flame_shadow_lab", "town_flame_lab", "火焰之影實驗室", "火焰之影實驗室", "flame_shadow_lab", "flame_shadow_lab_arrival"),
		new IntegratedTownDefinition("flame_audience_hall", "town_flame_audience", "炎魔謁見所", "炎魔謁見所", "flame_audience_hall", "flame_audience_hall_arrival")
	};

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<IntegratedTownDefinition>> ByMap = Definitions.GroupBy<IntegratedTownDefinition, string>((IntegratedTownDefinition definition) => definition.MapKey, StringComparer.Ordinal).ToDictionary<IGrouping<string, IntegratedTownDefinition>, string, IReadOnlyList<IntegratedTownDefinition>>((IGrouping<string, IntegratedTownDefinition> group) => group.Key, (IGrouping<string, IntegratedTownDefinition> group) => Array.AsReadOnly(group.ToArray()), StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, IntegratedTownDefinition> ByTown = Definitions.ToDictionary<IntegratedTownDefinition, string>((IntegratedTownDefinition definition) => definition.TownKey, StringComparer.Ordinal);

	public static IReadOnlyList<IntegratedTownDefinition> All => Definitions;

	public static IntegratedTownDefinition? FindByMap(string mapKey)
	{
		return FindAllByMap(mapKey).FirstOrDefault();
	}

	public static IReadOnlyList<IntegratedTownDefinition> FindAllByMap(string mapKey)
	{
		return ByMap.GetValueOrDefault(mapKey) ?? Array.Empty<IntegratedTownDefinition>();
	}

	public static IntegratedTownDefinition? FindByTown(string townKey)
	{
		return ByTown.GetValueOrDefault(townKey);
	}
}
