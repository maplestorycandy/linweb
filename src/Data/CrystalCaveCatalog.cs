using System.Collections.Generic;

namespace IdleLineage.Data;

public static class CrystalCaveCatalog
{
	public const string Floor1MapKey = "crystal_cave1";

	public const string Floor2MapKey = "crystal_cave2";

	public const string Floor3MapKey = "crystal_cave3";

	public const string SnowfieldMapKey = "mainland_south";

	public const string SnowQueenMobKey = "ice_queen";

	public const string SnowQueenLairLandmarkId = "crystal_cave_3f_queen_lair";

	public static IReadOnlyList<CrystalCaveMap> Maps { get; } = new CrystalCaveMap[3]
	{
		new CrystalCaveMap("crystal_cave1", 72, "水晶洞穴1樓"),
		new CrystalCaveMap("crystal_cave2", 73, "水晶洞穴2樓"),
		new CrystalCaveMap("crystal_cave3", 74, "水晶洞穴3樓")
	};

	public static IReadOnlyList<CrystalCaveStairLink> StairLinks { get; } = new CrystalCaveStairLink[8]
	{
		new CrystalCaveStairLink("crystal_cave1", "crystal_cave_1f_stairs_down", "crystal_cave2", "crystal_cave_2f_arrival_from_1f"),
		new CrystalCaveStairLink("crystal_cave2", "crystal_cave_2f_stairs_up", "crystal_cave1", "crystal_cave_1f_arrival_from_2f"),
		new CrystalCaveStairLink("crystal_cave2", "crystal_cave_2f_stairs_down", "crystal_cave3", "crystal_cave_3f_arrival_from_2f"),
		new CrystalCaveStairLink("crystal_cave3", "crystal_cave_3f_stairs_up", "crystal_cave2", "crystal_cave_2f_arrival_from_3f"),
		new CrystalCaveStairLink("crystal_cave1", "crystal_cave_1f_mouth", "mainland_south", "oren_cliff_cave_west_arrival"),
		new CrystalCaveStairLink("mainland_south", "oren_cliff_cave_west", "crystal_cave1", "crystal_cave_1f_mouth_arrival"),
		new CrystalCaveStairLink("crystal_cave1", "crystal_cave_1f_exit2", "mainland_south", "oren_cliff_cave_east_arrival"),
		new CrystalCaveStairLink("mainland_south", "oren_cliff_cave_east", "crystal_cave1", "crystal_cave_1f_exit2_arrival")
	};
}
