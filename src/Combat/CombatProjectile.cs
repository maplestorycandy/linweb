namespace IdleLineage.Combat;

public sealed class CombatProjectile
{
	public const string ArrowKind = "arrow";

	public const string BoltKind = "bolt";

	public const double ArrowSpeed = 640.0;

	public const double BoltSpeed = 560.0;

	public const double ArrowTurnRate = 5.0;

	public const double BoltTurnRate = 7.0;

	public const double LifetimeSeconds = 1.8;

	public const double HitRadius = 6.0;

	public const double ShooterChestOffsetY = -42.0;

	public const double TargetChestOffsetY = -30.0;

	public long Id { get; internal init; }

	public Combatant Source { get; internal init; }

	public Combatant Target { get; internal init; }

	public WorldPoint Pos { get; internal set; }

	public WorldPoint GroundPos { get; internal set; }

	public double VelX { get; internal set; }

	public double VelY { get; internal set; }

	public int Facing8 { get; internal set; }

	public double Speed { get; internal init; } = 640.0;

	public double TurnRate { get; internal init; } = 5.0;

	public double RemainingLife { get; internal set; } = 1.8;

	public double Radius { get; internal init; } = 6.0;

	public string Kind { get; internal init; } = "arrow";

	public bool TargetLost { get; internal set; }

	internal bool BasicAttack { get; init; }

	internal bool MagicWeaponAttack { get; init; }

	internal DirectDamageDelivery DamageDelivery { get; init; }

	internal PhysicalHitResult CommittedHit { get; init; }

	internal double CommittedMagicDamage { get; init; }
}
