using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class LanternRules
{
	public const string Slot = "lantern";

	public const string LanternEffect = "lantern";

	public const string OilEffect = "lamp_oil";

	public const double FullOilPercent = 100.0;

	public const double BurnPercentPerMinute = 1.0;

	public static bool IsLanternDefinition(JsonObject? definition)
	{
		bool flag = ReadEffect(definition) == "lantern";
		if (!flag)
		{
			int num = MainItemId(definition);
			bool flag2 = ((num == 40001 || num == 40005) ? true : false);
			flag = flag2;
		}
		return flag;
	}

	public static bool IsOilDefinition(JsonObject? definition)
	{
		return ReadEffect(definition) == "lamp_oil";
	}

	public static double OilOf(ItemStack lantern)
	{
		ArgumentNullException.ThrowIfNull(lantern, "lantern");
		return Math.Clamp(lantern.OilPercent ?? 100.0, 0.0, 100.0);
	}

	public static ItemStack? EquippedLantern(Combatant? owner)
	{
		return owner?.EquippedItems.GetValueOrDefault("lantern");
	}

	public static bool IsLit(Combatant? owner)
	{
		ItemStack itemStack = EquippedLantern(owner);
		if (itemStack != null)
		{
			return OilOf(itemStack) > 0.0;
		}
		return false;
	}

	public static void Burn(Combatant owner, double deltaSeconds)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (deltaSeconds <= 0.0)
		{
			return;
		}
		ItemStack itemStack = EquippedLantern(owner);
		if (itemStack != null)
		{
			double num = OilOf(itemStack);
			if (num <= 0.0)
			{
				itemStack.OilPercent = 0.0;
				return;
			}
			double num2 = (itemStack.ItemKey.EndsWith("40005", StringComparison.Ordinal) ? 12000 : 6000);
			itemStack.OilPercent = Math.Max(0.0, num - deltaSeconds * (100.0 / num2));
		}
	}

	public static ItemStack? Refill(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ItemStack itemStack = EquippedLantern(owner);
		if (itemStack != null && OilOf(itemStack) < 100.0)
		{
			itemStack.OilPercent = 100.0;
			return itemStack;
		}
		ItemStack itemStack2 = null;
		foreach (ItemStack inventoryStack in owner.InventoryStacks)
		{
			if (IsLanternDefinition(data.Item(inventoryStack.ItemKey)) && !(OilOf(inventoryStack) >= 100.0) && (itemStack2 == null || OilOf(inventoryStack) < OilOf(itemStack2)))
			{
				itemStack2 = inventoryStack;
			}
		}
		if (itemStack2 == null)
		{
			return null;
		}
		itemStack2.OilPercent = 100.0;
		return itemStack2;
	}

	private static string ReadEffect(JsonObject? definition)
	{
		if (!(definition?["eff"] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static int MainItemId(JsonObject? definition)
	{
		if (definition != null)
		{
			return CombatSkill.ReadInt(definition, "l1jItemId");
		}
		return 0;
	}
}
