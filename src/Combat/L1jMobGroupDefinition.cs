using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jMobGroupDefinition(int GroupId, string Note, bool RemoveGroupIfLeaderDies, int LeaderNpcId, string LeaderMobKey, IReadOnlyList<L1jMobGroupMinionDefinition> Minions);
