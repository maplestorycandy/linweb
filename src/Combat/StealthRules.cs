using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class StealthRules
{
	public const string InvisibleBuffKey = "sk_invisible";

	public const string LegacyStealthCloakKey = "arm_88";

	public static bool IsInvisible(IGameData? data, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Buffs.GetValueOrDefault("sk_invisible") > 0.0)
		{
			return true;
		}
		if (!actor.EquippedItems.TryGetValue("cloak", out ItemStack value) || value == null)
		{
			return false;
		}
		if (value.ItemKey == "arm_88")
		{
			return true;
		}
		JsonObject jsonObject = data?.Item(value.ItemKey);
		bool value2 = default(bool);
		return jsonObject != null && jsonObject["stealth"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value2) && value2;
	}
}
