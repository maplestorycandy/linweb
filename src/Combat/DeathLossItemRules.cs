using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class DeathLossItemRules
{
	public static bool CanDrop(IGameData? data, string itemKey)
	{
		if (data == null || string.IsNullOrWhiteSpace(itemKey))
		{
			return true;
		}
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return true;
		}
		if (MonsterCardRules.IsCardDefinition(jsonObject))
		{
			return false;
		}
		if (!ReadFlag(jsonObject, "noJunk") && !ReadFlag(jsonObject, "noSell"))
		{
			return !ReadFlag(jsonObject, "noTrade");
		}
		return false;
	}

	public static bool CanDrop(IGameData? data, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (!string.IsNullOrEmpty(stack.PetUid))
		{
			return false;
		}
		return CanDrop(data, stack.ItemKey);
	}

	private static bool ReadFlag(JsonObject definition, string field)
	{
		bool value = default(bool);
		return definition[field] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
