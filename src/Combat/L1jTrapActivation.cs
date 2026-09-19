namespace IdleLineage.Combat;

public readonly record struct L1jTrapActivation(int SpawnId, int InstanceIndex, int CellX, int CellY, L1jTrapDefinition Definition, bool Detected);
