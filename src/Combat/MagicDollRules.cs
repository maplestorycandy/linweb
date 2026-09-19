using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MagicDollRules
{
	public const string TableName = "MAGIC_DOLLS";

	public const string BagItemKey = "doll_bag";

	public const string DollTypeCounter = "doll:type";

	public const int CrystalCost = 50;

	public const int MaxDollCount = 1;

	public const double DurationSeconds = 1800.0;

	public const double RegenIntervalSeconds = 64.0;

	public const double HealthRegenAmount = 40.0;

	public const double ManaRegenAmount = 15.0;

	public const double AttackChancePercent = 3.0;

	public const double AttackBonusDamage = 15.0;

	public const double ShieldChancePercent = 4.0;

	public const double ShieldReduction = 15.0;

	public const double WeightReliefMultiplier = 1.2;

	public const double BowHitBonus = 1.0;

	public const double BowDamageBonus = 1.0;

	public const double ArmorImprovement = 1.0;

	private static readonly ConditionalWeakTable<IGameData, MagicDollCatalog> Cache = new ConditionalWeakTable<IGameData, MagicDollCatalog>();

	public static MagicDollCatalog LoadCatalog(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return Cache.GetValue(data, Build);
	}

	public static bool TryReadDoll(IGameData data, string itemKey, out MagicDollDefinition definition)
	{
		definition = null;
		if (itemKey.Length > 0)
		{
			return LoadCatalog(data).ByItemKey.TryGetValue(itemKey, out definition);
		}
		return false;
	}

	public static string RollBagReward(IGameData data, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		MagicDollCatalog magicDollCatalog = LoadCatalog(data);
		int val = magicDollCatalog.BagPool.Sum<(string, int)>(((string ItemKey, int Weight) entry) => entry.Weight);
		int num = random.Roll(1, Math.Max(1, val));
		foreach (var item3 in magicDollCatalog.BagPool)
		{
			string item = item3.ItemKey;
			int item2 = item3.Weight;
			num -= item2;
			if (num <= 0)
			{
				return item;
			}
		}
		IReadOnlyList<(string ItemKey, int Weight)> bagPool = magicDollCatalog.BagPool;
		return bagPool[bagPool.Count - 1].ItemKey;
	}

	public static int? ActiveDollType(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (!owner.Counters.TryGetValue("doll:type", out var value))
		{
			return null;
		}
		return value;
	}

	public static string AbilityDescription(int type, string dollName)
	{
		return type switch
		{
			0 => "負重上限 +20%",
			1 => "每64秒魔力恢復 15",
			2 => "攻擊時 3% 機率額外傷害 +15",
			3 => "每64秒魔力恢復 15",
			4 => "攻擊時 3% 機率額外傷害 +15",
			5 => "受到傷害時 4% 機率傷害減免 15",
			6 => "每64秒體力恢復 40",
			7 => "防禦力(AC) -3、寒冰耐性 +5",
			8 => "每64秒體力恢復 40、攻擊時 5% 機率使目標中毒",
			9 => "遠距離命中 +1、遠距離傷害 +1",
			10 => "遠距離命中 +2、近戰命中 +2、體力上限 +50",
			11 => "近戰額外傷害 +2、受擊時 4% 機率傷害減免 15",
			12 => "魔攻(SP) +2、每64秒魔力恢復 15",
			13 => "防禦力(AC) -1、傷害減免 +1",
			14 => "每64秒魔力恢復 15、負重上限 +15%",
			_ => "魔法娃娃輔助增益"
		};
	}

	public static double WeightCapacityMultiplier(Combatant owner)
	{
		int? type = ActiveDollType(owner);
		if (type == 0) return 1.2;
		if (type == 14) return 1.15;
		return 1.0;
	}

	public static double BowHitAdjustment(Combatant attacker)
	{
		int? type = ActiveDollType(attacker);
		if (type == 9) return 1.0;
		if (type == 10) return 2.0;
		return 0.0;
	}

	public static double BowDamageAdjustment(Combatant attacker)
	{
		return (ActiveDollType(attacker) == 9) ? 1.0 : 0.0;
	}

	public static double MeleeHitAdjustment(Combatant attacker)
	{
		return (ActiveDollType(attacker) == 10) ? 2.0 : 0.0;
	}

	public static double MeleeDamageAdjustment(Combatant attacker)
	{
		return (ActiveDollType(attacker) == 11) ? 2.0 : 0.0;
	}

	public static double MagicDamageAdjustment(Combatant attacker)
	{
		return (ActiveDollType(attacker) == 12) ? 2.0 : 0.0;
	}

	public static double ArmorClassAdjustment(Combatant target)
	{
		int? type = ActiveDollType(target);
		if (type == 7) return -3.0;
		if (type == 13) return -1.0;
		return 0.0;
	}

	public static double DamageReductionAdjustment(Combatant target)
	{
		return (ActiveDollType(target) == 13) ? 1.0 : 0.0;
	}

	public static bool RollAttackBonus(Combatant attacker, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int? type = ActiveDollType(attacker);
		if (type == 2 || type == 4)
		{
			return random.NextDouble() * 100.0 < 3.0;
		}
		return false;
	}

	public static bool RollDamageShield(Combatant target, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int? type = ActiveDollType(target);
		if (type == 5 || type == 11)
		{
			return random.NextDouble() * 100.0 < 4.0;
		}
		return false;
	}

	public static bool RollPoisonProc(Combatant attacker, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		return (ActiveDollType(attacker) == 8) && random.NextDouble() * 100.0 < 5.0;
	}

	private static MagicDollCatalog Build(IGameData data)
	{
		if (!(data.Table("MAGIC_DOLLS") is JsonObject jsonObject))
		{
			throw new InvalidDataException("MAGIC_DOLLS table failed to load.");
		}
		Dictionary<string, MagicDollDefinition> dictionary = new Dictionary<string, MagicDollDefinition>(StringComparer.Ordinal);
		foreach (JsonNode item in jsonObject["dolls"].AsArray())
		{
			JsonObject jsonObject2 = item.AsObject();
			MagicDollDefinition magicDollDefinition = new MagicDollDefinition(jsonObject2["key"].GetValue<string>(), jsonObject2["n"].GetValue<string>(), jsonObject2["l1jItemId"].GetValue<int>(), jsonObject2["npcId"].GetValue<int>(), jsonObject2["type"].GetValue<int>(), jsonObject2["gfx"].GetValue<int>());
			if (data.Item(magicDollDefinition.ItemKey) == null)
			{
				throw new InvalidDataException("Magic doll item '" + magicDollDefinition.ItemKey + "' is missing from DB.items.");
			}
			dictionary.Add(magicDollDefinition.ItemKey, magicDollDefinition);
		}
		if (dictionary.Count != 15)
		{
			throw new InvalidDataException($"MAGIC_DOLLS must define exactly 15 dolls, got {dictionary.Count}.");
		}
		string value = jsonObject["crystal"]["key"].GetValue<string>();
		(string, int)[] bagPool = (from node in jsonObject["bagPool"].AsArray()
			select (node["key"].GetValue<string>(), node["weight"].GetValue<int>())).ToArray();
		JsonObject jsonObject3 = jsonObject["arkaRecipe"].AsObject();
		(string, int)[] arkaMaterials = (from node in jsonObject3["materials"].AsArray()
			select (node["key"].GetValue<string>(), node["count"].GetValue<int>())).ToArray();
		return new MagicDollCatalog(dictionary, value, bagPool, arkaMaterials, jsonObject3["output"]["key"].GetValue<string>());
	}
}
