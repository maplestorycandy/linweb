namespace IdleLineage.Combat;

internal readonly record struct PhysicalHitResult(bool Hit, double Damage, bool Critical, bool Heavy, bool Ranged);
