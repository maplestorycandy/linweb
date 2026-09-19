using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PainwandRules
{
	public const string ItemKey = "l1j_item_40006";

	public const double LifetimeSeconds = 60.0;

	private static readonly int[] MobIds = new int[25]
	{
		45008, 45140, 45016, 45021, 45025, 45033, 45099, 45147, 45123, 45130,
		45046, 45092, 45138, 45098, 45127, 45143, 45149, 45171, 45040, 45155,
		45192, 45173, 45213, 45079, 45144
	};

	public static IReadOnlyList<int> CanonicalMobIds => MobIds;

	public static bool IsDefinition(JsonObject? definition)
	{
		if (definition?["eff"]?.GetValue<string>() == "painwand")
		{
			JsonNode? jsonNode = definition["l1jItemId"];
			if (jsonNode == null)
			{
				return false;
			}
			return jsonNode.GetValue<int>() == 40006;
		}
		return false;
	}

	public static PainwandUseResult TryUse(IGameData data, Combatant user, string itemUid, bool mapAllowsPainwand, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(user, "user");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (user.Kind != CombatantKind.Player || user.Dead)
		{
			return Failed(PainwandFailure.InvalidActor);
		}
		ItemStack itemStack = user.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null)
		{
			return Failed(PainwandFailure.ItemMissing);
		}
		if (itemStack.Locked)
		{
			return Failed(PainwandFailure.ItemLocked);
		}
		JsonObject jsonObject = data.Item(itemStack.ItemKey);
		if (!IsDefinition(jsonObject))
		{
			return Failed(PainwandFailure.NotPainwand);
		}
		if (!mapAllowsPainwand)
		{
			return Failed(PainwandFailure.MapBlocked);
		}
		int valueOrDefault = (jsonObject?["maxChargeCount"]?.GetValue<int>()).GetValueOrDefault();
		if (valueOrDefault <= 0 || itemStack.ChargeCount < 0 || itemStack.ChargeCount > valueOrDefault)
		{
			return Failed(PainwandFailure.InvalidDefinition);
		}
		int num = Math.Min(MobIds.Length - 1, (int)Math.Floor(Math.Clamp(random.NextDouble(), 0.0, Math.BitDecrement(1.0)) * (double)MobIds.Length));
		string text = $"l1j_{MobIds[num]}";
		if (data.Mob(text) == null)
		{
			return Failed(PainwandFailure.InvalidDefinition);
		}
		if (!ItemStackInventory.TryDetachOne(user.InventoryStacks, itemStack.Uid, NewUid, out ItemStack detached) || detached == null)
		{
			return Failed(PainwandFailure.ItemMissing);
		}
		bool flag = detached != itemStack;
		if (detached.ChargeCount == 0)
		{
			detached.ChargeCount = valueOrDefault;
		}
		detached.ChargeCount--;
		int chargeCount = detached.ChargeCount;
		ItemStack stored;
		if (chargeCount == 0)
		{
			if (!flag)
			{
				user.InventoryStacks.Remove(detached);
			}
		}
		else if (flag && !ItemStackInventory.TryAddOrStack(user.InventoryStacks, detached, out stored))
		{
			return Failed(PainwandFailure.ItemMissing);
		}
		CombatInventory.SyncLegacyView(user);
		return new PainwandUseResult(Success: true, PainwandFailure.None, text, chargeCount);
	}

	private static string NewUid()
	{
		return $"painwand-{Guid.NewGuid():N}";
	}

	private static PainwandUseResult Failed(PainwandFailure failure)
	{
		return new PainwandUseResult(Success: false, failure, string.Empty, 0);
	}
}
