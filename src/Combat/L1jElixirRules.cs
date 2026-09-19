using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jElixirRules
{
	public const int StrengthItemId = 40033;

	public const int ConstitutionItemId = 40034;

	public const int DexterityItemId = 40035;

	public const int IntelligenceItemId = 40036;

	public const int WisdomItemId = 40037;

	public const int CharismaItemId = 40038;

	public const int MaximumElixirs = 5;

	public const int MaximumBaseAttribute = 35;

	public static bool TryRead(IGameData? data, string itemKey, out L1jElixirSpec spec)
	{
		spec = Spec(CombatSkill.ReadInt(data?.Item(itemKey) ?? new JsonObject(), "l1jItemId"));
		return spec.ItemId != 0;
	}

	public static L1jElixirResult TryUse(IGameData data, Combatant actor, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (actor.Kind != CombatantKind.Player)
		{
			return Fail(L1jElixirFailure.UnsupportedActor);
		}
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null || itemStack.Quantity <= 0)
		{
			return Fail(L1jElixirFailure.ItemMissing);
		}
		if (itemStack.Locked)
		{
			return Fail(L1jElixirFailure.ItemLocked);
		}
		if (!TryRead(data, itemStack.ItemKey, out var spec))
		{
			return Fail(L1jElixirFailure.NotElixir);
		}
		if (!HasValidState(actor))
		{
			return Fail(L1jElixirFailure.InvalidElixirState, spec);
		}
		if (actor.ElixirStatus >= 5)
		{
			return Fail(L1jElixirFailure.ElixirMaximum, spec);
		}
		int num = EffectiveBaseAttribute(actor, spec.AttributeKey);
		if (num >= 35)
		{
			return Fail(L1jElixirFailure.AttributeMaximum, spec, num);
		}
		if (!ItemStackInventory.TryRemove(actor.InventoryStacks, itemUid, 1L, () => CombatInventory.NextUid(actor), out ItemStack _))
		{
			return Fail(L1jElixirFailure.ItemMissing, spec, num);
		}
		actor.ElixirBonuses[spec.AttributeKey] = actor.ElixirBonuses.GetValueOrDefault(spec.AttributeKey) + 1;
		actor.ElixirStatus++;
		CombatInventory.SyncLegacyView(actor);
		CombatantBuilder.RefreshPlayer(actor, data);
		return new L1jElixirResult(Success: true, L1jElixirFailure.None, spec.AttributeKey, spec.AttributeName, num + 1, actor.ElixirStatus);
		L1jElixirResult Fail(L1jElixirFailure failure, L1jElixirSpec failedSpec = default(L1jElixirSpec), int value = 0)
		{
			return new L1jElixirResult(Success: false, failure, failedSpec.AttributeKey ?? "", failedSpec.AttributeName ?? "", value, actor.ElixirStatus);
		}
	}

	public static bool HasValidState(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		int elixirStatus = actor.ElixirStatus;
		if ((elixirStatus < 0 || elixirStatus > 5) ? true : false)
		{
			return false;
		}
		elixirStatus = actor.UnspentElixirStatPoints;
		if ((elixirStatus < 0 || elixirStatus > 5) ? true : false)
		{
			return false;
		}
		int num = 0;
		foreach (KeyValuePair<string, int> elixirBonuse in actor.ElixirBonuses)
		{
			elixirBonuse.Deconstruct(out var key, out elixirStatus);
			string key2 = key;
			int num2 = elixirStatus;
			if (!IsAttributeKey(key2) || num2 < 0 || num2 > 5)
			{
				return false;
			}
			num += num2;
		}
		return num + actor.UnspentElixirStatPoints == actor.ElixirStatus;
	}

	public static int EffectiveBaseAttribute(Combatant actor, string attributeKey)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = attributeKey switch
		{
			"str" => actor.Base.Str, 
			"con" => actor.Base.Con, 
			"dex" => actor.Base.Dex, 
			"int" => actor.Base.Int, 
			"wis" => actor.Base.Wis, 
			"cha" => actor.Base.Cha, 
			_ => throw new ArgumentOutOfRangeException("attributeKey"), 
		};
		double num2 = Math.Max(num, Math.Min(18.0, num + (double)Math.Max(0, actor.Allocations.GetValueOrDefault(attributeKey))));
		return (int)Math.Min(35.0, num2 + (double)Math.Max(0, actor.LevelStatBonuses.GetValueOrDefault(attributeKey)) + (double)Math.Max(0, actor.ElixirBonuses.GetValueOrDefault(attributeKey)));
	}

	public static bool IsAttributeKey(string key)
	{
		switch (key)
		{
		case "str":
		case "con":
		case "dex":
		case "int":
		case "wis":
		case "cha":
			return true;
		default:
			return false;
		}
	}

	private static L1jElixirSpec Spec(int itemId)
	{
		return itemId switch
		{
			40033 => new L1jElixirSpec(itemId, "str", "力量"), 
			40034 => new L1jElixirSpec(itemId, "con", "體質"), 
			40035 => new L1jElixirSpec(itemId, "dex", "敏捷"), 
			40036 => new L1jElixirSpec(itemId, "int", "智慧"), 
			40037 => new L1jElixirSpec(itemId, "wis", "精神"), 
			40038 => new L1jElixirSpec(itemId, "cha", "魅力"), 
			_ => default(L1jElixirSpec), 
		};
	}
}
