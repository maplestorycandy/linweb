namespace IdleLineage.Combat;

public readonly record struct ItemGainOptions(ItemGainSource Source = ItemGainSource.Generic, ItemBlessing? FixedBlessing = null, bool Blank = false, bool ForceBlessed = false, bool RollBeforeForceBlessed = false, int ItemLevel = 0, EquipmentAffixDropGrade AffixGrade = EquipmentAffixDropGrade.Normal);
