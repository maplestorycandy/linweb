using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record L1jMapRule(int MapId, string LocationName, L1jMapBounds Bounds, double MonsterAmount, double DropRate, bool Underwater, bool Markable, bool Teleportable, bool Escapable, bool Resurrection, bool Painwand, bool Penalty, bool TakePets, bool RecallPets, bool UsableItem, bool UsableSkill, IReadOnlyList<string> MapKeys);
