namespace IdleLineage.Combat;

public readonly record struct ExplorationSpawnPlan(ExplorationSpawnKind Kind, string? MobKey, MapSpawnCell Cell, string SlotKey = "", MapSpawnCell? TargetCell = null);
