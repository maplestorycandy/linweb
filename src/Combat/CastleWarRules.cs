using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public static class CastleWarRules
{
	public const int MinimumLevel = 25;

	public const double AttemptSeconds = 1800.0;

	public const double GameDaySeconds = 14400.0;

	public const double FailureCooldownSeconds = 14400.0;

	public const double CaptureProtectionSeconds = 100800.0;

	public const int FixedTaxPercent = 10;

	public const long DailyIncome = 3000L;

	public const long TreasuryCap = 100000L;

	public const int AdenCastleId = 7;

	public const int AdenSubTowersRequired = 3;

	private static readonly CastleWarDefinition[] Definitions = new CastleWarDefinition[8]
	{
		new CastleWarDefinition(1, "肯特城", "mainland_south", 577, 589, 707, 699, 627, 640, new int[1] { 60514 }, "肯特守備軍"),
		new CastleWarDefinition(2, "妖魔城堡", "mainland_south", 238, 122, 338, 222, 286, 163, new int[1] { 60560 }, "妖魔守備軍"),
		new CastleWarDefinition(3, "風木城", "mainland_south", 59, 1222, 209, 1332, 111, 1251, new int[1] { 60552 }, "風木守備軍"),
		new CastleWarDefinition(4, "奇岩城", "mainland_south", 1047, 487, 1174, 627, 1119, 550, new int[3] { 60524, 60525, 60529 }, "奇岩守備軍"),
		new CastleWarDefinition(5, "海音城", "mainland_south", 946, 1187, 1071, 1362, 1012, 1268, new int[1] { 70857 }, "海音守備軍"),
		new CastleWarDefinition(6, "鐵門公會", "l1j_map_66", 51, 22, 166, 152, 124, 50, new int[3] { 70993, 70994, 70995 }, "鐵門守備軍"),
		new CastleWarDefinition(7, "亞丁城", "mainland_south", 1495, 1044, 1650, 1204, 1578, 1132, new int[2] { 60533, 60534 }, "亞丁守備軍"),
		new CastleWarDefinition(8, "狄亞得要塞", "diad_fortress", 248, 71, 430, 185, 393, 127, new int[2] { 71185, 71186 }, "狄亞得守備軍")
	};

	public static IReadOnlyList<CastleWarDefinition> Castles => Definitions;

	public static CastleWarDefinition? Find(int castleId)
	{
		return Definitions.FirstOrDefault((CastleWarDefinition castle) => castle.Id == castleId);
	}

	public static CastleWarDefinition? At(string mapKey, int cellX, int cellY)
	{
		return Definitions.FirstOrDefault((CastleWarDefinition castle) => castle.Contains(mapKey, cellX, cellY));
	}

	public static CastleWarDefinition? Registrar(string mapKey, int npcId, int cellX, int cellY)
	{
		return Definitions.FirstOrDefault((CastleWarDefinition castle) => string.Equals(castle.MapKey, mapKey, StringComparison.Ordinal) && castle.RegistrarNpcIds.Contains(npcId));
	}

	public static bool IsTower(L1jNpcSpawn spawn)
	{
		return string.Equals(spawn.Impl, "L1Tower", StringComparison.Ordinal);
	}

	public static CastleWarObjectKind TowerKind(CastleWarDefinition castle, L1jNpcSpawn spawn)
	{
		if (castle.Id == 7)
		{
			int npcId = spawn.NpcId;
			if (npcId >= 81190 && npcId <= 81193)
			{
				return CastleWarObjectKind.SubTower;
			}
		}
		return CastleWarObjectKind.MainTower;
	}

	public static string NpcKey(CastleWarObjectKind kind, L1jNpcSpawn spawn)
	{
		return $"{kind}:npc:{spawn.NpcId}:{spawn.CellX}:{spawn.CellY}";
	}

	public static string DoorKey(int doorId, int cellX, int cellY)
	{
		return $"Gate:door:{doorId}:{cellX}:{cellY}";
	}

	public static Combatant CreateStructure(string key, string displayName, string avatar, CastleWarObjectKind kind, int castleId, double hp, WorldPoint position, int bornSeq)
	{
		double num = Math.Max(1.0, hp);
		return new Combatant
		{
			Key = key,
			Disp = displayName,
			Avatar = avatar,
			Kind = CombatantKind.Mob,
			Level = 1,
			Hp = num,
			MaxHp = num,
			Pos = position,
			BornSeq = bornSeq,
			Radius = ((kind == CastleWarObjectKind.Gate) ? 38 : 30),
			MoveSpeed = 0.0,
			AttackRange = 0.0,
			AggroRange = 0.0,
			Passive = true,
			CannotAttack = true,
			ExperienceReward = 0.0,
			GoldMin = 0,
			GoldMax = 0,
			DropMultiplier = 0.0,
			CastleWarId = castleId,
			CastleWarObjectKind = kind,
			CastleWarObjectKey = key
		};
	}
}
