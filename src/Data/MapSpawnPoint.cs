using IdleLineage.Combat;

namespace IdleLineage.Data;

public readonly record struct MapSpawnPoint(string SlotKey, string MobKey, MapSpawnCell Cell, bool IsBoss, int RespawnMinimumSeconds = 0, int RespawnMaximumSeconds = 0, MapSpawnBounds? Area = null, int RandomX = 0, int RandomY = 0, MapSpawnCell? TargetCell = null);
