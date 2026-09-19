namespace IdleLineage.Combat;

public readonly record struct L1jNpcChatEmission(Combatant Speaker, string ChatToken, L1jNpcChatTiming Timing, bool Shout, bool WorldChat);
