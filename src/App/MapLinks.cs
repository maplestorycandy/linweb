using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class MapLinks
{
	public enum Edge
	{
		West,
		East,
		North,
		South
	}

	public sealed record Gate(Edge Side, string TargetKey, string TargetName, bool ToTown, string? SourceLandmarkId = null, string? DestinationLandmarkId = null, float TriggerRadius = 20f, (int X, int Y)? SourceGameCell = null, (int X, int Y)? DestinationGameCell = null);

	public const float TriggerRadius = 80f;

	private const float EdgeInset = 110f;

	private static readonly Gate[] PirateFrontGates = new Gate[5]
	{
		new Gate(Edge.West, "hidden_dock", "隱藏之港", ToTown: false, "pirate_front_port_pier", "hidden_dock_arrival"),
		new Gate(Edge.North, "pirate_dungeon", "海賊島地監1樓(西門)", ToTown: false, "pirate_front_dungeon_west_entrance", "pirate_dungeon_1f_west_arrival"),
		new Gate(Edge.South, "pirate_dungeon", "海賊島地監1樓(南門)", ToTown: false, "pirate_front_dungeon_south_entrance", "pirate_dungeon_1f_north_arrival"),
		new Gate(Edge.East, "pirate_back", "海賊島後半部(北)", ToTown: false, "pirate_front_back_north_crossing", "pirate_back_front_north_arrival"),
		new Gate(Edge.East, "pirate_back", "海賊島後半部(南)", ToTown: false, "pirate_front_back_south_crossing", "pirate_back_front_south_arrival")
	};

	private static readonly Gate[] PirateBackGates = new Gate[2]
	{
		new Gate(Edge.West, "pirate_wild", "海賊島前半部(北)", ToTown: false, "pirate_back_front_north_crossing", "pirate_front_back_north_return"),
		new Gate(Edge.South, "pirate_wild", "海賊島前半部(南)", ToTown: false, "pirate_back_front_south_crossing", "pirate_front_back_south_return")
	};

	private static readonly Gate[] PirateDungeon1FGates = new Gate[3]
	{
		new Gate(Edge.West, "pirate_wild", "海賊島前半部(西門)", ToTown: false, "pirate_dungeon_1f_west_exit", "pirate_front_dungeon_west_return"),
		new Gate(Edge.North, "pirate_wild", "海賊島前半部(南門)", ToTown: false, "pirate_dungeon_1f_north_exit", "pirate_front_dungeon_south_return"),
		new Gate(Edge.East, "pirate_dungeon_2f", "海賊島地監2樓", ToTown: false, "pirate_dungeon_1f_stairs_down", "pirate_dungeon_2f_arrival_from_1f")
	};

	private static readonly Gate[] PirateDungeon2FGates = new Gate[2]
	{
		new Gate(Edge.North, "pirate_dungeon", "海賊島地監1樓", ToTown: false, "pirate_dungeon_2f_stairs_up", "pirate_dungeon_1f_arrival_from_2f"),
		new Gate(Edge.South, "pirate_dungeon_3f", "海賊島地監3樓", ToTown: false, "pirate_dungeon_2f_stairs_down", "pirate_dungeon_3f_arrival_from_2f")
	};

	private static readonly Gate[] PirateDungeon3FGates = new Gate[2]
	{
		new Gate(Edge.East, "pirate_dungeon_2f", "海賊島地監2樓", ToTown: false, "pirate_dungeon_3f_stairs_up", "pirate_dungeon_2f_arrival_from_3f"),
		new Gate(Edge.West, "pirate_dungeon_4f", "海賊島地監4樓", ToTown: false, "pirate_dungeon_3f_stairs_down", "pirate_dungeon_4f_arrival_from_3f")
	};

	private static readonly Gate[] PirateDungeon4FGates = new Gate[2]
	{
		new Gate(Edge.North, "pirate_dungeon_3f", "海賊島地監3樓", ToTown: false, "pirate_dungeon_4f_stairs_up", "pirate_dungeon_3f_arrival_from_4f"),
		new Gate(Edge.South, "elf_grave", "精靈墓穴", ToTown: false, "pirate_dungeon_4f_circle")
	};

	private static readonly Gate[] HiddenDockGates = new Gate[1]
	{
		new Gate(Edge.West, "town_gludio", "燃柳村", ToTown: true, "hidden_dock_mainland_door")
	};

	private static readonly Gate[] ElfGraveGates = new Gate[1]
	{
		new Gate(Edge.North, "pirate_dungeon_4f", "海賊島地監4樓", ToTown: false, null, "pirate_dungeon_4f_circle_arrival")
	};

	private static readonly Dictionary<string, Gate[]> Links = new Dictionary<string, Gate[]>(StringComparer.Ordinal)
	{
		["pirate_wild"] = PirateFrontGates,
		["pirate_back"] = PirateBackGates,
		["pirate_dungeon"] = PirateDungeon1FGates,
		["pirate_dungeon_2f"] = PirateDungeon2FGates,
		["pirate_dungeon_3f"] = PirateDungeon3FGates,
		["pirate_dungeon_4f"] = PirateDungeon4FGates,
		["hidden_dock"] = HiddenDockGates,
		["elf_grave"] = ElfGraveGates,
		["talking_island"] = new Gate[2]
		{
			new Gate(Edge.West, "town_talking", "說話之島村莊", ToTown: true),
			new Gate(Edge.North, "zone_13", "說話之島地監1樓", ToTown: false)
		},
		["zone_13"] = new Gate[2]
		{
			new Gate(Edge.South, "talking_island", "說話之島周邊", ToTown: false),
			new Gate(Edge.North, "zone_14", "說話之島地監2樓", ToTown: false)
		},
		["zone_14"] = new Gate[1]
		{
			new Gate(Edge.South, "zone_13", "說話之島地監1樓", ToTown: false)
		}
	};

	private static readonly Gate[] ThebesDesertGates = new Gate[1]
	{
		new Gate(Edge.East, "thebes_pyramid", "底比斯 金字塔內部", ToTown: false, "thebes_desert_pyramid_entrance", "thebes_pyramid_surface_arrival")
	};

	private static readonly Gate[] ThebesPyramidGates = new Gate[1]
	{
		new Gate(Edge.West, "thebes_desert", "底比斯 沙漠", ToTown: false, "thebes_pyramid_surface_exit", "thebes_desert_pyramid_entrance")
	};

	private static readonly Gate[] ThebesTempleGates = new Gate[1]
	{
		new Gate(Edge.South, "thebes_pyramid", "底比斯 金字塔內部", ToTown: false, "thebes_temple_exit", "thebes_osiris_gate_interaction")
	};

	private static readonly Gate[] TikalAltarGates = new Gate[1]
	{
		new Gate(Edge.South, "tikal_area", "提卡爾神廟地區", ToTown: false, "tikal_altar_exit", "tikal_area_altar_gate_interaction")
	};

	private static readonly Gate[] CrystalCave1FGates = new Gate[3]
	{
		new Gate(Edge.South, "crystal_cave2", "水晶洞穴2樓", ToTown: false, "crystal_cave_1f_stairs_down", "crystal_cave_2f_arrival_from_1f"),
		new Gate(Edge.West, "mainland_south", "歐瑞雪原(出口1)", ToTown: false, "crystal_cave_1f_mouth", "oren_cliff_cave_west_arrival"),
		new Gate(Edge.East, "mainland_south", "歐瑞雪原(出口2)", ToTown: false, "crystal_cave_1f_exit2", "oren_cliff_cave_east_arrival")
	};

	private static readonly Gate[] OrenSnowfieldGates = new Gate[3]
	{
		new Gate(Edge.North, "crystal_cave1", "水晶洞穴1樓(出口1)", ToTown: false, "oren_cliff_cave_west", "crystal_cave_1f_mouth_arrival"),
		new Gate(Edge.North, "crystal_cave1", "水晶洞穴1樓(出口2)", ToTown: false, "oren_cliff_cave_east", "crystal_cave_1f_exit2_arrival"),
		new Gate(Edge.East, "shadow_temple", "暗影神殿外圍", ToTown: false, "shadow_temple_portal", "shadow_temple_1f_arrival")
	};

	private static readonly Gate[] ShadowTempleOuterGates = new Gate[2]
	{
		new Gate(Edge.West, "mainland_south", "歐瑞雪原", ToTown: false, "shadow_temple_1f_gate", "shadow_temple_portal_arrival"),
		new Gate(Edge.East, "shadow_temple_2f", "暗影神殿1樓", ToTown: false, "shadow_temple_1f_stairs_down", "shadow_temple_2f_arrival_from_1f")
	};

	private static readonly Gate[] ShadowTemple1FGates = new Gate[2]
	{
		new Gate(Edge.North, "shadow_temple", "暗影神殿外圍", ToTown: false, "shadow_temple_2f_stairs_up", "shadow_temple_1f_arrival_from_2f"),
		new Gate(Edge.South, "shadow_temple_3f", "暗影神殿2樓", ToTown: false, "shadow_temple_2f_stairs_down", "shadow_temple_3f_arrival_from_2f")
	};

	private static readonly Gate[] ShadowTemple2FGates = new Gate[2]
	{
		new Gate(Edge.North, "shadow_temple_2f", "暗影神殿1樓", ToTown: false, "shadow_temple_3f_stairs_up", "shadow_temple_2f_arrival_from_3f"),
		new Gate(Edge.South, "shadow_temple_4f", "暗影神殿3樓", ToTown: false, "shadow_temple_3f_stairs_down", "shadow_temple_4f_arrival_from_3f")
	};

	private static readonly Gate[] ShadowTemple3FGates = new Gate[1]
	{
		new Gate(Edge.North, "shadow_temple_3f", "暗影神殿2樓", ToTown: false, "shadow_temple_4f_stairs_up", "shadow_temple_3f_arrival_from_4f")
	};

	private static readonly Gate[] CrystalCave2FGates = new Gate[2]
	{
		new Gate(Edge.West, "crystal_cave1", "水晶洞穴1樓", ToTown: false, "crystal_cave_2f_stairs_up", "crystal_cave_1f_arrival_from_2f"),
		new Gate(Edge.East, "crystal_cave3", "水晶洞穴3樓", ToTown: false, "crystal_cave_2f_stairs_down", "crystal_cave_3f_arrival_from_2f")
	};

	private static readonly Gate[] CrystalCave3FGates = new Gate[1]
	{
		new Gate(Edge.North, "crystal_cave2", "水晶洞穴2樓", ToTown: false, "crystal_cave_3f_stairs_up", "crystal_cave_2f_arrival_from_3f")
	};

	private static readonly Gate[] MainlandSouthGates = new Gate[4]
	{
		new Gate(Edge.North, "silent_outer", "沉默洞穴周邊", ToTown: false, "silent_cave_entrance", "silent_cave_surface_arrival"),
		new Gate(Edge.North, "crystal_cave1", "水晶洞穴1樓(出口1)", ToTown: false, "oren_cliff_cave_west", "crystal_cave_1f_mouth_arrival"),
		new Gate(Edge.North, "crystal_cave1", "水晶洞穴1樓(出口2)", ToTown: false, "oren_cliff_cave_east", "crystal_cave_1f_exit2_arrival"),
		new Gate(Edge.East, "shadow_temple", "暗影神殿外圍", ToTown: false, "shadow_temple_portal", "shadow_temple_1f_arrival")
	};

	private static readonly Gate[] SilentCaveSurfaceGates = new Gate[1]
	{
		new Gate(Edge.South, "mainland_south", "亞丁大陸", ToTown: false, "silent_cave_surface_exit", "silent_cave_entrance")
	};

	private static readonly Gate[] AntharasCaveEntranceGates = new Gate[1]
	{
		new Gate(Edge.West, "antharas_nest_2", "安塔瑞斯洞穴(階段型)", ToTown: false, "antharas_cave_entrance_staged_route", "antharas_cave_arrival")
	};

	private static readonly Gate[] AntharasCaveGates = new Gate[1]
	{
		new Gate(Edge.North, "antharas_nest_3", "安塔瑞斯棲息地入口", ToTown: false, "antharas_cave_exit", "antharas_lair_entrance_arrival")
	};

	private static readonly Dictionary<string, Gate[]> LandmarkLinks = new Dictionary<string, Gate[]>(StringComparer.Ordinal)
	{
		["mainland_south"] = MainlandSouthGates,
		["silent_outer"] = SilentCaveSurfaceGates,
		["antharas_nest_1"] = AntharasCaveEntranceGates,
		["antharas_nest_2"] = AntharasCaveGates,
		["thebes_desert"] = ThebesDesertGates,
		["thebes_pyramid"] = ThebesPyramidGates,
		["thebes_temple"] = ThebesTempleGates,
		["tikal_altar"] = TikalAltarGates,
		["crystal_cave1"] = CrystalCave1FGates,
		["crystal_cave2"] = CrystalCave2FGates,
		["crystal_cave3"] = CrystalCave3FGates,
		["zone_03"] = OrenSnowfieldGates,
		["shadow_temple"] = ShadowTempleOuterGates,
		["shadow_temple_2f"] = ShadowTemple1FGates,
		["shadow_temple_3f"] = ShadowTemple2FGates,
		["shadow_temple_4f"] = ShadowTemple3FGates,
		["pirate_wild"] = PirateFrontGates,
		["pirate_back"] = PirateBackGates,
		["pirate_dungeon"] = PirateDungeon1FGates,
		["pirate_dungeon_2f"] = PirateDungeon2FGates,
		["pirate_dungeon_3f"] = PirateDungeon3FGates,
		["pirate_dungeon_4f"] = PirateDungeon4FGates,
		["hidden_dock"] = HiddenDockGates,
		["elf_grave"] = ElfGraveGates,
		["talking_island"] = new Gate[1]
		{
			new Gate(Edge.West, "zone_13", "冒險洞窟1樓", ToTown: false, "talking_island_adventure_cave_entrance", "adventure_cave_1f_arrival")
		},
		["zone_13"] = new Gate[2]
		{
			new Gate(Edge.South, "talking_island", "說話之島西方", ToTown: false, "adventure_cave_1f_exit", "talking_island_adventure_cave_return"),
			new Gate(Edge.North, "zone_14", "冒險洞窟2樓", ToTown: false, "adventure_cave_1f_stairs_down", "adventure_cave_2f_arrival")
		},
		["zone_14"] = new Gate[1]
		{
			new Gate(Edge.South, "zone_13", "冒險洞窟1樓", ToTown: false, "adventure_cave_2f_stairs_up", "adventure_cave_1f_stairs_return")
		}
	};

	private static readonly HashSet<string> RegionEntrances = new HashSet<string>(StringComparer.Ordinal) { "talking_island", "elf_grave" };

	private static readonly Dictionary<string, string> IsolatedMapNames = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["antharas_nest_1"] = "安塔瑞斯洞穴入口",
		["antharas_lair"] = "安塔瑞斯棲息地",
		["dream_island"] = "夢幻之島"
	};

	private static readonly Dictionary<string, string> GateNames = BuildGateNames();

	private const double ActorRadius = 20.0;

	public static IReadOnlyList<Gate> For(string mapKey)
	{
		List<Gate> list = new List<Gate>();
		Gate[] value2;
		if (LandmarkLinks.TryGetValue(mapKey, out Gate[] value))
		{
			list.AddRange(value);
		}
		else if (Links.TryGetValue(mapKey, out value2))
		{
			list.AddRange(value2);
		}
		foreach (Gate gate in CatalogMapLinks.For(mapKey))
		{
			if (!list.Any(delegate(Gate existing)
			{
				if (existing.TargetKey == gate.TargetKey && existing.SourceLandmarkId == gate.SourceLandmarkId && existing.DestinationLandmarkId == gate.DestinationLandmarkId)
				{
					(int, int)? sourceGameCell = existing.SourceGameCell;
					(int, int)? sourceGameCell2 = gate.SourceGameCell;
					bool hasValue = sourceGameCell.HasValue;
					if (hasValue == sourceGameCell2.HasValue)
					{
						(int, int) valueOrDefault2;
						(int, int) valueOrDefault;
						if (hasValue)
						{
							valueOrDefault = sourceGameCell.GetValueOrDefault();
							valueOrDefault2 = sourceGameCell2.GetValueOrDefault();
							if (valueOrDefault.Item1 != valueOrDefault2.Item1 || valueOrDefault.Item2 != valueOrDefault2.Item2)
							{
								goto IL_0111;
							}
						}
						sourceGameCell2 = existing.DestinationGameCell;
						sourceGameCell = gate.DestinationGameCell;
						hasValue = sourceGameCell2.HasValue;
						if (hasValue != sourceGameCell.HasValue)
						{
							return false;
						}
						if (!hasValue)
						{
							return true;
						}
						valueOrDefault2 = sourceGameCell2.GetValueOrDefault();
						valueOrDefault = sourceGameCell.GetValueOrDefault();
						if (valueOrDefault2.Item1 == valueOrDefault.Item1)
						{
							return valueOrDefault2.Item2 == valueOrDefault.Item2;
						}
						return false;
					}
				}
				goto IL_0111;
				IL_0111:
				return false;
			}))
			{
				list.Add(gate);
			}
		}
		return list;
	}

	public static bool HasGates(string mapKey)
	{
		if (!LandmarkLinks.ContainsKey(mapKey) && !Links.ContainsKey(mapKey))
		{
			return CatalogMapLinks.HasGates(mapKey);
		}
		return true;
	}

	public static bool IsWalkOnly(string mapKey)
	{
		if (!Links.ContainsKey(mapKey) || RegionEntrances.Contains(mapKey))
		{
			return CatalogMapLinks.IsWalkOnly(mapKey);
		}
		return true;
	}

	public static string DisplayName(IGameData data, string mapKey)
	{
		string text = CatalogMapLinks.DisplayName(data, mapKey);
		if (text != mapKey)
		{
			return text;
		}
		if (GateNames.TryGetValue(mapKey, out string value))
		{
			return value;
		}
		if (data.Table("L1J_MAP_NAMES") is JsonObject jsonObject && jsonObject["names"] is JsonObject jsonObject2)
		{
			JsonNode jsonNode = jsonObject2[mapKey];
			if (jsonNode != null)
			{
				return jsonNode.GetValue<string>();
			}
		}
		return text;
	}

	private static Dictionary<string, string> BuildGateNames()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(IsolatedMapNames, StringComparer.Ordinal);
		foreach (Gate[] item in LandmarkLinks.Values.Concat<Gate[]>(Links.Values))
		{
			foreach (Gate gate in item)
			{
				if (!string.IsNullOrWhiteSpace(gate.TargetName) && (!dictionary.TryGetValue(gate.TargetKey, out var value) || gate.TargetName.Length < value.Length))
				{
					dictionary[gate.TargetKey] = gate.TargetName;
				}
			}
		}
		return dictionary;
	}

	public static void ConfigureRastabad(string dataDirectory)
	{
		CatalogMapLinks.ConfigureRastabad(dataDirectory);
	}

	public static void ConfigureClassicMaps(string dataDirectory)
	{
		CatalogMapLinks.ConfigureClassicMaps(dataDirectory);
	}

	public static Vector2 GateWorldPosition(Edge side, Rect2 field, WorldCollisionGrid? grid)
	{
		Vector2 center = field.GetCenter();
		Vector2 vector = side switch
		{
			Edge.West => new Vector2(field.Position.X + 110f, center.Y), 
			Edge.East => new Vector2(field.End.X - 110f, center.Y), 
			Edge.North => new Vector2(center.X, field.Position.Y + 110f), 
			_ => new Vector2(center.X, field.End.Y - 110f), 
		};
		if (grid == null || IsUsable(grid, vector, center))
		{
			return vector;
		}
		bool flag = (uint)(side - 2) <= 1u;
		bool flag2 = flag;
		float num = 60f;
		float num2 = (flag2 ? field.Size.X : field.Size.Y) * 0.5f - 110f;
		for (float num3 = num; num3 <= num2; num3 += num)
		{
			for (int i = 0; i < 2; i++)
			{
				float num4 = ((i == 0) ? (-1f) : 1f);
				Vector2 vector2 = (flag2 ? new Vector2(vector.X + num4 * num3, vector.Y) : new Vector2(vector.X, vector.Y + num4 * num3));
				if (IsUsable(grid, vector2, center))
				{
					return vector2;
				}
			}
		}
		return vector;
	}

	private static bool IsUsable(WorldCollisionGrid grid, Vector2 point, Vector2 spawn)
	{
		WorldPoint worldPoint = new WorldPoint(point.X, point.Y);
		if (grid.CanOccupy(worldPoint, 20.0))
		{
			return grid.CanReach(new WorldPoint(spawn.X, spawn.Y), worldPoint, 20.0);
		}
		return false;
	}
}
