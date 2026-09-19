using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record SummonUnitPlan(string Form, int Level, double MaxHp, double AttackIntervalSeconds, double AttackRange, double ArmorClass, double DamageReduction, double MeleeHit, double MeleeDamage, int AttackDice, string Element, SummonMagicAttackProfile? MagicAttack, IReadOnlyList<SummonProcProfile> Procs, SummonAoeAttackProfile? AoeAttack, string Avatar = "", double MagicResistance = 0.0, double MoveSpeed = 0.0);
