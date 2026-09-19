using System;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class RoiBagRules
{
	public const int BagItemId = 41003;

	public const int InventorySlotGuard = int.MaxValue;

	public const int WeightPercentGuard = 90;

	private static readonly (int ItemId, long Quantity)[] Rewards = new(int, long)[5]
	{
		(40089, 1L),
		(40015, 1L),
		(40016, 1L),
		(40088, 1L),
		(40308, 10000L)
	};

	public static bool IsDefinition(JsonObject? definition)
	{
		return CombatSkill.ReadInt(definition ?? new JsonObject(), "l1jItemId") == 41003;
	}

	public static RoiBagResult TryOpen(IGameData data, Combatant owner, string bagUid, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(bagUid, "bagUid");
		ArgumentNullException.ThrowIfNull(random, "random");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack itemStack2) => itemStack2.Uid == bagUid);
		if (itemStack == null || !IsDefinition(data.Item(itemStack.ItemKey)))
		{
			return new RoiBagResult(Success: false, RoiBagFailure.BagMissing, "", 0, 0L);
		}
		if (itemStack.Locked)
		{
			return new RoiBagResult(Success: false, RoiBagFailure.BagLocked, "", 0, 0L);
		}
		if (!GameRateConfig.DisableWeightPenalty && WeightRules.Evaluate(data, owner).Percent > 90)
		{
			return new RoiBagResult(Success: false, RoiBagFailure.Overweight, "", 0, 0L);
		}
		int num = Math.Clamp((int)(random.NextDouble() * (double)Rewards.Length), 0, Rewards.Length - 1);
		(int ItemId, long Quantity) tuple = Rewards[num];
		int item = tuple.ItemId;
		long item2 = tuple.Quantity;
		string text = ((item == 40308) ? "gold" : L1jJavaNpcInteractionRules.FindItemKey(data, item));
		if (text == null)
		{
			return new RoiBagResult(Success: false, RoiBagFailure.RewardMissing, "", 0, 0L);
		}
		if (CombatInventory.Count(owner, text) > long.MaxValue - item2)
		{
			return new RoiBagResult(Success: false, RoiBagFailure.QuantityOverflow, "", 0, 0L);
		}
		if (!ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, bagUid, 1L, out ItemStack _))
		{
			return new RoiBagResult(Success: false, RoiBagFailure.BagMissing, "", 0, 0L);
		}
		CombatInventory.SyncLegacyView(owner);
		CombatInventory.Add(owner, text, item2);
		return new RoiBagResult(Success: true, RoiBagFailure.None, text, item, item2);
	}
}
