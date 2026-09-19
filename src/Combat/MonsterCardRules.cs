using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MonsterCardRules
{
	public const string CardEffect = "monster_card";

	public const string MaterialEffect = "charm_material";

	public const string NormalMaterialKey = "unsealed_card_normal";

	public const string SilverMaterialKey = "unsealed_card_silver";

	public const string GoldMaterialKey = "unsealed_card_gold";

	public const long ReuseCooldownMilliseconds = 300000L;

	public const int MinimumActiveCharmCost = 4;

	public const int MaximumActiveCharmCost = 20;

	private const double StrengthLevelsPerCharmPoint = 6.0;

	public static int ActiveCharmCost(IGameData data, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(mobKey, "mobKey");
		int num = ActiveCharmStrength(data.Mob(mobKey) ?? throw new KeyNotFoundException("Mob '" + mobKey + "' was not found."));
		return Math.Clamp(4 + (int)Math.Floor((double)num / 6.0), 4, 20);
	}

	public static int ActiveCharmCostFor(IGameData data, string mobKey, Combatant owner)
	{
		return PetRules.CompanionDeploymentCharmCost(owner, ActiveCharmCost(data, mobKey));
	}

	private static int ActiveCharmStrength(JsonObject mob)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		double num = Math.Max(1.0, CombatSkill.ReadDouble(mob, "lv"));
		double num2 = Math.Max(1.0, CombatSkill.ReadDouble(mob, "hp"));
		double num3 = Math.Max(10.0, num * 12.0);
		int num4 = Math.Clamp(RoundStrength(Math.Log2(num2 / num3) * 4.0), -4, 12);
		MobBasicAttackProfile mobBasicAttackProfile = MobBasicAttackRules.Resolve(mob);
		double num5 = (double)(mobBasicAttackProfile.MagicDiceCount * (mobBasicAttackProfile.MagicDiceSides + 1)) / 2.0 + mobBasicAttackProfile.MagicFlatDamage;
		if (mobBasicAttackProfile.UsesRangedPhysicalDamage && mob["rangedDb"] != null)
		{
			num5 += CombatSkill.ReadDouble(mob, "rangedDb") - mobBasicAttackProfile.MagicFlatDamage;
		}
		double num6 = CombatSkill.ReadDouble(mob, "atkSpd");
		double num7 = ((num6 > 0.0) ? num6 : 1.32);
		double num8 = Math.Clamp(1.32 / num7, 0.5, 2.0);
		int num9 = Math.Clamp(RoundStrength((num5 * num8 - num * 0.65) / 8.0), -3, 6);
		int num10 = Math.Clamp(RoundStrength((10.0 - num - CombatSkill.ReadDouble(mob, "ac")) / 12.0), -2, 4);
		double num11 = Math.Min(100.0, num * 0.8);
		int num12 = Math.Clamp(RoundStrength((CombatSkill.ReadDouble(mob, "mr") - num11) / 20.0), -2, 3);
		int num13 = Math.Min(4, mob.Count<KeyValuePair<string, JsonNode>>((KeyValuePair<string, JsonNode> pair) => IsMobSkillSlot(pair.Key)));
		return Math.Max(0, RoundStrength(num) + num4 + num9 + num10 + num12 + num13);
	}

	private static int RoundStrength(double value)
	{
		return (int)Math.Round(value, MidpointRounding.AwayFromZero);
	}

	private static bool IsMobSkillSlot(string key)
	{
		if (!key.StartsWith("mag", StringComparison.Ordinal))
		{
			return false;
		}
		if (key.Length == 3)
		{
			return true;
		}
		for (int i = 3; i < key.Length; i++)
		{
			if (!char.IsAsciiDigit(key[i]))
			{
				return false;
			}
		}
		return true;
	}

	public static int ActiveCharmCapacity(Combatant leader)
	{
		ArgumentNullException.ThrowIfNull(leader, "leader");
		return (int)Math.Min(2147483647.0, PetRules.MainCharmCapacity(leader));
	}

	public static string ResolveMobKey(IGameData data, Combatant mob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(mob, "mob");
		if (!string.IsNullOrWhiteSpace(mob.Avatar) && data.Mob(mob.Avatar) != null)
		{
			return mob.Avatar;
		}
		if (data.Mob(mob.Key) == null)
		{
			return string.Empty;
		}
		return mob.Key;
	}

	public static MonsterCardGrade Grade(IGameData data, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (GodotBossClassification.IsBoss(data, mobKey))
		{
			return MonsterCardGrade.Gold;
		}
		JsonObject jsonObject = data.Mob(mobKey);
		if (jsonObject == null || !CombatSkill.ReadBool(jsonObject, "hard"))
		{
			return MonsterCardGrade.Normal;
		}
		return MonsterCardGrade.Silver;
	}

	public static string MaterialKey(IGameData data, string mobKey)
	{
		return Grade(data, mobKey) switch
		{
			MonsterCardGrade.Gold => "unsealed_card_gold", 
			MonsterCardGrade.Silver => "unsealed_card_silver", 
			_ => "unsealed_card_normal", 
		};
	}

	public static string CardKey(string mobKey)
	{
		return MonsterCompanionRules.CardKey(mobKey);
	}

	public static bool IsCardDefinition(JsonObject? item)
	{
		if (item != null && string.Equals(CombatSkill.ReadString(item, "eff"), "monster_card", StringComparison.Ordinal))
		{
			return !string.IsNullOrWhiteSpace(CombatSkill.ReadString(item, "cardMob"));
		}
		return false;
	}

	public static bool IsAnyCardDefinition(JsonObject? item)
	{
		if (!IsCardDefinition(item))
		{
			if (item != null)
			{
				return string.Equals(CombatSkill.ReadString(item, "eff"), "charm_material", StringComparison.Ordinal);
			}
			return false;
		}
		return true;
	}

	public static bool TryReadMobKey(IGameData data, ItemStack stack, out string mobKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(stack, "stack");
		JsonObject? jsonObject = data.Item(stack.ItemKey);
		mobKey = (IsCardDefinition(jsonObject) ? CombatSkill.ReadString(jsonObject, "cardMob") : string.Empty);
		if (string.IsNullOrEmpty(mobKey) && stack.ItemKey.StartsWith("card_", StringComparison.Ordinal))
		{
			mobKey = stack.ItemKey.Substring("card_".Length);
		}
		if (mobKey.Length > 0)
		{
			JsonObject? mob = data.Mob(mobKey);
			if (mob != null)
			{
				return true;
			}
			return MonsterCompanionRules.IsRecruitable(mobKey, mob);
		}
		return false;
	}

	public static ItemStack? OwnedCard(Combatant owner, string mobKey)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		string cardKey = CardKey(mobKey);
		return owner.InventoryStacks.FirstOrDefault((ItemStack stack) => string.Equals(stack.ItemKey, cardKey, StringComparison.Ordinal));
	}

	public static ItemStack CreateCapturedCard(Combatant owner, string mobKey, int monsterLevel)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return new ItemStack(CombatInventory.NextUid(owner), CardKey(mobKey), 1L)
		{
			MonsterCardLevel = Math.Max(1, monsterLevel)
		};
	}
}
