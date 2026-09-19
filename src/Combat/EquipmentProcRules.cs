using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class EquipmentProcRules
{
	public static WeaponDirectStatusProc? DirectStatus(IGameData? data, Combatant actor)
	{
		if (!(EquippedDefinition(data, actor, "wpn")?["procStatus"] is JsonObject jsonObject))
		{
			return null;
		}
		string text = CombatSkill.ReadString(jsonObject, "kind");
		double num = Math.Clamp(CombatSkill.ReadDouble(jsonObject, "rate"), 0.0, 100.0);
		int durationTicks = Math.Max(1, ((jsonObject["dur"] == null) ? 6 : CombatSkill.ReadInt(jsonObject, "dur")) * 10);
		if (text.Length <= 0 || !(num > 0.0))
		{
			return null;
		}
		return new WeaponDirectStatusProc(text, num, durationTicks);
	}

	private static JsonObject? EquippedDefinition(IGameData? data, Combatant actor, string slot)
	{
		if (data == null || !actor.EquippedItems.TryGetValue(slot, out ItemStack value))
		{
			return null;
		}
		return data.Item(value.ItemKey);
	}
}
