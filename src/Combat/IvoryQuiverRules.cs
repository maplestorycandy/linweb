using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class IvoryQuiverRules
{
	public const int QuiverItemId = 49550;

	public const int ArrowItemId = 49551;

	public const long ArrowQuantity = 1000L;

	public const long CooldownSeconds = 86400L;

	public const int InventorySlotGuard = int.MaxValue;

	public const int WeightPercentGuard = 90;

	public static IvoryQuiverUseResult TryUse(IGameData data, Combatant owner, string sourceUid, long nowUnixMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceUid, "sourceUid");
		if (nowUnixMilliseconds < 0)
		{
			throw new ArgumentOutOfRangeException("nowUnixMilliseconds");
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, sourceUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.ItemNotFound, 0L, 0L);
		}
		if (!HasMainItemId(data.Item(itemStack.ItemKey), 49550))
		{
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.InvalidItem, 0L, 0L);
		}
		if (itemStack.ItemDelayReadyAtUnixMilliseconds > nowUnixMilliseconds)
		{
			long num = itemStack.ItemDelayReadyAtUnixMilliseconds - nowUnixMilliseconds;
			long remainingCooldownSeconds = Math.Max(1L, (num + 999) / 1000);
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.CooldownActive, 0L, remainingCooldownSeconds);
		}
		if (!GameRateConfig.DisableWeightPenalty && WeightRules.Evaluate(data, owner).Percent > 90)
		{
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.Overweight, 0L, 0L);
		}
		string text = FindUniqueItemKey(data, 49551);
		if (text == null)
		{
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.RewardMissing, 0L, 0L);
		}
		if (CombatInventory.Count(owner, text) > 9223372036854774807L)
		{
			return new IvoryQuiverUseResult(Success: false, IvoryQuiverFailure.QuantityOverflow, 0L, 0L);
		}
		CombatInventory.Add(owner, text, 1000L);
		itemStack.ItemDelayReadyAtUnixMilliseconds = checked(nowUnixMilliseconds + 86400000);
		return new IvoryQuiverUseResult(Success: true, IvoryQuiverFailure.None, 1000L, 0L);
	}

	public static bool IsDefinition(JsonObject? definition)
	{
		return HasMainItemId(definition, 49550);
	}

	private static string? FindUniqueItemKey(IGameData data, int itemId)
	{
		string text = null;
		foreach (var (text3, jsonNode2) in data.Items)
		{
			if (HasMainItemId(jsonNode2 as JsonObject, itemId))
			{
				if (text != null)
				{
					return null;
				}
				text = text3;
			}
		}
		return text;
	}

	private static bool HasMainItemId(JsonObject? definition, int itemId)
	{
		if (!(definition?["l1jItemId"] is JsonValue jsonValue))
		{
			return false;
		}
		if (jsonValue.TryGetValue<int>(out var value))
		{
			return value == itemId;
		}
		return false;
	}
}
