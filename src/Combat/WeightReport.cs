namespace IdleLineage.Combat;

public readonly record struct WeightReport(double CurrentWeight, double EquipmentWeight, double InventoryWeight, double BaseCapacity, double EquipmentCapacityBonus, double BuffCapacityBonus, double CollectionCapacityBonus, double TotalCapacity, int Percent, int LoadTier, int HitModifier, bool NaturalRegenerationAllowed, bool ActionsAllowed);
