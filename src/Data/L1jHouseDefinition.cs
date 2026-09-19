namespace IdleLineage.Data;

public sealed record L1jHouseDefinition(int HouseId, string Name, int Area, string Location, int KeeperId, string City, int Number, bool Operational, L1jHouseKeeper? Keeper, L1jHouseBasement? Basement);
