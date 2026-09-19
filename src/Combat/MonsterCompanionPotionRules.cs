using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MonsterCompanionPotionRules
{
	public const string CompanionPotionFlag = "companionPotion";

	public const string SourceItemField = "companionPotionSource";

	public static bool IsCompanionPotion(IGameData data, string itemKey)
	{
		string sourceItemKey;
		return TrySourceItem(data, itemKey, out sourceItemKey);
	}

	public static bool TrySourceItem(IGameData data, string itemKey, out string sourceItemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		sourceItemKey = "";
		if (!string.IsNullOrWhiteSpace(itemKey))
		{
			JsonObject jsonObject = data.Item(itemKey);
			if (jsonObject != null && CombatSkill.ReadBool(jsonObject, "companionPotion"))
			{
				sourceItemKey = CombatSkill.ReadString(jsonObject, "companionPotionSource");
				if (sourceItemKey.Length > 0)
				{
					return data.Item(sourceItemKey) != null;
				}
				return false;
			}
		}
		return false;
	}

	public static int HealingStrength(IGameData data, string itemKey)
	{
		if (!TrySourceItem(data, itemKey, out string sourceItemKey))
		{
			return -1;
		}
		return ConsumableRules.BaseHealingRange(data, sourceItemKey).Maximum;
	}

	public static MonsterCompanionPotionUseResult TryUse(IGameData data, Combatant owner, Combatant companion, string itemKey, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(companion, "companion");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (owner.Kind != CombatantKind.Player || !MonsterCompanionRules.IsCompanion(companion) || companion.Dead || companion.MaxHp <= 0.0 || companion.Hp >= companion.MaxHp || !TrySourceItem(data, itemKey, out string sourceItemKey) || CombatInventory.AvailableCount(owner, itemKey) <= 0)
		{
			return default(MonsterCompanionPotionUseResult);
		}
		double num = ConsumableRules.RollHealingAmount(data, companion, sourceItemKey, random, new ConsumableUseContext
		{
			Automatic = true
		});
		if (!CombatInventory.TryRemove(owner, itemKey, 1L))
		{
			return default(MonsterCompanionPotionUseResult);
		}
		double hpRestored = companion.Heal(num);
		companion.Buffs["_cooldown_item_potion"] = Math.Max(companion.Buffs.GetValueOrDefault("_cooldown_item_potion"), 0.1);
		return new MonsterCompanionPotionUseResult(Success: true, itemKey, sourceItemKey, num, hpRestored);
	}
}
