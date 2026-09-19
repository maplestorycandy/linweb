using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jWorldNpcCombatRules
{
	public const string GuardImpl = "L1Guard";

	public const string GuardianImpl = "L1Guardian";

	public const string ScarecrowImpl = "L1Scarecrow";

	public const string MonsterImpl = "L1Monster";

	public const int ScarecrowTrainingLevelLimit = 5;

	public const int ScarecrowTrainingExperience = 50;

	public static bool IsCombatNpc(L1jNpcSpawn spawn)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		switch (spawn.Impl)
		{
		case "L1Guard":
		case "L1Guardian":
		case "L1Scarecrow":
		case "L1Monster":
			return true;
		default:
			return false;
		}
	}

	public static bool SpawnsAsCombatant(L1jNpcSpawn spawn, int playerLevel)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		if (!(spawn.Impl != "L1Scarecrow"))
		{
			return playerLevel < 5;
		}
		return true;
	}

	public static bool ShowsPersistentNameplate(L1jNpcSpawn spawn, bool interactive)
	{
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		if (spawn.Impl != "L1Scarecrow")
		{
			if (!interactive)
			{
				return IsCombatNpc(spawn);
			}
			return true;
		}
		return false;
	}

	public static Combatant Create(IGameData data, L1jNpcSpawn spawn, string instanceKey, int bornSeq, WorldPoint position, ICombatRandom? random = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(spawn, "spawn");
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey, "instanceKey");
		if (!IsCombatNpc(spawn))
		{
			throw new ArgumentException($"NPC {spawn.NpcId} ({spawn.Impl}) 沒有戰鬥 runtime。", "spawn");
		}
		bool flag = spawn.Impl == "L1Scarecrow";
		bool flag2 = spawn.Ranged >= 2;
		double num = ((flag || spawn.MoveIntervalMilliseconds <= 0) ? 0.0 : Math.Clamp(IsometricMovementRules.BaseMoveSpeed * 640.0 / (double)spawn.MoveIntervalMilliseconds, 19.0, 85.0));
		double num2 = ((spawn.AttackIntervalMilliseconds > 0) ? ((double)spawn.AttackIntervalMilliseconds / 1000.0) : 2.0);
		JsonObject definition = new JsonObject
		{
			["src"] = "l1j",
			["npcid"] = spawn.NpcId,
			["n"] = spawn.Name,
			["lv"] = Math.Max(1, spawn.Level),
			["hp"] = Math.Max(1, spawn.Hp),
			["mp"] = Math.Max(0, spawn.Mp),
			["ac"] = spawn.ArmorClass,
			["str"] = spawn.Strength,
			["con"] = spawn.Constitution,
			["dex"] = spawn.Dexterity,
			["int"] = spawn.Intelligence,
			["wis"] = spawn.Wisdom,
			["mr"] = spawn.MagicResistance,
			["exp"] = (flag ? 50 : Math.Max(0, spawn.Experience)),
			["lawful"] = spawn.Lawful,
			["s"] = (string.Equals(spawn.Size, "large", StringComparison.OrdinalIgnoreCase) ? "L" : "S"),
			["beh"] = (spawn.Aggressive ? "主動" : "被動"),
			["family"] = spawn.Family,
			["race"] = spawn.Family,
			["moveSpd"] = num,
			["atkSpd"] = num2,
			["aggroRange"] = 960.0,
			["agroCoi"] = spawn.DetectInvisible,
			["dr"] = spawn.DamageReduction,
			["noAttack"] = flag,
			["ranged"] = flag2,
			["basicAttackType"] = (flag2 ? "ranged" : "melee"),
			["attackRange"] = (flag2 ? ((double)spawn.Ranged * 48.0) : 12.0)
		};
		Combatant combatant = CombatantBuilder.CreateMobFromDefinition(data, $"gfx:{spawn.Gfx}", definition, instanceKey, bornSeq, position, random);
		combatant.Avatar = $"gfx:{spawn.Gfx}";
		combatant.Facing8 = Math.Clamp(spawn.Heading, 0, 7);
		combatant.L1jWorldNpcId = spawn.NpcId;
		combatant.L1jWorldNpcImpl = spawn.Impl;
		combatant.TrainingScarecrow = flag;
		combatant.NeutralWorldNpc = spawn.Impl == "L1Guard";
		combatant.ReturnsHomeWhenIdle = spawn.Impl == "L1Guard";
		if (flag)
		{
			combatant.ExperienceReward = 50.0;
		}
		else if (spawn.Impl == "L1Guard")
		{
			combatant.ExperienceReward = 0.0;
		}
		return combatant;
	}
}
