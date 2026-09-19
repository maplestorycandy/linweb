using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class EquipmentTimerRules
{
	public static List<string>? Tick(IGameData data, Combatant actor, double deltaSeconds)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (deltaSeconds <= 0.0 || actor.EquippedItems.Count == 0)
		{
			return null;
		}
		List<string> list = null;
		foreach (KeyValuePair<string, ItemStack> equippedItem in actor.EquippedItems)
		{
			equippedItem.Deconstruct(out var key, out var value);
			string item = key;
			ItemStack itemStack = value;
			JsonObject jsonObject = data.Item(itemStack.ItemKey);
			if (jsonObject == null)
			{
				continue;
			}
			double num = CombatSkill.ReadDouble(jsonObject, "maxUseTime");
			if (!(num <= 0.0))
			{
				value = itemStack;
				double? remainingUseSeconds = value.RemainingUseSeconds;
				double valueOrDefault = remainingUseSeconds.GetValueOrDefault();
				if (!remainingUseSeconds.HasValue)
				{
					valueOrDefault = num;
					double? num2 = (value.RemainingUseSeconds = valueOrDefault);
				}
				itemStack.RemainingUseSeconds -= deltaSeconds;
				if (itemStack.RemainingUseSeconds <= 0.0)
				{
					(list ?? (list = new List<string>())).Add(item);
				}
			}
		}
		if (list == null)
		{
			return null;
		}
		foreach (string item2 in list)
		{
			actor.EquippedItems.Remove(item2);
		}
		return list;
	}
}
