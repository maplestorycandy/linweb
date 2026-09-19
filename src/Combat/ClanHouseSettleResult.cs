namespace IdleLineage.Combat;

public readonly record struct ClanHouseSettleResult(bool Changed, bool AcquiredHouse, bool LostForUnpaidTax, bool SaleExpired, int HouseId);
