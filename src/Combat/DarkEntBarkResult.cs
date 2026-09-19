namespace IdleLineage.Combat;

public readonly record struct DarkEntBarkResult(bool Attempted, bool Transformed, DarkEntBarkFailure Failure, string FormName);
