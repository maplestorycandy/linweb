namespace IdleLineage.Combat;

public sealed record SummonFormInfo(string Name, int RequiredLevel, int RequiredCharisma, bool Unlocked, bool NeedsControl, string LockReason);
