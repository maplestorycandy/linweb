using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class ItemStack
{
	public string Uid { get; init; }

	public string ItemKey { get; init; }

	public long Quantity { get; set; }

	public int Enhancement { get; set; }

	public ItemBlessing Blessing { get; set; }

	public int BrokenBladeStacks { get; set; }

	public int ChargeCount { get; set; }

	public bool Locked { get; set; }

	public bool IsIdentified { get; set; } = true;

	public int ItemLevel { get; init; }

	public IReadOnlyList<EquipmentAffixRoll> Affixes { get; init; } = Array.Empty<EquipmentAffixRoll>();

	public string PetUid { get; set; } = "";

	public double? OilPercent { get; set; }

	public int MonsterCardLevel { get; set; }

	public double MonsterCardExperience { get; set; }

	public long MonsterCardReadyAtUnixMilliseconds { get; set; }

	public long ItemDelayReadyAtUnixMilliseconds { get; set; }

	public int AttrEnchantKind { get; set; }

	public int AttrEnchantLevel { get; set; }

	public bool Sealed { get; set; }

	public double? RemainingUseSeconds { get; set; }

	public bool HasUniqueState
	{
		get
		{
			if (BrokenBladeStacks <= 0 && ChargeCount <= 0 && PetUid.Length <= 0 && MonsterCardLevel <= 0 && MonsterCardReadyAtUnixMilliseconds <= 0 && AttrEnchantLevel <= 0 && !OilPercent.HasValue && !RemainingUseSeconds.HasValue && !Sealed && ItemLevel <= 0)
			{
				return Affixes.Count > 0;
			}
			return true;
		}
	}

	public ItemStackSignature Signature => ItemStackSignature.From(this);

	public ItemStack(string uid, string itemKey, long quantity = 1L)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		Uid = uid;
		ItemKey = itemKey;
		Quantity = quantity;
	}

	public ItemStack Copy(string? uid = null, long? quantity = null)
	{
		return new ItemStack(uid ?? Uid, ItemKey, quantity ?? Quantity)
		{
			Enhancement = Enhancement,
			Blessing = Blessing,
			BrokenBladeStacks = BrokenBladeStacks,
			ChargeCount = ChargeCount,
			Locked = Locked,
			IsIdentified = IsIdentified,
			ItemLevel = ItemLevel,
			Affixes = Affixes.ToArray(),
			PetUid = PetUid,
			OilPercent = OilPercent,
			MonsterCardLevel = MonsterCardLevel,
			MonsterCardExperience = MonsterCardExperience,
			MonsterCardReadyAtUnixMilliseconds = MonsterCardReadyAtUnixMilliseconds,
			ItemDelayReadyAtUnixMilliseconds = ItemDelayReadyAtUnixMilliseconds,
			AttrEnchantKind = AttrEnchantKind,
			AttrEnchantLevel = AttrEnchantLevel,
			RemainingUseSeconds = RemainingUseSeconds,
			Sealed = Sealed
		};
	}
}
