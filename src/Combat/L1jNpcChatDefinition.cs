using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jNpcChatDefinition(int NpcId, string MobKey, L1jNpcChatTiming Timing, string Note, int StartDelayMs, IReadOnlyList<string> ChatTokens, int ChatIntervalMs, bool Shout, bool WorldChat, bool Repeat, int RepeatIntervalMs, int GameTime);
