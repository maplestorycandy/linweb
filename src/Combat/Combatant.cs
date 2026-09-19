using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class Combatant
{
	public CombatantKind Kind = CombatantKind.Mob;

	public bool UsesMonsterTemplate;
	public bool IsRemote;

	public string Key = "";

	public string Disp = "";

	public string Title = "";

	public string ClassId = "";

	public string Avatar = "";
	public string WeaponPrefix = "";

	public string DeathTransformMob = "";
	public string DeathTransformFx = "";
	public double DeathTransformDelay = 1.0;
	public double DeathTransformChance = 100.0;

	public int Level = 1;

	public double Experience;

	public long Gold;

	public double Alignment;

	public double Satiety = 40.0;

	public List<ItemStack> InventoryStacks = new List<ItemStack>();

	public Dictionary<string, ItemStack> EquippedItems = new Dictionary<string, ItemStack>(StringComparer.Ordinal);

	public long ItemUidSequence;

	public CharacterProgress Progress = new CharacterProgress();

	public Dictionary<string, long> Inventory = new Dictionary<string, long>(StringComparer.Ordinal);

	public int BornSeq;

	public double Hp = 1.0;

	public double Mp;

	public double MaxHp = 1.0;

	public double MaxMp;

	public double LastExpShare;

	public long LastGoldShare;

	public Attributes Base = new Attributes();

	public Dictionary<string, int> Allocations = new Dictionary<string, int>(StringComparer.Ordinal);

	public Dictionary<string, int> LevelStatBonuses = new Dictionary<string, int>(StringComparer.Ordinal);

	public Dictionary<string, int> ElixirBonuses = new Dictionary<string, int>(StringComparer.Ordinal);

	public int ElixirStatus;

	public int UnspentElixirStatPoints;

	public Dictionary<string, object?> Equip = new Dictionary<string, object>();

	public ClassKit? Kit;

	public string MainWeaponId = "";

	public string OffhandWeaponId = "";

	public string ElfElement = "";

	public string AutoAttackSkillId = "";

	public PlayerAttackPriority[] AttackPriorities = Array.Empty<PlayerAttackPriority>();

	public CompanionAttackPriority[] CompanionAttackPriorities = Array.Empty<CompanionAttackPriority>();

	public bool AutomaticCombatEnabled = true;

	public LearnedSkillCollection LearnedSkills = new LearnedSkillCollection();

	public HashSet<string> GrantedSkills = new HashSet<string>(StringComparer.Ordinal);

	public bool HasCustomAutoCast;

	public HashSet<string> AutoCastSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public string PolymorphForm = "";

	public double PolymorphGait = 16.0;

	public WorldPoint Pos = WorldPoint.Zero;

	public WorldPoint? MoveTarget;

	public WorldPoint? MarchTarget;

	public bool ManualMoveActive;

	public double VelX;

	public double VelY;

	public double Radius = 16.5;

	public int Facing8;

	public double MoveSpeed = 120.0;

	public double AttackRange = 60.0;

	public double AggroRange = 480.0;

	public double ProjectileSpeed = 640.0;

	public double ProjectileTurnRate = 5.0;

	public string BasicProjectileKind = "";

	public DerivedStats D = new DerivedStats();

	public Dictionary<string, int> Statuses = new Dictionary<string, int>();

	public Dictionary<string, double> Buffs = new Dictionary<string, double>();

	public Dictionary<string, PeriodicEffect> PeriodicEffects = new Dictionary<string, PeriodicEffect>();

	public Dictionary<string, int> Counters = new Dictionary<string, int>(StringComparer.Ordinal);

	public List<PeriodicEffect> Bleeds = new List<PeriodicEffect>();

	public string Element = "none";

	public string Size = "S";

	public string AttackElement = "none";

	public string Race = "";

	public bool IsBoss;

	public bool Passive;

	public bool CannotAttack;

	public bool FleeOnly;

	public bool Hard;

	public int L1jWorldNpcId;

	public string L1jWorldNpcImpl = "";

	public bool TrainingScarecrow;

	public bool NeutralWorldNpc;

	public bool ReturnsHomeWhenIdle;

	public int CastleWarId;

	public CastleWarObjectKind CastleWarObjectKind;

	public string CastleWarObjectKey = "";

	public bool CastleWarInvulnerable;

	public bool WantedByGuards;

	public bool WantedForElfGuardians;

	public double ExperienceReward;

	public int GoldMin;

	public int GoldMax;

	public double GoldChance = 1.0;

	public double DropMultiplier = 1.0;

	public double MobHealthRegenIntervalSeconds;

	public double MobHealthRegenAmount;

	public double MobManaRegenIntervalSeconds;

	public double MobManaRegenAmount;

	public int DelayTicks;

	public double AttackCd;

	public double OffhandCd;

	public double CastCd;

	public int HitstunUntil;

	public int ActionLockUntil;

	public bool Dead;

	public bool WasResurrected;

	public Combatant? MobGroupLeader { get; set; }

	public bool IsAlive
	{
		get
		{
			if (!Dead)
			{
				return Hp > 0.0;
			}
			return false;
		}
	}

	public bool IsHardControlled => L1jAbnormalStateRules.IsHardControlled(this);

	public bool CanCast
	{
		get
		{
			if (IsAlive && !IsHardControlled && !HasStatus("silence") && !HasStatus("poisonsilence"))
			{
				return !HasStatus("magicseal");
			}
			return false;
		}
	}

	public double ApplyDamage(double amount)
	{
		if (Dead)
		{
			return 0.0;
		}
		double hp = Hp;
		Hp = Math.Max(0.0, Hp - Math.Max(0.0, amount));
		if (Hp <= 0.0)
		{
			Dead = true;
		}
		return hp - Hp;
	}

	public double Heal(double amount)
	{
		if (Dead)
		{
			return 0.0;
		}
		double hp = Hp;
		Hp = Math.Min(MaxHp, Hp + Math.Max(0.0, amount));
		return Hp - hp;
	}

	public void RestoreMp(double amount)
	{
		Mp = Math.Clamp(Mp + amount, 0.0, MaxMp);
	}

	public bool HasStatus(string k)
	{
		if (Statuses.TryGetValue(k, out var value))
		{
			return value > 0;
		}
		return false;
	}

	public void AddStatus(string k, int ticks)
	{
		Statuses[k] = Math.Max(Statuses.GetValueOrDefault(k), ticks);
	}

	public override string ToString()
	{
		return $"[Combatant {Kind} {((Disp.Length > 0) ? Disp : Key)} Lv{Level} hp{Hp:0}/{MaxHp:0}]";
	}
}
