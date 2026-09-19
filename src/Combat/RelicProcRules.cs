using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class RelicProcRules
{
	public static RelicWeaponSpellProc? WeaponSpell(IGameData? data, Combatant actor)
	{
		JsonObject jsonObject = MainWeapon(data, actor);
		if (!(jsonObject?["spellProc"] is JsonObject jsonObject2))
		{
			return null;
		}
		string text = CombatSkill.ReadString(jsonObject2, "skn");
		if (text.Length == 0)
		{
			text = CombatSkill.ReadString(jsonObject2, "skill");
		}
		if (text.Length == 0)
		{
			text = "weapon-spell";
		}
		int diceCount = 0;
		int diceSides = 0;
		if (jsonObject2["dice"] is JsonArray { Count: >=2 } jsonArray)
		{
			diceCount = Math.Max(0, jsonArray[0]?.GetValue<int>() ?? 0);
			diceSides = Math.Max(0, jsonArray[1]?.GetValue<int>() ?? 0);
		}
		JsonObject jsonObject3 = jsonObject2["status"] as JsonObject;
		int val = (jsonObject2.ContainsKey("fix") ? CombatSkill.ReadInt(jsonObject2, "fix") : (jsonObject2.ContainsKey("dmg") ? CombatSkill.ReadInt(jsonObject2, "dmg") : CombatSkill.ReadInt(jsonObject2, "flat")));
		double procRate = CombatSkill.ReadDouble(jsonObject2, "pct", CombatSkill.ReadDouble(jsonObject2, "chance", CombatSkill.ReadDouble(jsonObject, "procRateBase", 1.0)));
		string targetMode = CombatSkill.ReadString(jsonObject2, "target");
		int area = CombatSkill.ReadInt(jsonObject2, "area");
		if (string.Equals(targetMode, "area", StringComparison.OrdinalIgnoreCase) && area == 0) area = 3;

		return new RelicWeaponSpellProc(
			text,
			Math.Clamp(procRate, 0.0, 100.0),
			Math.Max(0, val),
			Math.Max(0, CombatSkill.ReadInt(jsonObject2, "rnd")),
			CombatSkill.NormalizeElement(CombatSkill.ReadString(jsonObject2, "ele")),
			area,
			diceCount,
			diceSides,
			(jsonObject3 == null) ? targetMode : (string.IsNullOrEmpty(CombatSkill.ReadString(jsonObject3, "kind")) ? targetMode : CombatSkill.ReadString(jsonObject3, "kind")),
			(jsonObject3 == null) ? 0.0 : Math.Clamp(CombatSkill.ReadDouble(jsonObject3, "pct"), 0.0, 100.0),
			(jsonObject3 != null) ? Math.Max(0, CombatSkill.ReadInt(jsonObject3, "dur") * 10) : 0
		);
	}

	public static IEnumerable<(string ItemName, RelicWeaponSpellProc Proc)> ArmorSpells(IGameData? data, Combatant actor)
	{
		if (data == null || actor.EquippedItems == null) yield break;
		foreach (var kvp in actor.EquippedItems)
		{
			if (string.Equals(kvp.Key, "wpn", StringComparison.OrdinalIgnoreCase)) continue;
			if (kvp.Value == null || string.IsNullOrWhiteSpace(kvp.Value.ItemKey)) continue;
			JsonObject? itemObj = data.Item(kvp.Value.ItemKey);
			if (itemObj?["spellProc"] is JsonObject spObj)
			{
				string text = CombatSkill.ReadString(spObj, "skn");
				if (string.IsNullOrEmpty(text)) text = CombatSkill.ReadString(spObj, "skill");
				if (string.IsNullOrEmpty(text)) continue;

				double procRate = CombatSkill.ReadDouble(spObj, "pct", CombatSkill.ReadDouble(spObj, "chance", CombatSkill.ReadDouble(itemObj, "procRateBase", 1.0)));
				int val = spObj.ContainsKey("dmg") ? CombatSkill.ReadInt(spObj, "dmg") : (spObj.ContainsKey("fix") ? CombatSkill.ReadInt(spObj, "fix") : 0);
				string targetMode = spObj.ContainsKey("target") ? CombatSkill.ReadString(spObj, "target") : "self";
				string itemName = itemObj.ContainsKey("n") ? CombatSkill.ReadString(itemObj, "n") : kvp.Value.ItemKey;

				yield return (
					ItemName: itemName,
					Proc: new RelicWeaponSpellProc(
						text,
						Math.Clamp(procRate, 0.0, 100.0),
						val,
						0,
						CombatSkill.NormalizeElement(CombatSkill.ReadString(spObj, "ele")),
						string.Equals(targetMode, "area", StringComparison.OrdinalIgnoreCase) ? 3 : 0,
						0,
						0,
						targetMode,
						100.0,
						0
					)
				);
			}
		}
	}

	public static JsonObject? MainWeapon(IGameData? data, Combatant actor)
	{
		if (data == null)
		{
			return null;
		}
		string text = MainWeaponStack(actor)?.ItemKey ?? actor.MainWeaponId;
		if (text.Length <= 0)
		{
			return null;
		}
		return data.Item(text);
	}

	public static ItemStack? MainWeaponStack(Combatant actor)
	{
		if (!actor.EquippedItems.TryGetValue("wpn", out ItemStack value))
		{
			return null;
		}
		return value;
	}
}
