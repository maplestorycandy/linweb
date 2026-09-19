using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jDungeonRandomEntry(L1jDungeonRandomCell Source, IReadOnlyList<L1jDungeonRandomCell> Destinations, int Heading, string Note);
