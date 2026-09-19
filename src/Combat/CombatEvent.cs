using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly struct CombatEvent(CombatEventKind kind, Combatant? source = null, Combatant? target = null, double amount = 0.0, bool crit = false, DamageType dmgType = DamageType.Melee, string? element = null, string? skillId = null, string? statusKind = null, string? buffName = null, string? itemKey = null, int intArg = 0, ItemBlessing itemBlessing = ItemBlessing.Normal, int itemEnhancement = 0, bool itemIdentified = false, int itemLevel = 0, IReadOnlyList<EquipmentAffixRoll>? itemAffixes = null, string? text = null, double x = 0.0, double y = 0.0, double endX = 0.0, double endY = 0.0, double speed = 0.0)
{
	public readonly CombatEventKind Kind = kind;

	public readonly Combatant? Source = source;

	public readonly Combatant? Target = target;

	public readonly double Amount = amount;

	public readonly bool Crit = crit;

	public readonly DamageType DmgType = dmgType;

	public readonly string? Element = element;

	public readonly string? SkillId = skillId;

	public readonly string? StatusKind = statusKind;

	public readonly string? BuffName = buffName;

	public readonly string? ItemKey = itemKey;

	public readonly ItemBlessing ItemBlessing = itemBlessing;

	public readonly int ItemEnhancement = itemEnhancement;

	public readonly bool ItemIdentified = itemIdentified;

	public readonly int ItemLevel = itemLevel;

	public readonly IReadOnlyList<EquipmentAffixRoll> ItemAffixes = itemAffixes ?? Array.Empty<EquipmentAffixRoll>();

	public readonly int IntArg = intArg;

	public readonly string? Text = text;

	public readonly double X = x;

	public readonly double Y = y;

	public readonly double EndX = endX;

	public readonly double EndY = endY;

	public readonly double Speed = speed;

	public static CombatEvent Attack(Combatant src, Combatant tgt, int dir = 0)
	{
		return new CombatEvent(CombatEventKind.Attack, src, tgt, 0.0, crit: false, DamageType.Melee, null, null, null, null, null, dir);
	}

	public static CombatEvent Cast(Combatant src, string skillId, Combatant? tgt = null, bool isScroll = false)
	{
		return new CombatEvent(CombatEventKind.Cast, src, tgt, 0.0, crit: false, DamageType.Melee, null, skillId, null, null, null, isScroll ? 1 : 0);
	}

	public static CombatEvent Damage(Combatant src, Combatant tgt, double amount, DamageType type, bool crit = false, string? element = null)
	{
		return new CombatEvent(CombatEventKind.Damage, src, tgt, amount, crit, type, element);
	}

	public static CombatEvent Miss(Combatant src, Combatant tgt)
	{
		return new CombatEvent(CombatEventKind.Miss, src, tgt);
	}

	public static CombatEvent Heal(Combatant src, Combatant tgt, double amount)
	{
		return new CombatEvent(CombatEventKind.Heal, src, tgt, amount);
	}

	public static CombatEvent MpChange(Combatant who, double delta)
	{
		return new CombatEvent(CombatEventKind.MpChange, who, who, delta);
	}

	public static CombatEvent StatusAdd(Combatant tgt, string kind, int ticks)
	{
		return new CombatEvent(CombatEventKind.StatusAdd, null, tgt, 0.0, crit: false, DamageType.Melee, null, null, kind, null, null, ticks);
	}

	public static CombatEvent StatusRemove(Combatant tgt, string kind)
	{
		return new CombatEvent(CombatEventKind.StatusRemove, null, tgt, 0.0, crit: false, DamageType.Melee, null, null, kind);
	}

	public static CombatEvent BuffAdd(Combatant who, string name)
	{
		return new CombatEvent(CombatEventKind.BuffAdd, who, who, 0.0, crit: false, DamageType.Melee, null, null, null, name);
	}

	public static CombatEvent BuffRemove(Combatant who, string name)
	{
		return new CombatEvent(CombatEventKind.BuffRemove, who, who, 0.0, crit: false, DamageType.Melee, null, null, null, name);
	}

	public static CombatEvent Move(Combatant who)
	{
		return new CombatEvent(CombatEventKind.Move, who, who, 0.0, crit: false, DamageType.Melee, null, null, null, null, null, who.Facing8, ItemBlessing.Normal, 0, itemIdentified: false, 0, null, null, who.Pos.X, who.Pos.Y);
	}

	public static CombatEvent Projectile(Combatant src, Combatant tgt, string kind, int dir = 0, double speed = 420.0)
	{
		return Projectile(src, tgt, kind, src.Pos, tgt.Pos, speed, dir);
	}

	public static CombatEvent Projectile(Combatant src, Combatant tgt, string kind, WorldPoint start, WorldPoint aim, double speed, int dir = 0)
	{
		return new CombatEvent(CombatEventKind.Projectile, src, tgt, 0.0, crit: false, DamageType.Melee, null, kind, null, null, null, dir, ItemBlessing.Normal, 0, itemIdentified: false, 0, null, null, start.X, start.Y, aim.X, aim.Y, speed);
	}

	public static CombatEvent Spawn(Combatant who)
	{
		return new CombatEvent(CombatEventKind.Spawn, null, who, 0.0, crit: false, DamageType.Melee, null, null, null, null, null, 0, ItemBlessing.Normal, 0, itemIdentified: false, 0, null, null, who.Pos.X, who.Pos.Y);
	}

	public static CombatEvent Death(Combatant who, Combatant? killer = null)
	{
		return new CombatEvent(CombatEventKind.Death, killer, who);
	}

	public static CombatEvent LevelUp(Combatant who, int level)
	{
		return new CombatEvent(CombatEventKind.LevelUp, who, who, 0.0, crit: false, DamageType.Melee, null, null, null, null, null, level);
	}

	public static CombatEvent ExpGain(Combatant who, double amount)
	{
		return new CombatEvent(CombatEventKind.ExpGain, who, who, amount);
	}

	public static CombatEvent GoldGain(Combatant who, double amount)
	{
		return new CombatEvent(CombatEventKind.GoldGain, who, who, amount);
	}

	public static CombatEvent Drop(Combatant from, string itemKey, int qty, ItemBlessing itemBlessing = ItemBlessing.Normal, int itemEnhancement = 0, bool itemIdentified = false, int itemLevel = 0, IReadOnlyList<EquipmentAffixRoll>? itemAffixes = null)
	{
		return new CombatEvent(CombatEventKind.Drop, from, null, 0.0, crit: false, DamageType.Melee, null, null, null, null, itemKey, qty, itemBlessing, itemEnhancement, itemIdentified, itemLevel, itemAffixes, null, from.Pos.X, from.Pos.Y);
	}

	public static CombatEvent ItemGain(Combatant owner, string itemKey, int qty)
	{
		return new CombatEvent(CombatEventKind.ItemGain, owner, owner, 0.0, crit: false, DamageType.Melee, null, null, null, null, itemKey, qty);
	}

	public static CombatEvent LogLine(string text)
	{
		return new CombatEvent(CombatEventKind.Log, null, null, 0.0, crit: false, DamageType.Melee, null, null, null, null, null, 0, ItemBlessing.Normal, 0, itemIdentified: false, 0, null, text);
	}
}
