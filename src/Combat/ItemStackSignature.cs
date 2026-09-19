using System;
using System.Globalization;
using System.Linq;

namespace IdleLineage.Combat;

public readonly record struct ItemStackSignature(string ItemKey, int Enhancement, ItemBlessing Blessing, int BrokenBladeStacks, int AttrEnchantKind, int AttrEnchantLevel, bool IsIdentified, int ItemLevel, string AffixSignature)
{
	public static ItemStackSignature From(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		return new ItemStackSignature(item.ItemKey, item.Enhancement, item.Blessing, Math.Max(0, item.BrokenBladeStacks), item.AttrEnchantKind, item.AttrEnchantLevel, item.IsIdentified, item.ItemLevel, string.Join(";", item.Affixes.Select((EquipmentAffixRoll affix) => string.Join(":", affix.AffixId, affix.Tier.ToString(CultureInfo.InvariantCulture), affix.Value.ToString("R", CultureInfo.InvariantCulture)))));
	}
}
