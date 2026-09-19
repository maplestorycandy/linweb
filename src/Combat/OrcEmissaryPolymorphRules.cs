using System;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class OrcEmissaryPolymorphRules
{
	public const int ItemId = 49220;

	public const int Gfx = 6984;

	public const int DurationSeconds = 900;

	public const string Effect = "orc_emissary_poly";

	public static bool IsDefinition(JsonObject? definition)
	{
		if (definition != null && CombatSkill.ReadInt(definition, "l1jItemId") == 49220)
		{
			return string.Equals(definition["eff"]?.GetValue<string>(), "orc_emissary_poly", StringComparison.Ordinal);
		}
		return false;
	}

	public static PolymorphResult TryUse(IGameData data, Combatant owner, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == itemUid);
		if (itemStack == null)
		{
			return PolymorphResult.Fail(PolymorphFailure.ItemNotFound);
		}
		if (!IsDefinition(data.Item(itemStack.ItemKey)))
		{
			return PolymorphResult.Fail(PolymorphFailure.NotAPolymorphScroll);
		}
		if (itemStack.Locked)
		{
			return PolymorphResult.Fail(PolymorphFailure.ItemLocked);
		}
		owner.PolymorphForm = "妖魔密使";
		owner.Buffs["poly"] = 900.0;
		itemStack.Quantity--;
		if (itemStack.Quantity <= 0)
		{
			owner.InventoryStacks.Remove(itemStack);
		}
		CombatInventory.SyncLegacyView(owner);
		return new PolymorphResult(Success: true, "妖魔密使", PolymorphFailure.None);
	}
}
