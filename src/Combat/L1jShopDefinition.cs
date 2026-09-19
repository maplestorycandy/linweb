using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jShopDefinition(int NpcId, string Name, string Impl, IReadOnlyList<L1jShopItem> Items);
