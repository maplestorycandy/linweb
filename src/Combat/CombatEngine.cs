using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class CombatEngine
{
	public bool DisableMobAi { get; set; } = false;

	private sealed class AllySkillPlan
	{
		public int LearnedCount;

		public readonly List<string> Attack = new List<string>();

		public readonly List<(string SkillId, string StatusKind)> Debuff = new List<(string, string)>();

		public readonly List<string> Heal = new List<string>();

		public readonly List<string> Buffs = new List<string>();
	}

	private sealed class ActiveChaser
	{
		public int RemainingStrikes;

		public double NextStrikeAt;

		public required Combatant Attacker { get; init; }

		public required Combatant Target { get; init; }
	}

	private sealed class ExplorationPathState(MapSpawnCell goal, IReadOnlyList<WorldPoint> points, int index)
	{
		public int Index = index;

		public MapSpawnCell Goal { get; } = goal;

		public IReadOnlyList<WorldPoint> Points { get; } = points;
	}

	private readonly record struct HateEntry(int Value, long Seq);

	private sealed class IsometricStepState(WorldPoint start, WorldPoint end, int facing8, int totalFrames)
	{
		public int CompletedFrames;

		public WorldPoint LastApplied = start;

		public WorldPoint Start { get; } = start;

		public WorldPoint End { get; } = end;

		public int Facing8 { get; } = facing8;

		public int TotalFrames { get; } = Math.Max(1, totalFrames);
	}

	private sealed class L1jFireWallTile
	{
		public required Combatant Source { get; init; }

		public required IsometricGridPoint GridPoint { get; init; }

		public required WorldPoint Position { get; init; }

		public required double Damage { get; init; }

		public required double ExpiresAt { get; init; }

		public required double NextDamageAt { get; set; }
	}

	private sealed class GuardianSupplyState
	{
		public bool IsDropItems;

		public bool ForDropItems;

		public double WindowEndsAt;

		public double RefillAt;

		public Dictionary<string, long> Stock { get; } = new Dictionary<string, long>(StringComparer.Ordinal);
	}

	private sealed class ActiveDollRuntime
	{
		public required MagicDollDefinition Definition;

		public required Combatant Follower;

		public required string ItemUid;

		public required double ExpiresAt;

		public required double NextRegenAt;
	}

	private sealed record ManualCastRequest(string SkillId, Combatant? Target, string? PreferredSummonForm, bool IsScroll = false);

	private sealed class NavigationPathState(WorldGridCell goal, IReadOnlyList<WorldPoint> points, int index)
	{
		public int Index = index;

		public WorldGridCell Goal { get; } = goal;

		public IReadOnlyList<WorldPoint> Points { get; } = points;
	}

	private const string AllyPotionPaceBuff = "_ally_potion_pace";

	private const double AllyStandingBuffSeconds = 600.0;

	private const double AllyStandingBuffRefillBelow = 300.0;

	private const double AllyPotionPaceSeconds = 2.0;

	private const string AllyHealPaceBuff = "_ally_heal_pace";

	private const double AllyHealPaceSeconds = 1.2;

	private const double AllyInjuredHealPercent = 80.0;

	private readonly Dictionary<Combatant, AllyBehavior> _allyBehaviors = new Dictionary<Combatant, AllyBehavior>();

	private readonly Dictionary<Combatant, AllySkillPlan> _allySkillPlans = new Dictionary<Combatant, AllySkillPlan>();

	private static readonly (string ItemKey, string Buff)[] StandingClassPotions = new(string, string)[3]
	{
		("potion_brave", "brave"),
		("new_item_140", "cautious"),
		("new_item_139", "elfcookie")
	};

	private const double AwakeningMpIntervalSeconds = 4.0;

	private const double AwakeningMpCost = 8.0;

	private readonly Dictionary<Combatant, double> _awakeningMpElapsed = new Dictionary<Combatant, double>();

	internal const string ChaserEffectName = "追蹤者";

	internal const int ChaserEffectGfx = 7025;

	internal const int ChaserStrikeCount = 3;

	internal const double ChaserIntervalSeconds = 1.0;

	private readonly List<ActiveChaser> _activeChasers = new List<ActiveChaser>();

	public static Func<bool>? IsGuardianSoulInWorldCheck;

	public const double FixedStepSeconds = 1.0 / 60.0;

	public const double DefaultSpellRange = 72.0;

	public const double LegacyTimerStepSeconds = 0.1;

	private const double PositionEpsilonSquared = 1E-06;

	private const double RangeEpsilon = 1E-06;

	private const double MoveArrivalDistance = 5.0;

	private const double MobCorpseLifetimeSeconds = 1.0;

	private readonly List<Combatant> _combatants = new List<Combatant>();

	private readonly List<CombatProjectile> _projectiles = new List<CombatProjectile>();

	private readonly List<CombatEvent> _events = new List<CombatEvent>();

	private readonly HashSet<Combatant> _resolvedDeaths = new HashSet<Combatant>();

	private readonly Dictionary<Combatant, double> _corpseExpiry = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _healthRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _manaRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _mobHealthRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _mobManaRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _convertCooldowns = new Dictionary<Combatant, double>();

	private readonly ICombatRandom _random;

	private readonly IGameData? _data;

	private double _mapDropRate = 1.0;

	private bool _mapUnderwater;

	private double _mapHealthDrainPerCycle;

	private double _fixedStepAccumulator;

	private double _legacyTimerAccumulator;

	private long _nextProjectileId;

	private WorldBounds? _worldBounds;

	private const string ThrowAxeSkillId = "sk_warrior_throwaxe";

	private static readonly double ThrowAxeRangePx = 480.0;

	private const double AbnormalMagicHitBaseline = 10.0;

	private readonly Dictionary<(Combatant Caster, string SkillId), int> _cubeTicksRemaining = new Dictionary<(Combatant, string), int>();

	private const double CubeEffectRangePx = 144.0;

	private readonly Dictionary<Combatant, ExplorationPathState> _explorationNavigationPaths = new Dictionary<Combatant, ExplorationPathState>();

	private ExplorationNavigationGrid? _explorationNavigation;

	private readonly Dictionary<Combatant, Dictionary<Combatant, HateEntry>> _hate = new Dictionary<Combatant, Dictionary<Combatant, HateEntry>>();

	private readonly HashSet<Combatant> _receivedFirstHate = new HashSet<Combatant>();

	private readonly Dictionary<Combatant, double> _mobLastCombatAt = new Dictionary<Combatant, double>();

	private long _nextHateSeq;

	private static readonly CompanionAttackPriority[] DefaultCompanionAttackPriorities = new CompanionAttackPriority[1] { CompanionAttackPriority.Nearest };

	private const double HostileFieldPotionHpPercent = 70.0;

	private readonly Dictionary<Combatant, HashSet<Combatant>> _contestedByHostilePlayers = new Dictionary<Combatant, HashSet<Combatant>>();

	private const int IsometricWalkableSearchRadius = 16;

	private WorldPoint _isometricLatticeOrigin;

	private readonly Dictionary<Combatant, IsometricStepState> _isometricSteps = new Dictionary<Combatant, IsometricStepState>();

	private readonly Dictionary<Combatant, WorldPoint> _directionalMoveInputs = new Dictionary<Combatant, WorldPoint>();

	private readonly Dictionary<Combatant, WorldPoint> _queuedDirectionalMoveInputs = new Dictionary<Combatant, WorldPoint>();

	private readonly Dictionary<Combatant, WorldPoint> _sidestepOrigins = new Dictionary<Combatant, WorldPoint>();

	private readonly List<WorldPoint> _staticSolidBodies = new List<WorldPoint>();

	private readonly List<L1jFireWallTile> _l1jFireWallTiles = new List<L1jFireWallTile>();

	private readonly Dictionary<Combatant, GuardianSupplyState> _guardianSupplies = new Dictionary<Combatant, GuardianSupplyState>();

	private readonly Dictionary<Combatant, ActiveDollRuntime> _activeDolls = new Dictionary<Combatant, ActiveDollRuntime>();

	private readonly Dictionary<Combatant, ManualCastRequest> _manualCastQueue = new Dictionary<Combatant, ManualCastRequest>();

	private readonly Dictionary<Combatant, Combatant> _mobTauntTargets = new Dictionary<Combatant, Combatant>();
	private readonly Dictionary<Combatant, Combatant> _manualAttackTargets = new Dictionary<Combatant, Combatant>();

	private readonly Dictionary<Combatant, WorldPoint> _mobHomePositions = new Dictionary<Combatant, WorldPoint>();

	private readonly Dictionary<Combatant, WorldPoint> _mobWanderTargets = new Dictionary<Combatant, WorldPoint>();

	private readonly Dictionary<Combatant, double> _mobNextWanderAt = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, int> _mobWanderSequences = new Dictionary<Combatant, int>();

	private const double MobForcedPolymorphSeconds = 1800.0;

	private const string MobSelfHasteBuff = "mob_self_haste";

	private readonly Dictionary<Combatant, IReadOnlyList<MobSkillPlan>> _mobSkillPlans = new Dictionary<Combatant, IReadOnlyList<MobSkillPlan>>();

	private readonly Dictionary<Combatant, long> _mobSkillNextEvaluationStep = new Dictionary<Combatant, long>();

	private readonly Dictionary<Combatant, Dictionary<string, int>> _mobSkillUseCounts = new Dictionary<Combatant, Dictionary<string, int>>();

	private readonly Dictionary<Combatant, double> _mobHasteIntervals = new Dictionary<Combatant, double>();

	private const string MobSummonKeyPrefix = "~summon";

	private const int MobSummonFieldCap = 12;

	private int _mobSummonSequence;

	private readonly HashSet<Combatant> _mobInitialTeleportsCompleted = new HashSet<Combatant>();

	private const string MobTransformSkillPrefix = "mob_transform:";

	private readonly Dictionary<string, MobTransformationTransition?> _mobTransformationCache = new Dictionary<string, MobTransformationTransition>(StringComparer.Ordinal);

	private readonly Dictionary<Combatant, NavigationPathState> _navigationPaths = new Dictionary<Combatant, NavigationPathState>();

	private WorldCollisionGrid? _collisionGrid;

	public double? PlayerVisionLimit;

	public double PlayerVisionAspectY = 1.0;

	private readonly Dictionary<Combatant, double> _painwandMobExpiresAt = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, Combatant> _petOwners = new Dictionary<Combatant, Combatant>();

	private readonly Dictionary<Combatant, PetInstance> _petInstances = new Dictionary<Combatant, PetInstance>();

	private readonly Dictionary<Combatant, PetDerivedStats> _petProfiles = new Dictionary<Combatant, PetDerivedStats>();

	private readonly Dictionary<Combatant, double> _petReviveReadyAt = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _petRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, double> _petManaRegenElapsed = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, WorldPoint> _petAlertHomes = new Dictionary<Combatant, WorldPoint>();

	private const string RelicUndeadImmunityCooldownBuff = "_relicUndeadImmunityCooldown";

	private const string RelicFireNullifyCooldownBuff = "_relicFireNullifyCooldown";

	private const string RelicPhysicalReductionCooldownBuff = "_relicPhysicalReductionCooldown";

	public const double RenderSnapDistance = 64.0;

	private const double RenderSnapDistanceSquared = 4096.0;

	private readonly Dictionary<Combatant, WorldPoint> _renderPreviousPositions = new Dictionary<Combatant, WorldPoint>();

	private MapTopology? _explorationTopology;

	private readonly Dictionary<(Combatant Caster, string SkillId), WorldPoint> _stormCentres = new Dictionary<(Combatant, string), WorldPoint>();

	private readonly Dictionary<Combatant, Combatant> _summonOwners = new Dictionary<Combatant, Combatant>();

	private readonly Dictionary<Combatant, string> _summonSkillIds = new Dictionary<Combatant, string>();

	private readonly Dictionary<Combatant, int> _summonPetCosts = new Dictionary<Combatant, int>();

	private readonly Dictionary<Combatant, double> _summonExpiresAt = new Dictionary<Combatant, double>();

	private readonly Dictionary<Combatant, SummonMagicAttackProfile> _summonMagicAttacks = new Dictionary<Combatant, SummonMagicAttackProfile>();

	private readonly Dictionary<Combatant, IReadOnlyList<SummonProcProfile>> _summonProcs = new Dictionary<Combatant, IReadOnlyList<SummonProcProfile>>();

	private readonly Dictionary<Combatant, SummonAoeAttackProfile> _summonAoeAttacks = new Dictionary<Combatant, SummonAoeAttackProfile>();

	private long _nextSummonId;

	private const string TrapParalysisDurationCounter = "_trap_poison_paralysis_ticks";

	public int CurrentTick { get; private set; }

	public long CurrentStep { get; private set; }

	public double CurrentTimeSeconds { get; private set; }

	public IReadOnlyList<Combatant> Combatants => _combatants;

	public IReadOnlyList<CombatProjectile> Projectiles => _projectiles;

	public WorldBounds? Bounds => _worldBounds;

	public double MapDropRate => _mapDropRate;

	public bool MapUnderwater => _mapUnderwater;

	public double MapHealthDrainPerCycle => _mapHealthDrainPerCycle;

	public int LivingNormalMobCount
	{
		get
		{
			int num = 0;
			foreach (Combatant combatant in _combatants)
			{
				if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive && !combatant.IsBoss && !IsWorldNpc(combatant))
				{
					num++;
				}
			}
			return num;
		}
	}

	public bool PlayerPvpEnabled { get; set; }

	public IReadOnlyList<WorldPoint> StaticSolidBodies => _staticSolidBodies;

	public IReadOnlyList<WorldPoint> L1jFireWallPositions => _l1jFireWallTiles.Select((L1jFireWallTile tile) => tile.Position).ToArray();

	public WorldCollisionGrid? CollisionGrid => _collisionGrid;

	public MapTopology? ExplorationTopology => _explorationTopology;

	private bool IsActionLocked(Combatant combatant)
	{
		return combatant.ActionLockUntil > CurrentStep;
	}

	public void LockAction(Combatant combatant, double seconds)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (!(seconds <= 0.0) && combatant.IsAlive)
		{
			int num = (int)((double)CurrentStep + Math.Ceiling(seconds / (1.0 / 60.0)));
			if (num > combatant.ActionLockUntil)
			{
				combatant.ActionLockUntil = num;
			}
		}
	}

	public void CancelActionLock(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		combatant.ActionLockUntil = 0;
	}

	public bool IsPerformingAction(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return IsActionLocked(combatant);
	}

	public void SetAllyBehavior(Combatant ally, AllyBehavior behavior)
	{
		ArgumentNullException.ThrowIfNull(ally, "ally");
		if (ally.Kind != CombatantKind.Ally)
		{
			throw new InvalidOperationException("Behavior modes only apply to ally combatants.");
		}
		_allyBehaviors[ally] = behavior;
	}

	public AllyBehavior AllyBehaviorOf(Combatant ally)
	{
		ArgumentNullException.ThrowIfNull(ally, "ally");
		return _allyBehaviors.GetValueOrDefault(ally, AllyBehavior.Balanced);
	}

	private bool AllyLeashedToLeader(Combatant ally)
	{
		if (!ally.UsesMonsterTemplate)
		{
			return AllyBehaviorRules.LeashedToLeader(AllyBehaviorOf(ally), HealthPercent(ally));
		}
		return false;
	}

	private static double HealthPercent(Combatant actor)
	{
		if (!(actor.MaxHp <= 0.0))
		{
			return actor.Hp / actor.MaxHp * 100.0;
		}
		return 100.0;
	}

	private static double ManaPercent(Combatant actor)
	{
		if (!(actor.MaxMp <= 0.0))
		{
			return actor.Mp / actor.MaxMp * 100.0;
		}
		return 100.0;
	}

	private void AdvanceAllySupport(Combatant ally, Combatant? enemy)
	{
		if (_data != null && !ally.Dead && !ally.UsesMonsterTemplate)
		{
			AllyBehavior behavior = AllyBehaviorOf(ally);
			RefreshAllyStandingBuffs(ally);
			AdvanceAllyPotions(ally, behavior);
			AdvanceAllySkills(ally, enemy, behavior);
		}
	}

	private void RefreshAllyStandingBuffs(Combatant ally)
	{
		if (_data == null)
		{
			return;
		}
		RefillStandingBuff(ally, "haste");
		(string, string)[] standingClassPotions = StandingClassPotions;
		for (int i = 0; i < standingClassPotions.Length; i++)
		{
			var (itemKey, buffId) = standingClassPotions[i];
			if (ConsumableRules.RequirementAllows(_data, itemKey, ally))
			{
				RefillStandingBuff(ally, buffId);
				break;
			}
		}
	}

	private static void RefillStandingBuff(Combatant ally, string buffId)
	{
		if (ally.Buffs.GetValueOrDefault(buffId) < 300.0)
		{
			ally.Buffs[buffId] = 600.0;
		}
	}

	private void AdvanceAllyPotions(Combatant ally, AllyBehavior behavior)
	{
		if (!(HealthPercent(ally) >= AllyBehaviorRules.HealPotionHpPercent(behavior)) && !(ally.Buffs.GetValueOrDefault("_ally_potion_pace") > 0.0) && (TryDrinkAllyPotion(ally, "potion_heal") || TryDrinkAllyPotion(ally, "potion_strong") || TryDrinkAllyPotion(ally, "potion_ult")))
		{
			ally.Buffs["_ally_potion_pace"] = 2.0;
		}
	}

	private bool TryDrinkAllyPotion(Combatant ally, string itemKey)
	{
		ItemStack itemStack = ally.InventoryStacks.FirstOrDefault((ItemStack candidate) => candidate.ItemKey == itemKey && candidate.Quantity > 0 && !candidate.Locked);
		if (itemStack == null)
		{
			return false;
		}
		return TryUseConsumable(ally, itemStack.Uid).Success;
	}

	private void AdvanceAllySkills(Combatant ally, Combatant? enemy, AllyBehavior behavior)
	{
		if (!ally.CanCast || ally.CastCd > 0.0)
		{
			return;
		}
		AllySkillPlan plan = AllySkillPlanFor(ally);
		double mpPercent = ManaPercent(ally);
		bool flag = AllyBehaviorRules.AttackSkillsAllowed(behavior, mpPercent);
		double healThreshold = AllyBehaviorRules.HealTeammatePercent(behavior);
		switch (behavior)
		{
		case AllyBehavior.Aggressive:
			TryAllyAttackSkill(ally, enemy, plan);
			break;
		case AllyBehavior.Balanced:
			if (!TryAllyHealSkill(ally, plan, healThreshold, behavior) && !TryAllyBuffSkill(ally, plan))
			{
				TryAllyAttackSkill(ally, enemy, plan);
			}
			break;
		case AllyBehavior.Guardian:
			if (!TryAllyHealSkill(ally, plan, healThreshold, behavior) && !TryAllyBuffSkill(ally, plan) && flag)
			{
				TryAllyAttackSkill(ally, enemy, plan);
			}
			break;
		default:
			if (!TryAllyHealSkill(ally, plan, healThreshold, behavior) && !TryAllyDebuffSkill(ally, enemy, plan) && !TryAllyBuffSkill(ally, plan) && flag)
			{
				TryAllyAttackSkill(ally, enemy, plan);
			}
			break;
		}
	}

	private bool TryAllyAttackSkill(Combatant ally, Combatant? enemy, AllySkillPlan plan)
	{
		if (enemy == null)
		{
			return false;
		}
		foreach (string item in plan.Attack)
		{
			if (TryAutoCastSkill(ally, item, enemy))
			{
				return true;
			}
		}
		foreach (var (skillId, text) in plan.Debuff)
		{
			if ((text.Length <= 0 || !enemy.Statuses.ContainsKey(text)) && TryAutoCastSkill(ally, skillId, enemy))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryAllyDebuffSkill(Combatant ally, Combatant? enemy, AllySkillPlan plan)
	{
		if (enemy == null)
		{
			return false;
		}
		foreach (var (skillId, text) in plan.Debuff)
		{
			if ((text.Length <= 0 || !enemy.Statuses.ContainsKey(text)) && TryAutoCastSkill(ally, skillId, enemy))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryAllyHealSkill(Combatant ally, AllySkillPlan plan, double thresholdPercent, AllyBehavior behavior = AllyBehavior.Balanced)
	{
		if (plan.Heal.Count == 0)
		{
			return false;
		}
		if (ally.Buffs.GetValueOrDefault(AllyHealPaceBuff) > 0.0)
		{
			return false;
		}
		double mpPercent = ManaPercent(ally);
		if (!AllyBehaviorRules.HealSkillsAllowed(behavior, mpPercent))
		{
			return false;
		}
		Combatant combatant = SelectAllyHealTarget(ally, thresholdPercent);
		if (combatant == null)
		{
			return false;
		}
		foreach (string item in plan.Heal)
		{
			if (TryAutoCastSkill(ally, item, combatant))
			{
				ally.Buffs[AllyHealPaceBuff] = AllyHealPaceSeconds;
				return true;
			}
		}
		return false;
	}

	private bool TryAllyBuffSkill(Combatant ally, AllySkillPlan plan)
	{
		foreach (string buff in plan.Buffs)
		{
			if (!(ally.Buffs.GetValueOrDefault(buff) > 0.0) && TryAutoCastSkill(ally, buff, ally))
			{
				return true;
			}
		}
		return false;
	}

	private Combatant? SelectAllyHealTarget(Combatant ally, double thresholdPercent)
	{
		Combatant result = null;
		double lowestHpPct = thresholdPercent;
		foreach (Combatant combatant in _combatants)
		{
			if (!combatant.IsAlive || IsEnemy(ally, combatant))
			{
				continue;
			}
			CombatantKind kind = combatant.Kind;
			bool isTeammate = (kind == CombatantKind.Player || kind == CombatantKind.Ally || kind == CombatantKind.Pet);
			if (!isTeammate)
			{
				continue;
			}
			if (CombatRangeRules.DiamondDistance(ally.Pos, combatant.Pos) > 480.0)
			{
				continue;
			}
			double hpPct = HealthPercent(combatant);
			if (hpPct < lowestHpPct)
			{
				result = combatant;
				lowestHpPct = hpPct;
			}
		}
		return result;
	}

	private AllySkillPlan AllySkillPlanFor(Combatant ally)
	{
		if (_allySkillPlans.TryGetValue(ally, out AllySkillPlan value) && value.LearnedCount == ally.LearnedSkills.Count)
		{
			return value;
		}
		value = new AllySkillPlan
		{
			LearnedCount = ally.LearnedSkills.Count
		};
		if (_data != null)
		{
			foreach (string item in ally.LearnedSkills.Order<string>(StringComparer.Ordinal))
			{
				if (ally.HasCustomAutoCast && !ally.AutoCastSkills.Contains(item))
				{
					continue;
				}
				switch (AllyBehaviorRules.Classify(_data, item))
				{
				case AllySkillClass.Attack:
					value.Attack.Add(item);
					break;
				case AllySkillClass.Debuff:
					value.Debuff.Add((item, DebuffStatusKind(item)));
					break;
				case AllySkillClass.Heal:
					value.Heal.Add(item);
					break;
				case AllySkillClass.Buff:
					value.Buffs.Add(item);
					break;
				}
			}
			value.Attack.Sort((string left, string right) =>
			{
				bool isTripleLeft = IsTripleArrow(left);
				bool isTripleRight = IsTripleArrow(right);
				if (isTripleLeft != isTripleRight)
				{
					return isTripleLeft ? -1 : 1;
				}
				return SkillMpCost(right).CompareTo(SkillMpCost(left));
			});
		}
		_allySkillPlans[ally] = value;
		return value;
	}

	private int SkillMpCost(string skillId)
	{
		JsonObject jsonObject = _data?.Skill(skillId);
		if (jsonObject == null)
		{
			return 0;
		}
		return CombatSkill.ReadInt(jsonObject, "mp");
	}

	private string DebuffStatusKind(string skillId)
	{
		JsonObject jsonObject = _data?.Skill(skillId);
		if (jsonObject == null || !CombatSkill.TryRead(skillId, jsonObject, out CombatSkill skill) || skill?.Status == null)
		{
			return string.Empty;
		}
		return StatusRules.NormalizeKind(skill.Status.Kind);
	}

	private void ForgetAllyAiState(Combatant ally)
	{
		_allyBehaviors.Remove(ally);
		_allySkillPlans.Remove(ally);
	}

	private void AdvanceAwakeningMpDrain(Combatant actor)
	{
		if (!AwakeningRules.IsActive(actor))
		{
			_awakeningMpElapsed.Remove(actor);
			return;
		}
		double num = _awakeningMpElapsed.GetValueOrDefault(actor) + 0.1;
		if (num + 1E-09 < 4.0)
		{
			_awakeningMpElapsed[actor] = num;
			return;
		}
		_awakeningMpElapsed[actor] = num - 4.0;
		double num2 = actor.Mp - 8.0;
		double num3 = Math.Min(actor.Mp, 8.0);
		actor.Mp = Math.Max(0.0, num2);
		if (num3 > 0.0)
		{
			_events.Add(CombatEvent.MpChange(actor, 0.0 - num3));
		}
		if (num2 >= 0.0)
		{
			return;
		}
		foreach (string buffId in AwakeningRules.BuffIds)
		{
			if (actor.Buffs.Remove(buffId))
			{
				_events.Add(CombatEvent.BuffRemove(actor, buffId));
			}
		}
		_awakeningMpElapsed.Remove(actor);
		bool flag = _data != null;
		if (flag)
		{
			CombatantKind kind = actor.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = flag2 || HostilePlayerRules.IsHostilePlayer(actor);
		}
		if (flag && !MonsterCompanionRules.IsCompanion(actor))
		{
			CombatantBuilder.RefreshPlayer(actor, _data);
		}
	}

	private bool TryCastCallAllies(Combatant caster, JsonObject source, bool freeMp, bool ignoreCastLock, bool automatic)
	{
		if (automatic || (!ignoreCastLock && caster.CastCd > 0.0))
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, "sk_royal_callally", CombatModifierRules.SkillMpCost(caster, source, "sk_royal_callally")) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: true);
		}
		_events.Add(CombatEvent.Cast(caster, "sk_royal_callally"));
		Combatant[] array = (from candidate in _combatants.Where(delegate(Combatant candidate)
			{
				bool flag = candidate.IsAlive && candidate != caster;
				if (flag)
				{
					CombatantKind kind = candidate.Kind;
					bool flag2 = (uint)(kind - 2) <= 1u;
					flag = flag2;
				}
				return flag && !IsEnemy(caster, candidate);
			})
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			Combatant combatant = array[num2];
			combatant.Pos = ClampAndSnapPlacement(CallAllyRules.FormationPoint(caster, num2, array.Length), combatant.Radius);
			combatant.MoveTarget = null;
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
			_navigationPaths.Remove(combatant);
			_explorationNavigationPaths.Remove(combatant);
			_isometricSteps.Remove(combatant);
			_sidestepOrigins.Remove(combatant);
			_renderPreviousPositions.Remove(combatant);
			ResetIdleWander(combatant);
			_events.Add(CombatEvent.Move(combatant));
		}
		return true;
	}

	private void TryCastOnHurt(Combatant defender, Combatant attacker, DamageType damageType)
	{
		bool flag = _data == null;
		if (!flag)
		{
			bool flag2 = (uint)damageType <= 2u;
			flag = !flag2;
		}
		if (!flag && defender.IsAlive && attacker.IsAlive && CastOnHurtRules.TrySelectMagicSkill(_data, defender, _random, out string skillId) && TryCastSkillCore(defender, skillId, attacker, freeMp: true, ignoreCastLock: true, automatic: false))
		{
			_events.Add(CombatEvent.LogLine("【護身短刀】受擊反擊，免費施放 " + SkillName(skillId) + "。"));
		}

		if (!flag && defender.IsAlive)
		{
			foreach (var (itemName, proc) in RelicProcRules.ArmorSpells(_data, defender))
			{
				if (_random.NextDouble() * 100.0 < proc.ChancePercent)
				{
					string sId = proc.EffectId;
					if (int.TryParse(sId, out int numericId))
					{
						string mapped = $"sk_{numericId}";
						if (_data?.Skill(mapped) != null) sId = mapped;
					}
					bool targetSelf = string.Equals(proc.StatusKind, "self", StringComparison.OrdinalIgnoreCase) || sId.Contains("heal") || sId.Contains("shield") || sId.Contains("immune") || sId.Contains("cure");
					Combatant? procTarget = targetSelf ? defender : (attacker.IsAlive ? attacker : null);
					if (procTarget != null)
					{
						if (_data?.Skill(sId) != null)
						{
							TryCastSkillCore(defender, sId, procTarget, freeMp: true, ignoreCastLock: true, automatic: true, ignoreClassRequirement: true);
						}
						else
						{
							ApplyRelicWeaponSpell(defender, procTarget, proc);
						}
						_events.Add(CombatEvent.LogLine($"【{itemName}】受擊觸發，施放 {SkillName(sId)}！"));
					}
				}
			}
		}
	}

	private string SkillName(string skillId)
	{
		return _data?.Skill(skillId)?["n"]?.GetValue<string>() ?? skillId;
	}

	private bool TryCastCharmSkillCore(Combatant caster, JsonObject source, Combatant? requestedTarget, bool ignoreCastLock, bool automatic)
	{
		if (!CharmRules.IsCharmSkill(source) || (!ignoreCastLock && caster.CastCd > 0.0) || _data == null)
		{
			return false;
		}
		if (automatic)
		{
			return false;
		}
		if (requestedTarget == null)
		{
			return RejectCharm("迷魅失敗：這個目標無法捕捉。");
		}
		if (!IsCharmCandidate(caster, requestedTarget) || !IsRecruitableCharmTarget(requestedTarget))
		{
			return RejectCharm("迷魅失敗：這個目標無法捕捉。");
		}
		if (!IsWithinRange(caster, requestedTarget, 72.0) || !HasCombatLineOfSight(caster, requestedTarget))
		{
			return RejectCharm("迷魅失敗：目標超出射程或視線受阻。");
		}
		string text = MonsterCardRules.ResolveMobKey(_data, requestedTarget);
		if (text.Length == 0 || !MonsterCompanionRules.IsRecruitable(text, _data.Mob(text)))
		{
			return RejectCharm("迷魅失敗：這個目標無法捕捉。");
		}
		if (MonsterCardRules.OwnedCard(caster, text) != null)
		{
			return RejectCharm("迷魅失敗：你已擁有這種怪物的卡片。");
		}
		string itemKey = MonsterCardRules.MaterialKey(_data, text);
		if (CombatInventory.AvailableCount(caster, itemKey) < 1)
		{
			return RejectCharm("迷魅失敗：缺少目標對應的未封印卡。");
		}
		if (!CombatInventory.TryRemove(caster, itemKey, 1L))
		{
			return RejectCharm("迷魅失敗：缺少目標對應的未封印卡。");
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: false);
		}
		_events.Add(CombatEvent.Cast(caster, "sk_charm", requestedTarget));
		int probability = CharmProbability(caster, requestedTarget, source);
		if (!L1jMagicFormulas.ProbabilitySucceeds(_random, probability))
		{
			_events.Add(CombatEvent.Miss(caster, requestedTarget));
			_events.Add(CombatEvent.LogLine("迷魅失敗：" + requestedTarget.Disp + " 抵抗了迷魅，未封印卡不會退還。"));
			return true;
		}
		ItemStack itemStack = MonsterCardRules.CreateCapturedCard(caster, text, requestedTarget.Level);
		CombatInventory.Add(caster, itemStack);
		Remove(requestedTarget);
		_events.Add(CombatEvent.ItemGain(caster, itemStack.ItemKey, 1));
		_events.Add(CombatEvent.LogLine("成功迷魅 " + requestedTarget.Disp + "，取得專屬怪物卡片。"));
		return true;
	}

	private int CharmProbability(Combatant caster, Combatant target, JsonObject source)
	{
		L1jSkillFields? obj = L1jSkillFields.TryRead(source["l1j"] as JsonObject) ?? throw new InvalidDataException("迷魅術缺少 L1J 技能能力欄位。");
		ICombatRandom random = _random;
		L1jMagicFormulas.ProbabilityBranch branch = L1jMagicFormulas.BranchFor(obj.OfficialId);
		int probabilityDice = obj.ProbabilityDice;
		int probabilityValue = obj.ProbabilityValue;
		int level = caster.Level;
		int level2 = target.Level;
		int magicBonus = L1jMagicFormulas.MagicBonus((int)Math.Floor(Math.Max(0.0, caster.D.Int)));
		CombatantKind kind = caster.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		return CharmRules.FinalSuccessPercent(L1jMagicFormulas.Probability(random, branch, probabilityDice, probabilityValue, level, level2, magicBonus, (flag || HostilePlayerRules.IsHostilePlayer(caster)) ? ClassGrowthRules.MagicLevel(caster.ClassId, caster.Level) : L1jMagicFormulas.MagicLevel(caster.Level), CharmRules.MagicResistancePenalty(EffectiveMagicResist(target)), Math.Max(0, caster.D.OriginalMagicHit), 10, string.Equals(ClassKitRegistry.NormalizeClassId(caster.ClassId), "mage", StringComparison.Ordinal), 0, CharmRules.MagicHitBonusDice(caster.D.MagicHit)), caster.Level, target.Level, target);
	}

	private bool IsCharmCandidate(Combatant caster, Combatant target)
	{
		if (_data != null && target.IsAlive && target.Kind == CombatantKind.Mob)
		{
			return IsEnemy(caster, target);
		}
		return false;
	}

	private bool IsRecruitableCharmTarget(Combatant target)
	{
		if (_data == null)
		{
			return false;
		}
		string text = MonsterCardRules.ResolveMobKey(_data, target);
		if (text.Length > 0)
		{
			return MonsterCompanionRules.IsRecruitable(text, _data.Mob(text));
		}
		return false;
	}

	private bool RejectCharm(string message)
	{
		_events.Add(CombatEvent.LogLine(message));
		return false;
	}

	internal void TryStartL1jChaser(Combatant attacker, Combatant target)
	{
		bool flag = _data == null;
		if (!flag)
		{
			CombatantKind kind = attacker.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(attacker);
		}
		JsonObject jsonObject = default(JsonObject);
		int num;
		if (!flag && !MonsterCompanionRules.IsCompanion(attacker) && target.IsAlive)
		{
			jsonObject = RelicProcRules.MainWeapon(_data, attacker);
			num = ((jsonObject == null) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		bool flag3 = (byte)num != 0;
		if (!flag3)
		{
			if (jsonObject?["spellProc"] != null)
			{
				return;
			}
			int num2 = CombatSkill.ReadInt(jsonObject, "l1jItemId");
			bool flag2 = (uint)(num2 - 265) <= 3u;
			flag3 = !flag2;
		}
		if (!flag3 && !(L1jChaserTriggerProbability(attacker, target) <= (double)_random.Roll(1, 100)))
		{
			ApplyL1jChaserStrike(attacker, target);
			if (target.IsAlive)
			{
				_activeChasers.Add(new ActiveChaser
				{
					Attacker = attacker,
					Target = target,
					RemainingStrikes = 2,
					NextStrikeAt = CurrentTimeSeconds + 1.0
				});
			}
		}
	}

	internal double L1jChaserTriggerProbability(Combatant attacker, Combatant target)
	{
		int intelligence = (int)Math.Floor(Math.Max(0.0, attacker.D.Int));
		int num = ClassGrowthRules.MagicLevel(attacker.ClassId, attacker.Level) + L1jMagicFormulas.MagicBonus(intelligence);
		int num2 = (int)Math.Floor(EffectiveMagicResist(target)) - 2 * attacker.D.OriginalMagicHit;
		return Math.Max(3.0, 3.0 + (double)num * 0.18 - (double)(num2 / 10) * 0.1);
	}

	private void AdvanceL1jChasers()
	{
		for (int num = _activeChasers.Count - 1; num >= 0; num--)
		{
			ActiveChaser activeChaser = _activeChasers[num];
			if (!activeChaser.Target.IsAlive || !_combatants.Contains(activeChaser.Target) || activeChaser.RemainingStrikes <= 0)
			{
				_activeChasers.RemoveAt(num);
			}
			else if (!(CurrentTimeSeconds + 1E-09 < activeChaser.NextStrikeAt))
			{
				ApplyL1jChaserStrike(activeChaser.Attacker, activeChaser.Target);
				activeChaser.RemainingStrikes--;
				activeChaser.NextStrikeAt += 1.0;
				if (activeChaser.RemainingStrikes <= 0 || !activeChaser.Target.IsAlive)
				{
					_activeChasers.RemoveAt(num);
				}
			}
		}
	}

	private void ApplyL1jChaserStrike(Combatant attacker, Combatant target)
	{
		if (!target.IsAlive)
		{
			return;
		}
		_events.Add(CombatEvent.Cast(attacker, "追蹤者", target));
		if (!(target.Hp <= 1.0))
		{
			int num = (int)Math.Floor(Math.Max(0.0, attacker.D.Int));
			int num2 = (int)Math.Floor(Math.Max(0.0, attacker.D.ItemSpellPower));
			int num3 = num + num2 - 12;
			double num4 = Math.Max(1.0, 1.0 + 3.0 / 32.0 * (double)num3);
			double num5 = ((num > 18) ? (((double)num + 2.0) / (double)num) : ((num > 12) ? ((double)num * 0.065) : 0.78));
			double num6 = num5;
			double num7 = Math.Max(12, num);
			double damage = (double)(_random.Roll(1, 6) + 7) * num4 * num6 / 10.5 * num7 * 2.0;
			damage = L1jWeaponSkillReducedDamage(attacker, target, damage, "");
			if (target.Buffs.GetValueOrDefault("sk_holy_barrier") > 0.0)
			{
				damage /= 2.0;
			}
			damage = Math.Min(damage, target.Hp - 1.0);
			if (damage > 0.0)
			{
				ApplyWeaponSkillDamage(attacker, target, damage, "");
			}
		}
	}

	public bool HasCleanseTarget(Combatant caster, string skillId)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		JsonObject jsonObject = _data?.Skill(skillId);
		if (jsonObject == null)
		{
			return false;
		}
		IReadOnlyList<string> readOnlyList = CleanseRules.CurableStatuses(jsonObject);
		if (readOnlyList.Count == 0)
		{
			return false;
		}
		return SelectCleanseTarget(caster, readOnlyList, null) != null;
	}

	private Combatant? SelectCleanseTarget(Combatant caster, IReadOnlyList<string> curable, Combatant? requested)
	{
		if (CleanseRules.HasCurableStatus(caster, curable))
		{
			return caster;
		}
		if (requested != null && requested != caster && IsCleanseCandidate(caster, requested, curable))
		{
			return requested;
		}
		Combatant result = null;
		double num = double.MaxValue;
		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant combatant = _combatants[i];
			if (combatant != caster && IsCleanseCandidate(caster, combatant, curable))
			{
				double num2 = CombatRangeRules.DiamondDistance(caster.Pos, combatant.Pos);
				if (!(num2 >= num))
				{
					num = num2;
					result = combatant;
				}
			}
		}
		return result;
	}

	private bool IsCleanseCandidate(Combatant caster, Combatant candidate, IReadOnlyList<string> curable)
	{
		if (candidate.IsAlive && !IsEnemy(caster, candidate) && !IsNecroSkeleton(candidate) && CleanseRules.HasCurableStatus(candidate, curable))
		{
			return CombatRangeRules.DiamondDistance(caster.Pos, candidate.Pos) <= CleanseRules.Radius + 1E-06;
		}
		return false;
	}

	private bool TryCastCleanseSkill(Combatant caster, string skillId, JsonObject source, IReadOnlyList<string> curable, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock)
	{
		if (!ignoreCastLock && caster.CastCd > 0.0)
		{
			return false;
		}
		Combatant combatant = SelectCleanseTarget(caster, curable, requestedTarget);
		if (combatant == null)
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skillId, CombatModifierRules.SkillMpCost(caster, source, skillId)) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: true);
		}
		for (int i = 0; i < curable.Count; i++)
		{
			RemoveStatusCore(combatant, curable[i]);
		}
		_events.Add(CombatEvent.Cast(caster, skillId, combatant));
		string text = CombatSkill.ReadString(source, "msg");
		if (text.Length > 0)
		{
			_events.Add(CombatEvent.LogLine(text));
		}
		return true;
	}

	private void TryCounterAttackReaction(Combatant attacker, Combatant defender, bool attackLanded)
	{
		bool flag = !attackLanded || attacker.Kind != CombatantKind.Mob;
		if (!flag)
		{
			CombatantKind kind = defender.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(defender);
		}
		if (!flag && defender.IsAlive && attacker.IsAlive && CounterAttackRules.CanCounter(_data, defender) && CounterAttackRules.IsShortDistance(attacker, defender) && !(_random.NextDouble() >= 0.5))
		{
			_events.Add(CombatEvent.LogLine($"【反擊屏障】{defender.Disp} 立即反擊 {attacker.Disp}！"));
			ApplyBasicPhysicalAttack(defender, attacker, CommitBasicPhysicalAttack(defender, attacker));
		}
	}

	public CombatEngine(ICombatRandom random, IGameData? data = null)
	{
		_random = random ?? throw new ArgumentNullException("random");
		_data = data;
	}

	public void ConfigureMapRuntime(double dropRate, bool underwater, double healthDrainPerCycle = 0.0)
	{
		if (!double.IsFinite(dropRate) || dropRate < 0.0)
		{
			throw new ArgumentOutOfRangeException("dropRate");
		}
		if (!double.IsFinite(healthDrainPerCycle) || healthDrainPerCycle < 0.0)
		{
			throw new ArgumentOutOfRangeException("healthDrainPerCycle");
		}
		_mapDropRate = dropRate;
		_mapUnderwater = underwater;
		_mapHealthDrainPerCycle = healthDrainPerCycle;
	}

	public bool RevivePlayer(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (player.Kind != CombatantKind.Player || !_combatants.Contains(player) || !player.Dead)
		{
			return false;
		}
		player.Hp = player.MaxHp;
		player.Dead = false;
		player.AttackCd = 0.0;
		player.OffhandCd = 0.0;
		player.CastCd = 0.0;
		player.HitstunUntil = 0;
		player.MoveTarget = null;
		player.VelX = 0.0;
		player.VelY = 0.0;
		_resolvedDeaths.Remove(player);
		_healthRegenElapsed[player] = 0.0;
		_manaRegenElapsed[player] = 0.0;
		return true;
	}

	public void Add(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (string.IsNullOrWhiteSpace(combatant.Key))
		{
			throw new ArgumentException("Combatant.Key is required.", "combatant");
		}
		if (_combatants.Any((Combatant existing) => string.Equals(existing.Key, combatant.Key, StringComparison.Ordinal)))
		{
			throw new InvalidOperationException("A combatant named '" + combatant.Key + "' already exists.");
		}
		if (MonsterCompanionRules.IsCompanion(combatant))
		{
			combatant.UsesMonsterTemplate = true;
		}
		combatant.Pos = ClampAndSnapPlacement(combatant.Pos, combatant.Radius);
		_resolvedDeaths.Remove(combatant);
		_corpseExpiry.Remove(combatant);
		_painwandMobExpiresAt.Remove(combatant);
		combatant.HitstunUntil = 0;
		combatant.ActionLockUntil = 0;
		_healthRegenElapsed[combatant] = 0.0;
		_manaRegenElapsed[combatant] = 0.0;
		_mobHealthRegenElapsed[combatant] = 0.0;
		_mobManaRegenElapsed[combatant] = 0.0;
		_combatants.Add(combatant);
		if (combatant.Kind == CombatantKind.Mob)
		{
			_mobHomePositions[combatant] = combatant.Pos;
		}
		InitializeGuardianSupplies(combatant);
		_events.Add(CombatEvent.Spawn(combatant));
	}

	public bool Remove(Combatant combatant)
	{
		if (!_combatants.Remove(combatant))
		{
			return false;
		}
		_resolvedDeaths.Remove(combatant);
		_corpseExpiry.Remove(combatant);
		_painwandMobExpiresAt.Remove(combatant);
		ForgetAllyAiState(combatant);
		_healthRegenElapsed.Remove(combatant);
		_manaRegenElapsed.Remove(combatant);
		_mobHealthRegenElapsed.Remove(combatant);
		_mobManaRegenElapsed.Remove(combatant);
		_convertCooldowns.Remove(combatant);
		_isometricSteps.Remove(combatant);
		_directionalMoveInputs.Remove(combatant);
		_queuedDirectionalMoveInputs.Remove(combatant);
		_navigationPaths.Remove(combatant);
		CleanupExplorationNavigation(combatant);
		CleanupSummonRuntime(combatant);
		CleanupPetRuntime(combatant);
		CleanupStormRuntime(combatant);
		ForgetGuardianSupplies(combatant);
		CleanupMobSkillRuntime(combatant);
		CleanupMobBehaviorRuntime(combatant);
		_awakeningMpElapsed.Remove(combatant);
		return true;
	}

	public void SetMoveTarget(Combatant combatant, WorldPoint target, bool isManual = false)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before assigning a move target.");
		}
		combatant.MoveTarget = SnapToWalkableIsometricPoint(target, combatant.Radius);
		combatant.ManualMoveActive = isManual;
		_directionalMoveInputs.Remove(combatant);
		_queuedDirectionalMoveInputs.Remove(combatant);
		_navigationPaths.Remove(combatant);
		_explorationNavigationPaths.Remove(combatant);
		CancelActionLock(combatant);
	}

	public void SetMoveDirection(Combatant combatant, double screenX, double screenY)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before assigning movement input.");
		}
		if (!double.IsFinite(screenX) || !double.IsFinite(screenY))
		{
			throw new ArgumentOutOfRangeException("screenX", "Movement input must be finite.");
		}
		double num = screenX * screenX + screenY * screenY;
		if (num <= 1E-06)
		{
			ClearMoveTarget(combatant);
			return;
		}
		double num2 = Math.Sqrt(num);
		WorldPoint worldPoint = new WorldPoint(screenX / num2, screenY / num2);
		if (!_directionalMoveInputs.TryGetValue(combatant, out var value) || value.DistanceSquaredTo(worldPoint) > 1E-06)
		{
			_queuedDirectionalMoveInputs[combatant] = worldPoint;
		}
		_directionalMoveInputs[combatant] = worldPoint;
		combatant.MoveTarget = null;
		combatant.ManualMoveActive = true;
		_navigationPaths.Remove(combatant);
		_explorationNavigationPaths.Remove(combatant);
		CancelActionLock(combatant);
	}

	public void ReleaseMoveDirection(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		_directionalMoveInputs.Remove(combatant);
		combatant.ManualMoveActive = false;
		combatant.VelX = 0.0;
		combatant.VelY = 0.0;
	}

	public void ClearMoveTarget(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		_directionalMoveInputs.Remove(combatant);
		_queuedDirectionalMoveInputs.Remove(combatant);
		combatant.MoveTarget = null;
		combatant.ManualMoveActive = false;
		_navigationPaths.Remove(combatant);
		_explorationNavigationPaths.Remove(combatant);
		combatant.VelX = 0.0;
		combatant.VelY = 0.0;
	}

	public bool TryReviveAllyWithScroll(Combatant ally)
	{
		ArgumentNullException.ThrowIfNull(ally, "ally");
		Combatant combatant = PartyLeader();
		if (!CanReviveAlly(ally) || combatant == null || !CombatInventory.TryRemove(combatant, "scroll_revive", 1L))
		{
			return false;
		}
		ReviveAllyCore(ally, 0.5);
		return true;
	}

	public bool ReviveAlly(Combatant ally)
	{
		ArgumentNullException.ThrowIfNull(ally, "ally");
		if (!CanReviveAlly(ally))
		{
			return false;
		}
		ReviveAllyCore(ally, 0.5);
		return true;
	}

	public int ReviveAlliesAtTown()
	{
		int num = 0;
		Combatant[] array = _combatants.Where((Combatant actor) => actor.Kind == CombatantKind.Ally).ToArray();
		foreach (Combatant combatant in array)
		{
			if (combatant.Dead)
			{
				ReviveAllyCore(combatant, 1.0);
				num++;
				continue;
			}
			double hp = combatant.Hp;
			combatant.Hp = combatant.MaxHp;
			combatant.Mp = combatant.MaxMp;
			ClearTransientConditions(combatant);
			double num3 = combatant.Hp - hp;
			if (num3 > 0.0)
			{
				_events.Add(CombatEvent.Heal(combatant, combatant, num3));
			}
		}
		return num;
	}

	public int RefreshLivingAlliesAtTown()
	{
		int num = 0;
		Combatant[] array = _combatants.Where((Combatant actor) => actor.Kind == CombatantKind.Ally && !actor.Dead).ToArray();
		foreach (Combatant combatant in array)
		{
			double hp = combatant.Hp;
			combatant.Hp = combatant.MaxHp;
			combatant.Mp = combatant.MaxMp;
			ClearTransientConditions(combatant);
			num++;
			double num3 = combatant.Hp - hp;
			if (num3 > 0.0)
			{
				_events.Add(CombatEvent.Heal(combatant, combatant, num3));
			}
		}
		return num;
	}

	public bool ApplyBuff(Combatant combatant, string buffName, double durationSeconds)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		ArgumentException.ThrowIfNullOrWhiteSpace(buffName, "buffName");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before applying a buff.");
		}
		if (double.IsNaN(durationSeconds) || durationSeconds <= 0.0 || double.IsNegativeInfinity(durationSeconds))
		{
			throw new ArgumentOutOfRangeException("durationSeconds", "Buff duration must be positive seconds or positive infinity.");
		}
		if (combatant.Buffs.GetValueOrDefault(buffName) >= durationSeconds)
		{
			return false;
		}
		combatant.Buffs[buffName] = durationSeconds;
		if (!IsInternalBuff(buffName))
		{
			_events.Add(CombatEvent.BuffAdd(combatant, buffName));
		}
		return true;
	}

	public bool RemoveBuff(Combatant combatant, string buffName)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		ArgumentException.ThrowIfNullOrWhiteSpace(buffName, "buffName");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before removing a buff.");
		}
		if (!combatant.Buffs.Remove(buffName))
		{
			return false;
		}
		if (AwakeningRules.IsAwakening(buffName))
		{
			_awakeningMpElapsed.Remove(combatant);
		}
		if (!IsInternalBuff(buffName))
		{
			_events.Add(CombatEvent.BuffRemove(combatant, buffName));
		}
		RefreshBuffDerivedStats(combatant, buffName);
		return true;
	}

	public bool ApplyStatus(Combatant combatant, string statusKind, int durationTicks)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		ArgumentException.ThrowIfNullOrWhiteSpace(statusKind, "statusKind");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before applying a status.");
		}
		if (durationTicks <= 0)
		{
			throw new ArgumentOutOfRangeException("durationTicks", "Status duration must be positive ticks.");
		}
		return TryApplyStatusCore(combatant, statusKind, durationTicks, null);
	}

	public bool RemoveStatus(Combatant combatant, string statusKind)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		ArgumentException.ThrowIfNullOrWhiteSpace(statusKind, "statusKind");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before removing a status.");
		}
		return RemoveStatusCore(combatant, StatusRules.NormalizeKind(statusKind));
	}

	public ConsumableUseResult TryUseConsumable(Combatant combatant, string itemUid, ConsumableUseContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (!_combatants.Contains(combatant))
		{
			throw new InvalidOperationException("The combatant must be added before using a consumable.");
		}
		if (_data == null)
		{
			throw new InvalidOperationException("Consumable use requires a combat engine with game data.");
		}
		ConsumableUseResult result = ConsumableRules.TryUse(_data, combatant, itemUid, _random, context);
		if (!result.Success)
		{
			return result;
		}
		if (result.HpRestored > 0.0)
		{
			_events.Add(CombatEvent.Heal(combatant, combatant, result.HpRestored));
		}
		if (result.BuffApplied)
		{
			_events.Add(CombatEvent.BuffAdd(combatant, result.EffectKey));
		}
		IReadOnlyList<string> replacedBuffKeys = result.ReplacedBuffKeys;
		if (replacedBuffKeys != null && replacedBuffKeys.Count > 0)
		{
			foreach (string item in replacedBuffKeys)
			{
				_events.Add(CombatEvent.BuffRemove(combatant, item));
			}
		}
		IReadOnlyList<string> curedStatusKinds = result.CuredStatusKinds;
		if (curedStatusKinds != null && curedStatusKinds.Count > 0)
		{
			foreach (string item2 in curedStatusKinds)
			{
				_events.Add(CombatEvent.StatusRemove(combatant, item2));
			}
		}
		return result;
	}

	public Combatant? FindNearestEnemy(Combatant combatant, double range)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return SelectNearestEnemy(combatant, range);
	}

	public Combatant? FindNearestReachableEnemy(Combatant combatant, double range)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return SelectNearestEnemy(combatant, range, requireLineOfSight: false, requireReachability: true);
	}

	public void SetWorldBounds(WorldBounds? bounds)
	{
		if (bounds.HasValue && !bounds.GetValueOrDefault().IsValid)
		{
			throw new ArgumentOutOfRangeException("bounds", "World bounds must be finite and ordered.");
		}
		_worldBounds = bounds;
		if (!bounds.HasValue)
		{
			return;
		}
		WorldBounds valueOrDefault = bounds.GetValueOrDefault();
		foreach (Combatant combatant in _combatants)
		{
			combatant.Pos = ClampAndSnapToWalkable(valueOrDefault.Clamp(combatant.Pos), combatant.Radius);
			WorldPoint? moveTarget = combatant.MoveTarget;
			if (moveTarget.HasValue)
			{
				WorldPoint valueOrDefault2 = moveTarget.GetValueOrDefault();
				combatant.MoveTarget = ClampAndSnapToWalkable(valueOrDefault.Clamp(valueOrDefault2), combatant.Radius);
			}
		}
	}

	public static bool IsTripleArrow(string? skillId)
	{
		if (string.IsNullOrWhiteSpace(skillId))
		{
			return false;
		}
		return string.Equals(skillId, "sk_elf_triple", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(skillId, "三重矢", StringComparison.OrdinalIgnoreCase);
	}

	public bool TryCastSkill(Combatant caster, string skillId, Combatant? requestedTarget = null)
	{
		if (IsTripleArrow(skillId))
		{
			return TryCastSkillCore(caster, skillId, requestedTarget, freeMp: false, ignoreCastLock: false, automatic: false);
		}
		if (!string.Equals(skillId, "sk_charm", StringComparison.Ordinal))
		{
			return TryCastSkillAsSharedAction(caster, skillId, requestedTarget, automatic: false);
		}
		return TryCastSkillCore(caster, skillId, requestedTarget, freeMp: false, ignoreCastLock: true, automatic: false);
	}

	public bool TryAutoCastSkill(Combatant caster, string skillId, Combatant? requestedTarget = null)
	{
		if (IsTripleArrow(skillId))
		{
			return TryCastSkillCore(caster, skillId, requestedTarget, freeMp: false, ignoreCastLock: false, automatic: true);
		}
		return TryCastSkillAsSharedAction(caster, skillId, requestedTarget, automatic: true);
	}

	private bool TryCastSkillAsSharedAction(Combatant caster, string skillId, Combatant? requestedTarget, bool automatic, string? preferredSummonForm = null, bool freeMp = false, bool ignoreClassRequirement = false, bool isScroll = false)
	{
		if (!isScroll && SharedActionOnCooldown(caster))
		{
			return false;
		}
		double num = SharedActionCarry(caster);
		if (!TryCastSkillCore(caster, skillId, requestedTarget, freeMp, ignoreCastLock: isScroll, automatic, preferredSummonForm, ignoreClassRequirement, isScroll))
		{
			return false;
		}
		if (!isScroll)
		{
			double num2 = ((caster.CastCd > 1E-09) ? caster.CastCd : AttackIntervalSeconds(caster));
			CommitSharedActionCooldown(caster, num2 + num);
		}
		return true;
	}

	private bool TryCastSkillCore(Combatant caster, string skillId, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock, bool automatic, string? preferredSummonForm = null, bool ignoreClassRequirement = false, bool isScroll = false)
	{
		if (caster.CannotAttack)
		{
			return false;
		}
		JsonObject jsonObject = _data?.Skill(skillId);
		if (jsonObject == null || !caster.CanCast || StatusRules.BlocksMobSkillCasting(caster) || (!ignoreClassRequirement && !string.IsNullOrWhiteSpace(caster.ClassId) && !ClassKitRegistry.CanUseSkill(caster, skillId, _data)))
		{
			return false;
		}
		if (caster.Kind == CombatantKind.Player && ((!isScroll && IsPhysicallyMoving(caster)) || !WeightRules.ActionsAllowed(caster) || AbsoluteBarrierRules.IsActive(caster)))
		{
			return false;
		}
		if (!isScroll && (!ignoreCastLock || !CharmRules.IsCharmSkill(jsonObject)) && !IsTripleArrow(skillId) && IsActionLocked(caster))
		{
			return false;
		}
		string text = RelicConditionalCombatRules.EffectiveSkillId(_data, caster, skillId);
		JsonObject jsonObject2 = (string.Equals(text, skillId, StringComparison.Ordinal) ? jsonObject : _data?.Skill(text));
		if (jsonObject2 == null)
		{
			return false;
		}
		skillId = text;
		bool? flag = TryCastDedicatedL1jSkill(caster, skillId, jsonObject2, requestedTarget, freeMp, ignoreCastLock, automatic, isScroll);
		if (flag.HasValue)
		{
			return flag.Value;
		}
		if (ConvertSkill.TryRead(skillId, jsonObject2, out ConvertSkill skill) && skill != null)
		{
			return TryCastConvertSkill(caster, requestedTarget, jsonObject2, skill, freeMp, ignoreCastLock);
		}
		if (NecromancyRules.ReplacesAnimateDead(_data, caster, skillId))
		{
			return true;
		}
		if (SummonRules.IsSummonSkill(skillId, jsonObject2))
		{
			return TryCastSummonSkillCore(caster, skillId, jsonObject2, preferredSummonForm, freeMp, ignoreCastLock);
		}
		if (EnergySenseRules.IsEnergySenseSkill(jsonObject2))
		{
			return TryCastEnergySense(caster, jsonObject2, requestedTarget, freeMp, ignoreCastLock);
		}
		if (CharmRules.IsCharmSkill(jsonObject2))
		{
			return TryCastCharmSkillCore(caster, jsonObject2, requestedTarget, ignoreCastLock, automatic);
		}
		if (ReturnToNatureRules.IsSkill(skillId))
		{
			return TryCastReturnToNature(caster, jsonObject2, requestedTarget, freeMp, ignoreCastLock);
		}
		if (AbsoluteBarrierRules.IsBarrierSkill(jsonObject2))
		{
			return TryCastAbsoluteBarrier(caster, skillId, jsonObject2, freeMp, ignoreCastLock);
		}
		if (string.Equals(skillId, "sk_warrior_throwaxe", StringComparison.Ordinal))
		{
			return TryCastThrowAxe(caster, requestedTarget, jsonObject2, freeMp, ignoreCastLock);
		}
		IReadOnlyList<string> readOnlyList = CleanseRules.CurableStatuses(jsonObject2);
		if (readOnlyList.Count > 0)
		{
			return TryCastCleanseSkill(caster, skillId, jsonObject2, readOnlyList, requestedTarget, freeMp, ignoreCastLock);
		}
		if (BuffSkill.TryRead(skillId, jsonObject2, out BuffSkill skill2) && skill2 != null)
		{
			return TryCastBuffSkill(caster, jsonObject2, skill2, requestedTarget, freeMp, ignoreCastLock);
		}
		if (CallAllyRules.IsCallAllySkill(skillId, jsonObject2))
		{
			return TryCastCallAllies(caster, jsonObject2, freeMp, ignoreCastLock, automatic);
		}
		if ((!ignoreCastLock && caster.CastCd > 0.0) || !CombatSkill.TryRead(skillId, jsonObject2, out CombatSkill skill3) || skill3 == null || (!skill3.IsHeal && !skill3.IsMagicDamage && !skill3.IsPhysicalDamage && !skill3.FixedStatusOnly && !skill3.RoarFixed))
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skill3.Id, CombatModifierRules.SkillMpCost(caster, jsonObject2, skill3.Id)) : 0);
		if (automatic && skill3.IsMagicDamage && num > 0)
		{
			num = Math.Max(0, (int)Math.Round((double)num * RelicConditionalCombatRules.AutoCastManaMultiplier(_data, caster), MidpointRounding.AwayFromZero));
		}
		if (skill3.MpDamagePercentage > 0.0 && !freeMp)
		{
			num = MpDrainSpend(caster, skill3);
		}
		int num2 = (!freeMp ? CombatModifierRules.SkillHpCost(caster, jsonObject2, skill3.Id) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		if (num2 > 0 && (caster.Hp <= (double)num2 || (automatic && caster.Hp <= (double)(num2 + 50))))
		{
			return false;
		}
		Combatant combatant;
		IReadOnlyList<Combatant> readOnlyList2;
		if (skill3.IsHeal)
		{
			combatant = requestedTarget ?? caster;
			if (!combatant.IsAlive || IsNecroSkeleton(combatant) || IsEnemy(caster, combatant) || (combatant != caster && !IsWithinRange(caster, combatant, SkillRange(caster, skill3))))
			{
				return false;
			}
			readOnlyList2 = new Combatant[1] { combatant };
		}
		else if (skill3.TargetsAllEnemies)
		{
			double radius = ((skill3.EffectRadius > 0.0) ? skill3.EffectRadius : 72.0);
			double range = (skill3.CentersOnCaster ? radius : SkillRange(caster, skill3));
			combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsValidExplicitSkillTarget(caster, requestedTarget, jsonObject2) && IsWithinRange(caster, requestedTarget, range) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range, requireLineOfSight: true) : null));
			if (combatant == null)
			{
				return false;
			}
			Combatant centre = (skill3.CentersOnCaster ? caster : combatant);
			readOnlyList2 = _combatants.Where((Combatant candidate) => candidate.IsAlive && IsEnemy(caster, candidate) && IsWithinRange(centre, candidate, radius) && HasCombatLineOfSight(caster, candidate)).ToArray();
			if (readOnlyList2.Count == 0)
			{
				return false;
			}
		}
		else
		{
			double range2 = SkillRange(caster, skill3);
			combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsValidExplicitSkillTarget(caster, requestedTarget, jsonObject2) && IsWithinRange(caster, requestedTarget, range2) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range2, requireLineOfSight: true) : null));
			if (combatant == null)
			{
				return false;
			}
			readOnlyList2 = new Combatant[1] { combatant };
		}
		if (skill3.BossOnly && !skill3.IsHeal)
		{
			readOnlyList2 = readOnlyList2.Where((Combatant candidate) => candidate.IsBoss).ToArray();
			if (readOnlyList2.Count == 0)
			{
				return false;
			}
			combatant = readOnlyList2[0];
		}
		if (!skill3.IsHeal && ((skill3.RequiredTargetTag.Length > 0 && !HasTargetTag(combatant, skill3.RequiredTargetTag)) || !WeaponRequirementMet(caster, skill3.RequiredWeapon)))
		{
			return false;
		}
		if (skill3.HealCooldownTicks > 0 && caster.Buffs.GetValueOrDefault(HealCooldownKey(skill3.Id)) > 0.0)
		{
			return false;
		}
		if (skill3.NoRecastStatus.Length > 0 && combatant != null && combatant.HasStatus(skill3.NoRecastStatus))
		{
			return false;
		}
		if (skill3.DarkCritical)
		{
			if (automatic)
			{
				return false;
			}
			return TryCastDarkCritical(caster, combatant, skill3);
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		ApplySpellbladeBuff(caster, skill3, num);
		if (num2 > 0)
		{
			caster.Hp = Math.Max(1.0, caster.Hp - (double)num2);
		}
		ApplyAutoCastBacklash(caster, (automatic && skill3.IsMagicDamage) ? num : 0);
		if (!ignoreCastLock && !isScroll)
		{
			if (IsTripleArrow(skill3.Id))
			{
				caster.CastCd = 0.15;
			}
			else
			{
				caster.CastCd = Math.Max(NextCastCooldownSeconds(caster, skill3.IsHeal), L1jGlobalSkillDelaySeconds(skill3));
			}
		}
		_events.Add(CombatEvent.Cast(caster, skill3.Id, combatant, isScroll: isScroll));
		if (skill3.IsHeal)
		{
			if (skill3.HealCooldownTicks > 0)
			{
				ApplyBuff(caster, HealCooldownKey(skill3.Id), (double)skill3.HealCooldownTicks * 0.1);
			}
			ApplyHeal(caster, combatant, skill3);
			return true;
		}
		if (skill3.FixedStatusOnly)
		{
			TryApplyStatus(caster, combatant, skill3);
			return true;
		}
		if (skill3.RoarFixed)
		{
			ApplyRoarDamage(caster, readOnlyList2);
			return true;
		}
		if (skill3.IsPhysicalDamage)
		{
			ApplyPhysicalSkill(caster, combatant, skill3);
			return true;
		}
		ApplyMagicTargets(caster, readOnlyList2, skill3, automatic);
		TryEchoMagicSkill(caster, readOnlyList2, skill3, combatant, automatic);
		return true;
	}

	private bool TryCastAbsoluteBarrier(Combatant caster, string skillId, JsonObject source, bool freeMp, bool ignoreCastLock)
	{
		if (!ignoreCastLock && caster.CastCd > 0.0)
		{
			return false;
		}
		if (AbsoluteBarrierRules.IsActive(caster) || caster.Buffs.GetValueOrDefault("_barrier_cd:sk_abs_barrier") > 0.0)
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skillId, CombatModifierRules.SkillMpCost(caster, source, skillId)) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: true);
		}
		double num2 = ((source["l1j"] is JsonObject source2 && CombatSkill.ReadInt(source2, "officialId") == 78) ? ((double)Math.Max(1, CombatSkill.ReadInt(source2, "buffDuration"))) : Math.Max(1.0, CombatSkill.ReadDouble(source, "dur", 7.0)));
		ApplyBuff(caster, "sk_abs_barrier", num2);
		ApplyBuff(caster, "_barrier_cd:sk_abs_barrier", (source["l1j"] is JsonObject source3) ? Math.Max(num2, (double)CombatSkill.ReadInt(source3, "reuseDelay") / 1000.0) : AbsoluteBarrierRules.CooldownSeconds(num2));
		_events.Add(CombatEvent.Cast(caster, skillId, caster));
		string text = CombatSkill.ReadString(source, "msg");
		if (text.Length > 0)
		{
			_events.Add(CombatEvent.LogLine(text));
		}
		return true;
	}

	internal (int? Count, int? Sides) OffhandStrikeDice(Combatant attacker, Combatant target)
	{
		if (_data != null && DualWieldCombatRules.SuppliesOffhandDice(attacker, _data))
		{
			JsonObject jsonObject = _data.Item(attacker.OffhandWeaponId);
			if (jsonObject != null)
			{
				int num = Math.Max(1, CombatSkill.ReadInt(jsonObject, "dmgS"));
				int num2 = CombatSkill.ReadInt(jsonObject, "dmgL");
				int num3 = ((num2 > 0) ? num2 : num);
				return (Count: 1, Sides: (target.Size == "L") ? num3 : num);
			}
		}
		return (Count: null, Sides: null);
	}

	private void RelocateBehind(Combatant mover, Combatant target)
	{
		double dx = target.Pos.X - mover.Pos.X;
		double dy = target.Pos.Y - mover.Pos.Y;
		double length = Math.Sqrt(dx * dx + dy * dy);
		WorldPoint pos = mover.Pos;
		if (length > 1E-06)
		{
			IsometricStep[] array = IsometricMovementRules.Directions.OrderByDescending(delegate(IsometricStep step)
			{
				double num2 = Math.Sqrt(step.DeltaX * step.DeltaX + step.DeltaY * step.DeltaY);
				return (!(num2 <= 0.0)) ? ((dx * step.DeltaX + dy * step.DeltaY) / (length * num2)) : double.NegativeInfinity;
			}).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				IsometricStep isometricStep = array[num];
				WorldPoint worldPoint = SnapToWalkableIsometricPoint(new WorldPoint(target.Pos.X + isometricStep.DeltaX, target.Pos.Y + isometricStep.DeltaY), Math.Max(0.0, mover.Radius));
				if (!StepBlockedBySolidBody(mover, worldPoint))
				{
					pos = worldPoint;
					break;
				}
			}
		}
		mover.Pos = pos;
		mover.MoveTarget = null;
		mover.VelX = 0.0;
		mover.VelY = 0.0;
		_navigationPaths.Remove(mover);
		_explorationNavigationPaths.Remove(mover);
		_isometricSteps.Remove(mover);
		_sidestepOrigins.Remove(mover);
		_renderPreviousPositions.Remove(mover);
		ResetIdleWander(mover);
		_events.Add(CombatEvent.Move(mover));
	}

	private bool TryCastThrowAxe(Combatant caster, Combatant? requestedTarget, JsonObject source, bool freeMp, bool ignoreCastLock)
	{
		if (!ignoreCastLock && caster.CastCd > 0.0)
		{
			return false;
		}
		if (!ThrowAxeWeaponReady(caster, source))
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, "sk_warrior_throwaxe", CombatModifierRules.SkillMpCost(caster, source, "sk_warrior_throwaxe")) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		double range = CombatRangeRules.ConfiguredCastRange(source) ?? ThrowAxeRangePx;
		Combatant combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsEnemy(caster, requestedTarget) && IsWithinRange(caster, requestedTarget, range) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range, requireLineOfSight: true) : null));
		if (combatant == null)
		{
			return false;
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: false);
		}
		_events.Add(CombatEvent.Cast(caster, "sk_warrior_throwaxe", combatant));
		double damageMultiplier = SkillDamageMasteryMultiplier(caster, "sk_warrior_throwaxe");
		PhysicalHitResult physicalHitResult = PerformPhysicalHit(caster, combatant, null, forceHeavy: false, forceCritical: false, 0.0, 0.0, forceHit: false, basicAttack: true, damageMultiplier);
		if (!physicalHitResult.Hit || physicalHitResult.Damage <= 0.0)
		{
			return true;
		}
		if (combatant.IsAlive)
		{
			ApplyThrowAxeBleed(caster, combatant, physicalHitResult.Damage);
		}
		return true;
	}

	private bool ThrowAxeWeaponReady(Combatant caster, JsonObject source)
	{
		if (!CombatSkill.ReadBool(source, "reqWpnBlunt"))
		{
			return true;
		}
		if (_data == null)
		{
			return false;
		}
		switch (WeaponCombatProfile.ResolveFamily(caster.MainWeaponId, _data))
		{
		case WeaponFamily.OneHandBlunt:
		case WeaponFamily.TwoHandBlunt:
		case WeaponFamily.DualAxes:
			return true;
		default:
			return false;
		}
	}

	private bool TryCastBuffSkill(Combatant caster, JsonObject source, BuffSkill skill, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock)
	{
		if (!ignoreCastLock && caster.CastCd > 0.0)
		{
			return false;
		}
		bool flag = AwakeningRules.IsAwakening(skill.Id);
		Combatant castTarget = caster;
		WorldPoint? centre = null;
		if (StormBuffRules.IsStormBuff(skill.Id))
		{
			if (!TryResolveStormCast(caster, skill.Id, source, requestedTarget, out Combatant castTarget2, out centre) || castTarget2 == null)
			{
				return false;
			}
			castTarget = castTarget2;
		}
		else if (requestedTarget != null && L1jSkillTargetRules.RequiresManualCharacterTarget(source))
		{
			L1jSkillFields fields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
			castTarget = requestedTarget;
			if (!castTarget.IsAlive || !L1jSkillTargetRules.AllowsCharacterTarget(source, castTarget) || !IsWithinRange(caster, castTarget, DedicatedL1jRange(skill.Id, source, fields)) || !HasCombatLineOfSight(caster, castTarget))
			{
				return false;
			}
		}
		if (!flag && skill.NoRefresh && castTarget.Buffs.GetValueOrDefault(skill.Id) > 0.0)
		{
			return false;
		}
		if (!flag && SkillBuffRules.HasEquivalentActive(castTarget, skill.Id))
		{
			return false;
		}
		if (!DarkStealthRules.CanCast(castTarget, skill.Id))
		{
			return false;
		}
		if (!BehaviorBuffRules.CanCast(castTarget, skill.Id))
		{
			return false;
		}
		if (!BuffWeaponRequirementMet(castTarget, skill))
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skill.Id, CombatModifierRules.SkillMpCost(caster, source, skill.Id)) : 0);
		int num2 = CombatModifierRules.SkillHpCost(caster, source, skill.Id);
		if (caster.Mp < (double)num || caster.Hp <= (double)(num2 + 5))
		{
			return false;
		}
		string text = (flag ? AwakeningRules.BuffIds.FirstOrDefault((string buffId) => castTarget.Buffs.GetValueOrDefault(buffId) > 0.0) : null);
		if (text != null && !string.Equals(text, skill.Id, StringComparison.Ordinal))
		{
			return false;
		}
		if (flag && string.Equals(text, skill.Id, StringComparison.Ordinal))
		{
			if (!RemoveBuff(castTarget, skill.Id))
			{
				return false;
			}
		}
		else if (!ApplyBuff(castTarget, skill.Id, skill.DurationSeconds))
		{
			return false;
		}
		if (centre.HasValue)
		{
			WorldPoint valueOrDefault = centre.GetValueOrDefault();
			SetStormCentre(caster, skill.Id, valueOrDefault);
		}
		ApplySpeedBuffMutex(castTarget, skill.Id);
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		caster.Hp = Math.Max(1.0, caster.Hp - (double)num2);
		if (!ignoreCastLock)
		{
			caster.CastCd = Math.Max(NextCastCooldownSeconds(caster, support: true), L1jGlobalSkillDelaySeconds(source));
		}
		_events.Add(CombatEvent.Cast(caster, skill.Id, castTarget));
		if (skill.Haste)
		{
			double valueOrDefault2 = castTarget.Buffs.GetValueOrDefault("haste");
			castTarget.Buffs["haste"] = Math.Max(valueOrDefault2, skill.DurationSeconds);
		}
		bool flag2 = SkillBuffRules.AffectsDerivedStats(_data, skill.Id) && !MonsterCompanionRules.IsCompanion(castTarget);
		if (flag2)
		{
			CombatantKind kind = castTarget.Kind;
			bool flag3 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag2 = flag3 || HostilePlayerRules.IsHostilePlayer(castTarget);
		}
		if (flag2)
		{
			CombatantBuilder.RefreshPlayer(castTarget, _data);
		}
		return true;
	}

	private bool BuffWeaponRequirementMet(Combatant caster, BuffSkill skill)
	{
		if (!skill.RequiresShield && !skill.RequiresMeleeWeapon && !skill.RequiresBluntWeapon)
		{
			return true;
		}
		bool flag = caster.EquippedItems.ContainsKey("shield");
		if (skill.RequiresShield && flag && !skill.RequiresMeleeWeapon && !skill.RequiresBluntWeapon)
		{
			return true;
		}
		if (_data != null)
		{
			JsonObject jsonObject = _data.Item(caster.MainWeaponId);
			if (jsonObject != null)
			{
				WeaponFamily? weaponFamily = WeaponCombatProfile.ResolveFamily(caster.MainWeaponId, _data);
				if (skill.RequiresMeleeWeapon && (WeaponCombatProfile.ReadBool(jsonObject, "isBow") || WeaponCombatProfile.ReadBool(jsonObject, "ranged")))
				{
					return false;
				}
				bool flag2 = skill.RequiresBluntWeapon;
				if (flag2)
				{
					bool flag3;
					switch (weaponFamily)
					{
					default:
						flag3 = true;
						break;
					case WeaponFamily.OneHandBlunt:
					case WeaponFamily.TwoHandBlunt:
					case WeaponFamily.DualAxes:
						flag3 = false;
						break;
					}
					flag2 = flag3;
				}
				if (flag2)
				{
					return false;
				}
				if (skill.RequiresShield && !flag)
				{
					return false;
				}
				return true;
			}
		}
		return false;
	}

	private bool HasWeaponTag(string weaponId, string requiredTag)
	{
		if (!WeaponCombatProfile.IsActiveWeaponTag(requiredTag) || !(_data?.Table("WEAPON_TAGS") is JsonObject jsonObject) || !(jsonObject[weaponId] is JsonArray source))
		{
			return false;
		}
		return source.Any((JsonNode node) => node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && string.Equals(value, requiredTag, StringComparison.Ordinal));
	}

	private void ApplySpeedBuffMutex(Combatant caster, string skillId)
	{
		foreach (string item in CombatModifierRules.ClearConflictingSpeedBuffs(caster, skillId))
		{
			_events.Add(CombatEvent.BuffRemove(caster, item));
		}
	}

	private bool TryCastConvertSkill(Combatant caster, Combatant? requestedTarget, JsonObject source, ConvertSkill skill, bool freeMp, bool ignoreCastLock)
	{
		if (!ignoreCastLock && _convertCooldowns.GetValueOrDefault(caster) > 1E-09)
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skill.Id, CombatModifierRules.SkillMpCost(caster, source, skill.Id)) : 0);
		int num2 = CombatModifierRules.SkillHpCost(caster, source, skill.Id);
		if (caster.Mp < (double)num || caster.Hp <= (double)(num2 + 5))
		{
			return false;
		}
		Combatant combatant = null;
		if (skill.Drain)
		{
			combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsEnemy(caster, requestedTarget) && IsWithinRange(caster, requestedTarget, 72.0) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, 72.0, requireLineOfSight: true) : null));
			if (combatant == null)
			{
				return false;
			}
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		caster.Hp = Math.Max(1.0, caster.Hp - (double)num2);
		_convertCooldowns[caster] = NextIndependentCastCooldownSeconds(caster, _convertCooldowns.GetValueOrDefault(caster), support: true);
		_events.Add(CombatEvent.Cast(caster, skill.Id, combatant));
		int num3 = skill.MpGain;
		if (skill.Drain)
		{
			if (!AbnormalMagicHit(caster, combatant, 20, 0.0, SkillAbnormalMasteryBonus(caster, skill.Id)))
			{
				_events.Add(CombatEvent.Miss(caster, combatant));
				return true;
			}
			num3 = _random.Roll(1, Math.Max(1, combatant.Level / 2));
		}
		double mp = caster.Mp;
		caster.RestoreMp(num3);
		double num4 = caster.Mp - mp;
		if (num4 > 0.0)
		{
			_events.Add(CombatEvent.MpChange(caster, num4));
		}
		return true;
	}

	private double ApplyMagicTargets(Combatant caster, IReadOnlyList<Combatant> targets, CombatSkill skill, bool automatic)
	{
		double num = 0.0;
		foreach (Combatant target in targets)
		{
			if (target.IsAlive)
			{
				num += ApplyMagicSkillDamage(caster, target, skill, automatic);
				if (target.IsAlive && skill.Status != null)
				{
					TryApplyStatus(caster, target, skill);
				}
				if (target.IsAlive && skill.FreezePower > 0.0)
				{
					TryApplySkillFreeze(caster, target, skill.FreezePower);
				}
				if (target.IsAlive && skill.InstantKill != null)
				{
					TryInstantKill(caster, target, skill, skill.InstantKill);
				}
			}
		}
		if (skill.LifeSteal && num > 0.0)
		{
			double num2 = caster.Heal(num);
			if (num2 > 0.0)
			{
				_events.Add(CombatEvent.Heal(caster, caster, num2));
			}
		}
		return num;
	}

	private void TryEchoMagicSkill(Combatant caster, IReadOnlyList<Combatant> targets, CombatSkill skill, Combatant? castTarget, bool automatic)
	{
	}

	public IReadOnlyList<CombatEvent> Advance(double deltaSeconds)
	{
		return AdvanceCore(deltaSeconds, consumeEvents: true);
	}

	public IReadOnlyList<CombatEvent> Tick()
	{
		return AdvanceCore(0.1, consumeEvents: false);
	}

	private IReadOnlyList<CombatEvent> AdvanceCore(double deltaSeconds, bool consumeEvents)
	{
		if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
		{
			deltaSeconds = 0.0;
		}
		else if (deltaSeconds > 0.25)
		{
			deltaSeconds = 0.25;
		}
		if (deltaSeconds > 0.0)
		{
			Dictionary<Combatant, WorldPoint> dictionary = _combatants.ToDictionary((Combatant result2) => result2, (Combatant combatant3) => combatant3.Pos);
			_fixedStepAccumulator += deltaSeconds;
			while (_fixedStepAccumulator + 1E-09 >= 1.0 / 60.0)
			{
				SimulateFixedStep(1.0 / 60.0);
				_fixedStepAccumulator -= 1.0 / 60.0;
			}
			foreach (var (combatant2, other) in dictionary)
			{
				if (combatant2.Pos.DistanceSquaredTo(other) > 1E-06)
				{
					_events.Add(CombatEvent.Move(combatant2));
				}
			}
		}
		CombatEvent[] result = _events.ToArray();
		if (consumeEvents)
		{
			_events.Clear();
		}
		return result;
	}

	public IReadOnlyList<CombatEvent> DrainEvents()
	{
		CombatEvent[] result = _events.ToArray();
		_events.Clear();
		return result;
	}

	private void SimulateFixedStep(double deltaSeconds)
	{
		CaptureRenderPreviousPositions();
		CurrentStep++;
		CurrentTimeSeconds = (double)CurrentStep * (1.0 / 60.0);
		AdvanceSharedActionCooldowns(deltaSeconds);
		AdvanceL1jFireWalls();
		Combatant[] combatantsSnapshot = _combatants.ToArray();
		foreach (Combatant combatant in combatantsSnapshot)
		{
			if (combatant.Kind == CombatantKind.Player)
			{
				LanternRules.Burn(combatant, deltaSeconds);
			}
		}
		if (_data != null)
		{
			foreach (Combatant combatant2 in combatantsSnapshot)
			{
				if (EquipmentTimerRules.Tick(_data, combatant2, deltaSeconds) != null)
				{
					CombatantBuilder.RefreshPlayer(combatant2, _data);
				}
			}
		}
		_legacyTimerAccumulator += deltaSeconds;
		while (_legacyTimerAccumulator + 1E-09 >= 0.1)
		{
			CurrentTick++;
			AdvanceStatuses();
			AdvancePetRevives();
			foreach (Combatant combatant3 in _combatants.ToArray())
			{
				if (combatant3.DelayTicks > 0)
				{
					combatant3.DelayTicks--;
				}
			}
			AdvanceStormBuffs();
			AdvanceCubeBuffs();
			_legacyTimerAccumulator -= 0.1;
		}
		if (!DisableMobAi)
		{
			AdvanceMobSkills();
		}
		AdvanceSummonLifetimes();
		AdvanceMagicDolls();
		AdvanceMovement(deltaSeconds);
		ResolveCollisions();
		ClampCombatants();
		AbsorbEngineRepositioning();
		AdvanceProjectiles(deltaSeconds);
		AdvanceL1jChasers();
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant attacker in array)
		{
			AdvanceCombatant(attacker, deltaSeconds);
		}
		RemoveExpiredPainwandMobs();
		RemoveExpiredCorpses();
	}

	private void AdvanceStatuses()
	{
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant in array)
		{
			AdvancePeriodicEffects(combatant);
			AdvanceBuffs(combatant);
			AdvanceRegeneration(combatant);
			string[] array2 = combatant.Statuses.Keys.ToArray();
			foreach (string text in array2)
			{
				if (!string.Equals(text, "poisonsilence", StringComparison.Ordinal))
				{
					int num = combatant.Statuses[text] - 1;
					if (num > 0)
					{
						combatant.Statuses[text] = num;
						continue;
					}
					RemoveStatusCore(combatant, text);
					AdvanceL1jParalysisPoison(combatant, text);
				}
			}
		}
	}

	private void AdvanceBuffs(Combatant combatant)
	{
		bool flag = false;
		string[] array = combatant.Buffs.Keys.ToArray();
		foreach (string text in array)
		{
			double num = combatant.Buffs[text];
			if (double.IsPositiveInfinity(num))
			{
				continue;
			}
			num -= 0.1;
			if (double.IsFinite(num) && num > 1E-09)
			{
				combatant.Buffs[text] = num;
				continue;
			}
			combatant.Buffs.Remove(text);
			flag |= SkillBuffRules.AffectsDerivedStats(_data, text) || L1jCookingRules.AffectsDerivedStats(text);
			if (!IsInternalBuff(text))
			{
				_events.Add(CombatEvent.BuffRemove(combatant, text));
			}
		}
		bool flag2 = flag && _data != null;
		if (flag2)
		{
			CombatantKind kind = combatant.Kind;
			bool flag3 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag2 = flag3 || HostilePlayerRules.IsHostilePlayer(combatant);
		}
		if (flag2 && !MonsterCompanionRules.IsCompanion(combatant))
		{
			CombatantBuilder.RefreshPlayer(combatant, _data);
		}
		AdvanceAwakeningMpDrain(combatant);
	}

	private static bool IsInternalBuff(string buffName)
	{
		return buffName.StartsWith('_');
	}

	private void RefreshBuffDerivedStats(Combatant combatant, string buffName)
	{
		bool flag = _data != null;
		if (flag)
		{
			CombatantKind kind = combatant.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = flag2 || HostilePlayerRules.IsHostilePlayer(combatant);
		}
		if (flag && !MonsterCompanionRules.IsCompanion(combatant) && SkillBuffRules.AffectsDerivedStats(_data, buffName))
		{
			CombatantBuilder.RefreshPlayer(combatant, _data);
		}
	}

	private void AdvanceRegeneration(Combatant combatant)
	{
		if (combatant.Kind == CombatantKind.Pet)
		{
			AdvancePetRegeneration(combatant);
			return;
		}
		if (combatant.UsesMonsterTemplate && !HostilePlayerRules.UsesPlayerCombatRules(combatant))
		{
			AdvanceMobRegeneration(combatant);
			return;
		}
		CombatantKind kind = combatant.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally && !HostilePlayerRules.IsHostilePlayer(combatant))
		{
			return;
		}
		double num = _healthRegenElapsed.GetValueOrDefault(combatant) + 0.1;
		double num2 = RegenerationRules.HealthIntervalSeconds(combatant);
		if (num + 1E-09 >= num2)
		{
			num = Math.Max(0.0, num - num2);
			if (RegenerationRules.CanRestoreHealth(combatant))
			{
				double amount = RegenerationRules.RollHealthAmount(combatant, _random);
				double num3 = combatant.Heal(amount);
				if (num3 > 0.0)
				{
					_events.Add(CombatEvent.Heal(combatant, combatant, num3));
				}
			}
			if (_mapUnderwater && !HasUnderwaterProtection(combatant))
			{
				double num4 = combatant.ApplyDamage(20.0);
				if (num4 > 0.0)
				{
					_events.Add(CombatEvent.Damage(combatant, combatant, num4, DamageType.Dot));
				}
			}
			if (_mapHealthDrainPerCycle > 0.0)
			{
				double num5 = combatant.ApplyDamage(_mapHealthDrainPerCycle);
				if (num5 > 0.0)
				{
					_events.Add(CombatEvent.Damage(combatant, combatant, num5, DamageType.Dot));
				}
			}
		}
		_healthRegenElapsed[combatant] = num;
		double num6 = _manaRegenElapsed.GetValueOrDefault(combatant) + 0.1;
		double num7 = RegenerationRules.ManaIntervalSeconds(combatant);
		if (num6 + 1E-09 >= num7)
		{
			num6 = Math.Max(0.0, num6 - num7);
			if (RegenerationRules.CanRestoreMana(combatant))
			{
				double mp = combatant.Mp;
				combatant.RestoreMp(RegenerationRules.ManaAmount(combatant));
				double num8 = combatant.Mp - mp;
				if (num8 > 0.0)
				{
					_events.Add(CombatEvent.MpChange(combatant, num8));
				}
			}
		}
		_manaRegenElapsed[combatant] = num6;
	}

	private static bool IsPhysicallyMoving(Combatant combatant)
	{
		if (combatant.VelX == 0.0)
		{
			return combatant.VelY != 0.0;
		}
		return true;
	}

	private static bool HasUnderwaterProtection(Combatant combatant)
	{
		if (!Equipped("arm_95") && !combatant.HasStatus("underwater_breath") && !combatant.Buffs.ContainsKey("underwater_breath"))
		{
			if (Equipped("l1j_item_21048") && Equipped("l1j_item_21049"))
			{
				return Equipped("l1j_item_21050");
			}
			return false;
		}
		return true;
		bool Equipped(string itemKey)
		{
			return combatant.EquippedItems.Values.Any((ItemStack item) => string.Equals(item.ItemKey, itemKey, StringComparison.Ordinal));
		}
	}

	private void AdvanceMovement(double deltaSeconds)
	{
		foreach (Combatant combatant2 in _combatants)
		{
			combatant2.VelX = 0.0;
			combatant2.VelY = 0.0;
			if (combatant2.IsRemote) continue;
			double num = CombatModifierRules.EffectiveMoveSpeed(combatant2, _data);
			if (!combatant2.IsAlive)
			{
				_isometricSteps.Remove(combatant2);
			}
			else
			{
				if (combatant2.IsHardControlled || (combatant2.Kind != CombatantKind.Player && IsStaggered(combatant2)) || AbsoluteBarrierRules.IsActive(combatant2) || num <= 0.0 || (_isometricSteps.ContainsKey(combatant2) && AdvanceExistingIsometricStep(combatant2, deltaSeconds)) || IsActionLocked(combatant2))
				{
					continue;
				}
				if (combatant2.Kind == CombatantKind.Mob)
				{
					if (DisableMobAi)
					{
						continue;
					}
					if (AdvanceFleeOnlyMob(combatant2, deltaSeconds, num))
					{
						continue;
					}
					Combatant combatant = MobPursuitTarget(combatant2);
					if (combatant != null)
					{
						combatant2.Facing8 = FacingFromVector(combatant.Pos.X - combatant2.Pos.X, combatant.Pos.Y - combatant2.Pos.Y);
						ResetIdleWander(combatant2);
						if (!TryAdvanceL1jMobTeleport(combatant2, combatant))
						{
							MoveToward(combatant2, combatant.Pos, deltaSeconds, EffectiveRange(combatant2, combatant, combatant2.AttackRange), num, combatReachStop: true);
						}
						continue;
					}
					Combatant mobGroupLeader = combatant2.MobGroupLeader;
					if (mobGroupLeader != null && mobGroupLeader != combatant2 && mobGroupLeader.IsAlive && _combatants.Contains(mobGroupLeader) && CombatRangeRules.DiamondDistance(combatant2.Pos, mobGroupLeader.Pos) > 96.0)
					{
						ResetIdleWander(combatant2);
						MoveToward(combatant2, mobGroupLeader.Pos, deltaSeconds, 96.0, num);
					}
					else if (combatant2.MarchTarget.HasValue)
					{
						double targetDist = CombatRangeRules.DiamondDistance(combatant2.Pos, combatant2.MarchTarget.Value);
						if (targetDist > 32.0)
						{
							ResetIdleWander(combatant2);
							combatant2.Facing8 = FacingFromVector(combatant2.MarchTarget.Value.X - combatant2.Pos.X, combatant2.MarchTarget.Value.Y - combatant2.Pos.Y);
							MoveToward(combatant2, combatant2.MarchTarget.Value, deltaSeconds, 24.0, num);
						}
						else
						{
							AdvancePassiveWander(combatant2, deltaSeconds, num);
						}
					}
					else if (combatant2.ReturnsHomeWhenIdle)
					{
						AdvanceWorldNpcReturnHome(combatant2, deltaSeconds, num);
					}
					else
					{
						AdvancePassiveWander(combatant2, deltaSeconds, num);
					}
					continue;
				}
				if (combatant2.Kind == CombatantKind.Ally)
				{
					AdvanceAllyMovement(combatant2, deltaSeconds, num);
					continue;
				}
				if (combatant2.Kind == CombatantKind.Pet)
				{
					AdvancePetMovement(combatant2, deltaSeconds, num);
					continue;
				}
				if (combatant2.Kind == CombatantKind.Summon)
				{
					Combatant dollOwner = null;
					foreach (var pair in _activeDolls)
					{
						if (pair.Value.Follower == combatant2)
						{
							dollOwner = pair.Key;
							break;
						}
					}
					if (dollOwner != null)
					{
						AdvanceMagicDollMovement(combatant2, dollOwner, deltaSeconds, num);
						continue;
					}
					AdvanceSummonMovement(combatant2, deltaSeconds, num);
					continue;
				}
				if (_directionalMoveInputs.TryGetValue(combatant2, out var value))
				{
					if (TryBeginDirectionalIsometricStep(combatant2, value, num, deltaSeconds))
					{
						_queuedDirectionalMoveInputs.Remove(combatant2);
					}
					continue;
				}
				if (_queuedDirectionalMoveInputs.Remove(combatant2, out var value2))
				{
					TryBeginDirectionalIsometricStep(combatant2, value2, num, deltaSeconds);
					continue;
				}
				WorldPoint? moveTarget = combatant2.MoveTarget;
				if (moveTarget.HasValue)
				{
					WorldPoint valueOrDefault = moveTarget.GetValueOrDefault();
					if (MoveToward(combatant2, valueOrDefault, deltaSeconds, 5.0, num))
					{
						combatant2.MoveTarget = null;
						combatant2.ManualMoveActive = false;
					}
				}
			}
		}
	}

	private void AdvanceAllyMovement(Combatant ally, double deltaSeconds, double moveSpeed)
	{
		Combatant combatant = PartyLeader();
		if (combatant == null)
		{
			return;
		}
		bool usesMonsterTemplate = ally.UsesMonsterTemplate;
		if (!combatant.IsAlive)
		{
			if (!usesMonsterTemplate || !AdvanceFleeOnlyMob(ally, deltaSeconds, moveSpeed))
			{
				Combatant combatant2 = (usesMonsterTemplate ? MobPursuitTarget(ally) : SelectNearestEnemy(ally, ally.AggroRange, requireLineOfSight: false, requireReachability: true));
				AdvanceAllySupport(ally, combatant2);
				if (combatant2 != null)
				{
					MoveToward(ally, combatant2.Pos, deltaSeconds, EffectiveRange(ally, combatant2, ally.AttackRange), moveSpeed, combatReachStop: true);
				}
			}
			return;
		}
		Combatant[] array = (from actor in _combatants
			where actor.Kind == CombatantKind.Ally
			orderby actor.BornSeq, _combatants.IndexOf(actor)
			select actor).ToArray();
		int allyIndex = Math.Max(0, Array.IndexOf(array, ally));
		WorldPoint worldPoint = MercenaryRules.FormationPoint(combatant, allyIndex, array.Length);
		if (ally.Pos.DistanceSquaredTo(combatant.Pos) > 810000.0)
		{
			WorldPoint worldPoint2 = ClampAndSnapPlacement(worldPoint, ally.Radius);
			ally.Pos = (CanReachExplorationPoint(combatant.Pos, worldPoint2) ? worldPoint2 : ClampAndSnapPlacement(combatant.Pos, ally.Radius));
			_navigationPaths.Remove(ally);
			ally.MoveTarget = null;
		}
		else
		{
			if (usesMonsterTemplate && AdvanceFleeOnlyMob(ally, deltaSeconds, moveSpeed))
			{
				return;
			}
			Combatant combatant3 = (usesMonsterTemplate ? MobPursuitTarget(ally) : SelectNearestEnemy(ally, ally.AggroRange, requireLineOfSight: false, requireReachability: true));
			AdvanceAllySupport(ally, combatant3);
			bool flag = AllyLeashedToLeader(ally);
			if (flag && CombatRangeRules.DiamondDistance(ally.Pos, combatant.Pos) > 288.0)
			{
				ResetIdleWander(ally);
				MoveToward(ally, worldPoint, deltaSeconds, 5.0, moveSpeed);
				return;
			}
			double num = (flag ? 288.0 : 520.0);
			if (combatant3 != null && CombatRangeRules.DiamondDistance(combatant3.Pos, combatant.Pos) <= num)
			{
				ally.Facing8 = FacingFromVector(combatant3.Pos.X - ally.Pos.X, combatant3.Pos.Y - ally.Pos.Y);
				ResetIdleWander(ally);
				MoveToward(ally, combatant3.Pos, deltaSeconds, EffectiveRange(ally, combatant3, ally.AttackRange), moveSpeed, combatReachStop: true);
			}
			else if (ally.Pos.DistanceSquaredTo(worldPoint) > 9216.0)
			{
				ResetIdleWander(ally);
				MoveToward(ally, worldPoint, deltaSeconds, 5.0, moveSpeed);
			}
			else
			{
				AdvanceIdleWander(ally, deltaSeconds, moveSpeed, worldPoint);
			}
		}
	}

	private bool MoveToward(Combatant combatant, WorldPoint destination, double deltaSeconds, double stopDistance, double moveSpeed, bool combatReachStop = false)
	{
		double num = ((stopDistance > 0.0 && !HasLineOfSight(combatant.Pos, destination)) ? 0.0 : Math.Max(0.0, stopDistance));
		if ((combatReachStop ? CombatRangeRules.DiamondDistance(combatant.Pos, destination) : combatant.Pos.DistanceTo(destination)) - num <= 1E-06)
		{
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
			_navigationPaths.Remove(combatant);
			_explorationNavigationPaths.Remove(combatant);
			return true;
		}
		WorldPoint waypoint = NavigationWaypoint(combatant, destination);
		double num2 = waypoint.X - combatant.Pos.X;
		double num3 = waypoint.Y - combatant.Pos.Y;
		if (Math.Sqrt(num2 * num2 + num3 * num3) <= 1E-06)
		{
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
			_navigationPaths.Remove(combatant);
			_explorationNavigationPaths.Remove(combatant);
			return true;
		}
		if (!TryBeginIsometricStep(combatant, waypoint, moveSpeed, deltaSeconds))
		{
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
			if (combatant.Kind == CombatantKind.Player && combatant.MoveTarget.HasValue)
			{
				_navigationPaths.Remove(combatant);
				_explorationNavigationPaths.Remove(combatant);
				return true;
			}
		}
		return false;
	}

	private static int FacingFromVector(double x, double y)
	{
		return (((int)Math.Round(Math.Atan2(y, x) / (Math.PI / 4.0)) + 3) % 8 + 8) % 8;
	}


	private void ResolveCollisions()
	{
		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant combatant = _combatants[i];
			if (!combatant.IsAlive || combatant.Radius <= 0.0 || combatant.IsRemote || (DisableMobAi && combatant.Kind == CombatantKind.Mob))
			{
				continue;
			}
			for (int j = i + 1; j < _combatants.Count; j++)
			{
				Combatant combatant2 = _combatants[j];
				if (!combatant2.IsAlive || combatant2.Radius <= 0.0 || combatant2.IsRemote || (DisableMobAi && combatant2.Kind == CombatantKind.Mob))
				{
					continue;
				}
				double num = combatant.Radius + combatant2.Radius;
				double num2 = combatant2.Pos.X - combatant.Pos.X;
				double num3 = combatant2.Pos.Y - combatant.Pos.Y;
				double num4 = num2 * num2 + num3 * num3;
				if (!(num4 >= num * num))
				{
					double num5 = Math.Sqrt(num4);
					double num6;
					double num7;
					if (num5 <= 1E-06)
					{
						(double X, double Y) tuple = CoincidentSeparationDirection(combatant, combatant2, i, j);
						num6 = tuple.X;
						num7 = tuple.Y;
						num5 = 0.0;
					}
					else
					{
						num6 = num2 / num5;
						num7 = num3 / num5;
					}
					double num8 = num - num5;
					if (!SolidBodyRules.IsSolidPair(combatant, combatant2))
					{
						double num9 = num8 * 0.5;
						combatant.Pos = new WorldPoint(combatant.Pos.X - num6 * num9, combatant.Pos.Y - num7 * num9);
						combatant2.Pos = new WorldPoint(combatant2.Pos.X + num6 * num9, combatant2.Pos.Y + num7 * num9);
					}
				}
			}
		}
	}

	private (double X, double Y) CoincidentSeparationDirection(Combatant left, Combatant right, int leftIndex, int rightIndex)
	{
		double num = (double)(leftIndex * 31 + rightIndex * 17) * Math.PI / 4.0;
		return (X: Math.Cos(num), Y: Math.Sin(num));
	}

	private void ClampCombatants()
	{
		foreach (Combatant combatant in _combatants)
		{
			if (combatant.IsAlive)
			{
				if (combatant.IsRemote || (DisableMobAi && combatant.Kind == CombatantKind.Mob))
				{
					continue;
				}
				combatant.Pos = ClampAndSnapPlacement(combatant.Pos, combatant.Radius);
			}
		}
	}

	private void LaunchPhysicalProjectile(Combatant source, Combatant target, string kind, bool basicAttack, double damageMultiplier)
	{
		PhysicalHitResult committedHit = (basicAttack ? CommitBasicPhysicalAttack(source, target) : RollPhysicalHit(source, target, true, forceHeavy: false, forceCritical: false, 0.0, 0.0, forceHit: false, basicAttack: false, damageMultiplier));
		if (!basicAttack || _data == null || AmmunitionRules.ConsumeCommittedBasicShot(_data, source))
		{
			CreateProjectile(source, target, kind, basicAttack, magicWeaponAttack: false, committedHit, 0.0);
		}
	}

	private void LaunchMagicWeaponProjectile(Combatant source, Combatant target, string kind)
	{
		CreateProjectile(source, target, kind, basicAttack: false, magicWeaponAttack: true, default(PhysicalHitResult), RollMagicWeaponAttackDamage(source, target));
	}

	private void CreateProjectile(Combatant source, Combatant target, string kind, bool basicAttack, bool magicWeaponAttack, PhysicalHitResult committedHit, double committedMagicDamage)
	{
		double speed = Math.Max(1.0, source.ProjectileSpeed);
		double turnRate = Math.Max(0.0, source.ProjectileTurnRate);
		WorldPoint worldPoint = Offset(source.Pos, -42.0);
		WorldPoint worldPoint2 = Offset(target.Pos, -30.0);
		(double X, double Y) tuple = VelocityToward(worldPoint, worldPoint2, speed, source.Facing8);
		double item = tuple.X;
		double item2 = tuple.Y;
		CombatProjectile combatProjectile = new CombatProjectile
		{
			Id = ++_nextProjectileId,
			Source = source,
			Target = target,
			Pos = worldPoint,
			GroundPos = source.Pos,
			VelX = item,
			VelY = item2,
			Facing8 = FacingFromVector(item, item2),
			Speed = speed,
			TurnRate = turnRate,
			RemainingLife = 1.8,
			Kind = kind,
			BasicAttack = basicAttack,
			MagicWeaponAttack = magicWeaponAttack,
			DamageDelivery = ((!(basicAttack || magicWeaponAttack)) ? ((!(kind == "rapidfire")) ? DirectDamageDelivery.ActiveSkill : DirectDamageDelivery.SecondaryEffect) : DirectDamageDelivery.BasicAttack),
			CommittedHit = committedHit,
			CommittedMagicDamage = committedMagicDamage
		};
		_projectiles.Add(combatProjectile);
		_events.Add(CombatEvent.Projectile(source, target, kind, worldPoint, worldPoint2, speed, combatProjectile.Facing8));
	}

	private void AdvanceProjectiles(double deltaSeconds)
	{
		for (int num = _projectiles.Count - 1; num >= 0; num--)
		{
			CombatProjectile combatProjectile = _projectiles[num];
			combatProjectile.RemainingLife = Math.Max(0.0, combatProjectile.RemainingLife - deltaSeconds);
			bool flag = !combatProjectile.TargetLost && combatProjectile.Target.IsAlive && _combatants.Contains(combatProjectile.Target);
			if (!flag)
			{
				combatProjectile.TargetLost = true;
			}
			WorldPoint point = Offset(combatProjectile.Target.Pos, -30.0);
			if (flag)
			{
				double num2 = point.X - combatProjectile.Pos.X;
				double num3 = point.Y - combatProjectile.Pos.Y;
				if (num2 * num2 + num3 * num3 > 1E-06)
				{
					double num4 = Math.Atan2(num3, num2);
					double num5 = Math.Atan2(combatProjectile.VelY, combatProjectile.VelX);
					double num6 = Math.Clamp(NormalizeAngle(num4 - num5), (0.0 - combatProjectile.TurnRate) * deltaSeconds, combatProjectile.TurnRate * deltaSeconds);
					double num7 = num5 + num6;
					combatProjectile.VelX = Math.Cos(num7) * combatProjectile.Speed;
					combatProjectile.VelY = Math.Sin(num7) * combatProjectile.Speed;
					combatProjectile.Facing8 = FacingFromVector(combatProjectile.VelX, combatProjectile.VelY);
				}
			}
			WorldPoint pos = combatProjectile.Pos;
			WorldPoint groundPos = combatProjectile.GroundPos;
			combatProjectile.Pos = new WorldPoint(pos.X + combatProjectile.VelX * deltaSeconds, pos.Y + combatProjectile.VelY * deltaSeconds);
			combatProjectile.GroundPos = new WorldPoint(groundPos.X + combatProjectile.VelX * deltaSeconds, groundPos.Y + combatProjectile.VelY * deltaSeconds);
			if (!HasLineOfSight(groundPos, combatProjectile.GroundPos))
			{
				_projectiles.RemoveAt(num);
			}
			else
			{
				double num8 = Math.Max(0.0, combatProjectile.Radius + combatProjectile.Target.Radius);
				if (flag && SegmentDistanceSquared(point, pos, combatProjectile.Pos) <= num8 * num8)
				{
					_projectiles.RemoveAt(num);
					if (combatProjectile.MagicWeaponAttack)
					{
						ApplyCommittedMagicWeaponHit(combatProjectile.Source, combatProjectile.Target, combatProjectile.CommittedMagicDamage, combatProjectile.DamageDelivery);
						TryTriggerSummonAoeAttack(combatProjectile.Source, combatProjectile.Target);
					}
					else if (combatProjectile.BasicAttack)
					{
						ApplyBasicPhysicalAttack(combatProjectile.Source, combatProjectile.Target, combatProjectile.CommittedHit);
					}
					else
					{
						ApplyCommittedPhysicalHit(combatProjectile.Source, combatProjectile.Target, combatProjectile.CommittedHit, combatProjectile.DamageDelivery);
					}
				}
				else if (combatProjectile.RemainingLife <= 0.0)
				{
					_projectiles.RemoveAt(num);
				}
			}
		}
	}

	private static WorldPoint Offset(WorldPoint point, double y)
	{
		return new WorldPoint(point.X, point.Y + y);
	}

	private static (double X, double Y) VelocityToward(WorldPoint from, WorldPoint to, double speed, int fallbackFacing)
	{
		double num = to.X - from.X;
		double num2 = to.Y - from.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (num3 > 1E-06)
		{
			return (X: num / num3 * speed, Y: num2 / num3 * speed);
		}
		double num4 = (double)(fallbackFacing - 3) * Math.PI / 4.0;
		return (X: Math.Cos(num4) * speed, Y: Math.Sin(num4) * speed);
	}

	private static double NormalizeAngle(double angle)
	{
		while (angle > Math.PI)
		{
			angle -= Math.PI * 2.0;
		}
		while (angle < -Math.PI)
		{
			angle += Math.PI * 2.0;
		}
		return angle;
	}

	private static double SegmentDistanceSquared(WorldPoint point, WorldPoint start, WorldPoint end)
	{
		double num = end.X - start.X;
		double num2 = end.Y - start.Y;
		double num3 = num * num + num2 * num2;
		if (num3 <= 1E-06)
		{
			return point.DistanceSquaredTo(start);
		}
		double num4 = Math.Clamp(((point.X - start.X) * num + (point.Y - start.Y) * num2) / num3, 0.0, 1.0);
		double num5 = start.X + num * num4;
		double num6 = start.Y + num2 * num4;
		double num7 = point.X - num5;
		double num8 = point.Y - num6;
		return num7 * num7 + num8 * num8;
	}

	private void AdvanceCombatant(Combatant attacker, double deltaSeconds)
	{
		if (DisableMobAi && attacker.Kind == CombatantKind.Mob)
		{
			return;
		}
		if (attacker.IsRemote) return;
		if (!attacker.IsAlive)
		{
			_manualCastQueue.Remove(attacker);
			return;
		}
		if (_convertCooldowns.TryGetValue(attacker, out var value))
		{
			value -= deltaSeconds;
			if (value > 1E-09)
			{
				_convertCooldowns[attacker] = value;
			}
			else
			{
				_convertCooldowns.Remove(attacker);
			}
		}
		Combatant? manualTarget = (attacker.Kind == CombatantKind.Player) ? GetManualTarget(attacker) : null;

		if (attacker.Kind == CombatantKind.Player && (attacker.ManualMoveActive || _directionalMoveInputs.ContainsKey(attacker)))
		{
			// Player manually clicked somewhere to walk or is moving with WASD.
			// WASD movement or ground click destination ALWAYS takes priority over attacking!
			if (_directionalMoveInputs.ContainsKey(attacker) || manualTarget == null)
			{
				return;
			}
		}

		if (attacker.Kind == CombatantKind.Player && manualTarget != null && manualTarget.IsAlive)
		{
			if (IsWithinRange(attacker, manualTarget, attacker.AttackRange))
			{
				attacker.MoveTarget = null;
				attacker.ManualMoveActive = false;
				attacker.VelX = 0.0;
				attacker.VelY = 0.0;
				_navigationPaths.Remove(attacker);
				_isometricSteps.Remove(attacker);

				if (!attacker.IsHardControlled && !IsStaggered(attacker) && !IsActionLocked(attacker) && attacker.DelayTicks <= 0 && attacker.AttackCd <= 1E-09 && !TryResolveQueuedManualCast(attacker) && !attacker.CannotAttack && WeightRules.ActionsAllowed(attacker) && !AbsoluteBarrierRules.IsActive(attacker))
				{
					if (PerformAttack(attacker, manualTarget))
					{
						CommitAttackCooldown(attacker, Math.Max(0.0, AttackIntervalSeconds(attacker) + AttackCarry(attacker)));
					}
				}
			}
			else if (!_directionalMoveInputs.ContainsKey(attacker))
			{
				if (!attacker.MoveTarget.HasValue && !_isometricSteps.ContainsKey(attacker))
				{
					SetMoveTarget(attacker, manualTarget.Pos, isManual: false);
				}
			}
		}
		else if (attacker.Kind == CombatantKind.Player && attacker.AutomaticCombatEnabled)
		{
			// Check if any enemy is within attack range
			Combatant inRangeEnemy = SelectNearestEnemy(attacker, attacker.AttackRange, requireLineOfSight: true);
			if (inRangeEnemy != null)
			{
				// In attack range! Stop moving so attack can proceed immediately
				attacker.MoveTarget = null;
				attacker.ManualMoveActive = false;
				attacker.VelX = 0.0;
				attacker.VelY = 0.0;
				_navigationPaths.Remove(attacker);
				_isometricSteps.Remove(attacker);

				if (!attacker.IsHardControlled && !IsStaggered(attacker) && !IsActionLocked(attacker) && attacker.DelayTicks <= 0 && attacker.AttackCd <= 1E-09 && !TryResolveQueuedManualCast(attacker) && !attacker.CannotAttack && WeightRules.ActionsAllowed(attacker) && !AbsoluteBarrierRules.IsActive(attacker))
				{
					if (PerformAttack(attacker, inRangeEnemy))
					{
						CommitAttackCooldown(attacker, Math.Max(0.0, AttackIntervalSeconds(attacker) + AttackCarry(attacker)));
					}
				}
			}
			else
			{
				// Not in attack range. Check for enemy within 3 steps (~150px)
				const double ThreeStepsRange = 150.0;
				Combatant nearby = SelectNearestEnemy(attacker, ThreeStepsRange, requireLineOfSight: true);
				if (nearby != null)
				{
					if (IsWithinRange(attacker, nearby, attacker.AttackRange))
					{
						attacker.MoveTarget = null;
						attacker.ManualMoveActive = false;
						attacker.VelX = 0.0;
						attacker.VelY = 0.0;
						_navigationPaths.Remove(attacker);
						_isometricSteps.Remove(attacker);

						if (!attacker.IsHardControlled && !IsStaggered(attacker) && !IsActionLocked(attacker) && attacker.DelayTicks <= 0 && attacker.AttackCd <= 1E-09 && !TryResolveQueuedManualCast(attacker) && !attacker.CannotAttack && WeightRules.ActionsAllowed(attacker) && !AbsoluteBarrierRules.IsActive(attacker))
						{
							if (PerformAttack(attacker, nearby))
							{
								CommitAttackCooldown(attacker, Math.Max(0.0, AttackIntervalSeconds(attacker) + AttackCarry(attacker)));
							}
						}
					}
					else if (!attacker.MoveTarget.HasValue && !_isometricSteps.ContainsKey(attacker))
					{
						SetMoveTarget(attacker, nearby.Pos, isManual: false);
					}
				}
			}
		}
		else if (!attacker.IsHardControlled && !IsStaggered(attacker) && !IsActionLocked(attacker) && attacker.DelayTicks <= 0 && attacker.AttackCd <= 1E-09 && !TryResolveQueuedManualCast(attacker) && !attacker.CannotAttack && (!IsPhysicallyMoving(attacker) && WeightRules.ActionsAllowed(attacker) && !AbsoluteBarrierRules.IsActive(attacker)))
		{
			Combatant combatant = SelectTarget(attacker);
			if (combatant != null && PerformAttack(attacker, combatant))
			{
				CommitAttackCooldown(attacker, Math.Max(0.0, AttackIntervalSeconds(attacker) + AttackCarry(attacker)));
			}
		}
	}

	private void AdvanceSharedActionCooldowns(double deltaSeconds)
	{
		foreach (Combatant combatant in _combatants.ToArray())
		{
			if (combatant.AttackCd > 0.0)
			{
				combatant.AttackCd = Math.Max(0.0, combatant.AttackCd - deltaSeconds);
			}
			if (combatant.CastCd > 0.0)
			{
				combatant.CastCd = Math.Max(0.0, combatant.CastCd - deltaSeconds);
			}
		}
	}

	private static bool SharedActionOnCooldown(Combatant combatant)
	{
		if (!(combatant.AttackCd > 1E-09))
		{
			return combatant.CastCd > 1E-09;
		}
		return true;
	}

	private static double SharedActionCarry(Combatant combatant)
	{
		return Math.Min(0.0, Math.Max(combatant.AttackCd, combatant.CastCd));
	}

	private static void CommitSharedActionCooldown(Combatant combatant, double seconds)
	{
		combatant.CastCd = (combatant.AttackCd = Math.Max(0.0, seconds));
	}

	private static void CommitAttackCooldown(Combatant combatant, double seconds)
	{
		combatant.AttackCd = Math.Max(0.0, seconds);
	}

	private static double AttackCarry(Combatant combatant)
	{
		return Math.Min(0.0, combatant.AttackCd);
	}

	private Combatant? SelectTarget(Combatant attacker)
	{
		if (attacker.Kind != CombatantKind.Mob && (!attacker.UsesMonsterTemplate || HostilePlayerRules.UsesPlayerCombatRules(attacker)))
		{
			if (attacker.Kind != CombatantKind.Pet)
			{
				return SelectNearestEnemy(attacker, attacker.AttackRange, requireLineOfSight: true);
			}
			return PetAttackTarget(attacker);
		}
		return SelectMobTarget(attacker, attacker.AttackRange);
	}

	private static double SkillRange(Combatant caster, CombatSkill skill)
	{
		if (!(skill.CastRange > 0.0))
		{
			return caster.AttackRange;
		}
		return skill.CastRange;
	}

	public static bool IsWithinRange(Combatant source, Combatant target, double range)
	{
		double num = EffectiveRange(source, target, range) + 1E-06;
		return CombatRangeRules.DiamondDistance(source.Pos, target.Pos) <= num;
	}

	internal Combatant? SelectNearestEnemy(Combatant attacker, double range, bool requireLineOfSight = false, bool requireReachability = false)
	{
		Combatant combatant = null;
		double num = 0.0;
		int num2 = 0;
		PlayerAttackPriority[] array = ((attacker.Kind == CombatantKind.Player) ? attacker.AttackPriorities : Array.Empty<PlayerAttackPriority>());
		bool flag = array.Length != 0;
		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant combatant2 = _combatants[i];
			if (combatant2.IsAlive && IsEnemy(attacker, combatant2))
			{
				double num3 = CombatRangeRules.DiamondDistance(attacker.Pos, combatant2.Pos);
				if ((!(range >= 0.0) || !(num3 > EffectiveRange(attacker, combatant2, range) + 1E-06)) && (flag || combatant == null || (!(num3 > num) && (num3 != num || combatant2.BornSeq < num2))) && CanHostileInteract(attacker, combatant2) && (!requireLineOfSight || HasCombatLineOfSight(attacker, combatant2)) && (!requireReachability || CanNavigateTo(attacker, combatant2.Pos)) && (!flag || combatant == null || PlayerPriorityCandidateWins(attacker, combatant2, num3, combatant, num, array)))
				{
					combatant = combatant2;
					num = num3;
					num2 = combatant2.BornSeq;
				}
			}
		}
		return combatant;
	}

	private bool PlayerPriorityCandidateWins(Combatant player, Combatant candidate, double candidateDistance, Combatant current, double currentDistance, IReadOnlyList<PlayerAttackPriority> priorities)
	{
		foreach (PlayerAttackPriority priority in priorities)
		{
			switch (priority)
			{
			case PlayerAttackPriority.Nearest:
				if (candidateDistance != currentDistance)
				{
					return candidateDistance < currentDistance;
				}
				break;
			case PlayerAttackPriority.Boss:
				if (candidate.IsBoss != current.IsBoss)
				{
					return candidate.IsBoss;
				}
				break;
			case PlayerAttackPriority.Aggressive:
			{
				bool flag3 = candidate.Kind == CombatantKind.Mob && !candidate.Passive;
				bool flag4 = current.Kind == CombatantKind.Mob && !current.Passive;
				if (flag3 != flag4)
				{
					return flag3;
				}
				break;
			}
			case PlayerAttackPriority.AttackingPlayer:
			{
				bool flag = IsMobAttackingPlayer(candidate, player);
				bool flag2 = IsMobAttackingPlayer(current, player);
				if (flag != flag2)
				{
					return flag;
				}
				break;
			}
			}
		}
		return candidate.BornSeq < current.BornSeq;
	}

	private bool IsMobAttackingPlayer(Combatant candidate, Combatant player)
	{
		if (candidate.Kind != CombatantKind.Mob)
		{
			return false;
		}
		if ((MobTauntTarget(candidate) ?? MaximumHateTarget(candidate)) == player && L1jMobAggroRules.CanAcquireOrKeep(_data, candidate, player, alreadyKnown: true) && IsWithinRange(candidate, player, Math.Max(0.0, candidate.AggroRange)) && HasPlayerPursuitSlot(candidate, player))
		{
			return CanNavigateTo(candidate, player.Pos);
		}
		return false;
	}

	private static double EffectiveRange(Combatant source, Combatant target, double configuredRange)
	{
		if (configuredRange <= 0.0)
		{
			return Math.Max(0.0, configuredRange);
		}
		return (Math.Max(0.0, source.Radius) + Math.Max(0.0, target.Radius)) * 2.0 + configuredRange;
	}

	public static bool IsEnemy(Combatant source, Combatant candidate)
	{
		if (!candidate.CastleWarInvulnerable)
		{
			if (!candidate.TrainingScarecrow)
			{
				if (!candidate.NeutralWorldNpc || source.Kind == CombatantKind.Mob)
				{
					bool? flag = HostilePlayerFactionEnemy(source, candidate);
					if (!flag.HasValue)
					{
						if (source.Kind != CombatantKind.Mob)
						{
							return candidate.Kind == CombatantKind.Mob;
						}
						return candidate.Kind != CombatantKind.Mob;
					}
					return flag == true;
				}
				return false;
			}
			if (source.Kind == CombatantKind.Player)
			{
				return source.Level < 5;
			}
			return false;
		}
		return false;
	}

	private static bool IsValidExplicitSkillTarget(Combatant caster, Combatant candidate, JsonObject source)
	{
		if (!IsEnemy(caster, candidate))
		{
			if (!IsEnemy(caster, candidate))
			{
				return L1jSkillTargetRules.AllowsFriendlyMonsterCompanion(source, candidate);
			}
			return false;
		}
		return true;
	}

	private bool PerformAttack(Combatant attacker, Combatant target)
	{
		if (attacker.CannotAttack)
		{
			return false;
		}
		if (!CanHostileInteract(attacker, target))
		{
			return false;
		}
		if (_data != null && AmmunitionRules.RequiresArrow(_data, attacker) && !AmmunitionRules.CanLaunchBasicShot(_data, attacker))
		{
			return false;
		}
		MarkMobCombatActivity(attacker, target);
		RegisterContestedAttacker(target, attacker);
		double num = target.Pos.X - attacker.Pos.X;
		double num2 = target.Pos.Y - attacker.Pos.Y;
		if (num * num + num2 * num2 > 1E-06)
		{
			attacker.Facing8 = FacingFromVector(num, num2);
			if (target != null && target.IsAlive && target.Kind == CombatantKind.Mob)
			{
				target.Facing8 = FacingFromVector(-num, -num2);
			}
		}
		_events.Add(CombatEvent.Attack(attacker, target, attacker.Facing8));
		if (attacker.L1jWorldNpcImpl == "L1Guard")
		{
			attacker.NeutralWorldNpc = false;
		}
		if (CanL1jGuardianMaterialAction(attacker, target))
		{
			PhysicalHitResult committed = CommitBasicPhysicalAttack(attacker, target);
			if (!committed.Hit)
			{
				_events.Add(CombatEvent.Miss(attacker, target));
				return true;
			}
			if (!TryL1jGuardianMaterialAction(attacker, target))
			{
				ApplyBasicPhysicalAttack(attacker, target, committed);
			}
			return true;
		}
		if (target.TrainingScarecrow)
		{
			if (!CommitBasicPhysicalAttack(attacker, target).Hit)
			{
				_events.Add(CombatEvent.Miss(attacker, target));
				return true;
			}
			target.Facing8 = (target.Facing8 + 1) & 7;
			if (_data != null && attacker.Kind == CombatantKind.Player && attacker.Level < 5 && target.ExperienceReward > 0.0)
			{
				AwardMainPlayer(attacker, target.ExperienceReward);
			}
			return true;
		}
		if (TryPerformPetBasicAttack(attacker, target))
		{
			return true;
		}
		if (TryPerformSummonMagicAttack(attacker, target))
		{
			return true;
		}
		if (TryPerformMobMagicBasicAttack(attacker, target))
		{
			return true;
		}
		if (CombatModifierRules.UsesMagicWeaponAttack(attacker, _data))
		{
			if (attacker.BasicProjectileKind.Length > 0)
			{
				LaunchMagicWeaponProjectile(attacker, target, attacker.BasicProjectileKind);
			}
			else
			{
				ApplyMagicWeaponAttack(attacker, target);
			}
			return true;
		}
		string text = attacker.BasicProjectileKind;
		if (text.Length == 0 && attacker.D.UsesRangedAttack)
		{
			text = "arrow";
		}
		if (text.Length > 0)
		{
			LaunchPhysicalProjectile(attacker, target, text, basicAttack: true, 1.0);
			return true;
		}
		ResolveBasicPhysicalAttack(attacker, target);
		return true;
	}

	private void ResolveBasicPhysicalAttack(Combatant attacker, Combatant target)
	{
		PhysicalHitResult committed = CommitBasicPhysicalAttack(attacker, target);
		ApplyBasicPhysicalAttack(attacker, target, committed);
	}

	private PhysicalHitResult CommitBasicPhysicalAttack(Combatant attacker, Combatant target)
	{
		if (!target.IsAlive)
		{
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, Heavy: false, attacker.D.UsesRangedAttack);
		}
		bool flag = attacker.Counters.Remove("darkEvadeSure");
		bool forceCritical = attacker.Counters.Remove("darkEvadeCritical");
		if (!flag && TryDarkStealthEvade(target))
		{
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, Heavy: false, attacker.D.UsesRangedAttack);
		}
		return RollPhysicalHit(attacker, target, null, forceHeavy: false, forceCritical, 0.0, 0.0, flag, basicAttack: true);
	}

	private void ApplyBasicPhysicalAttack(Combatant attacker, Combatant target, PhysicalHitResult committed)
	{
		if (committed.Hit && committed.Damage > 0.0)
		{
			committed = committed with
			{
				Damage = RelicConditionalCombatRules.ApplyBasicAttackDamage(_data, attacker, target, committed.Damage, AttackIntervalSeconds(attacker))
			};
		}
		committed = ApplyDiceDaggerDamage(attacker, target, committed);
		PhysicalHitResult hit = ApplyCommittedPhysicalHit(attacker, target, committed, DirectDamageDelivery.BasicAttack);
		if (hit.Hit && hit.Damage > 0.0 && RelicConditionalCombatRules.HasteStrikeBonus(_data, attacker) > 0.0)
		{
			RemoveBuff(attacker, "haste");
		}
		if (hit.Hit && hit.Damage > 0.0)
		{
			TryApplyL1jMobPoisonAttack(attacker, target);
		}
		if (hit.Hit && hit.Damage > 0.0 && target.IsAlive)
		{
			TryApplyRelicElementExposure(attacker, target);
		}
		if (hit.Hit && hit.Damage > 0.0 && target.IsAlive)
		{
			ApplyWeakPointInsight(attacker, target);
		}
		if (hit.Hit && hit.Damage > 0.0 && target.IsAlive)
		{
			TryApplyDarkPoison(attacker, target, hit.Damage);
			TryTriggerSummonPhysicalProcs(attacker, target);
		}
		if (hit.Hit && hit.Damage > 0.0)
		{
			TryApplyRelicBasicHitEffects(attacker, target, hit);
		}
		if (hit.Critical && hit.Damage > 0.0)
		{
			_ = target.IsAlive;
		}
		TryApplyRelicAttackProcs(attacker, target, hit.Hit);
		TryCounterAttackReaction(attacker, target, hit.Hit && hit.Damage > 0.0);
	}

	private PhysicalHitResult PerformPhysicalHit(Combatant attacker, Combatant target, bool? rangedOverride = null, bool forceHeavy = false, bool forceCritical = false, double extraDamage = 0.0, double skillHitBonus = 0.0, bool forceHit = false, bool basicAttack = false, double damageMultiplier = 1.0, DirectDamageDelivery delivery = DirectDamageDelivery.ActiveSkill, int? attackDiceCountOverride = null, int? attackDiceSidesOverride = null)
	{
		if (basicAttack && !forceHit && TryDarkStealthEvade(target))
		{
			_events.Add(CombatEvent.Miss(attacker, target));
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, Heavy: false, rangedOverride ?? attacker.D.UsesRangedAttack);
		}
		PhysicalHitResult result = RollPhysicalHit(attacker, target, rangedOverride, forceHeavy, forceCritical, extraDamage, skillHitBonus, forceHit, basicAttack, damageMultiplier, attackDiceCountOverride, attackDiceSidesOverride);
		return ApplyCommittedPhysicalHit(attacker, target, result, delivery);
	}

	private PhysicalHitResult ApplyCommittedPhysicalHit(Combatant attacker, Combatant target, PhysicalHitResult result, DirectDamageDelivery delivery = DirectDamageDelivery.ActiveSkill)
	{
		if (!target.IsAlive)
		{
			return result with
			{
				Hit = false,
				Damage = 0.0
			};
		}
		if (!result.Hit)
		{
			_events.Add(CombatEvent.Miss(attacker, target));
			return result;
		}
		DamageType damageType = (result.Ranged ? DamageType.Ranged : DamageType.Melee);
		bool blocked;
		double num = ApplyDirectDamage(attacker, target, result.Damage, damageType, delivery, out blocked, result.Critical, attacker.AttackElement, result.Heavy, WeaponDurabilityRules.DamagePenalty(_data, attacker));
		if (blocked)
		{
			return result with
			{
				Damage = 0.0
			};
		}
		if (num > 0.0)
		{
			WeaponDurabilityRules.TryAccumulateBrokenBlade(_data, attacker, target, _random);
		}
		if (target.Dead)
		{
			ResolveDeath(target, attacker);
		}
		return result with
		{
			Damage = num
		};
	}

	private PhysicalHitResult RollPhysicalHit(Combatant attacker, Combatant target, bool? rangedOverride, bool forceHeavy, bool forceCritical, double extraDamage, double skillHitBonus = 0.0, bool forceHit = false, bool basicAttack = false, double damageMultiplier = 1.0, int? attackDiceCountOverride = null, int? attackDiceSidesOverride = null, double? baseDamageOverride = null)
	{
		bool flag = rangedOverride ?? attacker.D.UsesRangedAttack;
		(double Hit, double Damage, double ExtraDamage) tuple = SkillBuffRules.MonsterCompanionPhysicalBonuses(attacker, _data, flag);
		double item = tuple.Hit;
		double item2 = tuple.Damage;
		double item3 = tuple.ExtraDamage;
		double num = (flag ? attacker.D.RangedHit : attacker.D.MeleeHit) + item + attacker.D.ExtraHit + skillHitBonus + (basicAttack ? RelicConditionalCombatRules.HasteStrikeBonus(_data, attacker) : 0.0) + (double)WeightRules.CachedHitModifier(attacker) - StatusRules.PhysicalHitPenalty(attacker);
		bool flag2 = false;
		bool flag3;
		bool flag4;
		if (forceHeavy)
		{
			flag3 = true;
			flag4 = true;
		}
		else
		{
			bool flag5 = _data != null;
			if (flag5)
			{
				bool flag6;
				switch (WeaponCombatProfile.ResolveFamily(attacker.MainWeaponId, _data))
				{
				case WeaponFamily.Claw:
				case WeaponFamily.Kiringku:
					flag6 = true;
					break;
				default:
					flag6 = false;
					break;
				}
				flag5 = flag6;
			}
			if (flag5)
			{
				flag4 = true;
				flag3 = false;
			}
			else
			{
				bool flag7 = IsPlayerTypeCombatant(attacker);
				bool flag8 = IsPlayerTypeCombatant(target);
				double num2 = num + (double)(flag7 ? attacker.Level : 0) + BehaviorBuffRules.TargetHitValueAdjustment(target, flag8) + (flag ? MagicDollRules.BowHitAdjustment(attacker) : MagicDollRules.MeleeHitAdjustment(attacker));
				int num3 = (int)Math.Floor(target.D.ArmorClass + StatusRules.ArmorClassAdjustment(target) + MagicDollRules.ArmorClassAdjustment(target));
				num2 += (double)((num3 >= 0) ? num3 : ((int)(_random.NextDouble() * ((double)num3 * 1.5)) - 1));
				num2 *= 5.0;
				num2 = Math.Max(num2, BehaviorBuffRules.HitValueFloor(attacker));
				int num4 = BehaviorBuffRules.HitPercentLowerBound(target, flag8);
				int num5 = Math.Clamp((int)num2, num4, 95);
				int num6 = _random.Roll(1, 100);
				int num7 = RelicConditionalCombatRules.HeavyRollThreshold(_data, attacker);
				int num8 = ((num7 <= 20) ? ((21 - num7) * 5) : 5);
				flag3 = num6 <= num8;
				if (flag && RangedEvasionStageApplies(attacker, target) && (int)num2 > num6)
				{
					flag4 = forceHit || !RollSecondaryEvasion(target, ranged: true);
				}
				else
				{
					flag4 = forceHit || (flag3 && num4 > 0) || num5 >= num6;
					if (flag4 && !forceHit && !flag && flag8 && RollSecondaryEvasion(target, ranged: false))
					{
						flag4 = false;
					}
				}
				flag2 = !flag4 && ((num6 > num8 && num6 <= num8 + 5) || (basicAttack && _random.NextDouble() * 100.0 < RelicConditionalCombatRules.MissGrazeChancePercent(_data, attacker)));
				flag4 = flag4 || flag2;
			}
		}
		if (!flag4)
		{
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, flag3, flag);
		}
		if (basicAttack && SkillBuffRules.BlocksBasicAttack(target))
		{
			_events.Add(CombatEvent.LogLine("大地屏障 抵擋了 " + attacker.Disp + " 的攻擊！"));
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, flag3, flag);
		}
		double num9 = (flag ? attacker.D.RangedCritical : attacker.D.MeleeCritical) + CombatModifierRules.PhysicalCriticalRateBonus(attacker);
		bool flag9 = forceCritical || (!flag2 && _random.NextDouble() * 100.0 < num9);
		int num10;
		int num11;
		if (attackDiceCountOverride.HasValue || attackDiceSidesOverride.HasValue)
		{
			num10 = Math.Max(1, attackDiceCountOverride ?? 1);
			num11 = Math.Max(1, attackDiceSidesOverride ?? 1);
		}
		else if (attacker.UsesMonsterTemplate && !IsPlayerTypeCombatant(attacker))
		{
			num10 = Math.Max(1, attacker.D.AttackDiceSmall);
			num11 = Math.Max(1, attacker.D.AttackDiceLarge);
		}
		else
		{
			num10 = 1;
			num11 = Math.Max(1, (attacker.Kind == CombatantKind.Pet && IsPlayerTypeCombatant(target)) ? attacker.Level : ((target.Size == "L") ? attacker.D.AttackDiceLarge : attacker.D.AttackDiceSmall));
		}
		double num12 = (double)((flag3 || BehaviorBuffRules.MaximizesWeaponRoll(attacker, flag)) ? Math.Max(1, num10 * num11) : _random.Roll(num10, num11)) + (baseDamageOverride ?? (flag ? attacker.D.RangedDamage : attacker.D.MeleeDamage)) + item2 + (basicAttack ? RelicConditionalCombatRules.HasteStrikeBonus(_data, attacker) : 0.0) - StatusRules.PhysicalDamageFlatPenalty(attacker);
		double num13 = (flag9 ? (1.0 + (flag ? attacker.D.RangedCriticalDamage : (attacker.D.MeleeCriticalDamage + RelicConditionalCombatRules.PhysicalCriticalDamageBonus(_data, attacker, flag))) / 100.0) : 1.0);
		double num14 = target.D.DamageReduction + (double)CombatModifierRules.ArmorBodyReduction(target) + PetRandomPhysicalReduction(target) + RollPlayerAcDefense(target) + MagicDollRules.DamageReductionAdjustment(target);
		double num15 = Math.Max(1.0, Math.Floor(num12 * num13) + attacker.D.ExtraDamage + item3 - num14);
		if (flag3)
		{
			num15 = Math.Max(1.0, Math.Floor(num15 * RelicConditionalCombatRules.HeavyDamageMultiplier(_data, attacker)) + RelicConditionalCombatRules.HeavyBonusDamage(_data, attacker));
		}
		if (flag2)
		{
			num15 = Math.Max(1.0, Math.Floor(num15 * RelicConditionalCombatRules.GrazeDamageMultiplier(_data, attacker)));
		}
		num15 = Math.Max(1.0, num15 + (double)RollWeaponCounterDamage(attacker, target));
		num15 = Math.Max(1.0, num15 + (double)L1jAttrEnchantRules.BonusDamage(_data, attacker, target));
		num15 = Math.Max(1.0, num15 + extraDamage);
		if (basicAttack)
		{
			num15 = Math.Max(1.0, Math.Floor(num15 * BehaviorBuffRules.BasicAttackDamageMultiplier(_data, attacker, _random)));
			num15 += BehaviorBuffRules.ConsumeFlameSlashBonus(attacker, flag);
			if (flag)
			{
				num15 += MagicDollRules.BowDamageAdjustment(attacker);
			}
			else
			{
				num15 += MagicDollRules.MeleeDamageAdjustment(attacker);
			}
			num15 = Math.Max(1.0, Math.Floor(num15 * WarriorPassiveRules.BerserkDamageMultiplierFor(attacker, flag, _random)));
		}
		num15 = Math.Max(1.0, Math.Floor(num15 * TeamPreciseTargetDamageMultiplier(attacker)));
		if (basicAttack)
		{
			num15 = Math.Max(1.0, Math.Floor(num15 * StatusRules.BasicAttackIncomingDamageMultiplier(target)));
		}
		num15 = Math.Max(1.0, Math.Floor(num15 * StatusRules.OutgoingPhysicalDamageMultiplier(attacker)));
		num15 = Math.Max(1.0, Math.Floor(num15 * Math.Max(0.0, damageMultiplier)));
		num15 = Math.Max(1.0, Math.Floor(num15 * SkillBuffRules.IncomingDamageMultiplier(_data, target)));
		if (basicAttack && !flag2 && RelicConditionalCombatRules.BasicAttackDoubleStrikeChancePercent(_data, attacker) > 0.0)
		{
			num15 = RelicConditionalCombatRules.ApplyBasicAttackDoubleStrike(_data, attacker, num15, _random.NextDouble() * 100.0);
		}
		if (basicAttack && ((attacker.Kind == CombatantKind.Pet && IsPlayerTypeCombatant(target)) || (IsPlayerTypeCombatant(attacker) && target.Kind == CombatantKind.Pet)))
		{
			num15 = Math.Max(1.0, Math.Floor(num15 / 8.0));
		}
		return new PhysicalHitResult(Hit: true, num15, flag9, flag3, flag);
	}

	private double TeamPreciseTargetDamageMultiplier(Combatant source)
	{
		Combatant combatant = (from candidate in _combatants
			where candidate.IsAlive && !IsEnemy(source, candidate) && candidate.Buffs.GetValueOrDefault("sk_royal_precise") > 0.0
			orderby (candidate.Kind != CombatantKind.Player) ? 1 : 0, candidate.BornSeq
			select candidate).FirstOrDefault();
		if (combatant != null)
		{
			return CombatModifierRules.PreciseTargetDamageMultiplier(combatant);
		}
		return 1.0;
	}

	private bool TryDarkStealthEvade(Combatant target)
	{
		if (target.Kind == CombatantKind.Mob)
		{
			return false;
		}
		if (!DarkStealthRules.IsActive(target))
		{
			return false;
		}
		RemoveBuff(target, "sk_dark_stealth");
		ApplyBuff(target, "_dark_stealth_cooldown", 5.0);
		return true;
	}

	private static bool IsPlayerTypeCombatant(Combatant combatant)
	{
		return HostilePlayerRules.UsesPlayerCombatRules(combatant);
	}

	private static bool RangedEvasionStageApplies(Combatant attacker, Combatant target)
	{
		if (!IsPlayerTypeCombatant(target))
		{
			return false;
		}
		if (IsPlayerTypeCombatant(attacker))
		{
			return attacker.D.UsesRangedAttack;
		}
		if (attacker.D.UsesRangedAttack)
		{
			return attacker.Pos.DistanceTo(target.Pos) >= 96.0;
		}
		return false;
	}

	private bool RollSecondaryEvasion(Combatant target, bool ranged)
	{
		if (!ranged)
		{
			if (RelicConditionalCombatRules.CannotEvade(_data, target))
			{
				return false;
			}
			double meleeEvasion = target.D.MeleeEvasion;
			if (meleeEvasion <= 0.0)
			{
				return false;
			}
			return _random.Roll(1, 100) <= (int)Math.Floor(meleeEvasion);
		}
		double val = (target.HasStatus("preciseshot") ? 0.0 : (target.D.EvasionRating + WarriorPassiveRules.TitanBulletEvasionRating(target)));
		if (_random.Roll(1, 100) <= (int)Math.Floor(Math.Max(0.0, val)))
		{
			return !RelicConditionalCombatRules.CannotEvade(_data, target);
		}
		return false;
	}

	private void ApplyMagicWeaponAttack(Combatant attacker, Combatant target)
	{
		ApplyCommittedMagicWeaponHit(attacker, target, RollMagicWeaponAttackDamage(attacker, target));
	}

	private double RollMagicWeaponAttackDamage(Combatant attacker, Combatant target)
	{
		JsonObject jsonObject = _data?.Item(attacker.MainWeaponId);
		if (jsonObject == null)
		{
			return 0.0;
		}
		int val = ((target.Size == "L") ? attacker.D.AttackDiceLarge : attacker.D.AttackDiceSmall);
		double rolled = _random.Roll(1, Math.Max(1, val));
		double num = CombatMath.MagicDamageCoefficient(spellTier: CombatCurveMath.WeaponMagicTier(CombatSkill.ReadDouble(jsonObject, "gachaWeight"), CombatSkill.ReadBool(jsonObject, "legend"), CombatSkill.ReadBool(jsonObject, "relic")), intelligenceSpellPower: attacker.D.IntelligenceSpellPower, itemSpellPower: attacker.D.ItemSpellPower, attributeDefense: AttributeDefense(target, attacker.AttackElement));
		double num2 = CombatMath.MagicBaseDamage(rolled, attacker.D.ExtraDamage, attacker.D.MagicDamage) * num;
		double num3 = CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target));
		double num4 = Math.Max(1.0, Math.Floor(num2 * num3));
		num4 = Math.Max(1.0, num4 + (double)RollWeaponCounterDamage(attacker, target));
		num4 = Math.Max(1.0, num4 + (double)L1jAttrEnchantRules.BonusDamage(_data, attacker, target));
		num4 = Math.Max(1.0, Math.Floor(num4 * (1.0 + (double)attacker.Level / 50.0)));
		return Math.Max(1.0, Math.Floor(num4 * TeamPreciseTargetDamageMultiplier(attacker)));
	}

	private void ApplyCommittedMagicWeaponHit(Combatant attacker, Combatant target, double damage, DirectDamageDelivery delivery = DirectDamageDelivery.BasicAttack)
	{
		if (!target.IsAlive || damage <= 0.0)
		{
			return;
		}
		bool blocked;
		double num = ApplyDirectDamage(attacker, target, damage, DamageType.Magic, delivery, out blocked, critical: false, attacker.AttackElement);
		if (blocked)
		{
			return;
		}
		ConsumeMagicResistanceReduction(target, num);
		if (target.Dead)
		{
			ResolveDeath(target, attacker);
		}
		if (num > 0.0)
		{
			if (target.IsAlive)
			{
				TryApplyRelicElementExposure(attacker, target);
			}
			TryApplyManaDrain(attacker, target);
			TryApplyRelicAttackProcs(attacker, target, attackHit: true);
		}
	}

	internal void TryApplyDarkPoison(Combatant attacker, Combatant target, double hitDamage)
	{
		if (!(attacker.Buffs.GetValueOrDefault("sk_dark_poison") <= 0.0) && !(_random.NextDouble() >= 0.5))
		{
			double num = Math.Max(1.0, Math.Floor(hitDamage * 0.6 * RelicConditionalCombatRules.PoisonDamageMultiplier(_data, attacker)));
			if (!target.PeriodicEffects.TryGetValue("poison", out PeriodicEffect value) || !(value.Damage >= num))
			{
				TryApplyStatusCore(target, "poison", 50, new PeriodicEffect
				{
					TickEvery = 10,
					TicksUntilNext = 10,
					Damage = num,
					BonusTrueDamage = 0.0,
					DamageType = DamageType.Dot,
					Element = "none",
					Source = attacker
				});
			}
		}
	}

	private void ApplyThrowAxeBleed(Combatant attacker, Combatant target, double hitDamage)
	{
		if (_data != null && !RelicConditionalCombatRules.WeaponPreventsBleed(_data, attacker))
		{
			while (target.Bleeds.Count >= 5)
			{
				target.Bleeds.RemoveAt(0);
			}
			target.Bleeds.Add(new PeriodicEffect
			{
				TickEvery = 10,
				TicksUntilNext = 10,
				TicksRemaining = 80,
				Damage = Math.Max(1.0, Math.Floor(hitDamage * 0.2)),
				DamageType = DamageType.Dot,
				Element = "none",
				Source = attacker
			});
		}
	}

	private void ResolveDeath(Combatant dead, Combatant? killer)
	{
		dead.Hp = 0.0;
		dead.Dead = true;
		if (TryTransformDefeatedMob(dead) || !_resolvedDeaths.Add(dead))
		{
			return;
		}
		SatietyRules.ResetOnDeath(dead);
		_events.Add(CombatEvent.Death(dead, killer));
		ReleasePolymorphOnDeath(dead);
		TryApplyNecromancyOnDefeat(dead, killer);
		if (dead.Kind == CombatantKind.Mob)
		{
			_corpseExpiry[dead] = CurrentTimeSeconds + 1.0;
		}
		else if (dead.Kind == CombatantKind.Pet)
		{
			_petReviveReadyAt[dead] = CurrentTimeSeconds + 0.0;
			if (_petInstances.TryGetValue(dead, out PetInstance value))
			{
				value.Hp = 0.0;
				value.Experience = dead.Experience;
				value.Downed = true;
				value.Food = 20;
				value.CommandStatus = PetCommandStatus.Stay;
				ClearPetHate(dead);
			}
		}
		else if (dead.Kind == CombatantKind.Summon)
		{
			Combatant key = _activeDolls.FirstOrDefault<KeyValuePair<Combatant, ActiveDollRuntime>>((KeyValuePair<Combatant, ActiveDollRuntime> pair) => pair.Value.Follower == dead).Key;
			if (key != null)
			{
				RecallMagicDoll(key);
				return;
			}
			_summonExpiresAt.Remove(dead);
			_corpseExpiry[dead] = CurrentTimeSeconds + 1.0;
		}
		if (killer == null || dead.Kind != CombatantKind.Mob || killer.Kind == CombatantKind.Mob)
		{
			return;
		}
		Combatant combatant = ResolveRewardOwner(killer);
		int num = PartyRewardRules.ActiveMemberCount(_combatants);
		AwardMainAlignment(dead);
		double num2 = ContestedRewardMultiplier(dead, num);
		if (dead.ExperienceReward > 0.0)
		{
			AwardMainExperience(dead, dead.ExperienceReward);
		}
		if (num2 >= 1.0 && dead.GoldMax >= dead.GoldMin && dead.GoldMax > 0)
		{
			double num3 = Math.Clamp(dead.GoldChance * Math.Max(0.0, dead.DropMultiplier) * _mapDropRate * GameRateConfig.GlobalGoldChanceRate, 0.0, 1.0);
			if (_random.NextDouble() < num3)
			{
				int num4 = dead.GoldMin + (int)Math.Floor(_random.NextDouble() * (double)(dead.GoldMax - dead.GoldMin + 1));
				if (num4 > 0)
				{
					long totalGold = (long)Math.Ceiling(EquipmentAffixRules.ScaleMonsterGoldAmount(PartyRewardRules.ScaleGold(num4, num), combatant) * GameRateConfig.GlobalGoldAmountRate);
					IReadOnlyList<Combatant> partyMembers = PartyRewardRules.ExperienceRecipients(_combatants);
					if (partyMembers.Count > 1)
					{
						long goldPerMember = Math.Max(1L, totalGold / partyMembers.Count);
						dead.LastGoldShare = goldPerMember;
						foreach (Combatant member in partyMembers)
						{
							CombatWallet.Add(member, goldPerMember);
							_events.Add(CombatEvent.GoldGain(member, goldPerMember));
						}
					}
					else
					{
						dead.LastGoldShare = totalGold;
						CombatWallet.Add(combatant, totalGold);
						_events.Add(CombatEvent.GoldGain(combatant, totalGold));
					}
				}
			}
		}
		EmitDrops(dead, combatant, num, num2);
		RegisterMonsterKill(dead, combatant);
	}

	public void KillMobExternal(Combatant mob, Combatant? killer)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		mob.Hp = 0.0;
		mob.Dead = true;
		ResolveDeath(mob, killer);
	}

	public bool IsStepping(Combatant c)
	{
		return c != null && _isometricSteps.ContainsKey(c);
	}

	public void SetManualTarget(Combatant attacker, Combatant? target)
	{
		if (target == null || !target.IsAlive)
		{
			_manualAttackTargets.Remove(attacker);
		}
		else
		{
			_manualAttackTargets[attacker] = target;
		}
	}

	public Combatant? GetManualTarget(Combatant attacker)
	{
		if (_manualAttackTargets.TryGetValue(attacker, out var target) && target.IsAlive && _combatants.Contains(target))
		{
			return target;
		}
		_manualAttackTargets.Remove(attacker);
		return null;
	}

	private bool CanReviveAlly(Combatant ally)
	{
		if (ally.Kind == CombatantKind.Ally && ally.Dead)
		{
			return _combatants.Contains(ally);
		}
		return false;
	}

	private void ReviveAllyCore(Combatant ally, double healthRatio)
	{
		ClearTransientConditions(ally);
		ally.Dead = false;
		ally.Hp = Math.Max(1.0, Math.Floor(ally.MaxHp * Math.Clamp(healthRatio, 0.0, 1.0)));
		ally.Mp = ally.MaxMp;
		ally.AttackCd = 0.0;
		ally.OffhandCd = 0.0;
		ally.CastCd = 0.0;
		ally.HitstunUntil = 0;
		ally.MoveTarget = null;
		ally.VelX = 0.0;
		ally.VelY = 0.0;
		_resolvedDeaths.Remove(ally);
		_healthRegenElapsed[ally] = 0.0;
		_manaRegenElapsed[ally] = 0.0;
		_events.Add(CombatEvent.Heal(ally, ally, ally.Hp));
	}

	private void ClearTransientConditions(Combatant ally)
	{
		string[] array = ally.Statuses.Keys.ToArray();
		foreach (string kind in array)
		{
			_events.Add(CombatEvent.StatusRemove(ally, kind));
		}
		ally.Statuses.Clear();
		ally.PeriodicEffects.Clear();
		ally.Bleeds.Clear();
	}

	private Combatant? PartyLeader()
	{
		return (from actor in _combatants
			where actor.Kind == CombatantKind.Player
			orderby actor.BornSeq, _combatants.IndexOf(actor)
			select actor).FirstOrDefault();
	}

	private void RemoveExpiredCorpses()
	{
		KeyValuePair<Combatant, double>[] array = _corpseExpiry.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<Combatant, double> keyValuePair = array[i];
			var (combatant2, num2) = keyValuePair;
			if (!(CurrentTimeSeconds + 1E-09 < num2))
			{
				Remove(combatant2);
			}
		}
	}

	private Combatant ResolveRewardOwner(Combatant killer)
	{
		if (killer.Kind == CombatantKind.Player)
		{
			return killer;
		}
		if (killer.Kind == CombatantKind.Summon && _summonOwners.TryGetValue(killer, out Combatant value) && value.IsAlive)
		{
			return value;
		}
		if (killer.Kind == CombatantKind.Pet && _petOwners.TryGetValue(killer, out Combatant value2) && value2.IsAlive)
		{
			return value2;
		}
		return (from candidate in _combatants
			where candidate.Kind == CombatantKind.Player && candidate.IsAlive && !IsEnemy(killer, candidate)
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).FirstOrDefault() ?? killer;
	}

	private void AdvancePeriodicEffects(Combatant target)
	{
		KeyValuePair<string, PeriodicEffect>[] array = target.PeriodicEffects.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<string, PeriodicEffect> keyValuePair = array[i];
			var (text2, periodicEffect2) = keyValuePair;
			if (!target.IsAlive || !target.HasStatus(text2))
			{
				target.PeriodicEffects.Remove(text2);
				continue;
			}
			periodicEffect2.TicksUntilNext--;
			if (periodicEffect2.TicksUntilNext > 0)
			{
				continue;
			}
			periodicEffect2.TicksUntilNext = Math.Max(1, periodicEffect2.TickEvery);
			if (periodicEffect2.Damage <= 0.0)
			{
				continue;
			}
			double num = Math.Max(1.0, Math.Floor(periodicEffect2.Damage * StatusRules.PeriodicDamageMultiplier(target, text2)));
			double num2 = (string.Equals(text2, "poison", StringComparison.Ordinal) ? RelicConditionalCombatRules.PoisonHealingMultiplier(_data, target) : 0.0);
			if (num2 > 0.0)
			{
				double num3 = target.Heal(Math.Max(1.0, Math.Floor(num * num2)));
				if (num3 > 0.0)
				{
					_events.Add(CombatEvent.Heal(target, target, num3));
				}
				continue;
			}
			double num4 = ((periodicEffect2.DamageType == DamageType.True) ? target.ApplyDamage(num) : ApplyDamageWithStatusModifiers(target, num, periodicEffect2.Source));
			if (num4 <= 0.0)
			{
				continue;
			}
			_events.Add(new CombatEvent(CombatEventKind.Damage, periodicEffect2.Source, target, num4, crit: false, DamageType.Dot, periodicEffect2.Element));
			if (target.Dead)
			{
				ResolveDeath(target, periodicEffect2.Source);
			}
			if (!(periodicEffect2.BonusTrueDamage > 0.0) || !target.IsAlive)
			{
				continue;
			}
			double num5 = target.ApplyDamage(periodicEffect2.BonusTrueDamage);
			if (num5 > 0.0)
			{
				_events.Add(new CombatEvent(CombatEventKind.Damage, periodicEffect2.Source, target, num5, crit: false, DamageType.Dot, periodicEffect2.Element));
				if (target.Dead)
				{
					ResolveDeath(target, periodicEffect2.Source);
				}
			}
		}
		AdvanceBleeds(target);
	}

	private void AdvanceBleeds(Combatant target)
	{
		if (!target.IsAlive || target.Bleeds.Count == 0)
		{
			return;
		}
		double num = 0.0;
		double num2 = 0.0;
		Combatant combatant = null;
		Combatant combatant2 = null;
		for (int num3 = target.Bleeds.Count - 1; num3 >= 0; num3--)
		{
			PeriodicEffect periodicEffect = target.Bleeds[num3];
			periodicEffect.TicksRemaining--;
			periodicEffect.TicksUntilNext--;
			if (periodicEffect.TicksUntilNext <= 0)
			{
				periodicEffect.TicksUntilNext = Math.Max(1, periodicEffect.TickEvery);
				if (periodicEffect.DamageType == DamageType.True)
				{
					num2 += periodicEffect.Damage;
					combatant2 = periodicEffect.Source ?? combatant2;
				}
				else
				{
					num += periodicEffect.Damage;
					combatant = periodicEffect.Source ?? combatant;
				}
			}
			if (periodicEffect.TicksRemaining <= 0)
			{
				target.Bleeds.RemoveAt(num3);
			}
		}
		if (num > 0.0)
		{
			double amount = ApplyDamageWithStatusModifiers(target, Math.Max(1.0, num), combatant);
			_events.Add(new CombatEvent(CombatEventKind.Damage, combatant, target, amount, crit: false, DamageType.Dot));
			if (target.Dead)
			{
				ResolveDeath(target, combatant);
			}
		}
		if (!(num2 > 0.0) || !target.IsAlive)
		{
			return;
		}
		double num4 = ApplyDamageWithStatusModifiers(target, Math.Max(1.0, num2), combatant2, 0.0, bypassReduction: true);
		if (num4 > 0.0)
		{
			_events.Add(new CombatEvent(CombatEventKind.Damage, combatant2, target, num4, crit: false, DamageType.Dot));
			if (target.Dead)
			{
				ResolveDeath(target, combatant2);
			}
		}
	}

	private void ApplyPhysicalSkill(Combatant caster, Combatant target, CombatSkill skill)
	{
		if (skill.Slaughter)
		{
			for (int i = 0; i < skill.Hits; i++)
			{
				Combatant combatant = (target.IsAlive ? target : null);
				if (combatant != null)
				{
					double damageMultiplier = SkillDamageMasteryMultiplier(caster, skill);
					PerformPhysicalHit(caster, combatant, null, forceHeavy: false, forceCritical: false, 0.0, 0.0, forceHit: false, basicAttack: false, damageMultiplier);
					continue;
				}
				break;
			}
			return;
		}
		if (skill.WeaponDamage && skill.MagicScale)
		{
			if (skill.InstantKill == null || !TryInstantKill(caster, target, skill, skill.InstantKill))
			{
				ApplyMagicScaledWeaponSkill(caster, target, skill);
				if (target.IsAlive && skill.Status != null)
				{
					TryApplyStatus(caster, target, skill);
				}
			}
			return;
		}
		for (int j = 0; j < skill.Hits; j++)
		{
			if (!target.IsAlive)
			{
				break;
			}
			if (skill.Ranged)
			{
				_events.Add(CombatEvent.Projectile(caster, target, skill.Id, j));
			}
			double num = RelicConditionalCombatRules.FullHealthTripleArrowMultiplier(_data, caster, target, skill.Id, j);
			PhysicalHitResult physicalHitResult = PerformPhysicalHit(caster, target, skill.Ranged ? new bool?(true) : ((bool?)null), forceHeavy: false, forceCritical: false, skill.SkillAddDamage, 0.0, forceHit: false, basicAttack: false, num * SkillDamageMasteryMultiplier(caster, skill));
			if (physicalHitResult.Hit && !(physicalHitResult.Damage <= 0.0) && target.IsAlive)
			{
				if (skill.StunChance > 0.0 && _random.NextDouble() < skill.StunChance)
				{
					TryApplyNamedStatus(caster, target, "stun", 60);
				}
				if (target.IsAlive && skill.Status != null)
				{
					TryApplyStatus(caster, target, skill);
				}
				if (target.IsAlive && skill.InstantKill != null)
				{
					TryInstantKill(caster, target, skill, skill.InstantKill);
				}
			}
		}
	}

	private void ApplyMagicScaledWeaponSkill(Combatant caster, Combatant target, CombatSkill skill)
	{
		bool usesRangedAttack = caster.D.UsesRangedAttack;
		int val = ((target.Size == "L") ? caster.D.AttackDiceLarge : caster.D.AttackDiceSmall);
		double rolled = (double)_random.Roll(1, Math.Max(1, val)) + (usesRangedAttack ? caster.D.RangedDamage : caster.D.MeleeDamage);
		double num = CombatMath.MagicDamageCoefficient(caster.D.IntelligenceSpellPower, caster.D.ItemSpellPower, AttributeDefense(target, caster.AttackElement), skill.Tier);
		double num2 = CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target));
		double d = CombatMath.MagicBaseDamage(rolled, 0.0, caster.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(caster)) * num * num2;
		double num3 = Math.Max(1.0, Math.Floor(d) + skill.FlatBonus);
		num3 = Math.Max(1.0, Math.Floor(num3 * SkillDamageMasteryMultiplier(caster, skill)));
		num3 = Math.Max(1.0, num3 + (double)RollWeaponCounterDamage(caster, target));
		num3 = Math.Max(1.0, Math.Floor(num3 * TeamPreciseTargetDamageMultiplier(caster)));
		DamageType damageType = (usesRangedAttack ? DamageType.Ranged : DamageType.Melee);
		bool blocked;
		double appliedDamage = ApplyDirectDamage(caster, target, num3, damageType, DirectDamageDelivery.ActiveSkill, out blocked, critical: false, caster.AttackElement);
		if (!blocked)
		{
			ConsumeMagicResistanceReduction(target, appliedDamage, damageType);
			if (target.Dead)
			{
				ResolveDeath(target, caster);
			}
		}
	}

	private bool TryCastDarkCritical(Combatant caster, Combatant target, CombatSkill skill)
	{
		if (caster.Mp <= 0.0 || caster.MaxMp <= 0.0 || caster.Hp <= caster.MaxHp * 0.5)
		{
			return false;
		}
		double mp = caster.Mp;
		double num = mp / caster.MaxMp * 10.0;
		caster.Hp = Math.Max(1.0, Math.Floor(caster.Hp - caster.MaxHp * 0.5));
		caster.Mp = 0.0;
		_events.Add(CombatEvent.MpChange(caster, 0.0 - mp));
		caster.CastCd = NextCastCooldownSeconds(caster, support: false);
		_events.Add(CombatEvent.Cast(caster, skill.Id, target));
		PhysicalHitResult physicalHitResult = RollPhysicalHit(caster, target, null, forceHeavy: true, forceCritical: true, 0.0);
		double damage = Math.Max(1.0, Math.Floor(physicalHitResult.Damage * num * SkillDamageMasteryMultiplier(caster, skill)));
		DamageType damageType = (physicalHitResult.Ranged ? DamageType.Ranged : DamageType.Melee);
		ApplyDirectDamage(caster, target, damage, damageType, DirectDamageDelivery.ActiveSkill, out var blocked, critical: true, caster.AttackElement);
		if (!blocked && target.Dead)
		{
			ResolveDeath(target, caster);
		}
		return true;
	}

	private void ApplyRoarDamage(Combatant caster, IReadOnlyList<Combatant> targets)
	{
		double damage = (double)(50 + Math.Max(0, caster.Level - 30)) * SkillDamageMasteryMultiplier(caster, "sk_warrior_roar");
		foreach (Combatant target in targets)
		{
			if (target.IsAlive)
			{
				bool blocked;
				double num = ApplyDirectDamage(caster, target, damage, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked);
				if (!blocked && !(num <= 0.0) && target.Dead)
				{
					ResolveDeath(target, caster);
				}
			}
		}
	}

	private static int MpDrainSpend(Combatant caster, CombatSkill skill)
	{
		return Math.Max(1, (int)Math.Floor(caster.MaxMp * skill.MpDamagePercentage));
	}

	private double ApplyMpDrainSkillDamage(Combatant caster, Combatant target, CombatSkill skill, bool automatic)
	{
		double num = CombatMath.MagicDamageCoefficient(caster.D.IntelligenceSpellPower, caster.D.ItemSpellPower, AttributeDefense(target, skill.Element), skill.Tier);
		double num2 = CombatMath.MagicBaseDamage(MpDrainSpend(caster, skill), 0.0, caster.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(caster)) * num * (RelicConditionalCombatRules.IgnoresSpellMagicResistance(_data, caster) ? 1.0 : CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target)));
		double num3 = Math.Max(1.0, Math.Floor(num2 * SkillDamageMasteryMultiplier(caster, skill)));
		if (automatic)
		{
			num3 = Math.Max(1.0, Math.Floor(num3 * RelicConditionalCombatRules.AutoCastDamageMultiplier(_data, caster)));
		}
		num3 = Math.Max(1.0, Math.Floor(num3 * TeamPreciseTargetDamageMultiplier(caster)));
		bool blocked;
		double num4 = ApplyDirectDamage(caster, target, num3, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked, critical: false, skill.Element);
		if (blocked || num4 <= 0.0)
		{
			return 0.0;
		}
		ConsumeMagicResistanceReduction(target, num4);
		if (target.Dead)
		{
			ResolveDeath(target, caster);
		}
		return num4;
	}

	private double ApplyMagicSkillDamage(Combatant caster, Combatant target, CombatSkill skill, bool automatic)
	{
		if (skill.MpDamagePercentage > 0.0)
		{
			double num = ApplyMpDrainSkillDamage(caster, target, skill, automatic);
			if (num > 0.0)
			{
				return num;
			}
		}
		if (L1jSkillHandover.UsesL1jMagicDamage(skill))
		{
			bool critical;
			double total = L1jMagicSkillCoreDamage(caster, target, skill, out critical);
			return FinishMagicSkillDamage(caster, target, skill, automatic, total, critical);
		}
		if (skill.DamageDice.Count == 0)
		{
			return 0.0;
		}
		bool flag = _random.NextDouble() * 100.0 < caster.D.MagicCritical;
		double num2 = CombatMath.MagicDamageCoefficient(caster.D.IntelligenceSpellPower, caster.D.ItemSpellPower, AttributeDefense(target, skill.Element), skill.Tier);
		double num3 = (flag ? (1.0 + caster.D.MagicCriticalDamage / 100.0) : 1.0);
		double num4 = (RelicConditionalCombatRules.IgnoresSpellMagicResistance(_data, caster) ? 1.0 : CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target)));
		double num5 = 0.0;
		for (int i = 0; i < skill.DamageDice.Count; i++)
		{
			DiceTerm diceTerm = skill.DamageDice[i];
			bool flag2 = i == skill.DamageDice.Count - 1;
			double num6 = CombatMath.MagicBaseDamage(_random.Roll(diceTerm.Count, diceTerm.Sides), flag2 ? skill.DamageBase : 0.0, caster.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(caster)) * num2 * num3;
			double val = Math.Max(1.0, Math.Floor(num6 * num4));
			num5 += Math.Max(1.0, val);
		}
		return FinishMagicSkillDamage(caster, target, skill, automatic, num5, flag);
	}

	private double FinishMagicSkillDamage(Combatant caster, Combatant target, CombatSkill skill, bool automatic, double total, bool critical)
	{
		total = Math.Max(1.0, total + (double)RollElementCounterDamage(skill.Element, target));
		total = Math.Max(1.0, Math.Floor(total * SkillDamageMasteryMultiplier(caster, skill)));
		if (automatic)
		{
			total = Math.Max(1.0, Math.Floor(total * RelicConditionalCombatRules.AutoCastDamageMultiplier(_data, caster)));
		}
		total = Math.Max(1.0, Math.Floor(total * TeamPreciseTargetDamageMultiplier(caster)));
		bool blocked;
		double num = ApplyDirectDamage(caster, target, total, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked, critical, skill.Element);
		if (blocked || num <= 0.0)
		{
			return 0.0;
		}
		ConsumeMagicResistanceReduction(target, num);
		if (target.Dead)
		{
			ResolveDeath(target, caster);
		}
		return num;
	}

	private void ApplyAutoCastBacklash(Combatant caster, int spentMp)
	{
		if (spentMp > 0 && !(caster.Hp <= 200.0) && RelicConditionalCombatRules.HasAutoCastBacklash(_data, caster))
		{
			double num = Math.Min(spentMp, caster.Hp - 1.0);
			if (!(num <= 0.0))
			{
				caster.Hp -= num;
				_events.Add(CombatEvent.Damage(caster, caster, num, DamageType.True));
				_events.Add(CombatEvent.LogLine($"【血祭】短刀吸取了你的血肉，受到 {num:0} 點固定傷害。"));
			}
		}
	}

	private static string HealCooldownKey(string skillId)
	{
		return "_heal_cd:" + skillId;
	}

	private double HealAmountFor(Combatant caster, Combatant target, CombatSkill skill)
	{
		if (skill.FullRestore)
		{
			return Math.Max(0.0, target.MaxHp - target.Hp);
		}
		if (L1jSkillHandover.UsesL1jHealing(skill))
		{
			double num = Math.Max(1.0, Math.Floor(L1jHealAmount(caster, skill) * SkillRecoveryMasteryMultiplier(caster, skill)));
			if (!skill.IgnoreWaterVital)
			{
				num *= BehaviorBuffRules.ConsumeHealMultiplier(target);
			}
			return num;
		}
		double num2 = skill.HealDice.Sum((DiceTerm dice) => _random.Roll(dice.Count, dice.Sides));
		double num3 = Math.Max(1.0, Math.Floor(num2 + skill.HealBase + caster.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(caster)));
		if (skill.JusticeHeal)
		{
			num3 = Math.Max(1.0, Math.Floor(num3 * CombatCurveMath.JusticeHealMultiplier(caster.Alignment)));
		}
		if (skill.GroupHeal)
		{
			num3 = Math.Max(1.0, Math.Floor(num3 * RelicConditionalCombatRules.GroupHealMultiplier(_data, caster)));
		}
		num3 = Math.Max(1.0, Math.Floor(num3 * SkillRecoveryMasteryMultiplier(caster, skill)));
		if (!skill.IgnoreWaterVital)
		{
			num3 *= BehaviorBuffRules.ConsumeHealMultiplier(target);
		}
		return num3;
	}

	private void ApplyHeal(Combatant caster, Combatant target, CombatSkill skill, bool fullRestore = false)
	{
		if (skill.GroupHeal)
		{
			Combatant[] array = _combatants.ToArray();
			foreach (Combatant combatant in array)
			{
				if (combatant.IsAlive && !IsEnemy(caster, combatant) && !IsNecroSkeleton(combatant))
				{
					double num = combatant.Heal(HealAmountFor(caster, combatant, skill));
					if (num > 0.0)
					{
						_events.Add(CombatEvent.Heal(caster, combatant, num));
					}
				}
			}
		}
		else
		{
			double num2 = target.Heal(fullRestore ? Math.Max(0.0, target.MaxHp - target.Hp) : HealAmountFor(caster, target, skill));
			if (num2 > 0.0)
			{
				_events.Add(CombatEvent.Heal(caster, target, num2));
			}
		}
	}

	private bool TryApplyStatus(Combatant caster, Combatant target, CombatSkill skill)
	{
		StatusEffectSpec status = skill.Status;
		if (StatusRules.IsImmune(target, status.Kind))
		{
			return false;
		}
		if (L1jSkillHandover.UsesL1jProbability(skill))
		{
			if (!L1jProbabilitySucceeds(caster, target, skill))
			{
				return false;
			}
		}
		else
		{
			double num = SkillAbnormalMasteryBonus(caster, skill);
			if (status.FixedChancePercentage.HasValue && _random.NextDouble() * 100.0 >= Math.Min(100.0, status.FixedChancePercentage.Value + num))
			{
				return false;
			}
			if (!status.Force && !AbnormalMagicHit(caster, target, 20, status.HitOffset, status.FixedChancePercentage.HasValue ? 0.0 : num))
			{
				return false;
			}
		}
		PeriodicEffect periodicEffect = null;
		DiceTerm? damageDice = status.DamageDice;
		if (damageDice.HasValue)
		{
			DiceTerm valueOrDefault = damageDice.GetValueOrDefault();
			double num2 = CombatMath.MagicDamageCoefficient(caster.D.IntelligenceSpellPower, caster.D.ItemSpellPower, AttributeDefense(target, skill.Element), skill.Tier);
			double damage = Math.Max(1.0, Math.Floor((double)_random.Roll(valueOrDefault.Count, valueOrDefault.Sides) * num2));
			periodicEffect = new PeriodicEffect
			{
				TickEvery = status.TickEvery,
				TicksUntilNext = status.TickEvery,
				Damage = damage,
				Element = skill.Element,
				Source = caster
			};
		}
		int durationTicks = status.DurationTicks;
		return TryApplyStatusCore(target, status.Kind, durationTicks, periodicEffect, resistanceChecked: true, status.Potency);
	}

	private void TryApplySkillFreeze(Combatant caster, Combatant target, double power)
	{
		if (target.IsAlive && !StatusRules.IsImmune(target, "freeze"))
		{
			double num = Math.Clamp((power - EffectiveMagicResist(target)) / 200.0, 0.0, 1.0);
			if (!(_random.NextDouble() >= num))
			{
				TryApplyStatusCore(target, "freeze", 60, null, resistanceChecked: true);
			}
		}
	}

	private bool TryApplyNamedStatus(Combatant caster, Combatant target, string status, int durationTicks, double hitOffset = 0.0)
	{
		if (StatusRules.IsImmune(target, status))
		{
			return false;
		}
		if (!AbnormalMagicHit(caster, target, 20, hitOffset))
		{
			return false;
		}
		return TryApplyStatusCore(target, status, durationTicks, null, resistanceChecked: true);
	}

	private bool TryApplyStatusCore(Combatant target, string statusKind, int durationTicks, PeriodicEffect? periodicEffect, bool resistanceChecked = false, double potency = 0.0)
	{
		statusKind = StatusRules.NormalizeKind(statusKind);
		if (!target.IsAlive)
		{
			return false;
		}
		int valueOrDefault = target.Statuses.GetValueOrDefault(statusKind);
		bool flag = durationTicks > valueOrDefault;
		PeriodicEffect value;
		bool flag2 = periodicEffect != null && (!target.PeriodicEffects.TryGetValue(statusKind, out value) || periodicEffect.Damage > value.Damage);
		string key = StatusRules.PotencyCounterKey(statusKind);
		bool flag3 = potency > (double)target.Counters.GetValueOrDefault(key);
		if (!flag && !flag2 && !flag3)
		{
			return false;
		}
		if (!resistanceChecked && StatusRules.IsImmune(target, statusKind))
		{
			return false;
		}
		if (flag)
		{
			target.Statuses[statusKind] = durationTicks;
		}
		if (periodicEffect != null && (flag || flag2))
		{
			target.PeriodicEffects[statusKind] = periodicEffect;
		}
		if (flag3)
		{
			target.Counters[key] = (int)Math.Ceiling(potency);
		}
		_events.Add(CombatEvent.StatusAdd(target, statusKind, target.Statuses[statusKind]));
		return true;
	}

	private bool RemoveStatusCore(Combatant target, string statusKind)
	{
		statusKind = StatusRules.NormalizeKind(statusKind);
		if (string.Equals(statusKind, "poison", StringComparison.Ordinal))
		{
			bool result = false;
			{
				foreach (string poisonStatusKind in L1jPoisonAttackRules.PoisonStatusKinds)
				{
					if (target.Statuses.Remove(poisonStatusKind))
					{
						target.PeriodicEffects.Remove(poisonStatusKind);
						target.Counters.Remove(StatusRules.PotencyCounterKey(poisonStatusKind));
						_events.Add(CombatEvent.StatusRemove(target, poisonStatusKind));
						result = true;
					}
				}
				return result;
			}
		}
		bool num = target.Statuses.Remove(statusKind);
		target.PeriodicEffects.Remove(statusKind);
		target.Counters.Remove(StatusRules.PotencyCounterKey(statusKind));
		if (!num)
		{
			return false;
		}
		_events.Add(CombatEvent.StatusRemove(target, statusKind));
		return true;
	}

	private bool TryInstantKill(Combatant caster, Combatant target, CombatSkill skill, InstantKillSpec instantKill)
	{
		if (target.IsBoss || (instantKill.RequiredTag.Length > 0 && !HasTargetTag(target, instantKill.RequiredTag)))
		{
			return false;
		}
		double num = SkillAbnormalMasteryBonus(caster, skill);
		if (!(instantKill.Chance.HasValue ? (_random.NextDouble() < Math.Min(1.0, instantKill.Chance.Value + num / 100.0)) : AbnormalMagicHit(caster, target, instantKill.MaxHitValue, 0.0, num)))
		{
			return false;
		}
		double num2 = target.ApplyDamage(target.Hp);
		if (num2 <= 0.0)
		{
			return false;
		}
		_events.Add(CombatEvent.Damage(caster, target, num2, DamageType.True));
		if (target.Dead)
		{
			ResolveDeath(target, caster);
		}
		return true;
	}

	private bool AbnormalMagicHit(Combatant caster, Combatant target, int maxHitValue = 20, double hitOffset = 0.0, double bonusPercentagePoints = 0.0)
	{
		double value = 10.0 + caster.D.MagicHit + hitOffset - EffectiveMagicResist(target) / 10.0;
		value = Math.Clamp(value, 0.0, Math.Clamp(maxHitValue, 1, 20));
		int num = _random.Roll(1, 20);
		bool flag = num switch
		{
			1 => false, 
			20 => true, 
			_ => value >= (double)num, 
		};
		if (flag || bonusPercentagePoints <= 0.0)
		{
			return flag;
		}
		double num2 = (Math.Clamp((int)Math.Floor(value) - 1, 0, 18) + 1) * 5;
		double num3 = 100.0 - num2;
		if (num3 > 0.0)
		{
			return _random.NextDouble() * 100.0 < Math.Min(100.0, bonusPercentagePoints / num3 * 100.0);
		}
		return false;
	}

	private static double SkillDamageMasteryMultiplier(Combatant caster, CombatSkill skill)
	{
		return 1.0;
	}

	private static double SkillDamageMasteryMultiplier(Combatant caster, string skillId)
	{
		return 1.0;
	}

	private static double SkillRecoveryMasteryMultiplier(Combatant caster, CombatSkill skill)
	{
		return 1.0;
	}

	private static double SkillAbnormalMasteryBonus(Combatant caster, CombatSkill skill)
	{
		return 0.0;
	}

	private static double SkillAbnormalMasteryBonus(Combatant caster, string skillId)
	{
		return 0.0;
	}

	private bool WeaponRequirementMet(Combatant caster, string requirement)
	{
		if (requirement.Length == 0)
		{
			return true;
		}
		JsonObject jsonObject = _data?.Item(caster.MainWeaponId);
		if (jsonObject == null)
		{
			return false;
		}
		if (!(requirement == "w2h"))
		{
			if (requirement == "bow")
			{
				return WeaponCombatProfile.ReadBool(jsonObject, "isBow");
			}
			return false;
		}
		return WeaponCombatProfile.ReadBool(jsonObject, "w2h") && !WeaponCombatProfile.ReadBool(jsonObject, "isBow");
	}

	private bool HasTargetTag(Combatant target, string tag)
	{
		return CounterDamageRules.HasTargetTag(_data, target, tag);
	}

	private double AttributeDefense(Combatant target, string element)
	{
		string text = CombatSkill.NormalizeElement(element);
		return CombatCurveMath.EffectiveResistancePercent(text switch
		{
			"fire" => target.D.ResistFire, 
			"water" => target.D.ResistWater, 
			"wind" => target.D.ResistWind, 
			"earth" => target.D.ResistEarth, 
			_ => 0.0, 
		} - ElementalFallDownRules.ResistancePenalty(target, text)) / 100.0;
	}

	private static double EffectiveMagicResist(Combatant target)
	{
		return StatusRules.EffectiveMagicResistance(target, target.D.MagicResist);
	}

	private double RollPlayerAcDefense(Combatant target)
	{
		CombatantKind kind = target.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		if (!flag || !ClassGrowthRules.IsKnownClass(target.ClassId))
		{
			return 0.0;
		}
		int num = ClassGrowthRules.AcDefenseMaximum(target.ClassId, target.D.ArmorClass);
		return (num > 0) ? (_random.Roll(1, num + 1) - 1) : 0;
	}

	private double ApplyDamageWithStatusModifiers(Combatant target, double damage, Combatant? source = null, double finalFlatPenalty = 0.0, bool bypassReduction = false)
	{
		if (!target.IsAlive || damage <= 0.0 || (source != null && !CanHostileInteract(source, target)))
		{
			return 0.0;
		}
		if (target.TrainingScarecrow)
		{
			return 0.0;
		}
		if (target.CastleWarInvulnerable)
		{
			return 0.0;
		}
		double num = (bypassReduction ? damage : AdjustIncomingDamage(target, damage));
		num = Math.Max(1.0, num - Math.Max(0.0, finalFlatPenalty));
		double num2 = target.ApplyDamage(num);
		if (num2 > 0.0)
		{
			RegisterContestedAttacker(target, source);
		}
		if (num2 > 0.0 && target.HasStatus("sleep"))
		{
			RemoveStatusCore(target, "sleep");
		}
		return num2;
	}

	private void ConsumeMagicResistanceReduction(Combatant target, double appliedDamage, DamageType damageType = DamageType.Magic)
	{
		if (!(appliedDamage <= 0.0))
		{
			if (target.HasStatus("mrhalf"))
			{
				RemoveStatusCore(target, "mrhalf");
			}
			if (damageType == DamageType.Magic && target.HasStatus("energybreak"))
			{
				RemoveStatusCore(target, "energybreak");
			}
		}
	}

	private void EmitDrops(Combatant dead, Combatant recipient, int activePartySize, double contestedMultiplier = 1.0)
	{
		ItemStack[] array = dead.EquippedItems.Values.ToArray();
		foreach (ItemStack itemStack in array)
		{
			_events.Add(CombatEvent.Drop(dead, itemStack.ItemKey, checked((int)itemStack.Quantity), itemStack.Blessing, itemStack.Enhancement, itemStack.IsIdentified, itemStack.ItemLevel, itemStack.Affixes));
		}
		dead.EquippedItems.Clear();
		dead.InventoryStacks.Clear();
		CombatInventory.SyncLegacyView(dead);
		CombatEquipment.SyncLegacyView(dead);
		if (dead.Kind == CombatantKind.Mob && GameRateConfig.GuardianSoulEnabled && (IsGuardianSoulInWorldCheck == null || !IsGuardianSoulInWorldCheck()) && _random.NextDouble() < GameRateConfig.GuardianSoulDropChance)
		{
			_events.Add(CombatEvent.Drop(dead, "item_guardian_soul", 1, ItemBlessing.Normal, 0, itemIdentified: true));
		}
		if (!(_data?.Table("MOB_DROPS") is JsonObject jsonObject) || !(jsonObject[dead.Avatar] is JsonArray jsonArray))
		{
			return;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (!(item is JsonArray { Count: >=2 } jsonArray2))
			{
				continue;
			}
			string text = jsonArray2[0]?.GetValue<string>();
			if (string.IsNullOrWhiteSpace(text) || !TryReadNumber(jsonArray2[1], out var value) || (jsonArray2.Count >= 5 && jsonArray2[4] is JsonObject jsonObject2 && jsonObject2["classId"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value2) && !string.Equals(recipient.ClassId, value2, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			double num = EquipmentAffixRules.ScaleMonsterItemDropChance(PartyRewardRules.ScaleDropChance(value * Math.Max(0.0, dead.DropMultiplier) * _mapDropRate * GameRateConfig.GlobalItemDropRate / 100.0, activePartySize), recipient) * Math.Clamp(contestedMultiplier, 0.0, 1.0);
			if (_random.NextDouble() >= num)
			{
				continue;
			}
			int num2 = 1;
			int num3 = 1;
			ItemBlessing? fixedBlessing = null;
			if (jsonArray2.Count >= 4)
			{
				if (!TryReadNumber(jsonArray2[2], out var value3) || !TryReadNumber(jsonArray2[3], out var value4))
				{
					continue;
				}
				num2 = (int)value3;
				num3 = (int)value4;
				if (num2 < 0 || num3 < num2)
				{
					continue;
				}
			}
			if (jsonArray2.Count >= 5 && jsonArray2[4] is JsonValue jsonValue2 && jsonValue2.TryGetValue<string>(out string value5))
			{
				fixedBlessing = value5 switch
				{
					"blessed" => ItemBlessing.Blessed, 
					"normal" => ItemBlessing.Normal, 
					"cursed" => ItemBlessing.Cursed, 
					_ => null, 
				};
				if (!fixedBlessing.HasValue)
				{
					continue;
				}
			}
			int num4 = num2;
			if (num3 > num2)
			{
				num4 += (int)Math.Floor(_random.NextDouble() * (double)(num3 - num2 + 1));
			}
			if (num4 <= 0)
			{
				continue;
			}
			ItemGainPreview itemGainPreview;
			try
			{
				itemGainPreview = ItemGainRules.Preview(_data, recipient.Key, recipient.Progress.ItemGainAttemptSequence, text, new ItemGainOptions(ItemGainRules.DropSource(dead.IsBoss), fixedBlessing, Blank: false, ForceBlessed: false, RollBeforeForceBlessed: false, dead.Level, EquipmentAffixGradeFor(dead)));
			}
			catch (KeyNotFoundException)
			{
				continue;
			}
			catch (ArgumentNullException)
			{
				continue;
			}
			if (!itemGainPreview.UsesCommittedRoll || recipient.Progress.ItemGainAttemptSequence != long.MaxValue)
			{
				if (itemGainPreview.UsesCommittedRoll)
				{
					recipient.Progress.ItemGainAttemptSequence++;
				}
				int num5 = ((itemGainPreview.ItemLevel <= 0) ? 1 : num4);
				int qty = ((itemGainPreview.ItemLevel > 0) ? 1 : num4);
				for (int j = 0; j < num5; j++)
				{
					_events.Add(CombatEvent.Drop(dead, itemGainPreview.ResolvedItemKey, qty, itemGainPreview.Blessing, itemGainPreview.Enhancement, itemIdentified: false, itemGainPreview.ItemLevel, itemGainPreview.Affixes));
				}
			}
		}
	}

	private static EquipmentAffixDropGrade EquipmentAffixGradeFor(Combatant dead)
	{
		if (!dead.IsBoss)
		{
			if (!dead.Hard)
			{
				return EquipmentAffixDropGrade.Normal;
			}
			return EquipmentAffixDropGrade.Strong;
		}
		return EquipmentAffixDropGrade.Boss;
	}

	public double AttackCycleSeconds(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return AttackIntervalSeconds(combatant);
	}

	public double AttackSpeedRatio(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		double num = AttackIntervalSeconds(combatant);
		double attackInterval = combatant.D.AttackInterval;
		if (!(num > 0.0) || !(attackInterval > 0.0) || !double.IsFinite(attackInterval))
		{
			return 1.0;
		}
		return attackInterval / num;
	}

	private double AttackIntervalSeconds(Combatant combatant)
	{
		double num = CombatModifierRules.EffectiveAttackInterval(combatant, _data);
		if (combatant.UsesMonsterTemplate && !HostilePlayerRules.UsesPlayerCombatRules(combatant) && combatant.Buffs.GetValueOrDefault("mob_self_haste") > 0.0 && _mobHasteIntervals.TryGetValue(combatant, out var value))
		{
			num = Math.Min(num, value);
		}
		if (combatant.HasStatus("slowAtk"))
		{
			num *= 2.0;
		}
		if (combatant.HasStatus("slow"))
		{
			num += 1.0;
		}
		return Math.Max(0.1, num);
	}

	private static double NextCastCooldownSeconds(Combatant caster, bool support)
	{
		return NextIndependentCastCooldownSeconds(caster, caster.CastCd, support);
	}

	private static double NextIndependentCastCooldownSeconds(Combatant caster, double currentSeconds, bool support)
	{
		double num = CombatMath.NextCastCooldown(currentSeconds * 10.0, caster.D.CastLockTicks, null, caster.D.CastLockTicks, support) / 10.0;
		if (caster.HasStatus("windshackle"))
		{
			num *= 1.25;
		}
		if (CombatModifierRules.HasThirdSpeed(caster))
		{
			num /= CombatModifierRules.ResolveThirdHasteCastFactor(caster);
		}
		return num;
	}

	private static bool TryReadNumber(JsonNode? node, out double value)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out value))
		{
			return true;
		}
		if (node is JsonValue jsonValue2 && jsonValue2.TryGetValue<int>(out var value2))
		{
			value = value2;
			return true;
		}
		if (node is JsonValue jsonValue3 && jsonValue3.TryGetValue<long>(out var value3))
		{
			value = value3;
			return true;
		}
		if (node is JsonValue jsonValue4 && jsonValue4.TryGetValue<decimal>(out var value4))
		{
			value = (double)value4;
			return true;
		}
		value = 0.0;
		return false;
	}

	private void AdvanceCubeBuffs()
	{
		if (_data == null)
		{
			return;
		}
		(Combatant, string)[] array = _cubeTicksRemaining.Keys.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			var (combatant, text) = array[i];
			if (!combatant.IsAlive || combatant.Buffs.GetValueOrDefault(text) <= 0.0 || !_combatants.Contains(combatant))
			{
				_cubeTicksRemaining.Remove((combatant, text));
			}
		}
		Combatant[] array2 = _combatants.ToArray();
		foreach (Combatant combatant2 in array2)
		{
			bool flag = !combatant2.IsAlive;
			if (!flag)
			{
				CombatantKind kind = combatant2.Kind;
				bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
				flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(combatant2);
			}
			if (flag)
			{
				continue;
			}
			foreach (string skillId in CubeBuffRules.SkillIds)
			{
				if (combatant2.Buffs.GetValueOrDefault(skillId) <= 0.0)
				{
					continue;
				}
				CubeEffectSpec cubeEffectSpec = CubeBuffRules.Read(_data, skillId);
				if ((object)cubeEffectSpec != null)
				{
					(Combatant, string) key = (combatant2, skillId);
					int intervalTicks = cubeEffectSpec.IntervalTicks;
					int num = Math.Min(intervalTicks, _cubeTicksRemaining.GetValueOrDefault(key, intervalTicks)) - 1;
					if (num > 0)
					{
						_cubeTicksRemaining[key] = num;
						continue;
					}
					_cubeTicksRemaining[key] = intervalTicks;
					ApplyCubeEffect(combatant2, cubeEffectSpec);
				}
			}
		}
	}

	private void ApplyCubeEffect(Combatant caster, CubeEffectSpec effect)
	{
		switch (effect.Kind)
		{
		case CubeEffectKind.DamageAll:
			ApplyCubeDamageToAll(caster, effect);
			break;
		case CubeEffectKind.SlowAll:
		{
			Combatant[] array = LivingCubeEnemies(caster);
			foreach (Combatant target2 in array)
			{
				TryApplyNamedStatus(caster, target2, "slow", effect.StatusDurationTicks);
			}
			break;
		}
		case CubeEffectKind.MagicResistHalf:
		{
			Combatant[] array = LivingCubeEnemies(caster);
			foreach (Combatant target3 in array)
			{
				TryApplyNamedStatus(caster, target3, "mrhalf", effect.StatusDurationTicks);
			}
			break;
		}
		case CubeEffectKind.DamageTargetAndRestoreTeamMp:
		{
			RestoreCubeTeamMp(caster, effect.MpRestore);
			CombatSkill damageSkill = effect.DamageSkill;
			if (damageSkill != null)
			{
				Combatant[] array = LivingCubeEnemies(caster);
				foreach (Combatant target in array)
				{
					ApplyMagicSkillDamage(caster, target, damageSkill, automatic: false);
				}
			}
			break;
		}
		}
	}

	private void ApplyCubeDamageToAll(Combatant caster, CubeEffectSpec effect)
	{
		CombatSkill damageSkill = effect.DamageSkill;
		if (damageSkill != null)
		{
			Combatant[] array = LivingCubeEnemies(caster);
			foreach (Combatant target in array)
			{
				ApplyMagicSkillDamage(caster, target, damageSkill, automatic: false);
			}
		}
	}

	private Combatant[] LivingCubeEnemies(Combatant caster)
	{
		return _combatants.Where((Combatant target) => target.IsAlive && IsEnemy(caster, target) && CanHostileInteract(caster, target) && CombatRangeRules.DiamondDistance(caster.Pos, target.Pos) <= 144.0).ToArray();
	}

	private void RestoreCubeTeamMp(Combatant caster, int amount)
	{
		if (amount <= 0)
		{
			return;
		}
		foreach (Combatant combatant in _combatants)
		{
			bool flag = !combatant.IsAlive || IsEnemy(caster, combatant);
			if (!flag)
			{
				CombatantKind kind = combatant.Kind;
				bool flag2 = ((kind == CombatantKind.Player || (uint)(kind - 2) <= 1u) ? true : false);
				flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(combatant);
			}
			if (!flag)
			{
				double mp = combatant.Mp;
				combatant.RestoreMp(amount);
				double num = combatant.Mp - mp;
				if (num > 0.0)
				{
					_events.Add(CombatEvent.MpChange(combatant, num));
				}
			}
		}
	}

	internal double ApplyDirectDamage(Combatant attacker, Combatant target, double damage, DamageType damageType, DirectDamageDelivery delivery, out bool blocked, bool critical = false, string element = "", bool heavy = false, double finalFlatPenalty = 0.0)
	{
		blocked = false;
		if (!target.IsAlive || damage <= 0.0 || !CanHostileInteract(attacker, target))
		{
			return 0.0;
		}
		if (AbsoluteBarrierRules.IsActive(target) || target.Buffs.GetValueOrDefault("sk_elf_earthshield") > 0.0)
		{
			blocked = true;
			return 0.0;
		}
		if (TryBlockUndeadRelicAttack(attacker, target))
		{
			blocked = true;
			return 0.0;
		}
		if (TryBlockFireRelicDamage(attacker, target, element))
		{
			blocked = true;
			return 0.0;
		}
		damage = WearerElementRules.ApplyIncomingDamage(_data, target, damage, damageType, element);
		damage = Math.Max(0.0, Math.Floor(damage * RelicConditionalCombatRules.IncomingRaceDamageMultiplier(_data, target, attacker)));
		damage = Math.Max(0.0, Math.Floor(damage * RelicConditionalCombatRules.IncomingHeavyDamageMultiplier(_data, target, heavy)));
		damage = ApplyRelicGatedPhysicalReduction(target, damage, damageType, delivery);
		if (damage <= 0.0)
		{
			return 0.0;
		}
		damage = Math.Max(1.0, Math.Floor(damage * PetIncomingDamageMultiplier(target, damageType)));
		damage = Math.Max(1.0, damage - PetHardenDamageReduction(target, damageType));
		if (delivery == DirectDamageDelivery.BasicAttack && attacker.Kind == CombatantKind.Mob && MobTauntTarget(attacker) == target)
		{
			damage = Math.Max(1.0, Math.Floor(damage * 0.9));
		}
		damage = ApplyRelicElementExposureDamage(target, damage, element);
		damage = ApplyMagicDollDamageReduction(target, damage, damageType);
		if (target.HasStatus("guardian_soul") || CombatInventory.Count(target, "item_guardian_soul") > 0)
		{
			double reduction = Math.Clamp(GameRateConfig.GuardianSoulDamageReductionPercent / 100.0, 0.0, 0.99);
			damage = Math.Max(1.0, Math.Floor(damage * (1.0 - reduction)));
		}
		if (damage <= 0.0)
		{
			return 0.0;
		}
		double num = ApplyDamageWithStatusModifiers(target, damage, attacker, finalFlatPenalty);
		if (num <= 0.0)
		{
			return num;
		}
		MarkMobCombatActivity(attacker, target);
		ApplyHitImpact(attacker, target, damageType, heavy);
		AddHate(target, attacker, num);
		RegisterPetDefenseHate(target, attacker);
		_events.Add(CombatEvent.Damage(attacker, target, num, damageType, critical, element));
		TryReflectPain(target, attacker, num, damageType);
		TryReflectDeadlyBody(target, attacker, num, damageType);
		TryReflectTitan(target, attacker, num, damageType);
		if (!target.IsAlive)
		{
			return num;
		}
		TryCastOnHurt(target, attacker, damageType);
		return num;
	}

	private bool TryCastEnergySense(Combatant caster, JsonObject source, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock)
	{
		if (!EnergySenseRules.IsEnergySenseSkill(source) || (!ignoreCastLock && caster.CastCd > 0.0))
		{
			return false;
		}
		double range = CombatRangeRules.ConfiguredCastRange(source) ?? 72.0;
		Combatant combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsEnemy(caster, requestedTarget) && IsWithinRange(caster, requestedTarget, range) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range, requireLineOfSight: true) : null));
		if (combatant == null)
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, "sk_energy_sense", CombatModifierRules.SkillMpCost(caster, source, "sk_energy_sense")) : 0);
		if (caster.Mp < (double)num)
		{
			return false;
		}
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (!ignoreCastLock)
		{
			caster.CastCd = NextCastCooldownSeconds(caster, support: false);
		}
		_events.Add(CombatEvent.Cast(caster, "sk_energy_sense", combatant));
		_events.Add(CombatEvent.LogLine(combatant.Disp + " 是 " + EnergySenseRules.ElementLabel(combatant.Element) + "屬性"));
		return true;
	}

	private void TryApplyWeaponDirectStatus(Combatant attacker, Combatant target)
	{
		if (!target.IsAlive)
		{
			return;
		}
		WeaponDirectStatusProc? weaponDirectStatusProc = EquipmentProcRules.DirectStatus(_data, attacker);
		if (weaponDirectStatusProc.HasValue)
		{
			WeaponDirectStatusProc valueOrDefault = weaponDirectStatusProc.GetValueOrDefault();
			if (!(_random.NextDouble() * 100.0 >= valueOrDefault.ChancePercent))
			{
				TryApplyNamedStatus(attacker, target, valueOrDefault.Kind, valueOrDefault.DurationTicks);
			}
		}
	}

	private void AwardMainAlignment(Combatant defeated)
	{
		if (!_hate.TryGetValue(defeated, out Dictionary<Combatant, HateEntry> value))
		{
			return;
		}
		KeyValuePair<Combatant, int>[] source = (from pair in value
			where pair.Key.IsAlive && _combatants.Contains(pair.Key) && pair.Value.Value > 0
			select new KeyValuePair<Combatant, int>(pair.Key, pair.Value.Value)).ToArray();
		KeyValuePair<Combatant, int>[] array = source.Where((KeyValuePair<Combatant, int> pair) => pair.Key.Kind == CombatantKind.Player || (pair.Key.Kind == CombatantKind.Ally && !pair.Key.UsesMonsterTemplate)).ToArray();
		long num = ((IEnumerable<KeyValuePair<Combatant, int>>)array).Sum((Func<KeyValuePair<Combatant, int>, long>)((KeyValuePair<Combatant, int> pair) => pair.Value));
		if (num <= 0)
		{
			return;
		}
		int monsterLawful = (int)Math.Truncate(Math.Clamp(defeated.Alignment, -32767.0, 32767.0));
		Combatant[] array2 = PartyRewardRules.ExperienceRecipients(_combatants).ToArray();
		Combatant key;
		if (array2.Length <= 1)
		{
			KeyValuePair<Combatant, int>[] array3 = array;
			foreach (KeyValuePair<Combatant, int> keyValuePair in array3)
			{
				keyValuePair.Deconstruct(out key, out var value2);
				Combatant character = key;
				int num3 = value2;
				int distributedLawful = AlignmentRules.DistributedMonsterLawful(monsterLawful, num3, num);
				AlignmentRules.Change(character, AlignmentRules.MonsterKillDelta(distributedLawful));
			}
			return;
		}
		HashSet<Combatant> memberSet = new HashSet<Combatant>(array2);
		foreach (KeyValuePair<Combatant, int> item in array.Where((KeyValuePair<Combatant, int> pair) => !memberSet.Contains(pair.Key)))
		{
			item.Deconstruct(out key, out var num2);
			Combatant character2 = key;
			int num4 = num2;
			int distributedLawful2 = AlignmentRules.DistributedMonsterLawful(monsterLawful, num4, num);
			AlignmentRules.Change(character2, AlignmentRules.MonsterKillDelta(distributedLawful2));
		}
		long num5 = array.Where((KeyValuePair<Combatant, int> pair) => memberSet.Contains(pair.Key)).Sum((Func<KeyValuePair<Combatant, int>, long>)((KeyValuePair<Combatant, int> pair) => pair.Value));
		if (num5 <= 0)
		{
			return;
		}
		int num6 = AlignmentRules.DistributedMonsterLawful(monsterLawful, num5, num);
		double num7 = array2.Sum((Combatant combatant) => (double)combatant.Level * (double)combatant.Level);
		if (num7 <= 0.0)
		{
			return;
		}
		Combatant[] array4 = array2;
		foreach (Combatant member in array4)
		{
			int distributedLawful3 = (int)Math.Truncate((double)num6 * ((double)member.Level * (double)member.Level / num7));
			long num8 = ((IEnumerable<KeyValuePair<Combatant, int>>)source.Where((KeyValuePair<Combatant, int> pair) => PartyMemberOf(pair.Key) == member).ToArray()).Sum((Func<KeyValuePair<Combatant, int>, long>)((KeyValuePair<Combatant, int> pair) => pair.Value));
			bool flag = array.Any((KeyValuePair<Combatant, int> pair) => pair.Key == member);
			if (num8 <= 0 || flag)
			{
				AlignmentRules.Change(member, AlignmentRules.MonsterKillDelta(distributedLawful3));
			}
		}
	}

	private void AwardMainExperience(Combatant defeated, double baseExperience)
	{
		if (_data == null || !double.IsFinite(baseExperience) || baseExperience <= 0.0)
		{
			return;
		}
		int num2 = (int)Math.Min(2147483647.0, Math.Floor(baseExperience));
		Combatant[] array2 = PartyRewardRules.ExperienceRecipients(_combatants).ToArray();
		if (array2.Length > 1)
		{
			AwardMainPartyExperience(defeated, num2, array2);
			return;
		}
		if (!_hate.TryGetValue(defeated, out Dictionary<Combatant, HateEntry> value))
		{
			if (array2.Length == 1)
			{
				defeated.LastExpShare = num2;
				AwardMainPlayer(array2[0], num2);
			}
			return;
		}
		KeyValuePair<Combatant, int>[] array = (from pair in value
			where pair.Key.IsAlive && _combatants.Contains(pair.Key) && pair.Value.Value > 0
			select new KeyValuePair<Combatant, int>(pair.Key, pair.Value.Value)).ToArray();
		long num = ((IEnumerable<KeyValuePair<Combatant, int>>)array).Sum((Func<KeyValuePair<Combatant, int>, long>)((KeyValuePair<Combatant, int> pair) => pair.Value));
		if (num <= 0)
		{
			if (array2.Length == 1)
			{
				defeated.LastExpShare = num2;
				AwardMainPlayer(array2[0], num2);
			}
			return;
		}
		defeated.LastExpShare = num2;
		KeyValuePair<Combatant, int>[] array3 = array;
		foreach (KeyValuePair<Combatant, int> keyValuePair in array3)
		{
			keyValuePair.Deconstruct(out var key, out var value2);
			Combatant source = key;
			int num4 = value2;
			double amount = Math.Truncate((double)num2 * (double)num4 / (double)num);
			AwardMainAcquisitor(source, amount);
		}
	}

	private void AwardMainPartyExperience(Combatant defeated, int monsterExperience, IReadOnlyList<Combatant> members)
	{
		if (members.Count == 0) return;
		double num2 = (double)monsterExperience;
		double num3 = ((members[0].ClassId == "royal") ? 0.059 : 0.0);
		double num4 = 0.04 * (double)Math.Max(0, members.Count - 1);
		num2 = Math.Truncate(num2 * (1.0 + num4 + num3));
		double expPerMember = Math.Max(1.0, Math.Floor(num2 / members.Count));
		defeated.LastExpShare = expPerMember;
		foreach (Combatant member in members)
		{
			AwardMainPlayer(member, expPerMember);
		}
	}

	private Combatant? PartyMemberOf(Combatant source)
	{
		CombatantKind kind = source.Kind;
		if ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false)
		{
			return source;
		}
		if (source.Kind == CombatantKind.Pet)
		{
			return _petOwners.GetValueOrDefault(source);
		}
		if (source.Kind == CombatantKind.Summon)
		{
			return _summonOwners.GetValueOrDefault(source);
		}
		return null;
	}

	private void AwardMainAcquisitor(Combatant source, double amount)
	{
		CombatantKind kind = source.Kind;
		PetInstance value;
		if ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false)
		{
			AwardMainPlayer(source, amount);
		}
		else if (source.Kind == CombatantKind.Pet && _petInstances.TryGetValue(source, out value))
		{
			double num = Math.Floor(amount);
			if (!(num <= 0.0))
			{
				_events.Add(CombatEvent.ExpGain(source, num));
				AwardPetExperience(source, value, num);
			}
		}
	}

	private void AwardMainPlayer(Combatant recipient, double rawAmount)
	{
		double num = ProgressionRules.ApplyMainPlayerRate(rawAmount, recipient.Level);
		if (num <= 0.0)
		{
			return;
		}
		_events.Add(CombatEvent.ExpGain(recipient, num));
		int level = recipient.Level;
		if (ProgressionRules.ApplyExperience(recipient, num, _data) > 0)
		{
			if (ClassKitRegistry.TryGet(recipient.ClassId, out ClassKit _))
			{
				double num2 = ((recipient.MaxHp > 0.0) ? (recipient.Hp / recipient.MaxHp) : 0.0);
				CombatantBuilder.RefreshPlayer(recipient, _data);
				recipient.Hp = Math.Clamp(Math.Floor(recipient.MaxHp * num2), 1.0, recipient.MaxHp);
				recipient.Mp = recipient.MaxMp;
			}
			else if (MonsterCompanionRules.IsCompanion(recipient) && recipient.Avatar.Length > 0)
			{
				double num3 = ((recipient.MaxHp > 0.0) ? (recipient.Hp / recipient.MaxHp) : 0.0);
				Combatant combatant = MonsterCompanionRules.Create(_data, recipient.Avatar, recipient.Level, recipient.Key, recipient.BornSeq);
				recipient.D = combatant.D;
				recipient.MaxHp = combatant.MaxHp;
				recipient.MaxMp = combatant.MaxMp;
				recipient.Hp = Math.Clamp(Math.Floor(recipient.MaxHp * num3), 1.0, recipient.MaxHp);
				recipient.Mp = recipient.MaxMp;
			}
			for (int i = level + 1; i <= recipient.Level; i++)
			{
				_events.Add(CombatEvent.LevelUp(recipient, i));
			}
		}
	}

	public static bool IsWorldNpc(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.L1jWorldNpcId != 0;
	}

	public IReadOnlyList<MapSpawnCell> LivingNormalMobCells()
	{
		ExplorationNavigationGrid explorationNavigation = _explorationNavigation;
		if (explorationNavigation == null)
		{
			return Array.Empty<MapSpawnCell>();
		}
		List<MapSpawnCell> list = new List<MapSpawnCell>();
		foreach (Combatant combatant in _combatants)
		{
			if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive && !combatant.IsBoss && !IsWorldNpc(combatant) && explorationNavigation.TryCellAt(combatant.Pos, out var cell))
			{
				list.Add(cell);
			}
		}
		return list;
	}

	public bool IsExplorationMobEngaged(Combatant mob)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		if (mob.Kind != CombatantKind.Mob || !mob.IsAlive || !_combatants.Contains(mob))
		{
			return false;
		}
		if (MaximumHateTarget(mob) == null && MobTauntTarget(mob) == null)
		{
			return _projectiles.Any((CombatProjectile projectile) => projectile.Source == mob || projectile.Target == mob);
		}
		return true;
	}

	public IReadOnlyList<Combatant> RetireDistantExplorationMobs(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (!_combatants.Contains(player))
		{
			throw new InvalidOperationException("The player must be present in the combat engine.");
		}
		if (player.Kind != CombatantKind.Player)
		{
			throw new ArgumentException("Exploration retirement requires the main player.", "player");
		}
		MapTopology explorationTopology = _explorationTopology;
		if (explorationTopology != null)
		{
			ExplorationNavigationGrid explorationNavigation = _explorationNavigation;
			if (explorationNavigation != null && explorationNavigation.TryCellAt(player.Pos, out var cell))
			{
				int normalRetirementDistanceCells = explorationTopology.SpawnSettings.NormalRetirementDistanceCells;
				List<Combatant> list = new List<Combatant>();
				Combatant[] array = _combatants.ToArray();
				foreach (Combatant combatant in array)
				{
					if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive && !combatant.IsBoss && !IsWorldNpc(combatant) && explorationNavigation.TryCellAt(combatant.Pos, out var cell2))
					{
						int num = Math.Max(Math.Abs(cell2.X - cell.X), Math.Abs(cell2.Y - cell.Y));
						bool flag = IsExplorationMobEngaged(combatant);
						if (flag && num > 30 && MobCombatIdleFor(combatant, 10.0))
						{
							ClearHateTable(combatant);
							_mobTauntTargets.Remove(combatant);
							combatant.MoveTarget = null;
							flag = IsExplorationMobEngaged(combatant);
						}
						if (!flag && !IsInsideExpandedPlayerView(explorationTopology, cell, cell2) && num > normalRetirementDistanceCells)
						{
							list.Add(combatant);
						}
					}
				}
				{
					foreach (Combatant item in list)
					{
						Remove(item);
					}
					return list;
				}
			}
		}
		return Array.Empty<Combatant>();
	}

	private static bool IsInsideExpandedPlayerView(MapTopology topology, MapSpawnCell player, MapSpawnCell candidate)
	{
		(double X, double Y) tuple = topology.DisplayPixelCenter(player.X, player.Y);
		double item = tuple.X;
		double item2 = tuple.Y;
		(double X, double Y) tuple2 = topology.DisplayPixelCenter(candidate.X, candidate.Y);
		double item3 = tuple2.X;
		double item4 = tuple2.Y;
		double num = (double)topology.SpawnSettings.VisibleWorldWidthPixels * 0.5 + (double)topology.SpawnSettings.OffscreenMarginPixels;
		double num2 = (double)topology.SpawnSettings.VisibleWorldHeightPixels * 0.5 + (double)topology.SpawnSettings.OffscreenMarginPixels;
		if (Math.Abs(item3 - item) <= num)
		{
			return Math.Abs(item4 - item2) <= num2;
		}
		return false;
	}

	public bool AreExplorationCellsConnected(MapSpawnCell from, MapSpawnCell to)
	{
		return _explorationNavigation?.AreCellsConnected(from, to) ?? true;
	}

	private bool IsExplorationWalkablePoint(WorldPoint point)
	{
		return _explorationNavigation?.IsWalkable(point) ?? true;
	}

	private bool CanTraverseExplorationStep(WorldPoint from, WorldPoint to)
	{
		return _explorationNavigation?.CanTraverseStep(from, to) ?? true;
	}

	private bool CanTraverseExplorationSegment(WorldPoint from, WorldPoint to)
	{
		return _explorationNavigation?.CanTraverseSegment(from, to) ?? true;
	}

	private bool HasExplorationLineOfSight(WorldPoint from, WorldPoint to)
	{
		return _explorationNavigation?.HasArrowLineOfSight(from, to) ?? true;
	}

	private bool CanReachExplorationPoint(WorldPoint from, WorldPoint to)
	{
		return _explorationNavigation?.CanReach(from, to) ?? true;
	}

	private WorldPoint SnapToExplorationWalkablePoint(WorldPoint point)
	{
		return _explorationNavigation?.SnapToNearestWalkable(point) ?? point;
	}

	public WorldPoint SnapToExplorationLandingPoint(WorldPoint point)
	{
		return SnapToExplorationWalkablePoint(point);
	}

	private WorldPoint ExplorationNavigationWaypoint(Combatant combatant, WorldPoint destination)
	{
		ExplorationNavigationGrid explorationNavigation = _explorationNavigation;
		if (explorationNavigation == null)
		{
			return destination;
		}
		WorldPoint worldPoint = explorationNavigation.SnapToNearestWalkable(destination);
		if (!explorationNavigation.TryCellAt(worldPoint, out var cell))
		{
			_explorationNavigationPaths.Remove(combatant);
			return combatant.Pos;
		}
		if (!_explorationNavigationPaths.TryGetValue(combatant, out ExplorationPathState value) || value.Goal != cell || value.Index >= value.Points.Count || !explorationNavigation.CanTraverseSegment(combatant.Pos, value.Points[value.Index]))
		{
			IReadOnlyList<WorldPoint> readOnlyList = explorationNavigation.FindWorldPath(combatant.Pos, worldPoint);
			if (readOnlyList.Count == 0)
			{
				_explorationNavigationPaths.Remove(combatant);
				return combatant.Pos;
			}
			value = new ExplorationPathState(cell, readOnlyList, Math.Min(1, readOnlyList.Count - 1));
			_explorationNavigationPaths[combatant] = value;
		}
		WorldPoint worldPoint2 = value.Points[value.Index];
		MapSpawnCell cell2;
		MapSpawnCell cell3;
		while (value.Index < value.Points.Count - 1 && explorationNavigation.TryCellAt(combatant.Pos, out cell2) && explorationNavigation.TryCellAt(worldPoint2, out cell3) && cell2 == cell3)
		{
			value.Index++;
			worldPoint2 = value.Points[value.Index];
		}
		return worldPoint2;
	}

	private void CleanupExplorationNavigation(Combatant combatant)
	{
		_explorationNavigationPaths.Remove(combatant);
	}

	public int HateBetween(Combatant npc, Combatant source)
	{
		ArgumentNullException.ThrowIfNull(npc, "npc");
		ArgumentNullException.ThrowIfNull(source, "source");
		if (!_hate.TryGetValue(npc, out Dictionary<Combatant, HateEntry> value) || !value.TryGetValue(source, out var value2))
		{
			return 0;
		}
		return value2.Value;
	}

	internal bool HasHateEntry(Combatant npc, Combatant source)
	{
		if (_hate.TryGetValue(npc, out Dictionary<Combatant, HateEntry> value))
		{
			return value.ContainsKey(source);
		}
		return false;
	}

	public void AddHateExternal(Combatant npc, Combatant source, double amount)
	{
		AddHate(npc, source, amount, linkFamily: true);
	}

	private void AddHate(Combatant npc, Combatant? source, double amount, bool linkFamily = true)
	{
		if (source != null && (npc.UsesMonsterTemplate || npc.Kind == CombatantKind.Pet) && npc != source && source.IsAlive && IsEnemy(npc, source))
		{
			if (linkFamily && npc.Kind == CombatantKind.Mob)
			{
				RegisterMobFamilyLinks(npc, source);
			}
			int num = ((!(amount <= 0.0)) ? ((amount >= 2147483647.0) ? int.MaxValue : ((int)Math.Truncate(amount))) : 0);
			if (num != 0 && _receivedFirstHate.Add(npc))
			{
				int num2 = ((npc.MaxHp >= 21474836470.0) ? int.MaxValue : ((int)Math.Truncate(Math.Max(0.0, npc.MaxHp) / 10.0)));
				num += num2;
			}
			if (!_hate.TryGetValue(npc, out Dictionary<Combatant, HateEntry> value))
			{
				value = new Dictionary<Combatant, HateEntry>();
				_hate[npc] = value;
			}
			value[source] = (value.TryGetValue(source, out var value2) ? value2 with
			{
				Value = value2.Value + num
			} : new HateEntry(num, _nextHateSeq++));
		}
	}

	private Combatant? MaximumHateTarget(Combatant npc)
	{
		RemoveInvalidHateTargets(npc);
		if (!_hate.TryGetValue(npc, out Dictionary<Combatant, HateEntry> value))
		{
			return null;
		}
		Combatant combatant = null;
		HateEntry hateEntry = new HateEntry(int.MinValue, long.MaxValue);
		foreach (var (combatant3, hateEntry3) in value)
		{
			if (combatant == null || hateEntry.Value < hateEntry3.Value || (hateEntry.Value == hateEntry3.Value && hateEntry3.Seq < hateEntry.Seq))
			{
				combatant = combatant3;
				hateEntry = hateEntry3;
			}
		}
		return combatant;
	}

	private void RemoveInvalidHateTargets(Combatant npc)
	{
		if (!_hate.TryGetValue(npc, out Dictionary<Combatant, HateEntry> value))
		{
			return;
		}
		Combatant[] array = value.Keys.ToArray();
		foreach (Combatant combatant in array)
		{
			if (!combatant.IsAlive || !_combatants.Contains(combatant) || !IsEnemy(npc, combatant) || !CanHostileInteract(npc, combatant))
			{
				value.Remove(combatant);
			}
		}
		if (value.Count == 0)
		{
			_hate.Remove(npc);
		}
	}

	private Combatant? AcquireOrGetHateTarget(Combatant npc)
	{
		if (MonsterCompanionRules.IsCompanion(npc))
		{
			return SelectCompanionPriorityTarget(npc);
		}
		Combatant combatant = MaximumHateTarget(npc);
		if (combatant != null)
		{
			return combatant;
		}
		double range = Math.Max(0.0, npc.AggroRange);
		List<Combatant> list = new List<Combatant>();
		foreach (Combatant combatant3 in _combatants)
		{
			if (combatant3.IsAlive && IsEnemy(npc, combatant3) && CanHostileInteract(npc, combatant3) && L1jMobAggroRules.CanAcquireOrKeep(_data, npc, combatant3, alreadyKnown: false) && IsWithinRange(npc, combatant3, range) && CanNavigateTo(npc, combatant3.Pos))
			{
				list.Add(combatant3);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		Combatant combatant2 = NearestCandidate(npc, list);
		AddHate(npc, combatant2, 0.0, linkFamily: false);
		return combatant2;
	}

	internal Combatant? SelectCompanionPriorityTarget(Combatant companion)
	{
		ArgumentNullException.ThrowIfNull(companion, "companion");
		if (!MonsterCompanionRules.IsCompanion(companion))
		{
			return null;
		}
		RemoveInvalidHateTargets(companion);
		Combatant combatant = PartyLeader();
		CompanionAttackPriority[] array = combatant?.CompanionAttackPriorities;
		IReadOnlyList<CompanionAttackPriority> priorities = ((array != null && array.Length > 0) ? ((IReadOnlyList<CompanionAttackPriority>)array) : ((IReadOnlyList<CompanionAttackPriority>)DefaultCompanionAttackPriorities));
		double range = Math.Max(0.0, companion.AggroRange);
		Combatant combatant2 = null;
		foreach (Combatant combatant3 in _combatants)
		{
			bool alreadyKnown = HasHateEntry(companion, combatant3);
			if (combatant3.IsAlive && IsEnemy(companion, combatant3) && CanHostileInteract(companion, combatant3) && L1jMobAggroRules.CanAcquireOrKeep(_data, companion, combatant3, alreadyKnown) && IsWithinRange(companion, combatant3, range) && CanNavigateTo(companion, combatant3.Pos) && (combatant2 == null || CompanionPriorityCandidateWins(companion, combatant3, combatant2, combatant, priorities)))
			{
				combatant2 = combatant3;
			}
		}
		if (combatant2 != null && !HasHateEntry(companion, combatant2))
		{
			AddHate(companion, combatant2, 0.0, linkFamily: false);
		}
		return combatant2;
	}

	private bool CompanionPriorityCandidateWins(Combatant companion, Combatant candidate, Combatant incumbent, Combatant? player, IReadOnlyList<CompanionAttackPriority> priorities)
	{
		foreach (CompanionAttackPriority priority in priorities)
		{
			int num = priority switch
			{
				CompanionAttackPriority.AttackingSelf => ComparePreferredBool(IsMonsterAttacking(candidate, companion), IsMonsterAttacking(incumbent, companion)), 
				CompanionAttackPriority.AttackingPlayer => ComparePreferredBool(IsMonsterAttacking(candidate, player), IsMonsterAttacking(incumbent, player)), 
				CompanionAttackPriority.Nearest => CompareNearest(companion, candidate, incumbent), 
				CompanionAttackPriority.Boss => ComparePreferredBool(candidate.IsBoss, incumbent.IsBoss), 
				CompanionAttackPriority.Aggressive => ComparePreferredBool(candidate.UsesMonsterTemplate && !candidate.Passive, incumbent.UsesMonsterTemplate && !incumbent.Passive), 
				_ => 0, 
			};
			if (num != 0)
			{
				return num > 0;
			}
		}
		return candidate.BornSeq < incumbent.BornSeq;
	}

	private bool IsMonsterAttacking(Combatant candidate, Combatant? target)
	{
		if (target == null || !candidate.UsesMonsterTemplate)
		{
			return false;
		}
		return (MobTauntTarget(candidate) ?? MaximumHateTarget(candidate)) == target;
	}

	private static int ComparePreferredBool(bool candidate, bool incumbent)
	{
		if (candidate != incumbent)
		{
			if (!candidate)
			{
				return -1;
			}
			return 1;
		}
		return 0;
	}

	private static int CompareNearest(Combatant origin, Combatant candidate, Combatant incumbent)
	{
		double num = CombatRangeRules.DiamondDistance(origin.Pos, candidate.Pos);
		double num2 = CombatRangeRules.DiamondDistance(origin.Pos, incumbent.Pos);
		if (num < num2 - 1E-09)
		{
			return 1;
		}
		if (num > num2 + 1E-09)
		{
			return -1;
		}
		return 0;
	}

	private void ClearHateTable(Combatant npc)
	{
		_hate.Remove(npc);
		_receivedFirstHate.Remove(npc);
	}

	public void ClearGuardHostilityToward(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		foreach (Combatant combatant in _combatants)
		{
			if (HostilePlayerRules.IsGuardExecutioner(combatant))
			{
				ClearHateTable(combatant);
				if (_mobTauntTargets.GetValueOrDefault(combatant) == player)
				{
					_mobTauntTargets.Remove(combatant);
				}
				if (combatant.L1jWorldNpcImpl == "L1Guard")
				{
					combatant.NeutralWorldNpc = true;
				}
				combatant.MoveTarget = null;
				combatant.VelX = 0.0;
				combatant.VelY = 0.0;
				_navigationPaths.Remove(combatant);
				_explorationNavigationPaths.Remove(combatant);
			}
		}
	}

	private void MarkMobCombatActivity(Combatant actor)
	{
		if (actor.Kind == CombatantKind.Mob && actor.IsAlive && !actor.IsBoss && !IsWorldNpc(actor))
		{
			_mobLastCombatAt[actor] = CurrentTimeSeconds;
		}
	}

	private void MarkMobCombatActivity(Combatant attacker, Combatant target)
	{
		MarkMobCombatActivity(attacker);
		MarkMobCombatActivity(target);
	}

	private bool MobCombatIdleFor(Combatant mob, double seconds)
	{
		if (_mobLastCombatAt.TryGetValue(mob, out var value))
		{
			return CurrentTimeSeconds - value >= seconds;
		}
		return true;
	}

	private static Combatant NearestCandidate(Combatant npc, IReadOnlyList<Combatant> candidates)
	{
		Combatant combatant = candidates[0];
		double num = CombatRangeRules.DiamondDistance(npc.Pos, combatant.Pos);
		for (int i = 1; i < candidates.Count; i++)
		{
			Combatant combatant2 = candidates[i];
			double num2 = CombatRangeRules.DiamondDistance(npc.Pos, combatant2.Pos);
			if (num2 < num - 1E-09 || (Math.Abs(num2 - num) <= 1E-09 && combatant2.BornSeq < combatant.BornSeq))
			{
				combatant = combatant2;
				num = num2;
			}
		}
		return combatant;
	}

	private void CleanupHate(Combatant combatant)
	{
		_hate.Remove(combatant);
		_receivedFirstHate.Remove(combatant);
		_mobLastCombatAt.Remove(combatant);
		foreach (Dictionary<Combatant, HateEntry> value in _hate.Values)
		{
			value.Remove(combatant);
		}
	}

	private void ApplyHitImpact(Combatant attacker, Combatant target, DamageType damageType, bool heavy)
	{
		if (!HitImpactRules.CausesHitstun(damageType, target, heavy) || !target.IsAlive)
		{
			return;
		}
		target.HitstunUntil = HitImpactRules.HitstunUntil(target, CurrentStep);
		if (HitImpactRules.CanBeKnockedBack(target) && !SolidBodyRules.IsSolidPair(attacker, target))
		{
			WorldPoint? worldPoint = HitImpactRules.KnockbackTarget(attacker, target);
			if (worldPoint.HasValue)
			{
				WorldPoint valueOrDefault = worldPoint.GetValueOrDefault();
				target.Pos = ClampAndSnapPlacement(valueOrDefault, target.Radius);
			}
		}
	}

	private bool IsStaggered(Combatant combatant)
	{
		return HitImpactRules.IsStaggered(combatant, CurrentStep);
	}

	private bool AdvanceHostileFieldPlayer(Combatant hostile)
	{
		if (!hostile.Key.StartsWith("hostile-", StringComparison.Ordinal) || !HostilePlayerRules.IsHostilePlayer(hostile))
		{
			return false;
		}
		if (HealthPercent(hostile) < 70.0 && hostile.Buffs.GetValueOrDefault("_ally_potion_pace") <= 0.0 && TryDrinkAllyPotion(hostile, "potion_heal"))
		{
			hostile.Buffs["_ally_potion_pace"] = 2.0;
		}
		if (!hostile.CanCast || hostile.LearnedSkills.Count == 0)
		{
			return true;
		}
		Combatant combatant = SelectMobTarget(hostile, hostile.AggroRange);
		if (combatant == null)
		{
			return true;
		}
		AllySkillPlan allySkillPlan = AllySkillPlanFor(hostile);
		if (HealthPercent(hostile) < 40.0)
		{
			foreach (string item2 in allySkillPlan.Heal)
			{
				if (TryAutoCastSkill(hostile, item2, hostile))
				{
					return true;
				}
			}
		}
		if (allySkillPlan.Buffs.Count > 0 && _random.NextDouble() < 0.2)
		{
			foreach (string buff in allySkillPlan.Buffs)
			{
				if (!(hostile.Buffs.GetValueOrDefault(buff) > 0.0) && TryAutoCastSkill(hostile, buff, hostile))
				{
					return true;
				}
			}
		}
		List<string> list = new List<string>(allySkillPlan.Attack.Count + allySkillPlan.Debuff.Count);
		list.AddRange(allySkillPlan.Attack);
		foreach (var (item, text) in allySkillPlan.Debuff)
		{
			if (text.Length == 0 || !combatant.Statuses.ContainsKey(text))
			{
				list.Add(item);
			}
		}
		if (list.Count == 0)
		{
			return true;
		}
		int num = Math.Clamp((int)(_random.NextDouble() * (double)list.Count), 0, list.Count - 1);
		for (int i = 0; i < list.Count && !TryAutoCastSkill(hostile, list[(num + i) % list.Count], combatant); i++)
		{
		}
		return true;
	}

	private void RegisterContestedAttacker(Combatant target, Combatant? source)
	{
		if (source != null && target.Kind == CombatantKind.Mob && !HostilePlayerRules.IsHostilePlayer(target) && HostilePlayerRules.IsHostilePlayer(source))
		{
			if (!_contestedByHostilePlayers.TryGetValue(target, out HashSet<Combatant> value))
			{
				value = (_contestedByHostilePlayers[target] = new HashSet<Combatant>());
			}
			value.Add(source);
		}
	}

	private double ContestedRewardMultiplier(Combatant dead, int activePartySize)
	{
		if (!_contestedByHostilePlayers.TryGetValue(dead, out HashSet<Combatant> value))
		{
			return 1.0;
		}
		return HostilePlayerRules.ContestedRewardMultiplier(activePartySize, value.Count);
	}

	private void CleanupContestedTracking(Combatant combatant)
	{
		_contestedByHostilePlayers.Remove(combatant);
		if (!HostilePlayerRules.IsHostilePlayer(combatant))
		{
			return;
		}
		foreach (HashSet<Combatant> value in _contestedByHostilePlayers.Values)
		{
			value.Remove(combatant);
		}
	}

	private static bool? HostilePlayerFactionEnemy(Combatant source, Combatant candidate)
	{
		return HostilePlayerRules.FactionEnemy(source, candidate);
	}

	private bool HostilePlayerEngagementAllowed(Combatant source, Combatant target)
	{
		bool flag = HostilePlayerRules.IsHostilePlayer(source);
		bool flag2 = HostilePlayerRules.IsHostilePlayer(target);
		if (flag == flag2)
		{
			return true;
		}
		Combatant hostilePlayer = (flag ? source : target);
		if ((flag ? target : source).Kind == CombatantKind.Mob)
		{
			return true;
		}
		return HostilePlayerRules.PlayerSideMayEngage(hostilePlayer, PlayerPvpEnabled);
	}

	private void AbsorbEngineRepositioning()
	{
		foreach (KeyValuePair<Combatant, IsometricStepState> isometricStep in _isometricSteps)
		{
			isometricStep.Deconstruct(out var key, out var value);
			Combatant combatant = key;
			value.LastApplied = combatant.Pos;
		}
	}

	private bool AdvanceExistingIsometricStep(Combatant combatant, double deltaSeconds)
	{
		if (!_isometricSteps.TryGetValue(combatant, out IsometricStepState value))
		{
			return false;
		}
		if (combatant.Pos.DistanceSquaredTo(value.LastApplied) > 1E-06)
		{
			_isometricSteps.Remove(combatant);
			return false;
		}
		if (value.CompletedFrames >= value.TotalFrames)
		{
			_isometricSteps.Remove(combatant);
			return false;
		}
		value.CompletedFrames++;
		WorldPoint pos = combatant.Pos;
		combatant.Pos = IsometricMovementRules.Lerp(value.Start, value.End, value.CompletedFrames, value.TotalFrames);
		value.LastApplied = combatant.Pos;
		combatant.Facing8 = value.Facing8;
		SetMovementVelocity(combatant, pos, deltaSeconds);
		return true;
	}

	private bool TryBeginDirectionalIsometricStep(Combatant combatant, WorldPoint direction, double moveSpeed, double deltaSeconds)
	{
		combatant.Pos = SnapToWalkableIsometricPoint(combatant.Pos, combatant.Radius);
		Span<bool> span = stackalloc bool[8];
		for (int i = 0; i < IsometricMovementRules.Directions.Count; i++)
		{
			int num = -1;
			double num2 = 0.01;
			for (int j = 0; j < IsometricMovementRules.Directions.Count; j++)
			{
				if (!span[j])
				{
					IsometricStep isometricStep = IsometricMovementRules.Directions[j];
					double num3 = Math.Sqrt(isometricStep.DeltaX * isometricStep.DeltaX + isometricStep.DeltaY * isometricStep.DeltaY);
					double num4 = (isometricStep.DeltaX * direction.X + isometricStep.DeltaY * direction.Y) / num3;
					if (!(num4 <= num2))
					{
						num2 = num4;
						num = j;
					}
				}
			}
			if (num < 0)
			{
				break;
			}
			span[num] = true;
			IsometricStep isometricStep2 = IsometricMovementRules.Directions[num];
			WorldPoint worldPoint = new WorldPoint(combatant.Pos.X + isometricStep2.DeltaX, combatant.Pos.Y + isometricStep2.DeltaY);
			if (CanUseIsometricStep(combatant, worldPoint) && !StepBlockedBySolidBody(combatant, worldPoint))
			{
				_sidestepOrigins.Remove(combatant);
				_isometricSteps[combatant] = new IsometricStepState(combatant.Pos, worldPoint, isometricStep2.Facing8, IsometricMovementRules.FramesForSpeed(moveSpeed));
				return AdvanceExistingIsometricStep(combatant, deltaSeconds);
			}
		}
		return false;
	}

	private static int GridStepDistance(IsometricGridPoint from, IsometricGridPoint to)
	{
		return Math.Max(Math.Abs(to.AxisA - from.AxisA), Math.Abs(to.AxisB - from.AxisB));
	}

	private bool TryBeginIsometricStep(Combatant combatant, WorldPoint waypoint, double moveSpeed, double deltaSeconds)
	{
		combatant.Pos = SnapToWalkableIsometricPoint(combatant.Pos, combatant.Radius);
		IsometricGridPoint to = IsometricMovementRules.GridPointAt(waypoint, _isometricLatticeOrigin);
		int num = GridStepDistance(IsometricMovementRules.GridPointAt(combatant.Pos, _isometricLatticeOrigin), to);
		double num2 = waypoint.X - combatant.Pos.X;
		double num3 = waypoint.Y - combatant.Pos.Y;
		IsometricStep? isometricStep = null;
		WorldPoint end = default(WorldPoint);
		int num4 = int.MaxValue;
		double num5 = double.NegativeInfinity;
		bool flag = false;
		IsometricStep? isometricStep2 = null;
		WorldPoint worldPoint = default(WorldPoint);
		double num6 = double.NegativeInfinity;
		double num7 = double.NegativeInfinity;
		double? num8 = null;
		WorldPoint value;
		bool flag2 = _sidestepOrigins.TryGetValue(combatant, out value);
		foreach (IsometricStep direction in IsometricMovementRules.Directions)
		{
			WorldPoint worldPoint2 = new WorldPoint(combatant.Pos.X + direction.DeltaX, combatant.Pos.Y + direction.DeltaY);
			if (!CanUseIsometricStep(combatant, worldPoint2) || (flag2 && worldPoint2.DistanceSquaredTo(value) < 1.0))
			{
				continue;
			}
			int num9 = GridStepDistance(IsometricMovementRules.GridPointAt(worldPoint2, _isometricLatticeOrigin), to);
			double num10 = Math.Sqrt(direction.DeltaX * direction.DeltaX + direction.DeltaY * direction.DeltaY);
			double num11 = (direction.DeltaX * num2 + direction.DeltaY * num3) / num10;
			bool flag3 = StepBlockedBySolidBody(combatant, worldPoint2);
			if (num9 >= num)
			{
				if (!flag3 && num9 <= num + 1)
				{
					double valueOrDefault = num8.GetValueOrDefault();
					if (!num8.HasValue)
					{
						valueOrDefault = SolidBodyClearance(combatant, combatant.Pos);
						num8 = valueOrDefault;
					}
					double num12 = SolidBodyClearance(combatant, worldPoint2);
					if (num12 >= num8.Value - 1E-06 && (num11 > num6 + 1E-06 || (Math.Abs(num11 - num6) <= 1E-06 && num12 > num7)))
					{
						isometricStep2 = direction;
						worldPoint = worldPoint2;
						num6 = num11;
						num7 = num12;
					}
				}
			}
			else if (flag3)
			{
				flag = true;
			}
			else if (num9 <= num4 && (num9 != num4 || !(num11 <= num5 + 1E-06)))
			{
				isometricStep = direction;
				end = worldPoint2;
				num4 = num9;
				num5 = num11;
			}
		}
		if (!isometricStep.HasValue && flag && isometricStep2.HasValue)
		{
			isometricStep = isometricStep2;
			end = worldPoint;
			_sidestepOrigins[combatant] = combatant.Pos;
		}
		else if (isometricStep.HasValue)
		{
			_sidestepOrigins.Remove(combatant);
		}
		if (isometricStep.HasValue)
		{
			IsometricStep valueOrDefault2 = isometricStep.GetValueOrDefault();
			IsometricStepState value2 = new IsometricStepState(combatant.Pos, end, valueOrDefault2.Facing8, IsometricMovementRules.FramesForSpeed(moveSpeed));
			_isometricSteps[combatant] = value2;
			return AdvanceExistingIsometricStep(combatant, deltaSeconds);
		}
		return false;
	}

	private bool CanUseIsometricStep(Combatant combatant, WorldPoint candidate)
	{
		if (!CanTraverseExplorationStep(combatant.Pos, candidate))
		{
			return false;
		}
		WorldBounds? worldBounds = _worldBounds;
		if (worldBounds.HasValue && worldBounds.GetValueOrDefault().Clamp(candidate) != candidate)
		{
			return false;
		}
		return _collisionGrid?.CanTraverseSegment(combatant.Pos, candidate, Math.Max(0.0, combatant.Radius)) ?? true;
	}

	private double SolidBodyClearance(Combatant mover, WorldPoint point)
	{
		double num = double.PositiveInfinity;
		foreach (Combatant combatant in _combatants)
		{
			if (combatant != mover && combatant.IsAlive && SolidBodyRules.IsSolidPair(mover, combatant))
			{
				num = Math.Min(num, point.DistanceTo(combatant.Pos) - (mover.Radius + combatant.Radius));
			}
		}
		return num;
	}

	private bool StepBlockedBySolidBody(Combatant mover, WorldPoint candidate)
	{
		if (mover.Radius <= 0.0)
		{
			return false;
		}
		foreach (Combatant combatant in _combatants)
		{
			if (combatant != mover && combatant.IsAlive && !(combatant.Radius <= 0.0) && SolidBodyRules.IsSolidPair(mover, combatant))
			{
				if (SolidBodyRules.StepBlocked(mover, candidate, combatant, _isometricLatticeOrigin))
				{
					return true;
				}
				if (_isometricSteps.TryGetValue(combatant, out IsometricStepState value) && SolidBodyRules.StepBlockedByPoint(candidate, value.End, _isometricLatticeOrigin))
				{
					return true;
				}
			}
		}
		bool? flag = null;
		foreach (WorldPoint staticSolidBody in _staticSolidBodies)
		{
			if (!SolidBodyRules.StepBlockedByPoint(candidate, staticSolidBody, _isometricLatticeOrigin))
			{
				continue;
			}
			bool valueOrDefault = flag == true;
			if (!flag.HasValue)
			{
				valueOrDefault = _staticSolidBodies.Any((WorldPoint currentBody) => SolidBodyRules.StepBlockedByPoint(mover.Pos, currentBody, _isometricLatticeOrigin));
				flag = valueOrDefault;
			}
			if (!flag.Value)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsGridCellOccupied(WorldPoint point)
	{
		foreach (Combatant combatant in _combatants)
		{
			if (combatant.IsAlive && !(combatant.Radius <= 0.0) && SolidBodyRules.StepBlockedByPoint(point, combatant.Pos, _isometricLatticeOrigin))
			{
				return true;
			}
		}
		foreach (WorldPoint staticSolidBody in _staticSolidBodies)
		{
			if (SolidBodyRules.StepBlockedByPoint(point, staticSolidBody, _isometricLatticeOrigin))
			{
				return true;
			}
		}
		return false;
	}

	public void SetStaticSolidBodies(IEnumerable<WorldPoint> bodies)
	{
		ArgumentNullException.ThrowIfNull(bodies, "bodies");
		_staticSolidBodies.Clear();
		_staticSolidBodies.AddRange(bodies);
	}

	public WorldPoint WalkStepOrigin(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		return SnapToWalkableIsometricPoint(combatant.Pos, Math.Max(0.0, combatant.Radius));
	}

	private WorldPoint SnapToWalkableIsometricPoint(WorldPoint point, double radius)
	{
		WorldPoint point2 = _worldBounds?.Clamp(point) ?? point;
		point2 = SnapToExplorationWalkablePoint(point2);
		IsometricGridPoint isometricGridPoint = IsometricMovementRules.GridPointAt(point2, _isometricLatticeOrigin);
		WorldPoint result = default(WorldPoint);
		double num = double.PositiveInfinity;
		bool flag = false;
		for (int i = 0; i <= 16; i++)
		{
			for (int j = -i; j <= i; j++)
			{
				for (int k = -i; k <= i; k++)
				{
					if (Math.Max(Math.Abs(j), Math.Abs(k)) != i)
					{
						continue;
					}
					WorldPoint worldPoint = IsometricMovementRules.WorldPointAt(new IsometricGridPoint(isometricGridPoint.AxisA + j, isometricGridPoint.AxisB + k), _isometricLatticeOrigin);
					WorldBounds? worldBounds = _worldBounds;
					if ((!worldBounds.HasValue || !(worldBounds.GetValueOrDefault().Clamp(worldPoint) != worldPoint)) && IsExplorationWalkablePoint(worldPoint) && (_collisionGrid == null || _collisionGrid.CanOccupy(worldPoint, Math.Max(0.0, radius))))
					{
						double num2 = worldPoint.DistanceSquaredTo(point2);
						if (!(num2 >= num - 1E-06))
						{
							result = worldPoint;
							num = num2;
							flag = true;
						}
					}
				}
			}
			if (flag)
			{
				return result;
			}
		}
		return ClampAndSnapToWalkable(point2, radius);
	}

	private static void SetMovementVelocity(Combatant combatant, WorldPoint before, double deltaSeconds)
	{
		double num = combatant.Pos.X - before.X;
		double num2 = combatant.Pos.Y - before.Y;
		if (deltaSeconds <= 0.0 || num * num + num2 * num2 <= 1E-06)
		{
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
		}
		else
		{
			combatant.VelX = num / deltaSeconds;
			combatant.VelY = num2 / deltaSeconds;
		}
	}

	private static string NewLightningWandUid()
	{
		return $"lwand-{Guid.NewGuid():N}";
	}

	public bool TryUseL1jLightningWand(Combatant caster, string itemUid)
	{
		string text = caster.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid)?.ItemKey ?? string.Empty;
		if (text.Length != 0)
		{
			JsonObject jsonObject = _data?.Item(text);
			if (jsonObject != null && CombatSkill.ReadInt(jsonObject, "l1jItemId") == 40007)
			{
				Combatant combatant = SelectNearestEnemy(caster, 72.0, requireLineOfSight: true);
				if (combatant == null)
				{
					return false;
				}
				if (!ItemStackInventory.TryDetachOne(caster.InventoryStacks, itemUid, NewLightningWandUid, out ItemStack detached) || detached == null)
				{
					return false;
				}
				bool flag = detached.Uid != itemUid;
				if (detached.ChargeCount <= 0)
				{
					detached.ChargeCount = 15;
				}
				int num = Math.Max(1, L1jRandomInclusive(5, 14) + (int)Math.Floor(caster.D.Str));
				ApplyDirectDamage(caster, combatant, num, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out var _);
				if (combatant.Dead)
				{
					ResolveDeath(combatant, caster);
				}
				detached.ChargeCount--;
				if (detached.ChargeCount <= 0)
				{
					if (!flag)
					{
						caster.InventoryStacks.Remove(detached);
					}
				}
				else if (flag)
				{
					ItemStackInventory.TryAddOrStack(caster.InventoryStacks, detached, out ItemStack _);
				}
				CombatInventory.SyncLegacyView(caster);
				_events.Add(CombatEvent.Cast(caster, "item_40007", combatant));
				return true;
			}
		}
		return false;
	}

	private bool? TryCastDedicatedL1jSkill(Combatant caster, string skillId, JsonObject source, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock, bool automatic, bool isScroll = false)
	{
		if (!(source["l1j"] is JsonObject source2))
		{
			return null;
		}
		int num = CombatSkill.ReadInt(source2, "officialId");
		bool blocked;
		switch (num)
		{
		case 18:
		case 39:
		case 44:
		case 58:
		case 61:
		case 80:
		case 87:
		case 108:
		case 132:
		case 157:
		case 158:
		case 187:
		case 203:
		case 208:
			blocked = true;
			break;
		default:
			blocked = false;
			break;
		}
		if (!blocked)
		{
			return null;
		}
		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source2);
		if (!ignoreCastLock && caster.CastCd > 0.0)
		{
			return false;
		}
		switch (num)
		{
		case 61:
			return TryCastL1jResurrection(caster, skillId, source, l1jSkillFields, requestedTarget, freeMp, ignoreCastLock, isScroll);
		case 158:
			return TryCastL1jNaturesTouch(caster, skillId, source, l1jSkillFields, requestedTarget, freeMp, ignoreCastLock, isScroll);
		case 157:
			return TryCastL1jEarthShield(caster, skillId, source, l1jSkillFields, requestedTarget, freeMp, ignoreCastLock, isScroll);
		default:
		{
			double range = DedicatedL1jRange(skillId, source, l1jSkillFields);
			Combatant combatant = ((requestedTarget != null && requestedTarget.IsAlive && IsEnemy(caster, requestedTarget) && IsWithinRange(caster, requestedTarget, range) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range, requireLineOfSight: true) : null));
			if (combatant == null)
			{
				return false;
			}
			if (num == 18)
			{
				int undeadType = CounterDamageRules.UndeadType(_data, combatant);
				bool isUndead = (undeadType == 1 || (uint)(undeadType - 3) <= 1u);
				if (!isUndead)
				{
					return false;
				}
			}
			if (num == 132 && (_data == null || !AmmunitionRules.CanLaunchBasicShot(_data, caster)))
			{
				return false;
			}
			if (!WeaponRequirementMet(caster, CombatSkill.ReadString(source, "reqWpn")))
			{
				return false;
			}
			if (!CanPayDedicatedL1jCost(caster, skillId, source, l1jSkillFields, freeMp))
			{
				return false;
			}
			CommitDedicatedL1jCost(caster, skillId, source, l1jSkillFields, combatant, freeMp, ignoreCastLock, support: false, isScroll: isScroll);
			CombatSkill skill = DedicatedL1jAttackSkill(skillId, source);
			switch (num)
			{
			case 18:
			{
				int i = CounterDamageRules.UndeadType(_data, combatant);
				blocked = ((i == 1 || (uint)(i - 3) <= 1u) ? true : false);
				if (blocked && L1jProbabilitySucceeds(caster, combatant, skill))
				{
					ApplyDirectDamage(caster, combatant, combatant.Hp, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked, critical: false, "earth");
					if (combatant.Dead)
					{
						ResolveDeath(combatant, caster);
					}
				}
				else
				{
					_events.Add(CombatEvent.Miss(caster, combatant));
				}
				break;
			}
			case 39:
			{
				if (!L1jProbabilitySucceeds(caster, combatant, skill))
				{
					_events.Add(CombatEvent.Miss(caster, combatant));
					break;
				}
				int num2 = Math.Min((int)Math.Floor(combatant.Mp), L1jRandomInclusive(5, 14) + (int)Math.Floor(Math.Max(0.0, caster.D.Int) / 2.0));
				if (num2 > 0)
				{
					combatant.Mp -= num2;
					caster.RestoreMp(num2);
					_events.Add(CombatEvent.MpChange(combatant, -num2));
					_events.Add(CombatEvent.MpChange(caster, num2));
				}
				break;
			}
			case 44:
			{
				if (caster.Kind == CombatantKind.Player && combatant.Kind == CombatantKind.Player && !L1jProbabilitySucceeds(caster, combatant, skill))
				{
					_events.Add(CombatEvent.Miss(caster, combatant));
					break;
				}
				string[] array = combatant.Buffs.Keys.Where(IsL1jCancellableBuff).ToArray();
				foreach (string buffName in array)
				{
					RemoveBuff(combatant, buffName);
				}
				break;
			}
			case 58:
				SpawnL1jFireWall(caster, combatant.Pos, l1jSkillFields);
				break;
			case 80:
			{
				double radius = CombatRangeRules.SpellAreaRadius(skillId) ?? ((double)Math.Max(1, l1jSkillFields.Area) * 48.0);
				Combatant[] array2 = _combatants.Where((Combatant candidate) => candidate.IsAlive && IsEnemy(caster, candidate) && IsWithinRange(caster, candidate, radius) && HasCombatLineOfSight(caster, candidate)).ToArray();
				foreach (Combatant combatant2 in array2)
				{
					ApplyL1jDedicatedMagicDamage(caster, combatant2, skill);
					if (combatant2.IsAlive && L1jProbabilitySucceeds(caster, combatant2, skill))
					{
						TryApplyStatusCore(combatant2, "freeze", 60, null, resistanceChecked: true);
					}
				}
				break;
			}
			case 87:
				if (L1jProbabilitySucceeds(caster, combatant, skill))
				{
					TryApplyStatusCore(combatant, "stun", _random.Roll(1, 6) * 10, null, resistanceChecked: true);
				}
				else
				{
					_events.Add(CombatEvent.Miss(caster, combatant));
				}
				break;
			case 108:
			{
				double damage = Math.Max(1.0, caster.Mp + (double)l1jSkillFields.MpConsume);
				ApplyDirectDamage(caster, combatant, damage, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked);
				if (combatant.Dead)
				{
					ResolveDeath(combatant, caster);
				}
				caster.Hp = 1.0;
				caster.Mp = 0.0;
				break;
			}
			case 132:
			{
				for (int k = 0; k < 3; k++)
				{
					if (!combatant.IsAlive)
					{
						break;
					}
					_events.Add(CombatEvent.Projectile(caster, combatant, "arrow", k));
					double tripleMult = RelicConditionalCombatRules.FullHealthTripleArrowMultiplier(_data, caster, combatant, skillId, k);
					PerformPhysicalHit(caster, combatant, true, forceHeavy: false, forceCritical: false, 0.0, 0.0, forceHit: false, basicAttack: false, tripleMult * SkillDamageMasteryMultiplier(caster, skill));
					if (_data != null)
					{
						AmmunitionRules.ConsumeCommittedBasicShot(_data, caster);
					}
				}
				break;
			}
			case 187:
			{
				for (int j = 0; j < 3; j++)
				{
					if (!combatant.IsAlive)
					{
						break;
					}
					PerformPhysicalHit(caster, combatant);
				}
				break;
			}
			case 203:
				ApplyL1jDedicatedMagicDamage(caster, combatant, skill);
				break;
			case 208:
				ApplyL1jDedicatedMagicDamage(caster, combatant, skill);
				if (combatant.IsAlive && L1jProbabilitySucceeds(caster, combatant, skill))
				{
					TryApplyStatusCore(combatant, "stun", Math.Max(1, l1jSkillFields.BuffDurationSeconds) * 10, null, resistanceChecked: true);
				}
				break;
			}
			return true;
		}
		}
	}

	private bool TryCastL1jEarthShield(Combatant caster, string skillId, JsonObject source, L1jSkillFields fields, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock, bool isScroll = false)
	{
		Combatant combatant = requestedTarget ?? caster;
		if (!combatant.IsAlive || IsEnemy(caster, combatant) || !IsWithinRange(caster, combatant, DedicatedL1jRange(skillId, source, fields)))
		{
			return false;
		}
		if (!CanPayDedicatedL1jCost(caster, skillId, source, fields, freeMp))
		{
			return false;
		}
		CommitDedicatedL1jCost(caster, skillId, source, fields, combatant, freeMp, ignoreCastLock, support: true, isScroll: isScroll);
		ApplyBuff(combatant, skillId, Math.Max(1, fields.BuffDurationSeconds));
		TryApplyStatusCore(combatant, "paralyze", fields.BuffDurationSeconds * 10, null, resistanceChecked: true);
		return true;
	}

	private bool TryCastL1jNaturesTouch(Combatant caster, string skillId, JsonObject source, L1jSkillFields fields, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock, bool isScroll = false)
	{
		Combatant combatant = requestedTarget ?? caster;
		if (!combatant.IsAlive || IsEnemy(caster, combatant) || !IsWithinRange(caster, combatant, DedicatedL1jRange(skillId, source, fields)))
		{
			return false;
		}
		if (!CanPayDedicatedL1jCost(caster, skillId, source, fields, freeMp))
		{
			return false;
		}
		CommitDedicatedL1jCost(caster, skillId, source, fields, combatant, freeMp, ignoreCastLock, support: true, isScroll: isScroll);
		ApplyBuff(combatant, skillId, Math.Max(1, fields.BuffDurationSeconds));
		return true;
	}

	private bool TryCastL1jResurrection(Combatant caster, string skillId, JsonObject source, L1jSkillFields fields, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock, bool isScroll = false)
	{
		bool flag = requestedTarget == null || requestedTarget.IsAlive || IsEnemy(caster, requestedTarget);
		if (!flag)
		{
			CombatantKind kind = requestedTarget.Kind;
			bool flag2 = (uint)(kind - 2) <= 1u;
			flag = !flag2;
		}
		if (flag || !IsWithinRange(caster, requestedTarget, DedicatedL1jRange(skillId, source, fields)))
		{
			return false;
		}
		if (!CanPayDedicatedL1jCost(caster, skillId, source, fields, freeMp))
		{
			return false;
		}
		CommitDedicatedL1jCost(caster, skillId, source, fields, requestedTarget, freeMp, ignoreCastLock, support: true, isScroll: isScroll);
		if (requestedTarget.Kind == CombatantKind.Ally)
		{
			ReviveAllyCore(requestedTarget, 0.5);
		}
		else
		{
			RevivePetCore(requestedTarget, 0.5);
		}
		return true;
	}

	private bool CanPayDedicatedL1jCost(Combatant caster, string skillId, JsonObject source, L1jSkillFields fields, bool freeMp)
	{
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skillId, CombatModifierRules.SkillMpCost(caster, source, skillId)) : 0);
		if (caster.Mp >= (double)num)
		{
			return caster.Hp > (double)fields.HpConsume;
		}
		return false;
	}

	private void CommitDedicatedL1jCost(Combatant caster, string skillId, JsonObject source, L1jSkillFields fields, Combatant? target, bool freeMp, bool ignoreCastLock, bool support, bool isScroll = false)
	{
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skillId, CombatModifierRules.SkillMpCost(caster, source, skillId)) : 0);
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		if (fields.HpConsume > 0)
		{
			caster.Hp = Math.Max(1.0, caster.Hp - (double)fields.HpConsume);
		}
		if (!ignoreCastLock)
		{
			if (IsTripleArrow(skillId))
			{
				caster.CastCd = 0.15;
			}
			else
			{
				caster.CastCd = Math.Max(NextCastCooldownSeconds(caster, support), (double)fields.ReuseDelayMilliseconds / 1000.0);
			}
		}
		_events.Add(CombatEvent.Cast(caster, skillId, target, isScroll: isScroll));
	}

	private double DedicatedL1jRange(string skillId, JsonObject source, L1jSkillFields fields)
	{
		double? num = CombatRangeRules.ConfiguredCastRange(source);
		if (!num.HasValue)
		{
			double? num2 = CombatRangeRules.SpellCastRange(skillId);
			if (!num2.HasValue)
			{
				if (fields.Ranged <= 0)
				{
					return 72.0;
				}
				return (double)fields.Ranged * 48.0;
			}
			return num2.GetValueOrDefault();
		}
		return num.GetValueOrDefault();
	}

	private static CombatSkill DedicatedL1jAttackSkill(string skillId, JsonObject source)
	{
		JsonObject jsonObject = (JsonObject)source.DeepClone();
		jsonObject["type"] = "atk";
		jsonObject["dmgType"] = "magic";
		jsonObject["target"] = "one";
		if (!CombatSkill.TryRead(skillId, jsonObject, out CombatSkill skill) || skill == null)
		{
			throw new InvalidDataException("Unable to build L1J attack form for '" + skillId + "'.");
		}
		return skill;
	}

	private void ApplyL1jDedicatedMagicDamage(Combatant caster, Combatant target, CombatSkill skill)
	{
		bool critical;
		double damage = L1jMagicSkillCoreDamage(caster, target, skill, out critical);
		ApplyDirectDamage(caster, target, damage, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out var _, critical, skill.Element);
		if (target.Dead)
		{
			ResolveDeath(target, caster);
		}
	}

	private void SpawnL1jFireWall(Combatant caster, WorldPoint targetPosition, L1jSkillFields fields)
	{
		IsometricGridPoint current = IsometricMovementRules.GridPointAt(caster.Pos, _isometricLatticeOrigin);
		IsometricGridPoint isometricGridPoint = IsometricMovementRules.GridPointAt(targetPosition, _isometricLatticeOrigin);
		double expiresAt = CurrentTimeSeconds + (double)Math.Max(1, fields.BuffDurationSeconds);
		for (int i = 0; i < 8; i++)
		{
			if (!(current != isometricGridPoint))
			{
				break;
			}
			current = new IsometricGridPoint(current.AxisA + Math.Sign(isometricGridPoint.AxisA - current.AxisA), current.AxisB + Math.Sign(isometricGridPoint.AxisB - current.AxisB));
			WorldPoint position = IsometricMovementRules.WorldPointAt(current, _isometricLatticeOrigin);
			_l1jFireWallTiles.RemoveAll((L1jFireWallTile tile) => tile.GridPoint == current);
			_l1jFireWallTiles.Add(new L1jFireWallTile
			{
				Source = caster,
				GridPoint = current,
				Position = position,
				Damage = Math.Max(0, fields.DamageValue),
				ExpiresAt = expiresAt,
				NextDamageAt = CurrentTimeSeconds
			});
		}
	}

	private void AdvanceL1jFireWalls()
	{
		L1jFireWallTile[] array = _l1jFireWallTiles.ToArray();
		foreach (L1jFireWallTile tile in array)
		{
			if (CurrentTimeSeconds + 1E-09 >= tile.ExpiresAt)
			{
				_l1jFireWallTiles.Remove(tile);
			}
			else if (!(CurrentTimeSeconds + 1E-09 < tile.NextDamageAt))
			{
				tile.NextDamageAt += 1.0;
				Combatant[] array2 = _combatants.Where((Combatant candidate) => candidate.IsAlive && IsEnemy(tile.Source, candidate) && IsometricMovementRules.GridPointAt(candidate.Pos, _isometricLatticeOrigin) == tile.GridPoint).ToArray();
				foreach (Combatant target in array2)
				{
					ApplyL1jFireWallDamage(tile, target);
				}
			}
		}
	}

	private void ApplyL1jFireWallDamage(L1jFireWallTile tile, Combatant target)
	{
		if (AbsoluteBarrierRules.IsActive(target) || target.Buffs.GetValueOrDefault("sk_elf_earthshield") > 0.0)
		{
			return;
		}
		double num = Math.Floor(tile.Damage * Math.Max(0.0, 1.0 - AttributeDefense(target, "fire")));
		if (num <= 0.0)
		{
			return;
		}
		double num2 = ApplyDamageWithStatusModifiers(target, num, tile.Source, 0.0, bypassReduction: true);
		if (!(num2 <= 0.0))
		{
			_events.Add(new CombatEvent(CombatEventKind.Damage, tile.Source, target, num2, crit: false, DamageType.Dot, "fire"));
			if (target.Dead)
			{
				ResolveDeath(target, tile.Source);
			}
		}
	}

	private int L1jRandomInclusive(int minimum, int maximum)
	{
		return minimum + (int)Math.Floor(Math.Clamp(_random.NextDouble(), 0.0, Math.BitDecrement(1.0)) * (double)(maximum - minimum + 1));
	}

	private static bool IsL1jCancellableBuff(string buffId)
	{
		if (!buffId.StartsWith("_", StringComparison.Ordinal))
		{
			switch (buffId)
			{
			default:
				return !(buffId == "sk_dragon_awaken3");
			case "sk_abs_barrier":
			case "sk_elf_earthshield":
			case "sk_counter_barrier":
			case "sk_dragon_awaken1":
			case "sk_dragon_awaken2":
				return false;
			}
		}
		return false;
	}

	private void InitializeGuardianSupplies(Combatant guardian)
	{
		bool flag;
		switch (guardian.L1jWorldNpcId)
		{
		case 70846:
		case 70848:
		case 70850:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			GuardianSupplyState guardianSupplyState = new GuardianSupplyState();
			_guardianSupplies[guardian] = guardianSupplyState;
			RefillGuardian(guardianSupplyState, guardian.L1jWorldNpcId, CurrentTimeSeconds);
		}
	}

	private void ForgetGuardianSupplies(Combatant guardian)
	{
		_guardianSupplies.Remove(guardian);
	}

	private bool CanL1jGuardianMaterialAction(Combatant player, Combatant guardian)
	{
		if (_data != null && player.Kind == CombatantKind.Player && ElfElementRules.IsElf(player) && player.MainWeaponId.Length == 0)
		{
			return _guardianSupplies.ContainsKey(guardian);
		}
		return false;
	}

	private bool TryL1jGuardianMaterialAction(Combatant player, Combatant guardian)
	{
		if (!CanL1jGuardianMaterialAction(player, guardian) || !_guardianSupplies.TryGetValue(guardian, out GuardianSupplyState value))
		{
			return false;
		}
		RefreshGuardianSupplyWindow(value, guardian.L1jWorldNpcId);
		int num = _random.Roll(1, 100);
		string itemKey = "";
		long num2 = 0L;
		switch (guardian.L1jWorldNpcId)
		{
		case 70846:
		{
			long num3 = CombatInventory.AvailableCount(player, "l1j_item_40507");
			if (num3 > 0 && CombatInventory.TryRemove(player, "l1j_item_40507", num3))
			{
				CombatInventory.Add(value.Stock, "l1j_item_40507", num3);
			}
			long num4 = CombatInventory.Count(value.Stock, "l1j_item_40507");
			if (num4 > 0 && CombatInventory.TryRemove(value.Stock, "l1j_item_40507", num4))
			{
				itemKey = "new_item_171";
				num2 = num4;
			}
			break;
		}
		case 70848:
		{
			long num5 = CombatInventory.AvailableCount(player, "new_item_166");
			if (num5 > 0 && CombatInventory.TryRemove(player, "new_item_166", num5))
			{
				CombatInventory.Add(value.Stock, "new_item_166", num5);
			}
			long num6 = CombatInventory.Count(value.Stock, "new_item_166");
			if (num6 > 0 && CombatInventory.TryRemove(value.Stock, "new_item_166", num6))
			{
				itemKey = "l1j_item_40505";
				num2 = num6;
				if (!value.IsDropItems)
				{
					value.RefillAt = CurrentTimeSeconds + 180.0;
				}
			}
			else if (CombatInventory.Count(value.Stock, "l1j_item_40507") > 0)
			{
				if (num <= 25 && CombatInventory.TryRemove(value.Stock, "l1j_item_40507", 6L))
				{
					itemKey = "l1j_item_40507";
					num2 = 6L;
				}
			}
			else if (CombatInventory.Count(value.Stock, "new_item_141") > 0)
			{
				if (num <= 10 && CombatInventory.TryRemove(value.Stock, "new_item_141", 1L))
				{
					itemKey = "new_item_141";
					num2 = 1L;
				}
			}
			else
			{
				ScheduleGuardianRefillWhenClosed(value);
			}
			break;
		}
		case 70850:
			if (CombatInventory.Count(value.Stock, "new_item_163") > 0)
			{
				if (num <= 30 && CombatInventory.TryRemove(value.Stock, "new_item_163", 5L))
				{
					itemKey = "new_item_163";
					num2 = 5L;
				}
			}
			else
			{
				ScheduleGuardianRefillWhenClosed(value);
			}
			break;
		}
		if (num2 > 0)
		{
			CombatInventory.Add(player, itemKey, num2);
			_events.Add(CombatEvent.ItemGain(player, itemKey, checked((int)num2)));
		}
		return (object)PolymorphRules.ActiveForm(_data, player) == null;
	}

	private void RefreshGuardianSupplyWindow(GuardianSupplyState state, int npcId)
	{
		if (state.ForDropItems && CurrentTimeSeconds >= state.WindowEndsAt)
		{
			state.ForDropItems = false;
		}
		if (state.RefillAt > 0.0 && CurrentTimeSeconds >= state.RefillAt)
		{
			RefillGuardian(state, npcId, CurrentTimeSeconds);
		}
	}

	private void ScheduleGuardianRefillWhenClosed(GuardianSupplyState state)
	{
		if (!state.ForDropItems && !(state.RefillAt > 0.0))
		{
			state.IsDropItems = false;
			state.RefillAt = CurrentTimeSeconds + 600.0;
		}
	}

	private static void RefillGuardian(GuardianSupplyState state, int npcId, double now)
	{
		if (npcId == 70848 && CombatInventory.Count(state.Stock, "l1j_item_40505") == 0L && CombatInventory.Count(state.Stock, "new_item_141") == 0L && CombatInventory.Count(state.Stock, "l1j_item_40507") == 0L)
		{
			CombatInventory.Add(state.Stock, "new_item_141", 1L);
			CombatInventory.Add(state.Stock, "l1j_item_40507", 66L);
			CombatInventory.Add(state.Stock, "l1j_item_40505", 8L);
		}
		else if (npcId == 70850 && CombatInventory.Count(state.Stock, "new_item_163") == 0L)
		{
			CombatInventory.Add(state.Stock, "new_item_163", 30L);
		}
		state.IsDropItems = true;
		state.ForDropItems = true;
		state.WindowEndsAt = now + 600.0;
		state.RefillAt = 0.0;
	}

	internal void TryApplyL1jMobPoisonAttack(Combatant attacker, Combatant target)
	{
		L1jPoisonAttackType l1jPoisonAttackType = L1jPoisonAttackRules.AttackType(_data, attacker);
		if (l1jPoisonAttackType == L1jPoisonAttackType.None || !L1jPoisonAttackRules.CanInfect(_data, target) || _random.Roll(1, 100) > 15)
		{
			return;
		}
		bool flag;
		switch (l1jPoisonAttackType)
		{
		case L1jPoisonAttackType.Damage:
			flag = TryApplyStatusCore(target, "poison", 300, new PeriodicEffect
			{
				TickEvery = 30,
				TicksUntilNext = 30,
				Damage = 5.0,
				DamageType = DamageType.Dot,
				Element = "none",
				Source = attacker
			}, resistanceChecked: true);
			break;
		case L1jPoisonAttackType.Silence:
			flag = TryApplyStatusCore(target, "poisonsilence", int.MaxValue, null, resistanceChecked: true);
			break;
		case L1jPoisonAttackType.Paralysis:
			if (target.Kind == CombatantKind.Player || HostilePlayerRules.IsHostilePlayer(target))
			{
				flag = TryApplyStatusCore(target, "poisonparalyzing", 200, null, resistanceChecked: true);
				break;
			}
			goto default;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			_events.Add(CombatEvent.LogLine(attacker.Disp + " 的毒性攻擊感染了" + target.Disp + "。"));
		}
	}

	private void AdvanceL1jParalysisPoison(Combatant target, string expiredStatus)
	{
		if (string.Equals(expiredStatus, "poisonparalyzing", StringComparison.Ordinal) && target.IsAlive && (target.Kind == CombatantKind.Player || HostilePlayerRules.IsHostilePlayer(target)))
		{
			TryApplyStatusCore(target, "poisonparalyzed", TrapParalysisDuration(target), null, resistanceChecked: true);
		}
	}

	private double L1jMagicSkillCoreDamage(Combatant caster, Combatant target, CombatSkill skill, out bool critical)
	{
		L1jSkillFields l1jSkillFields = skill.L1j ?? throw new InvalidOperationException("Skill '" + skill.Id + "' reached the L1J magic handler without L1J fields.");
		L1jMagicFormulas.MagicDamageRoll magicDamageRoll = L1jMagicFormulas.RollMagicDiceDamage(_random, l1jSkillFields.DamageDiceCount, l1jSkillFields.DamageDice, l1jSkillFields.DamageValue, 0, (int)Math.Floor(caster.D.Int) + (int)Math.Floor(Math.Max(0.0, caster.D.ItemSpellPower)), AttributeDefense(target, skill.Element), l1jSkillFields.SkillLevel <= 6, caster.D.OriginalMagicCritical, caster.D.OriginalMagicDamage + (int)Math.Floor(Math.Max(0.0, caster.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(caster))), 0, 1.0 + Math.Max(0.0, caster.D.MagicCriticalDamage) / 100.0);
		critical = magicDamageRoll.Critical;
		if (RelicConditionalCombatRules.IgnoresSpellMagicResistance(_data, caster))
		{
			return Math.Max(1, magicDamageRoll.Damage);
		}
		int val = L1jMagicFormulas.MagicResistanceDefense(magicDamageRoll.Damage, (int)Math.Floor(Math.Max(0.0, EffectiveMagicResist(target))), Math.Max(0, caster.D.OriginalMagicHit));
		return Math.Max(1, val);
	}

	private double L1jHealAmount(Combatant caster, CombatSkill skill)
	{
		L1jSkillFields l1jSkillFields = skill.L1j ?? throw new InvalidOperationException("Skill '" + skill.Id + "' reached the L1J heal handler without L1J fields.");
		return L1jMagicFormulas.Healing(_random, l1jSkillFields.DamageDice, l1jSkillFields.DamageValue, L1jMagicFormulas.MagicBonus((int)Math.Floor(Math.Max(0.0, caster.D.Int))), (int)Math.Round(caster.Alignment), 10);
	}

	private bool L1jProbabilitySucceeds(Combatant caster, Combatant target, CombatSkill skill)
	{
		L1jSkillFields l1jSkillFields = skill.L1j ?? throw new InvalidOperationException("Skill '" + skill.Id + "' reached the L1J probability handler without L1J fields.");
		ICombatRandom random = _random;
		L1jMagicFormulas.ProbabilityBranch branch = L1jMagicFormulas.BranchFor(l1jSkillFields.OfficialId);
		int probabilityDice = l1jSkillFields.ProbabilityDice;
		int probabilityValue = l1jSkillFields.ProbabilityValue;
		int level = caster.Level;
		int level2 = target.Level;
		int magicBonus = L1jMagicFormulas.MagicBonus((int)Math.Floor(Math.Max(0.0, caster.D.Int)));
		CombatantKind kind = caster.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		int num = L1jMagicFormulas.Probability(random, branch, probabilityDice, probabilityValue, level, level2, magicBonus, (flag || HostilePlayerRules.IsHostilePlayer(caster)) ? ClassGrowthRules.MagicLevel(caster.ClassId, caster.Level) : L1jMagicFormulas.MagicLevel(caster.Level), (int)Math.Floor(Math.Max(0.0, EffectiveMagicResist(target))), Math.Max(0, caster.D.OriginalMagicHit), 10, string.Equals(ClassKitRegistry.NormalizeClassId(caster.ClassId), "mage", StringComparison.Ordinal), StatusRules.L1jStatusResistance(_data, target, l1jSkillFields.OfficialId));
		num = Math.Min(100, num + (int)SkillAbnormalMasteryBonus(caster, skill));
		return L1jMagicFormulas.ProbabilitySucceeds(_random, num);
	}

	private static double L1jGlobalSkillDelaySeconds(CombatSkill skill)
	{
		if (L1jSkillHandover.IsLive(skill))
		{
			L1jSkillFields l1j = skill.L1j;
			if (l1j != null)
			{
				return (double)Math.Max(0, l1j.ReuseDelayMilliseconds) / 1000.0;
			}
		}
		return 0.0;
	}

	private static double L1jGlobalSkillDelaySeconds(JsonObject? source)
	{
		if (!L1jSkillHandover.IsLive(source) || !(source["l1j"] is JsonObject source2))
		{
			return 0.0;
		}
		return (double)Math.Max(0, CombatSkill.ReadInt(source2, "reuseDelay")) / 1000.0;
	}

	public bool TryUseMagicDoll(Combatant owner, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (_data == null)
		{
			return false;
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null || !MagicDollRules.TryReadDoll(_data, itemStack.ItemKey, out MagicDollDefinition definition))
		{
			return false;
		}
		if (_activeDolls.TryGetValue(owner, out ActiveDollRuntime value))
		{
			if (!string.Equals(value.ItemUid, itemUid, StringComparison.Ordinal))
			{
				return false;
			}
			RecallMagicDoll(owner);
			return true;
		}
		MagicDollCatalog magicDollCatalog = MagicDollRules.LoadCatalog(_data);
		if (CombatInventory.Count(owner, magicDollCatalog.CrystalItemKey) < 50)
		{
			return false;
		}
		if (!CombatInventory.TryRemove(owner, magicDollCatalog.CrystalItemKey, 50L))
		{
			return false;
		}
		Combatant combatant = new Combatant
		{
			Kind = CombatantKind.Summon,
			Key = $"doll:{owner.Key}:{definition.L1jItemId}",
			Disp = definition.Name,
			Avatar = $"gfx:{definition.Gfx}",
			Level = owner.Level,
			Hp = 1.0,
			MaxHp = 1.0,
			Passive = true,
			AttackRange = 0.0,
			Radius = 0.0,
			MoveSpeed = 160.0,
			Pos = SummonRules.FormationPoint(owner, 0, 1),
			BornSeq = owner.BornSeq
		};
		_combatants.Add(combatant);
		_activeDolls[owner] = new ActiveDollRuntime
		{
			Definition = definition,
			Follower = combatant,
			ItemUid = itemUid,
			ExpiresAt = CurrentTimeSeconds + 1800.0,
			NextRegenAt = CurrentTimeSeconds + 64.0
		};
		owner.Counters["doll:type"] = definition.Type;
		if (definition.Type == 10)
		{
			owner.MaxHp += 50.0;
			owner.Hp += 50.0;
		}
		return true;
	}

	public bool RestoreActiveMagicDoll(Combatant owner, string itemKey, string itemUid, double remainingSeconds)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (_data == null || remainingSeconds <= 0.0)
		{
			return false;
		}
		if (!MagicDollRules.TryReadDoll(_data, itemKey, out MagicDollDefinition definition))
		{
			return false;
		}
		RecallMagicDoll(owner);

		Combatant combatant = new Combatant
		{
			Kind = CombatantKind.Summon,
			Key = $"doll:{owner.Key}:{definition.L1jItemId}",
			Disp = definition.Name,
			Avatar = $"gfx:{definition.Gfx}",
			Level = owner.Level,
			Hp = 1.0,
			MaxHp = 1.0,
			Passive = true,
			AttackRange = 0.0,
			Radius = 0.0,
			MoveSpeed = 160.0,
			Pos = SummonRules.FormationPoint(owner, 0, 1),
			BornSeq = owner.BornSeq
		};
		_combatants.Add(combatant);
		_activeDolls[owner] = new ActiveDollRuntime
		{
			Definition = definition,
			Follower = combatant,
			ItemUid = itemUid,
			ExpiresAt = CurrentTimeSeconds + remainingSeconds,
			NextRegenAt = CurrentTimeSeconds + 64.0
		};
		owner.Counters["doll:type"] = definition.Type;
		if (definition.Type == 10)
		{
			owner.MaxHp += 50.0;
			owner.Hp += 50.0;
		}
		return true;
	}

	public void RecallMagicDoll(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (_activeDolls.Remove(owner, out ActiveDollRuntime value))
		{
			owner.Counters.Remove("doll:type");
			_combatants.Remove(value.Follower);
			if (value.Definition.Type == 10)
			{
				owner.MaxHp = Math.Max(1.0, owner.MaxHp - 50.0);
				owner.Hp = Math.Min(owner.Hp, owner.MaxHp);
			}
		}
	}

	public MagicDollDefinition? ActiveMagicDollOf(Combatant owner)
	{
		if (!_activeDolls.TryGetValue(owner, out ActiveDollRuntime value))
		{
			return null;
		}
		return value.Definition;
	}

	public (MagicDollDefinition Definition, double RemainingSeconds, Combatant Follower)? GetActiveDollInfo(Combatant owner)
	{
		if (owner == null || !_activeDolls.TryGetValue(owner, out ActiveDollRuntime value))
		{
			return null;
		}
		double remaining = Math.Max(0.0, value.ExpiresAt - CurrentTimeSeconds);
		return (value.Definition, remaining, value.Follower);
	}

	private void AdvanceMagicDolls()
	{
		if (_activeDolls.Count == 0)
		{
			return;
		}
		KeyValuePair<Combatant, ActiveDollRuntime>[] array = _activeDolls.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<Combatant, ActiveDollRuntime> keyValuePair = array[i];
			var (combatant2, activeDollRuntime2) = keyValuePair;
			if (CurrentTimeSeconds >= activeDollRuntime2.ExpiresAt || !combatant2.IsAlive || !_combatants.Contains(combatant2))
			{
				RecallMagicDoll(combatant2);
				continue;
			}
			if (!(CurrentTimeSeconds >= activeDollRuntime2.NextRegenAt))
			{
				continue;
			}
			activeDollRuntime2.NextRegenAt += 64.0;
			if (!AbsoluteBarrierRules.IsActive(combatant2))
			{
				switch (activeDollRuntime2.Definition.Type)
				{
				case 6:
				case 8:
					combatant2.Hp = Math.Min(combatant2.MaxHp, combatant2.Hp + 40.0);
					break;
				case 1:
				case 3:
				case 12:
				case 14:
					combatant2.Mp = Math.Min(combatant2.MaxMp, combatant2.Mp + 15.0);
					break;
				}
			}
		}
	}

	private void TryApplyMagicDollAttackProcs(Combatant attacker, Combatant originalTarget)
	{
		if (originalTarget.IsAlive && MagicDollRules.RollAttackBonus(attacker, _random))
		{
			double num = originalTarget.ApplyDamage(15.0);
			if (num > 0.0)
			{
				_events.Add(CombatEvent.Damage(attacker, originalTarget, num, DamageType.True));
			}
		}
		if (originalTarget.IsAlive && MagicDollRules.RollPoisonProc(attacker, _random))
		{
			ApplySummonPoison(attacker, originalTarget, 5.0);
		}
	}

	private double ApplyMagicDollDamageReduction(Combatant target, double damage, DamageType damageType)
	{
		if ((uint)damageType > 2u)
		{
			return damage;
		}
		if (damage <= 0.0)
		{
			return damage;
		}
		double flatReduction = MagicDollRules.DamageReductionAdjustment(target);
		if (flatReduction > 0.0)
		{
			damage = Math.Max(0.0, damage - flatReduction);
		}
		if (!MagicDollRules.RollDamageShield(target, _random))
		{
			return damage;
		}
		return Math.Max(0.0, damage - 15.0);
	}

	public ManualCastRequestResult QueueManualCast(Combatant caster, string skillId, Combatant? requestedTarget = null, string? preferredSummonForm = null, bool isScroll = false)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		if (string.Equals(skillId, "sk_charm", StringComparison.Ordinal) || IsTripleArrow(skillId))
		{
			if (!TryCastSkill(caster, skillId, requestedTarget))
			{
				return ManualCastRequestResult.Rejected;
			}
			return ManualCastRequestResult.Cast;
		}
		_manualCastQueue.Remove(caster);
		JsonObject jsonObject = _data?.Skill(skillId);
		if (!_combatants.Contains(caster) || !caster.IsAlive || caster.CannotAttack || jsonObject == null || (!isScroll && !string.IsNullOrWhiteSpace(caster.ClassId) && !ClassKitRegistry.CanUseSkill(caster, skillId, _data)))
		{
			RejectQueuedManualCast(skillId, jsonObject);
			return ManualCastRequestResult.Rejected;
		}
		ManualCastRequest manualCastRequest = new ManualCastRequest(skillId, requestedTarget, preferredSummonForm, isScroll);
		_manualCastQueue[caster] = manualCastRequest;
		if (ManualCastMustWait(caster, isScroll))
		{
			return ManualCastRequestResult.Queued;
		}
		_manualCastQueue.Remove(caster);
		NormalizeReadyManualActionClock(caster);
		if (TryExecuteManualCast(caster, manualCastRequest))
		{
			return ManualCastRequestResult.Cast;
		}
		RejectQueuedManualCast(skillId, jsonObject);
		return ManualCastRequestResult.Rejected;
	}

	public bool HasQueuedManualCast(Combatant caster)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		return _manualCastQueue.ContainsKey(caster);
	}

	private bool TryResolveQueuedManualCast(Combatant caster)
	{
		if (!_manualCastQueue.TryGetValue(caster, out ManualCastRequest value))
		{
			return false;
		}
		if (!value.IsScroll && caster.Kind == CombatantKind.Player && IsPhysicallyMoving(caster))
		{
			return true;
		}
		_manualCastQueue.Remove(caster);
		NormalizeReadyManualActionClock(caster);
		if (!TryExecuteManualCast(caster, value))
		{
			RejectQueuedManualCast(value.SkillId, _data?.Skill(value.SkillId));
		}
		return true;
	}

	private bool ManualCastMustWait(Combatant caster, bool isScroll = false)
	{
		if (isScroll)
		{
			return caster.IsHardControlled;
		}
		if (!SharedActionOnCooldown(caster) && !IsActionLocked(caster) && !caster.IsHardControlled && !IsStaggered(caster) && caster.DelayTicks <= 0)
		{
			if (caster.Kind == CombatantKind.Player)
			{
				return IsPhysicallyMoving(caster);
			}
			return false;
		}
		return true;
	}

	private static void NormalizeReadyManualActionClock(Combatant caster)
	{
		if (caster.AttackCd > 0.0 && caster.AttackCd <= 1E-09)
		{
			caster.AttackCd = 0.0;
		}
		if (caster.CastCd > 0.0 && caster.CastCd <= 1E-09)
		{
			caster.CastCd = 0.0;
		}
	}

	private bool TryExecuteManualCast(Combatant caster, ManualCastRequest request)
	{
		if (IsTripleArrow(request.SkillId))
		{
			return TryCastSkillCore(caster, request.SkillId, request.Target, freeMp: false, ignoreCastLock: false, automatic: false);
		}
		return TryCastSkillAsSharedAction(caster, request.SkillId, request.Target, automatic: false, request.PreferredSummonForm, freeMp: request.IsScroll, ignoreClassRequirement: request.IsScroll, isScroll: request.IsScroll);
	}

	private void RejectQueuedManualCast(string skillId, JsonObject? source)
	{
		string text = source?["n"]?.GetValue<string>() ?? skillId;
		_events.Add(CombatEvent.LogLine("手動施法失敗：" + text + " 目前無法施放。"));
	}

	private bool TryPerformMobMagicBasicAttack(Combatant attacker, Combatant target)
	{
		if (!attacker.UsesMonsterTemplate || HostilePlayerRules.UsesPlayerCombatRules(attacker) || _data == null)
		{
			return false;
		}
		string text = MobSkillRules.DefinitionKey(_data, attacker);
		if (text.Length != 0)
		{
			JsonObject jsonObject = _data.Mob(text);
			if (jsonObject != null)
			{
				MobBasicAttackProfile mobBasicAttackProfile = MobBasicAttackRules.Resolve(jsonObject);
				if (!mobBasicAttackProfile.UsesMagicDamage)
				{
					return false;
				}
				double num = RollMobMagicBasicAttackDamage(attacker, target, mobBasicAttackProfile);
				if (mobBasicAttackProfile.ProjectileKind.Length > 0)
				{
					CreateProjectile(attacker, target, mobBasicAttackProfile.ProjectileKind, basicAttack: false, magicWeaponAttack: true, default(PhysicalHitResult), num);
				}
				else
				{
					ApplyCommittedMagicWeaponHit(attacker, target, num);
				}
				return true;
			}
		}
		return false;
	}

	private double RollMobMagicBasicAttackDamage(Combatant attacker, Combatant target, MobBasicAttackProfile profile)
	{
		double num = Math.Max(1.0, (double)_random.Roll(profile.MagicDiceCount, profile.MagicDiceSides) + profile.MagicFlatDamage);
		double num2 = Math.Max(1, L1jMagicFormulas.NpcMagicResistanceDefense(_random, (int)num, (int)Math.Floor(Math.Max(0.0, EffectiveMagicResist(target)))));
		return Math.Max(1.0, num2 + (double)RollElementCounterDamage(attacker.AttackElement, target));
	}

	private Combatant? MobTauntTarget(Combatant mob)
	{
		Combatant valueOrDefault = _mobTauntTargets.GetValueOrDefault(mob);
		if (mob.HasStatus("taunt") && valueOrDefault != null && valueOrDefault.IsAlive && _combatants.Contains(valueOrDefault) && IsEnemy(mob, valueOrDefault) && CanHostileInteract(mob, valueOrDefault))
		{
			return valueOrDefault;
		}
		_mobTauntTargets.Remove(mob);
		return null;
	}

	private Combatant? MobPursuitTarget(Combatant mob)
	{
		if (mob.Kind == CombatantKind.Pet)
		{
			return PetPursuitTarget(mob);
		}
		if (mob.CannotAttack)
		{
			return null;
		}
		Combatant combatant = MobTauntTarget(mob);
		if (combatant != null && HasPlayerPursuitSlot(mob, combatant) && CanNavigateTo(mob, combatant.Pos))
		{
			return combatant;
		}
		Combatant combatant2 = AcquireOrGetHateTarget(mob);
		if (combatant2 == null || !L1jMobAggroRules.CanAcquireOrKeep(_data, mob, combatant2, HasHateEntry(mob, combatant2)) || !IsWithinRange(mob, combatant2, Math.Max(0.0, mob.AggroRange)) || !HasPlayerPursuitSlot(mob, combatant2) || !CanNavigateTo(mob, combatant2.Pos))
		{
			return null;
		}
		return combatant2;
	}

	private bool MobCanEngageEnemies(Combatant mob)
	{
		return MobPursuitTarget(mob) != null;
	}

	internal Combatant? SelectMobTarget(Combatant mob, double range)
	{
		if (mob.CannotAttack)
		{
			return null;
		}
		Combatant combatant = MobTauntTarget(mob);
		if (combatant != null)
		{
			if (!IsWithinRange(mob, combatant, range) || !HasPlayerPursuitSlot(mob, combatant) || !HasCombatLineOfSight(mob, combatant))
			{
				return null;
			}
			return combatant;
		}
		Combatant combatant2 = AcquireOrGetHateTarget(mob);
		if (combatant2 == null || !IsWithinRange(mob, combatant2, Math.Min(Math.Max(0.0, range), Math.Max(0.0, mob.AggroRange))) || !HasPlayerPursuitSlot(mob, combatant2) || !HasCombatLineOfSight(mob, combatant2))
		{
			return null;
		}
		return combatant2;
	}

	private bool HasPlayerPursuitSlot(Combatant mob, Combatant target)
	{
		if (target.Kind != CombatantKind.Player || mob.Kind != CombatantKind.Mob || mob.IsBoss || IsWorldNpc(mob))
		{
			return true;
		}
		double num = CombatRangeRules.DiamondDistance(mob.Pos, target.Pos);
		int num2 = 0;
		foreach (Combatant combatant in _combatants)
		{
			if (combatant == mob || combatant.Kind != CombatantKind.Mob || !combatant.IsAlive || combatant.IsBoss || IsWorldNpc(combatant) || (MobTauntTarget(combatant) ?? MaximumHateTarget(combatant)) != target)
			{
				continue;
			}
			double num3 = CombatRangeRules.DiamondDistance(combatant.Pos, target.Pos);
			if (num3 < num - 1E-09 || (Math.Abs(num3 - num) <= 1E-09 && combatant.BornSeq < mob.BornSeq))
			{
				num2++;
				if (num2 >= 12)
				{
					return false;
				}
			}
		}
		return true;
	}

	private bool MobHasAnyTarget(Combatant mob)
	{
		if (MobTauntTarget(mob) != null)
		{
			return true;
		}
		return MaximumHateTarget(mob) != null;
	}

	private void RegisterMobFamilyLinks(Combatant attacked, Combatant? source)
	{
		if (attacked.Kind != CombatantKind.Mob || source == null || source.Kind != CombatantKind.Player || !source.IsAlive)
		{
			return;
		}
		foreach (Combatant combatant in _combatants)
		{
			bool flag = attacked.MobGroupLeader != null && combatant.MobGroupLeader == attacked.MobGroupLeader;
			if (combatant.Kind == CombatantKind.Mob && combatant.IsAlive && !MobHasAnyTarget(combatant) && !(CombatRangeRules.DiamondDistance(source.Pos, combatant.Pos) > 960.0) && (flag || L1jMobAggroRules.SupportsAttackedFamily(_data, combatant, attacked)))
			{
				AddHate(combatant, source, 0.0, linkFamily: false);
				_mobWanderTargets.Remove(combatant);
				_mobNextWanderAt.Remove(combatant);
				combatant.MoveTarget = null;
			}
		}
	}

	private void AdvanceWorldNpcReturnHome(Combatant mob, double deltaSeconds, double moveSpeed)
	{
		if (!_mobHomePositions.TryGetValue(mob, out var value))
		{
			_mobHomePositions[mob] = mob.Pos;
			return;
		}
		ResetIdleWander(mob);
		if (CombatRangeRules.DiamondDistance(mob.Pos, value) <= 5.0)
		{
			mob.MoveTarget = null;
		}
		else
		{
			MoveToward(mob, value, deltaSeconds, 5.0, moveSpeed);
		}
	}

	private void AdvancePassiveWander(Combatant mob, double deltaSeconds, double moveSpeed)
	{
		if (!_mobHomePositions.TryGetValue(mob, out var value))
		{
			value = mob.Pos;
			_mobHomePositions[mob] = value;
		}
		AdvanceIdleWander(mob, deltaSeconds, moveSpeed, value);
	}

	private bool AdvanceFleeOnlyMob(Combatant mob, double deltaSeconds, double moveSpeed)
	{
		if (!mob.FleeOnly)
		{
			return false;
		}
		Combatant combatant = MaximumHateTarget(mob);
		if (combatant == null)
		{
			return false;
		}
		ResetIdleWander(mob);
		if (CombatRangeRules.DiamondDistance(mob.Pos, combatant.Pos) > 720.0)
		{
			ClearHateTable(mob);
			return false;
		}
		double num = mob.Pos.X - combatant.Pos.X;
		double num2 = mob.Pos.Y - combatant.Pos.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (num3 <= 1E-06)
		{
			double num4 = (double)(mob.BornSeq & 7) * Math.PI / 4.0;
			num = Math.Cos(num4);
			num2 = Math.Sin(num4);
			num3 = 1.0;
		}
		double num5 = 144.0;
		WorldPoint worldPoint = new WorldPoint(mob.Pos.X + num / num3 * num5, mob.Pos.Y + num2 / num3 * num5);
		worldPoint = _worldBounds?.Clamp(worldPoint) ?? worldPoint;
		MoveToward(mob, worldPoint, deltaSeconds, 5.0, moveSpeed);
		return true;
	}

	private void AdvanceIdleWander(Combatant unit, double deltaSeconds, double moveSpeed, WorldPoint home)
	{
		double value3;
		if (_mobWanderTargets.TryGetValue(unit, out var value))
		{
			bool num = MoveToward(unit, value, deltaSeconds, 5.0, moveSpeed);
			double value2;
			bool flag = _mobNextWanderAt.TryGetValue(unit, out value2) && CurrentTimeSeconds > value2;
			if (num || flag)
			{
				_mobWanderTargets.Remove(unit);
				ScheduleIdleWander(unit);
			}
		}
		else if (!_mobNextWanderAt.TryGetValue(unit, out value3))
		{
			ScheduleIdleWander(unit);
		}
		else if (!(CurrentTimeSeconds + 1E-09 < value3))
		{
			int valueOrDefault = _mobWanderSequences.GetValueOrDefault(unit);
			_mobWanderSequences[unit] = valueOrDefault + 1;
			double num2 = DeterministicUnit(unit.BornSeq, valueOrDefault, 17) * Math.PI * 2.0;
			double num3 = 1.0 + DeterministicUnit(unit.BornSeq, valueOrDefault, 53) * 2.0;
			double num4 = 48.0 * num3;
			WorldPoint worldPoint = new WorldPoint(home.X + Math.Cos(num2) * num4, home.Y + Math.Sin(num2) * num4);
			worldPoint = _worldBounds?.Clamp(worldPoint) ?? worldPoint;
			_mobWanderTargets[unit] = SnapToWalkableIsometricPoint(worldPoint, Math.Max(0.0, unit.Radius));
			_mobNextWanderAt[unit] = CurrentTimeSeconds + 10.0;
		}
	}

	private void ScheduleIdleWander(Combatant unit)
	{
		int valueOrDefault = _mobWanderSequences.GetValueOrDefault(unit);
		double num = DeterministicUnit(unit.BornSeq, valueOrDefault, 91);
		_mobNextWanderAt[unit] = CurrentTimeSeconds + 2.0 + num * 3.0;
	}

	public void ResetIdleWander(Combatant unit)
	{
		_mobWanderTargets.Remove(unit);
	}

	private static double DeterministicUnit(int bornSequence, int sequence, int salt)
	{
		int num = bornSequence * 747796405 + (int)(sequence * 2891336453u) + salt * 277803737;
		int num2 = (num ^ (num >>> 16)) * -2048144777;
		return (double)(uint)((num2 ^ (num2 >>> 13)) & 0xFFFFFF) / 16777216.0;
	}

	private void CleanupMobBehaviorRuntime(Combatant combatant)
	{
		_mobTauntTargets.Remove(combatant);
		_mobHomePositions.Remove(combatant);
		_mobWanderTargets.Remove(combatant);
		_mobNextWanderAt.Remove(combatant);
		_mobWanderSequences.Remove(combatant);
		CleanupL1jMobTeleportRuntime(combatant);
		CleanupHate(combatant);
		CleanupContestedTracking(combatant);
	}

	private void AdvanceMobRegeneration(Combatant mob)
	{
		AdvanceMobHealthRegeneration(mob);
		AdvanceMobManaRegeneration(mob);
	}

	private void AdvanceMobHealthRegeneration(Combatant mob)
	{
		double mobHealthRegenIntervalSeconds = mob.MobHealthRegenIntervalSeconds;
		double mobHealthRegenAmount = mob.MobHealthRegenAmount;
		if (!mob.IsAlive || mobHealthRegenIntervalSeconds <= 0.0 || mobHealthRegenAmount <= 0.0 || mob.Hp >= mob.MaxHp)
		{
			_mobHealthRegenElapsed[mob] = 0.0;
			return;
		}
		double num = _mobHealthRegenElapsed.GetValueOrDefault(mob) + 0.1;
		while (num + 1E-09 >= mobHealthRegenIntervalSeconds)
		{
			num = Math.Max(0.0, num - mobHealthRegenIntervalSeconds);
			double num2 = mob.Heal(mobHealthRegenAmount);
			if (num2 > 0.0)
			{
				_events.Add(CombatEvent.Heal(mob, mob, num2));
			}
			if (mob.Hp >= mob.MaxHp)
			{
				num = 0.0;
				break;
			}
		}
		_mobHealthRegenElapsed[mob] = num;
	}

	private void AdvanceMobManaRegeneration(Combatant mob)
	{
		double mobManaRegenIntervalSeconds = mob.MobManaRegenIntervalSeconds;
		double mobManaRegenAmount = mob.MobManaRegenAmount;
		if (!mob.IsAlive || mobManaRegenIntervalSeconds <= 0.0 || mobManaRegenAmount <= 0.0 || mob.Mp >= mob.MaxMp)
		{
			_mobManaRegenElapsed[mob] = 0.0;
			return;
		}
		double num = _mobManaRegenElapsed.GetValueOrDefault(mob) + 0.1;
		while (num + 1E-09 >= mobManaRegenIntervalSeconds)
		{
			num = Math.Max(0.0, num - mobManaRegenIntervalSeconds);
			double mp = mob.Mp;
			mob.RestoreMp(mobManaRegenAmount);
			double num2 = mob.Mp - mp;
			if (num2 > 0.0)
			{
				_events.Add(CombatEvent.MpChange(mob, num2));
			}
			if (mob.Mp >= mob.MaxMp)
			{
				num = 0.0;
				break;
			}
		}
		_mobManaRegenElapsed[mob] = num;
	}

	private bool TryExecuteMobPolymorph(Combatant caster, MobSkillPlan plan)
	{
		if (_data == null)
		{
			return false;
		}
		string text = CombatSkill.ReadString(plan.Source, "polyForm");
		if (text.Length == 0 || (object)PolymorphRules.Find(_data, text) == null)
		{
			return false;
		}
		double value = Math.Max(1.0 / 60.0, CombatSkill.ReadDouble(plan.Source, "dur", 1800.0));
		Combatant combatant = null;
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant2 in array)
		{
			if ((combatant2.Kind == CombatantKind.Player || HostilePlayerRules.IsHostilePlayer(combatant2)) && combatant2.IsAlive && IsEnemy(caster, combatant2) && IsWithinRange(caster, combatant2, caster.AggroRange) && HasCombatLineOfSight(caster, combatant2))
			{
				combatant2.PolymorphForm = text;
				combatant2.Buffs["poly"] = value;
				_events.Add(CombatEvent.BuffAdd(combatant2, "poly"));
				if (combatant == null)
				{
					combatant = combatant2;
				}
			}
		}
		if (combatant == null)
		{
			return false;
		}
		EmitMobSkillCast(caster, combatant, plan);
		return true;
	}

	private void ReleasePolymorphOnDeath(Combatant dead)
	{
		if (dead.Kind == CombatantKind.Player || HostilePlayerRules.IsHostilePlayer(dead))
		{
			dead.Buffs.Remove("poly");
			dead.PolymorphForm = string.Empty;
			_events.Add(CombatEvent.BuffRemove(dead, "poly"));
		}
	}

	private void AdvanceMobSkills()
	{
		if (_data == null)
		{
			return;
		}
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant in array)
		{
			bool flag = combatant.Kind == CombatantKind.Pet;
			bool flag2 = combatant.UsesMonsterTemplate && !HostilePlayerRules.UsesPlayerCombatRules(combatant);
			if ((combatant.Kind != CombatantKind.Mob && !flag2 && !flag) || !combatant.IsAlive || combatant.CannotAttack || combatant.IsHardControlled || StatusRules.BlocksMobSkillCasting(combatant) || AdvanceHostileFieldPlayer(combatant) || SharedActionOnCooldown(combatant) || IsActionLocked(combatant) || IsStaggered(combatant) || combatant.DelayTicks > 0 || _mobSkillNextEvaluationStep.GetValueOrDefault(combatant) > CurrentStep)
			{
				continue;
			}
			_mobSkillNextEvaluationStep[combatant] = CurrentStep + (long)Math.Ceiling(6.0);
			if (!_mobSkillPlans.TryGetValue(combatant, out IReadOnlyList<MobSkillPlan> value))
			{
				value = MobSkillRules.Plans(_data, combatant);
				_mobSkillPlans[combatant] = value;
			}
			if (value.Count == 0)
			{
				continue;
			}
			if (_mobSkillUseCounts.ContainsKey(combatant) && !MobCanEngageEnemies(combatant))
			{
				_mobSkillUseCounts.Remove(combatant);
			}
			List<(MobSkillPlan, Combatant)> list = new List<(MobSkillPlan, Combatant)>();
			foreach (MobSkillPlan item3 in value)
			{
				if (MobSkillRules.IsImplemented(item3) && ((flag2 ? MobCanEngageEnemies(combatant) : (flag ? (PetPursuitTarget(combatant) != null) : MobCanEngageEnemies(combatant))) || MobSkillRunsOutOfCombat(item3)) && TryTriggerMobSkill(combatant, item3, out Combatant forcedTarget) && (!item3.Chance.HasValue || !(_random.NextDouble() > item3.Chance.Value)))
				{
					list.Add((item3, forcedTarget));
				}
			}
			if (list.Count == 0)
			{
				continue;
			}
			int index = ((list.Count != 1) ? Math.Clamp((int)(_random.NextDouble() * (double)list.Count), 0, list.Count - 1) : 0);
			(MobSkillPlan, Combatant) tuple = list[index];
			MobSkillPlan item = tuple.Item1;
			Combatant item2 = tuple.Item2;
			double num = SharedActionCarry(combatant);
			if (TryExecuteMobSkill(combatant, item, item2))
			{
				if (item.Trigger.MaxUses > 0)
				{
					CountMobSkillUse(combatant, item.Slot);
				}
				CommitSharedActionCooldown(combatant, Math.Max(1.0 / 60.0, (double)item.CooldownTicks * 0.1 + num));
			}
		}
	}

	private bool TryTriggerMobSkill(Combatant mob, MobSkillPlan plan, out Combatant? forcedTarget)
	{
		MobSkillTrigger trigger = plan.Trigger;
		forcedTarget = SwapMobSkillTarget(mob, trigger);
		if (!trigger.Gated)
		{
			return true;
		}
		if (trigger.SelfHpPercent > 0 && MobHpPercent(mob) > trigger.SelfHpPercent)
		{
			return false;
		}
		if (trigger.CompanionHpPercent > 0)
		{
			Combatant combatant = LowestHpCompanion(mob);
			if (combatant == null || MobHpPercent(combatant) > trigger.CompanionHpPercent)
			{
				return false;
			}
			forcedTarget = combatant;
		}
		if (trigger.RangeCells != 0)
		{
			Combatant combatant2 = forcedTarget ?? MobPursuitTarget(mob);
			if (combatant2 == null || !trigger.DistanceSatisfied(CellsBetween(mob, combatant2)))
			{
				return false;
			}
		}
		if (trigger.MaxUses > 0)
		{
			return MobSkillUseCount(mob, plan.Slot) < trigger.MaxUses;
		}
		return true;
	}

	private Combatant? SwapMobSkillTarget(Combatant mob, MobSkillTrigger trigger)
	{
		return trigger.TargetSwap switch
		{
			MobSkillTargetSwap.Self => mob, 
			MobSkillTargetSwap.RandomHated => RandomHatedTarget(mob, trigger), 
			_ => null, 
		};
	}

	private Combatant? RandomHatedTarget(Combatant mob, MobSkillTrigger trigger)
	{
		List<Combatant> list = new List<Combatant>();
		foreach (Combatant combatant in _combatants)
		{
			if (combatant.IsAlive && IsEnemy(mob, combatant) && CanHostileInteract(mob, combatant) && HasHateEntry(mob, combatant) && trigger.IsTriggerDistance(CellsBetween(mob, combatant)) && HasCombatLineOfSight(mob, combatant))
			{
				list.Add(combatant);
			}
		}
		if (list.Count != 0)
		{
			return list[Math.Clamp((int)(_random.NextDouble() * (double)list.Count), 0, list.Count - 1)];
		}
		return null;
	}

	private Combatant? LowestHpCompanion(Combatant mob)
	{
		string b = MobSkillFamily(mob);
		Combatant result = null;
		double num = 100.0;
		foreach (Combatant combatant in _combatants)
		{
			if (combatant != mob && combatant.UsesMonsterTemplate && combatant.IsAlive && !IsEnemy(mob, combatant) && string.Equals(MobSkillFamily(combatant), b, StringComparison.Ordinal))
			{
				double num2 = MobHpPercent(combatant);
				if (!(num2 >= num))
				{
					num = num2;
					result = combatant;
				}
			}
		}
		return result;
	}

	private string MobSkillFamily(Combatant mob)
	{
		if (_data == null)
		{
			return mob.Key;
		}
		string text = MobSkillRules.DefinitionKey(_data, mob);
		if (text.Length == 0)
		{
			return mob.Key;
		}
		JsonObject jsonObject = _data.Mob(text);
		string text2 = ((jsonObject != null) ? CombatSkill.ReadString(jsonObject, "family") : string.Empty);
		if (text2.Length <= 0)
		{
			return text;
		}
		return text2;
	}

	private static int MobHpPercent(Combatant combatant)
	{
		if (!(combatant.MaxHp <= 0.0))
		{
			return (int)(combatant.Hp * 100.0 / combatant.MaxHp);
		}
		return 0;
	}

	private static double CellsBetween(Combatant from, Combatant to)
	{
		return CombatRangeRules.DiamondDistance(from.Pos, to.Pos) / 48.0;
	}

	private int MobSkillUseCount(Combatant mob, string slot)
	{
		if (!_mobSkillUseCounts.TryGetValue(mob, out Dictionary<string, int> value))
		{
			return 0;
		}
		return value.GetValueOrDefault(slot);
	}

	private void CountMobSkillUse(Combatant mob, string slot)
	{
		if (!_mobSkillUseCounts.TryGetValue(mob, out Dictionary<string, int> value))
		{
			value = new Dictionary<string, int>(StringComparer.Ordinal);
			_mobSkillUseCounts[mob] = value;
		}
		value[slot] = value.GetValueOrDefault(slot) + 1;
	}

	private static bool MobSkillRunsOutOfCombat(MobSkillPlan plan)
	{
		string type = plan.Type;
		if (type == "self_heal" || type == "heal_target")
		{
			return true;
		}
		return false;
	}

	private bool TryExecuteMobSkill(Combatant caster, MobSkillPlan plan, Combatant? forcedTarget = null)
	{
		if (MobSkillRules.IsSupport(plan))
		{
			if (!TryExecuteMobSupportSkill(caster, plan, forcedTarget, out Combatant presentationTarget))
			{
				return false;
			}
			EmitMobSkillCast(caster, presentationTarget, plan);
			return true;
		}
		if (MobSkillRules.IsPhysicalAttack(plan))
		{
			return TryExecuteMobPhysicalSkill(caster, plan, forcedTarget);
		}
		if (MobSkillRules.IsSummon(plan))
		{
			return TryExecuteMobSummon(caster, plan);
		}
		if (MobSkillRules.IsPolymorph(plan))
		{
			return TryExecuteMobPolymorph(caster, plan);
		}
		Combatant[] array = MobSkillTargets(caster, plan, forcedTarget);
		if (array.Length == 0)
		{
			return false;
		}
		EmitMobSkillCast(caster, array[0], plan);
		Combatant[] array2;
		if (plan.Source["dmg"] is JsonArray || plan.Type == "spell" || plan.Type == "damage")
		{
			array2 = array;
			foreach (Combatant combatant in array2)
			{
				if (combatant.IsAlive)
				{
					ApplyMobSkillDamage(caster, combatant, plan.Source);
				}
			}
			return true;
		}
		array2 = array;
		foreach (Combatant combatant2 in array2)
		{
			if (combatant2.IsAlive && !TryBlockUndeadRelicAttack(caster, combatant2))
			{
				TryApplyMobSkillStatus(caster, combatant2, plan.Source);
			}
		}
		return true;
	}

	private bool TryExecuteMobPhysicalSkill(Combatant caster, MobSkillPlan plan, Combatant? forcedTarget = null)
	{
		if (plan.Type == "call_ally" && _combatants.Count((Combatant target) => target.UsesMonsterTemplate && !IsEnemy(caster, target) && target.IsAlive) >= 3)
		{
			return false;
		}
		bool flag = MobPhysicalSkillIsRanged(caster, plan);
		double val = (plan.Name.Contains("矢", StringComparison.Ordinal) ? 480.0 : 72.0);
		double range = (flag ? Math.Max(caster.AttackRange, val) : caster.AttackRange);
		Combatant combatant = UsableForcedTarget(caster, forcedTarget) ?? SelectMobTarget(caster, range);
		if (combatant == null)
		{
			return false;
		}
		EmitMobSkillCast(caster, combatant, plan);
		double num = CombatSkill.ReadDouble(plan.Source, "areaHeight");
		if (num > 0.0)
		{
			Combatant[] array = BoxEnemies(caster, combatant, CombatSkill.ReadDouble(plan.Source, "areaWidth"), num);
			if (array.Length == 0)
			{
				return false;
			}
			Combatant[] array2 = array;
			foreach (Combatant combatant2 in array2)
			{
				if (caster.IsAlive && combatant2.IsAlive)
				{
					PhysicalHitResult result = CommitMobSkillPhysicalHit(caster, combatant2, plan.Source, flag);
					PhysicalHitResult result2 = ApplyCommittedPhysicalHit(caster, combatant2, result);
					TryApplyMobSkillStun(caster, combatant2, plan.Source, result2);
				}
			}
			return true;
		}
		PhysicalHitResult physicalHitResult = CommitMobSkillPhysicalHit(caster, combatant, plan.Source, flag);
		if (flag && MobPhysicalSkillUsesArrow(caster, plan))
		{
			CreateProjectile(caster, combatant, "arrow", basicAttack: false, magicWeaponAttack: false, physicalHitResult, 0.0);
			return true;
		}
		PhysicalHitResult result3 = ApplyCommittedPhysicalHit(caster, combatant, physicalHitResult);
		TryApplyMobSkillStun(caster, combatant, plan.Source, result3);
		return true;
	}

	private PhysicalHitResult CommitMobSkillPhysicalHit(Combatant caster, Combatant target, JsonObject source, bool ranged)
	{
		if (TryDarkStealthEvade(target))
		{
			return new PhysicalHitResult(Hit: false, 0.0, Critical: false, Heavy: false, ranged);
		}
		PhysicalHitResult physicalHitResult = RollPhysicalHit(caster, target, ranged, forceHeavy: false, forceCritical: false, 0.0);
		double num = MobSkillRules.DamageMultiplier(caster, source);
		if (!physicalHitResult.Hit || num == 1.0)
		{
			return physicalHitResult;
		}
		return physicalHitResult with
		{
			Damage = Math.Max(1.0, Math.Floor(physicalHitResult.Damage * num))
		};
	}

	private void TryApplyMobSkillStun(Combatant caster, Combatant target, JsonObject source, PhysicalHitResult result)
	{
		double num = Math.Max(0.0, CombatSkill.ReadDouble(source, "stunChance"));
		if (result.Hit && target.IsAlive && !(num <= 0.0) && !StatusRules.IsImmune(target, "stun") && !(_random.NextDouble() * 100.0 >= num))
		{
			TryApplyStatusCore(target, "stun", 60, null, resistanceChecked: true);
		}
	}

	private static bool MobPhysicalSkillIsRanged(Combatant caster, MobSkillPlan plan)
	{
		if (!caster.D.UsesRangedAttack && !plan.Name.Contains("矢", StringComparison.Ordinal) && !plan.Name.Contains("劍氣", StringComparison.Ordinal))
		{
			return plan.Name.Contains("波動", StringComparison.Ordinal);
		}
		return true;
	}

	private static bool MobPhysicalSkillUsesArrow(Combatant caster, MobSkillPlan plan)
	{
		if (!caster.D.UsesRangedAttack)
		{
			return plan.Name.Contains("矢", StringComparison.Ordinal);
		}
		return true;
	}

	private void EmitMobSkillCast(Combatant caster, Combatant target, MobSkillPlan plan)
	{
		_events.Add(CombatEvent.Cast(caster, plan.EventSkillId, target));
	}

	private bool TryExecuteMobSupportSkill(Combatant caster, MobSkillPlan plan, Combatant? forcedTarget, out Combatant presentationTarget)
	{
		presentationTarget = caster;
		switch (plan.Type)
		{
		case "self_heal":
		{
			if (caster.Hp >= caster.MaxHp)
			{
				return false;
			}
			double num2 = caster.Heal(RollMobHealing(plan.Source));
			if (num2 <= 0.0)
			{
				return false;
			}
			_events.Add(CombatEvent.Heal(caster, caster, num2));
			return true;
		}
		case "heal_target":
		{
			Combatant combatant = UsableForcedTarget(caster, forcedTarget);
			if (combatant == null || combatant.Kind != CombatantKind.Mob || combatant.Hp >= combatant.MaxHp)
			{
				return false;
			}
			double num = combatant.Heal(RollMobHealing(plan.Source));
			if (num <= 0.0)
			{
				return false;
			}
			_events.Add(CombatEvent.Heal(caster, combatant, num));
			presentationTarget = combatant;
			return true;
		}
		case "self_haste":
			_mobHasteIntervals[caster] = Math.Max(1.0 / 60.0, CombatSkill.ReadDouble(plan.Source, "spd", caster.D.AttackInterval / 1.3333333333333333));
			ApplyBuff(caster, "mob_self_haste", Math.Max(1.0 / 60.0, ReadMobSkillInt(plan.Source["dur"], 8)));
			return true;
		default:
			return false;
		}
	}

	private int RollMobHealing(JsonObject source)
	{
		if (source["healDice"] is JsonArray source2)
		{
			var (count, sides) = ReadMobSkillDice(source2);
			return _random.Roll(count, sides);
		}
		return 0;
	}

	private Combatant[] MobSkillTargets(Combatant caster, MobSkillPlan plan, Combatant? forcedTarget = null)
	{
		Combatant combatant = UsableForcedTarget(caster, forcedTarget);
		if (plan.Area)
		{
			double radius = ((plan.EffectRadius > 0.0) ? plan.EffectRadius : plan.CastRange);
			if (string.Equals(CombatSkill.ReadString(plan.Source, "aoeShape"), "line", StringComparison.Ordinal))
			{
				return ForwardLineEnemies(caster, CombatSkill.ReadDouble(plan.Source, "aoeCells", 4.0) * 48.0);
			}
			return AreaEnemies(caster, caster.Pos, radius);
		}
		Combatant combatant2 = combatant ?? SelectMobTarget(caster, plan.CastRange);
		if (combatant2 != null)
		{
			return new Combatant[1] { combatant2 };
		}
		return Array.Empty<Combatant>();
	}

	private Combatant? UsableForcedTarget(Combatant caster, Combatant? forcedTarget)
	{
		if (forcedTarget == null)
		{
			return null;
		}
		if (forcedTarget == caster)
		{
			return caster;
		}
		if (!forcedTarget.IsAlive || !_combatants.Contains(forcedTarget))
		{
			return null;
		}
		return forcedTarget;
	}

	private Combatant[] BoxEnemies(Combatant caster, Combatant target, double widthCells, double depthCells)
	{
		var (num, num2) = TileOffset(caster.Pos, target.Pos);
		if (num == 0.0 && num2 == 0.0)
		{
			return new Combatant[1] { target };
		}
		double num3 = Math.Round(Math.Atan2(num2, num) / (Math.PI / 4.0)) * (Math.PI / 4.0);
		double num4 = Math.Cos(num3);
		double num5 = Math.Sin(num3);
		List<Combatant> list = new List<Combatant>();
		foreach (Combatant combatant in _combatants)
		{
			if (!combatant.IsAlive || !IsEnemy(caster, combatant) || !HasCombatLineOfSight(caster, combatant))
			{
				continue;
			}
			(double A, double B) tuple2 = TileOffset(caster.Pos, combatant.Pos);
			double item = tuple2.A;
			double item2 = tuple2.B;
			double num6 = Math.Max(Math.Abs(item), Math.Abs(item2));
			if (num6 < 0.5)
			{
				list.Add(combatant);
			}
			else if (!(num6 > depthCells) || !(num6 > widthCells))
			{
				double num7 = Math.Round(item * num4 + item2 * num5);
				double value = Math.Round((0.0 - item) * num5 + item2 * num4);
				if (num7 > 0.0 && num6 <= depthCells && Math.Abs(value) <= widthCells)
				{
					list.Add(combatant);
				}
			}
		}
		return (from candidate in list
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	private static (double A, double B) TileOffset(WorldPoint from, WorldPoint to)
	{
		double num = to.X - from.X;
		double num2 = to.Y - from.Y;
		return (A: num / 48.0 + num2 / 24.0, B: num / 48.0 - num2 / 24.0);
	}

	private Combatant[] ForwardLineEnemies(Combatant caster, double length)
	{
		double safeLength = Math.Max(0.0, length);
		return (from candidate in _combatants
			where candidate.IsAlive && IsEnemy(caster, candidate) && HasCombatLineOfSight(caster, candidate) && CombatRangeRules.DiamondDistance(caster.Pos, candidate.Pos) <= safeLength && FacingFromVector(candidate.Pos.X - caster.Pos.X, candidate.Pos.Y - caster.Pos.Y) == caster.Facing8
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	private void ApplyMobSkillDamage(Combatant caster, Combatant target, JsonObject source)
	{
		if (BehaviorBuffRules.TryAbsorbMagicDamage(target))
		{
			_events.Add(CombatEvent.LogLine($"魔法屏障吸收了攻擊！（{3.0:0} 秒內無法再次施展）"));
			return;
		}
		(int Count, int Sides) tuple = ReadMobSkillDice(source["dmg"]);
		int item = tuple.Count;
		int item2 = tuple.Sides;
		if (!(source["dmg"] is JsonArray))
		{
			item = Math.Max(1, (int)(caster.D?.MeleeDamage ?? 10));
			item2 = Math.Max(item, (int)((caster.D?.MeleeDamage ?? 15) * 2));
		}
		double num = CombatSkill.ReadDouble(source, "db");
		if (CombatSkill.ReadBool(source, "dbLv"))
		{
			num += (double)caster.Level * Math.Max(1.0, CombatSkill.ReadDouble(source, "dbLvMult", 1.0));
		}
		string text = NormalizeMobSkillElement(CombatSkill.ReadString(source, "ele"));
		double num2 = ((double)_random.Roll(item, item2) + (double)MobSkillRules.RollCompanionSkillDamageCeilingBonus(_data, caster, _random) + num) * MobSkillRules.DamageMultiplier(caster, source);
		double num3 = CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target));
		double val = 1.0 - AttributeDefense(target, text);
		double num4 = Math.Max(1.0, Math.Floor(num2 * num3 * Math.Max(0.0, val)));
		num4 = Math.Max(1.0, num4 + (double)RollElementCounterDamage(text, target));
		num4 = Math.Max(1.0, Math.Floor(num4 * SkillBuffRules.IncomingDamageMultiplier(_data, target)));
		bool blocked;
		double appliedDamage = ApplyDirectDamage(caster, target, num4, DamageType.Magic, DirectDamageDelivery.ActiveSkill, out blocked, critical: false, text);
		if (!blocked)
		{
			ConsumeMagicResistanceReduction(target, appliedDamage);
			TryReflectMirror(target, caster, appliedDamage);
			if (target.IsAlive && source["sec"] is JsonObject source2)
			{
				TryApplyMobSkillStatus(caster, target, source2);
			}
			if (target.Dead)
			{
				ResolveDeath(target, caster);
			}
		}
	}

	private bool TryApplyMobSkillStatus(Combatant caster, Combatant target, JsonObject source)
	{
		string text = NormalizeMobStatus(CombatSkill.ReadString(source, "type"));
		if (text.Length == 0)
		{
			return false;
		}
		if (StatusRules.IsImmune(target, text))
		{
			return false;
		}
		double num = MobStatusChance(source, text, target);
		if (_random.NextDouble() >= num)
		{
			return false;
		}
		int durationTicks = Math.Max(1, ReadMobSkillInt(source["dur"], DefaultMobStatusSeconds(text)) * 10);
		PeriodicEffect periodicEffect = null;
		double num2 = CombatSkill.ReadDouble(source, "d");
		if (num2 > 0.0)
		{
			int num3 = Math.Max(1, ReadMobSkillInt(source["tick"], 3) * 10);
			periodicEffect = new PeriodicEffect
			{
				TickEvery = num3,
				TicksUntilNext = num3,
				Damage = Math.Max(1.0, Math.Floor(num2)),
				DamageType = DamageType.Dot,
				Element = NormalizeMobSkillElement(CombatSkill.ReadString(source, "ele")),
				Source = caster
			};
		}
		return TryApplyStatusCore(target, text, durationTicks, periodicEffect, resistanceChecked: true);
	}

	private double MobStatusChance(JsonObject source, string status, Combatant target)
	{
		bool flag = source["pbase"] != null;
		if (!flag && status == "burn")
		{
			return 1.0;
		}
		return Math.Clamp(((flag ? CombatSkill.ReadDouble(source, "pbase") : DefaultMobStatusPower(status)) - EffectiveMagicResist(target)) / 200.0, 0.0, 1.0);
	}

	private static int DefaultMobStatusSeconds(string status)
	{
		switch (status)
		{
		case "poison":
		case "burn":
		case "bleed":
			return 15;
		case "slowAtk":
		case "potionFrost":
		case "foulWater":
			return 8;
		case "weaken":
			return 15;
		case "disease":
			return 20;
		default:
			return 6;
		}
	}

	private static double DefaultMobStatusPower(string status)
	{
		int num;
		switch (status)
		{
		case "paralyze":
			num = 50;
			break;
		case "silence":
			num = 60;
			break;
		case "poison":
			num = 100;
			break;
		case "freeze":
		case "sleep":
			num = 200;
			break;
		default:
			num = 150;
			break;
		}
		return num;
	}

	private static string NormalizeMobStatus(string type)
	{
		return type switch
		{
			"poison" => "poison", 
			"burn" => "burn", 
			"bleed" => "bleed", 
			"stone" => "stone", 
			"paralyze" => "paralyze", 
			"silence" => "silence", 
			"magicseal" => "magicseal", 
			"freeze" => "freeze", 
			"sleep" => "sleep", 
			"stun" => "stun", 
			"slowatk" => "slowAtk", 
			"weaken" => "weaken", 
			"disease" => "disease", 
			"potionfrost" => "potionFrost", 
			"foulwater" => "foulWater", 
			_ => string.Empty, 
		};
	}

	private static string NormalizeMobSkillElement(string element)
	{
		return element.Trim().ToLowerInvariant() switch
		{
			"fire" => "fire", 
			"water" => "water", 
			"wind" => "wind", 
			"earth" => "earth", 
			_ => "none", 
		};
	}

	private static (int Count, int Sides) ReadMobSkillDice(JsonNode? source)
	{
		if (!(source is JsonArray { Count: >=2 } jsonArray))
		{
			return (Count: 1, Sides: 1);
		}
		return (Count: Math.Max(1, ReadMobSkillInt(jsonArray[0], 1)), Sides: Math.Max(1, ReadMobSkillInt(jsonArray[1], 1)));
	}

	private static int ReadMobSkillInt(JsonNode? node, int fallback)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return fallback;
		}
		return value;
	}

	private void CleanupMobSkillRuntime(Combatant combatant)
	{
		_mobSkillPlans.Remove(combatant);
		_mobSkillNextEvaluationStep.Remove(combatant);
		_mobSkillUseCounts.Remove(combatant);
		_mobHasteIntervals.Remove(combatant);
	}

	private bool TryExecuteMobSummon(Combatant caster, MobSkillPlan plan)
	{
		if (_data == null)
		{
			return false;
		}
		string text = CombatSkill.ReadString(plan.Source, "summonKey");
		if (text.Length == 0 || _data.Mob(text) == null)
		{
			return false;
		}
		int num = 0;
		foreach (Combatant combatant2 in _combatants)
		{
			if (combatant2.UsesMonsterTemplate && !IsEnemy(caster, combatant2) && combatant2.IsAlive)
			{
				num++;
			}
		}
		if (num >= 12)
		{
			return false;
		}
		int num2 = Math.Max(1, ReadMobSkillInt(plan.Source["summonMin"], 1));
		int num3 = Math.Max(num2, ReadMobSkillInt(plan.Source["summonMax"], num2));
		int num4 = Math.Min(num2 + (int)Math.Floor(Math.Clamp(_random.NextDouble(), 0.0, 0.9999999999999999) * (double)(num3 - num2 + 1)), 12 - num);
		if (num4 <= 0)
		{
			return false;
		}
		List<Combatant> list = new List<Combatant>(num4);
		for (int i = 0; i < num4; i++)
		{
			Combatant combatant = CombatantBuilder.CreateMob(_data, text, $"{caster.Key}{"~summon"}{++_mobSummonSequence}", 0, MobSummonPoint(caster), _random);
			if (caster.Kind == CombatantKind.Ally && caster.UsesMonsterTemplate)
			{
				combatant.Kind = CombatantKind.Ally;
				combatant.ClassId = "monster";
				combatant.ExperienceReward = 0.0;
				combatant.GoldMin = 0;
				combatant.GoldMax = 0;
				combatant.DropMultiplier = 0.0;
			}
			combatant.DropMultiplier = 0.0;
			combatant.GoldMin = 0;
			combatant.GoldMax = 0;
			Add(combatant);
			list.Add(combatant);
		}
		if (list.Count == 0)
		{
			return false;
		}
		EmitMobSkillCast(caster, caster, plan);
		return true;
	}

	private WorldPoint MobSummonPoint(Combatant caster)
	{
		for (int i = 0; i < 8; i++)
		{
			int num = (int)Math.Floor(_random.NextDouble() * 17.0) - 8;
			int num2 = (int)Math.Floor(_random.NextDouble() * 17.0) - 8;
			if (num != 0 || num2 != 0)
			{
				WorldPoint worldPoint = new WorldPoint(caster.Pos.X + 24.0 * (double)(num + num2), caster.Pos.Y + 12.0 * (double)(num - num2));
				if (CanNavigateTo(caster, worldPoint))
				{
					return worldPoint;
				}
			}
		}
		return caster.Pos;
	}

	internal bool TryAdvanceL1jMobTeleport(Combatant mob, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		ArgumentNullException.ThrowIfNull(target, "target");
		if (mob.Kind != CombatantKind.Mob || !mob.IsAlive || !target.IsAlive || !L1jMobTeleportRules.Enabled(_data, mob))
		{
			return false;
		}
		double cells = CombatRangeRules.DiamondDistance(mob.Pos, target.Pos) / 48.0;
		if (!_mobInitialTeleportsCompleted.Contains(mob) && L1jMobTeleportRules.InInitialDistance(cells) && TryL1jNearTeleport(mob, target))
		{
			_mobInitialTeleportsCompleted.Add(mob);
			return true;
		}
		if (_random.NextDouble() < 0.19 && mob.Mp >= 10.0 && L1jMobTeleportRules.InRepeatDistance(cells))
		{
			return TryL1jNearTeleport(mob, target);
		}
		return false;
	}

	private bool TryL1jNearTeleport(Combatant mob, Combatant target)
	{
		IsometricGridPoint isometricGridPoint = IsometricMovementRules.GridPointAt(target.Pos, _isometricLatticeOrigin);
		for (int i = 0; i < 2; i++)
		{
			int num = L1jTeleportOffset();
			int num2 = L1jTeleportOffset();
			WorldPoint worldPoint = IsometricMovementRules.WorldPointAt(new IsometricGridPoint(isometricGridPoint.AxisA + num, isometricGridPoint.AxisB + num2), _isometricLatticeOrigin);
			WorldBounds? worldBounds = _worldBounds;
			if ((!worldBounds.HasValue || !(worldBounds.GetValueOrDefault().Clamp(worldPoint) != worldPoint)) && IsExplorationWalkablePoint(worldPoint) && (_collisionGrid == null || _collisionGrid.CanOccupy(worldPoint, Math.Max(0.0, mob.Radius))) && !StepBlockedBySolidBody(mob, worldPoint))
			{
				mob.Pos = worldPoint;
				mob.Facing8 = L1jRandomIndex(8);
				mob.MoveTarget = null;
				mob.VelX = 0.0;
				mob.VelY = 0.0;
				_navigationPaths.Remove(mob);
				_explorationNavigationPaths.Remove(mob);
				_isometricSteps.Remove(mob);
				_sidestepOrigins.Remove(mob);
				_renderPreviousPositions.Remove(mob);
				ResetIdleWander(mob);
				double mp = mob.Mp;
				mob.Mp = Math.Max(0.0, mp - 10.0);
				double num3 = mp - mob.Mp;
				if (num3 > 0.0)
				{
					_events.Add(CombatEvent.MpChange(mob, 0.0 - num3));
				}
				_events.Add(CombatEvent.Move(mob));
				return true;
			}
		}
		return false;
	}

	private int L1jTeleportOffset()
	{
		return L1jRandomIndex(7) - 3;
	}

	private int L1jRandomIndex(int exclusiveMaximum)
	{
		return Math.Min(exclusiveMaximum - 1, (int)Math.Floor(_random.NextDouble() * (double)exclusiveMaximum));
	}

	private void CleanupL1jMobTeleportRuntime(Combatant combatant)
	{
		_mobInitialTeleportsCompleted.Remove(combatant);
	}

	private bool TryTransformDefeatedMob(Combatant mob)
	{
		if (!mob.Dead || !TryGetMobTransformation(mob, out MobTransformationTransition transition) || (object)transition == null)
		{
			return false;
		}
		TransformMob(mob, transition);
		return true;
	}

	private bool TryGetMobTransformation(Combatant mob, out MobTransformationTransition? transition)
	{
		transition = null;
		if (_data == null || mob.Kind != CombatantKind.Mob || string.IsNullOrWhiteSpace(mob.Avatar))
		{
			return false;
		}
		if (!_mobTransformationCache.TryGetValue(mob.Avatar, out transition))
		{
			MobTransformationRules.TryResolveNext(_data, mob.Avatar, out transition);
			_mobTransformationCache[mob.Avatar] = transition;
		}
		return (object)transition != null;
	}

	private void TransformMob(Combatant current, MobTransformationTransition transition)
	{
		if (_data != null && _combatants.Contains(current))
		{
			WorldPoint pos = current.Pos;
			int bornSeq = current.BornSeq;
			bool isBoss = current.IsBoss;
			string nextInstanceKey = ((bornSeq > 0) ? $"{transition.NextMobKey}#{bornSeq}" : (transition.NextMobKey + "@" + current.Key));
			if (_combatants.Any((Combatant combatant2) => combatant2 != current && string.Equals(combatant2.Key, nextInstanceKey, StringComparison.Ordinal)))
			{
				throw new InvalidOperationException("Mob transform target instance '" + nextInstanceKey + "' already exists.");
			}
			_events.Add(CombatEvent.Cast(current, "mob_transform:" + transition.NextMobKey, current));
			current.Hp = 0.0;
			current.Dead = true;
			Remove(current);
			Combatant combatant = CombatantBuilder.CreateMob(_data, transition.NextMobKey, nextInstanceKey, bornSeq, pos, _random);
			if (isBoss)
			{
				combatant.IsBoss = true;
			}
			_events.Add(CombatEvent.LogLine(current.Disp + "變身為" + combatant.Disp + "！"));
			Add(combatant);
		}
	}

	public MonsterCompanionPotionUseResult TryUseMonsterCompanionPotion(Combatant owner, Combatant companion, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(companion, "companion");
		if (_data == null || !_combatants.Contains(owner) || !_combatants.Contains(companion))
		{
			return default(MonsterCompanionPotionUseResult);
		}
		MonsterCompanionPotionUseResult result = MonsterCompanionPotionRules.TryUse(_data, owner, companion, itemKey, _random);
		if (result.Success && result.HpRestored > 0.0)
		{
			_events.Add(CombatEvent.Heal(owner, companion, result.HpRestored));
		}
		return result;
	}

	private void RegisterMonsterKill(Combatant dead, Combatant recipient)
	{
		if (_data != null && dead.Kind == CombatantKind.Mob)
		{
			string text = MonsterCardRules.ResolveMobKey(_data, dead);
			if (text.Length != 0)
			{
				recipient.Progress.Collections?.RegisterKill(text);
				NpcActionCatalog.RegisterMonsterKill(_data, recipient, text);
			}
		}
	}

	public bool HasLineOfSight(WorldPoint from, WorldPoint to)
	{
		if (HasExplorationLineOfSight(from, to))
		{
			return _collisionGrid?.CanTraverseSegment(from, to) ?? true;
		}
		return false;
	}

	private bool CanNavigateTo(Combatant combatant, WorldPoint destination)
	{
		if (CanReachExplorationPoint(combatant.Pos, destination))
		{
			return _collisionGrid?.CanReach(combatant.Pos, destination, Math.Max(0.0, combatant.Radius)) ?? true;
		}
		return false;
	}

	private bool HasCombatLineOfSight(Combatant source, Combatant target)
	{
		if (CanHostileInteract(source, target) && HasLineOfSight(source.Pos, target.Pos))
		{
			if (source.Kind == CombatantKind.Player)
			{
				double? playerVisionLimit = PlayerVisionLimit;
				if (playerVisionLimit.HasValue)
				{
					double valueOrDefault = playerVisionLimit.GetValueOrDefault();
					return WithinVisionEllipse(source.Pos, target.Pos, valueOrDefault);
				}
			}
			return true;
		}
		return false;
	}

	private bool WithinVisionEllipse(WorldPoint from, WorldPoint to, double limit)
	{
		double num = to.X - from.X;
		double num2 = (to.Y - from.Y) * PlayerVisionAspectY;
		return num * num + num2 * num2 <= limit * limit;
	}

	public void SetCollisionGrid(WorldCollisionGrid? collisionGrid)
	{
		_collisionGrid = collisionGrid;
		_navigationPaths.Clear();
		_explorationNavigationPaths.Clear();
		if (collisionGrid == null)
		{
			return;
		}
		foreach (Combatant combatant in _combatants)
		{
			combatant.Pos = SnapToWalkable(combatant.Pos, combatant.Radius);
			WorldPoint? moveTarget = combatant.MoveTarget;
			if (moveTarget.HasValue)
			{
				WorldPoint valueOrDefault = moveTarget.GetValueOrDefault();
				combatant.MoveTarget = SnapToWalkable(valueOrDefault, combatant.Radius);
			}
		}
	}

	private WorldPoint ClampAndSnapPlacement(WorldPoint point, double radius)
	{
		return SnapToExplorationWalkablePoint(ClampAndSnapToWalkable(point, radius));
	}

	private WorldPoint ClampAndSnapToWalkable(WorldPoint point, double radius)
	{
		WorldPoint point2 = _worldBounds?.Clamp(point) ?? point;
		return SnapToWalkable(point2, radius);
	}

	private WorldPoint SnapToWalkable(WorldPoint point, double radius)
	{
		if (_collisionGrid == null)
		{
			return point;
		}
		if (_collisionGrid.TryFindNearestWalkable(point, Math.Max(0.0, radius), out var walkable))
		{
			return walkable;
		}
		throw new InvalidOperationException("The collision grid has no walkable cell large enough for this combatant.");
	}

	private WorldPoint NavigationWaypoint(Combatant combatant, WorldPoint destination)
	{
		if (_explorationNavigation != null)
		{
			return ExplorationNavigationWaypoint(combatant, destination);
		}
		WorldCollisionGrid collisionGrid = _collisionGrid;
		if (collisionGrid == null)
		{
			return destination;
		}
		double radius = Math.Max(0.0, combatant.Radius);
		if (collisionGrid.CanTraverseSegment(combatant.Pos, destination, radius))
		{
			_navigationPaths.Remove(combatant);
			return destination;
		}
		if (!collisionGrid.TryFindNearestWalkable(destination, radius, out var walkable))
		{
			_navigationPaths.Remove(combatant);
			return combatant.Pos;
		}
		WorldGridCell worldGridCell = collisionGrid.CellAt(walkable);
		if (!_navigationPaths.TryGetValue(combatant, out NavigationPathState value) || value.Goal != worldGridCell || value.Index >= value.Points.Count || !collisionGrid.CanTraverseSegment(combatant.Pos, value.Points[value.Index], radius))
		{
			IReadOnlyList<WorldPoint> readOnlyList = collisionGrid.FindPath(combatant.Pos, walkable, radius);
			if (readOnlyList.Count == 0)
			{
				_navigationPaths.Remove(combatant);
				return combatant.Pos;
			}
			value = new NavigationPathState(worldGridCell, readOnlyList, Math.Min(1, readOnlyList.Count - 1));
			_navigationPaths[combatant] = value;
		}
		double num = Math.Max(2.0, collisionGrid.CellSize * 0.12);
		double num2 = num * num;
		WorldPoint worldPoint = SnapToWalkableIsometricPoint(value.Points[value.Index], radius);
		while (value.Index < value.Points.Count - 1 && combatant.Pos.DistanceSquaredTo(worldPoint) <= num2)
		{
			value.Index++;
			worldPoint = SnapToWalkableIsometricPoint(value.Points[value.Index], radius);
		}
		return worldPoint;
	}

	public IReadOnlyList<Combatant> ActiveNecroSkeletons()
	{
		return (from candidate in _combatants.Where(IsNecroSkeleton)
			where candidate.IsAlive
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	public bool IsNecroSkeleton(Combatant candidate)
	{
		ArgumentNullException.ThrowIfNull(candidate, "candidate");
		if (candidate.Kind == CombatantKind.Summon)
		{
			return string.Equals(_summonSkillIds.GetValueOrDefault(candidate), "_necro_skeleton", StringComparison.Ordinal);
		}
		return false;
	}

	public bool HasActiveStandardSummonContract(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return _combatants.Any((Combatant candidate) => candidate.IsAlive && _summonOwners.GetValueOrDefault(candidate) == owner && SummonRules.SkillIds.Contains(_summonSkillIds.GetValueOrDefault(candidate) ?? string.Empty));
	}

	private bool NecromancyContractActive(Combatant owner)
	{
		Combatant combatant = _combatants.FirstOrDefault((Combatant candidate) => candidate.Kind == CombatantKind.Player);
		if (combatant != null && combatant.IsAlive && _combatants.Contains(owner))
		{
			return NecromancyRules.PassiveEnabled(_data, owner);
		}
		return false;
	}

	private void TryApplyNecromancyOnDefeat(Combatant defeated, Combatant? killer)
	{
		if (_data == null || defeated.Kind != CombatantKind.Mob || string.Equals(defeated.Race, "建築", StringComparison.Ordinal) || killer == null || killer.Kind == CombatantKind.Mob)
		{
			return;
		}
		Combatant[] array = (from candidate in _combatants.Where(delegate(Combatant candidate)
			{
				bool flag = candidate.IsAlive;
				if (flag)
				{
					CombatantKind kind = candidate.Kind;
					bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
					flag = flag2;
				}
				return flag && NecromancyRules.IsBookEquipped(_data, candidate);
			})
			orderby (candidate.Kind != CombatantKind.Player) ? 1 : 0, candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
		if (array.Length == 0)
		{
			return;
		}
		HealNecromancyTeam(array);
		Combatant[] array2 = array.Where((Combatant holder) => NecromancyRules.PassiveEnabled(_data, holder) && NecromancyRules.TryCreateSkeletonPlan(_data, holder, out SummonUnitPlan _)).ToArray();
		RefreshNecroSkeletonTiers();
		IReadOnlyList<Combatant> living = ActiveNecroSkeletons();
		int num = NecromancyRules.MaximumSkeletons(_data);
		if (array2.Length != 0 && living.Count < num)
		{
			Combatant owner = (from holder in array2
				orderby living.Count((Combatant skeleton) => _summonOwners.GetValueOrDefault(skeleton) == holder), (holder.Kind != CombatantKind.Player) ? 1 : 0, holder.BornSeq, _combatants.IndexOf(holder)
				select holder).First();
			SpawnNecroSkeleton(owner, defeated, living.Count, num);
		}
		else if (living.Count >= num)
		{
			Combatant combatant = (from skeleton in living
				orderby skeleton.Hp, skeleton.BornSeq, _combatants.IndexOf(skeleton)
				select skeleton).First();
			double num2 = combatant.Heal(combatant.MaxHp);
			if (num2 > 0.0)
			{
				Combatant src = _summonOwners.GetValueOrDefault(combatant) ?? array[0];
				_events.Add(CombatEvent.Heal(src, combatant, num2));
			}
		}
	}

	private void HealNecromancyTeam(IReadOnlyList<Combatant> holders)
	{
		Combatant combatant = holders.OrderByDescending((Combatant holder) => NecromancyRules.TeamHealPercent(_data, holder)).First();
		double num = NecromancyRules.TeamHealPercent(_data, combatant);
		if (num <= 0.0)
		{
			return;
		}
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant2 in array)
		{
			if (combatant2.IsAlive && combatant2.Kind != CombatantKind.Mob)
			{
				double amount = Math.Max(1.0, Math.Floor(combatant2.MaxHp * num / 100.0));
				double num3 = combatant2.Heal(amount);
				if (num3 > 0.0)
				{
					_events.Add(CombatEvent.Heal(combatant, combatant2, num3));
				}
			}
		}
	}

	private void SpawnNecroSkeleton(Combatant owner, Combatant defeated, int currentCount, int maximum)
	{
		if (_data != null && NecromancyRules.TryCreateSkeletonPlan(_data, owner, out SummonUnitPlan plan) && (object)plan != null)
		{
			int bornSeq = _combatants.Select((Combatant actor) => actor.BornSeq).DefaultIfEmpty(0).Max() + 1;
			WorldPoint worldPoint = SummonRules.FormationPoint(owner, currentCount, maximum);
			Combatant combatant = SummonRules.CreateCombatant(plan, owner, $"necro:{owner.Key}:{++_nextSummonId}", bornSeq, _worldBounds?.Clamp(worldPoint) ?? worldPoint);
			combatant.Disp = "骷髏";
			combatant.Avatar = "骷髏召喚物";
			bool flag = NecromancyRules.IsBookEquipped(_data, owner);
			if (!flag)
			{
				combatant.Level = Math.Max(1, defeated.Level);
				combatant.Counters["_spec_skeleton_level"] = combatant.Level;
			}
			_summonOwners[combatant] = owner;
			_summonSkillIds[combatant] = "_necro_skeleton";
			_summonExpiresAt[combatant] = double.PositiveInfinity;
			_events.Add(CombatEvent.Cast(owner, "sk_zombie", defeated));
			Add(combatant);
			_events.Add(CombatEvent.LogLine(owner.Disp + "的" + (flag ? "死靈之書" : "復甦骷髏") + $"喚起了骷髏 Lv.{combatant.Level}" + $"（{currentCount + 1}/{maximum}）。"));
		}
	}

	private void RefreshNecroSkeletonTiers()
	{
		if (_data == null)
		{
			return;
		}
		foreach (Combatant item in ActiveNecroSkeletons())
		{
			Combatant valueOrDefault = _summonOwners.GetValueOrDefault(item);
			if (valueOrDefault != null && NecromancyRules.TryCreateSkeletonPlan(_data, valueOrDefault, out SummonUnitPlan plan) && (object)plan != null)
			{
				double num = Math.Clamp(item.Hp / Math.Max(1.0, item.MaxHp), 0.0, 1.0);
				int valueOrDefault2 = item.Counters.GetValueOrDefault("_spec_skeleton_level");
				item.Level = ((valueOrDefault2 > 0) ? valueOrDefault2 : Math.Max(1, plan.Level));
				item.MaxHp = Math.Max(1.0, plan.MaxHp);
				item.Hp = Math.Max(1.0, Math.Round(item.MaxHp * num));
				item.AttackRange = plan.AttackRange;
				item.Element = plan.Element;
				item.AttackElement = plan.Element;
				item.D.AttackInterval = plan.AttackIntervalSeconds;
				item.D.ArmorClass = plan.ArmorClass;
				item.D.DamageReduction = plan.DamageReduction;
				item.D.MeleeHit = plan.MeleeHit;
				item.D.Hit = plan.MeleeHit;
				item.D.MeleeDamage = plan.MeleeDamage;
				item.D.AttackDiceSmall = Math.Max(1, plan.AttackDice);
				item.D.AttackDiceLarge = Math.Max(1, plan.AttackDice);
			}
		}
	}

	private void TryReflectPain(Combatant defender, Combatant attacker, double appliedDamage, DamageType damageType)
	{
		if (!PainReflectRules.Reflects(defender, attacker, damageType, appliedDamage))
		{
			return;
		}
		double amount = AdjustIncomingDamage(attacker, appliedDamage);
		double num = attacker.ApplyDamage(amount);
		if (!(num <= 0.0))
		{
			_events.Add(CombatEvent.Damage(defender, attacker, num, DamageType.Magic));
			_events.Add(CombatEvent.LogLine($"【疼痛的歡愉】{defender.Disp} 將痛楚化為反擊，對 {attacker.Disp} 造成 {num:0} 點傷害。"));
			if (attacker.Dead)
			{
				ResolveDeath(attacker, defender);
			}
		}
	}

	private void TryReflectTitan(Combatant defender, Combatant attacker, double appliedDamage, DamageType damageType)
	{
		bool flag = appliedDamage <= 0.0 || !attacker.IsAlive || attacker.Kind != CombatantKind.Mob;
		if (!flag)
		{
			CombatantKind kind = defender.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(defender);
		}
		if (flag || !WarriorPassiveRules.ReflectsDamage(defender, damageType))
		{
			return;
		}
		double amount = AdjustIncomingDamage(attacker, appliedDamage);
		double num = attacker.ApplyDamage(amount);
		if (!(num <= 0.0))
		{
			string value = ((damageType == DamageType.Magic) ? "泰坦：魔法" : "泰坦：岩石");
			_events.Add(CombatEvent.Damage(defender, attacker, num, DamageType.Magic));
			_events.Add(CombatEvent.LogLine($"【{value}】{defender.Disp} 反射相同傷害，對 {attacker.Disp} 造成 {num:0} 點傷害。"));
			if (attacker.Dead)
			{
				ResolveDeath(attacker, defender);
			}
		}
	}

	private void TryReflectMirror(Combatant defender, Combatant attacker, double appliedDamage)
	{
		bool flag = !attacker.IsAlive || attacker.Kind != CombatantKind.Mob;
		if (!flag)
		{
			CombatantKind kind = defender.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(defender);
		}
		if (flag || !BehaviorBuffRules.MirrorReflects(defender, appliedDamage, _random))
		{
			return;
		}
		double num = attacker.ApplyDamage(appliedDamage);
		if (!(num <= 0.0))
		{
			_events.Add(CombatEvent.Damage(defender, attacker, num, DamageType.Magic));
			_events.Add(CombatEvent.LogLine($"【鏡反射】{defender.Disp} 將 {num:0} 點傷害原樣返還給 {attacker.Disp}！"));
			if (attacker.Dead)
			{
				ResolveDeath(attacker, defender);
			}
		}
	}

	private void TryReflectDeadlyBody(Combatant defender, Combatant attacker, double appliedDamage, DamageType damageType)
	{
		bool flag = appliedDamage <= 0.0 || !attacker.IsAlive || attacker.Kind != CombatantKind.Mob;
		if (!flag)
		{
			CombatantKind kind = defender.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(defender);
		}
		bool flag3 = flag;
		if (!flag3)
		{
			bool flag2 = (uint)damageType <= 2u;
			flag3 = !flag2;
		}
		if (flag3 || !BehaviorBuffRules.DeadlyBodyReflects(defender, appliedDamage, _random))
		{
			return;
		}
		double amount = AdjustIncomingDamage(attacker, appliedDamage);
		double num = attacker.ApplyDamage(amount);
		if (!(num <= 0.0))
		{
			_events.Add(CombatEvent.Damage(defender, attacker, num, DamageType.Magic));
			_events.Add(CombatEvent.LogLine($"【致命身軀】{defender.Disp} 反射相同傷害，對 {attacker.Disp} 造成 {num:0} 點傷害。"));
			if (attacker.Dead)
			{
				ResolveDeath(attacker, defender);
			}
		}
	}

	public void MarkPainwandMob(Combatant mob)
	{
		ArgumentNullException.ThrowIfNull(mob, "mob");
		if (mob.Kind != CombatantKind.Mob || !_combatants.Contains(mob))
		{
			throw new InvalidOperationException("Painwand mob must be a live hostile mob in this engine.");
		}
		_painwandMobExpiresAt[mob] = CurrentTimeSeconds + 60.0;
	}

	public bool IsPainwandMob(Combatant mob)
	{
		return _painwandMobExpiresAt.ContainsKey(mob);
	}

	private void RemoveExpiredPainwandMobs()
	{
		KeyValuePair<Combatant, double>[] array = _painwandMobExpiresAt.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<Combatant, double> keyValuePair = array[i];
			var (combatant2, num2) = keyValuePair;
			if (!(CurrentTimeSeconds + 1E-09 < num2) || !combatant2.IsAlive)
			{
				_painwandMobExpiresAt.Remove(combatant2);
				if (combatant2.IsAlive)
				{
					Remove(combatant2);
				}
			}
		}
	}

	public IReadOnlyList<Combatant> ActivePetsOf(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return (from candidate in _combatants
			where candidate.IsAlive && _petOwners.GetValueOrDefault(candidate) == owner
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	public Combatant? PetOwnerOf(Combatant pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		return _petOwners.GetValueOrDefault(pet);
	}

	public PetInstance? PetInstanceOf(Combatant pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		return _petInstances.GetValueOrDefault(pet);
	}

	public PetAcquisitionResult TryTamePet(PetRoster roster, Combatant owner, Combatant target, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(target, "target");
		PetAcquisitionResult result = PetAcquisitionRules.TryGiveTamingItem(_data ?? throw new InvalidOperationException("Pet taming requires game data."), roster, owner, target, itemUid, _random);
		if (!result.Success || result.Pet == null)
		{
			return result;
		}
		Remove(target);
		return result;
	}

	public int CallActivePets(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		Combatant[] array = (from pair in _petInstances
			where _petOwners.GetValueOrDefault(pair.Key) == owner
			select pair.Key into pet
			where pet.IsAlive && _combatants.Contains(pet)
			select pet).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			array[num].Pos = PetRules.FormationPoint(owner, num, array.Length);
			array[num].MoveTarget = null;
			if (_petInstances.TryGetValue(array[num], out PetInstance value))
			{
				value.CommandStatus = PetCommandStatus.Whistle;
			}
			ClearPetHate(array[num]);
		}
		return array.Length;
	}

	public Combatant DeployPet(IGameData data, Combatant owner, PetInstance instance, int index, int count)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(instance, "instance");
		if (!_combatants.Contains(owner) || owner.Kind != CombatantKind.Player || !string.Equals(instance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The deployed pet must be assigned to the active player.");
		}
		string key = "pet:" + instance.Uid;
		Combatant combatant = _combatants.FirstOrDefault((Combatant actor) => string.Equals(actor.Key, key, StringComparison.Ordinal));
		if (combatant != null)
		{
			if (combatant.Kind != CombatantKind.Pet || _petInstances.GetValueOrDefault(combatant) != instance)
			{
				throw new InvalidOperationException("Combatant key '" + key + "' is already in use.");
			}
			return combatant;
		}
		int bornSeq = _combatants.Select((Combatant actor) => actor.BornSeq).DefaultIfEmpty(0).Max() + 1;
		WorldPoint worldPoint = PetRules.FormationPoint(owner, index, count);
		PetDerivedStats petDerivedStats = PetRules.Derive(data, instance, owner);
		Combatant combatant2 = new Combatant
		{
			Kind = CombatantKind.Pet,
			Key = key,
			Disp = instance.DisplayName,
			Avatar = petDerivedStats.MobKey,
			Level = instance.Level,
			Experience = instance.Experience,
			BornSeq = bornSeq,
			Pos = (_worldBounds?.Clamp(worldPoint) ?? worldPoint),
			Radius = 20.0,
			MoveSpeed = petDerivedStats.MoveSpeed,
			AttackRange = petDerivedStats.AttackRange,
			AggroRange = 480.0,
			Hp = Math.Clamp(instance.Hp, 0.0, petDerivedStats.EffectiveMaxHp),
			Dead = (instance.Downed || instance.Hp <= 0.0),
			MaxHp = petDerivedStats.EffectiveMaxHp,
			Mp = Math.Clamp(instance.Mp, 0.0, petDerivedStats.EffectiveMaxMp),
			MaxMp = petDerivedStats.EffectiveMaxMp,
			Alignment = petDerivedStats.Lawful,
			Size = "S",
			BasicProjectileKind = petDerivedStats.ProjectileKind,
			MobHealthRegenIntervalSeconds = petDerivedStats.HealthRegenIntervalSeconds,
			MobHealthRegenAmount = petDerivedStats.HealthRegen,
			MobManaRegenIntervalSeconds = petDerivedStats.ManaRegenIntervalSeconds,
			MobManaRegenAmount = petDerivedStats.ManaRegen,
			Base = new Attributes
			{
				Str = petDerivedStats.Strength,
				Con = petDerivedStats.Constitution,
				Dex = petDerivedStats.Dexterity,
				Int = petDerivedStats.Intelligence,
				Wis = petDerivedStats.Wisdom
			},
			D = 
			{
				AttackInterval = petDerivedStats.AttackIntervalSeconds,
				MeleeHit = petDerivedStats.Hit,
				Hit = petDerivedStats.Hit,
				MeleeDamage = petDerivedStats.FlatDamage,
				AttackDiceSmall = petDerivedStats.AttackDice,
				AttackDiceLarge = petDerivedStats.AttackDice,
				ArmorClass = petDerivedStats.ArmorClass,
				DamageReduction = petDerivedStats.DamageReduction,
				EvasionRating = petDerivedStats.EvasionRating,
				MagicResist = petDerivedStats.MagicResist,
				Str = petDerivedStats.Strength,
				Con = petDerivedStats.Constitution,
				Dex = petDerivedStats.Dexterity,
				Int = petDerivedStats.Intelligence,
				Wis = petDerivedStats.Wisdom,
				ItemSpellPower = petDerivedStats.SpellPower,
				UsesRangedAttack = petDerivedStats.UsesRangedAttack,
				HitstunTicks = petDerivedStats.HitstunTicks
			}
		};
		_petOwners[combatant2] = owner;
		_petInstances[combatant2] = instance;
		_petProfiles[combatant2] = petDerivedStats;
		_petRegenElapsed[combatant2] = 0.0;
		_petManaRegenElapsed[combatant2] = 0.0;
		if (instance.CommandStatus == PetCommandStatus.Alert)
		{
			_petAlertHomes[combatant2] = combatant2.Pos;
		}
		Add(combatant2);
		return combatant2;
	}

	public bool SynchronizePet(PetInstance instance)
	{
		ArgumentNullException.ThrowIfNull(instance, "instance");
		Combatant key = _petInstances.FirstOrDefault<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => pair.Value == instance).Key;
		if (key == null)
		{
			return false;
		}
		instance.Level = key.Level;
		instance.Experience = key.Experience;
		instance.Hp = Math.Clamp(key.Hp, 0.0, key.MaxHp);
		instance.Downed = key.Dead;
		instance.Mp = Math.Max(0.0, key.Mp);
		instance.Lawful = (int)Math.Truncate(key.Alignment);
		return true;
	}

	public int RefreshPetProfiles()
	{
		if (_data == null)
		{
			return 0;
		}
		int num = 0;
		KeyValuePair<Combatant, PetInstance>[] array = _petInstances.Where<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => _combatants.Contains(pair.Key)).ToArray();
		foreach (KeyValuePair<Combatant, PetInstance> keyValuePair in array)
		{
			keyValuePair.Deconstruct(out var key, out var value);
			Combatant combatant = key;
			PetInstance petInstance = value;
			Combatant valueOrDefault = _petOwners.GetValueOrDefault(combatant);
			if (valueOrDefault != null)
			{
				PetDerivedStats petDerivedStats = PetRules.Derive(_data, petInstance, valueOrDefault);
				_petProfiles[combatant] = petDerivedStats;
				combatant.MaxHp = petDerivedStats.EffectiveMaxHp;
				combatant.MaxMp = petDerivedStats.EffectiveMaxMp;
				combatant.Hp = Math.Clamp(combatant.Hp, 0.0, combatant.MaxHp);
				combatant.Mp = Math.Clamp(combatant.Mp, 0.0, combatant.MaxMp);
				combatant.D.AttackInterval = petDerivedStats.AttackIntervalSeconds;
				combatant.D.MeleeHit = petDerivedStats.Hit;
				combatant.D.Hit = petDerivedStats.Hit;
				combatant.D.MeleeDamage = petDerivedStats.FlatDamage;
				combatant.D.AttackDiceSmall = petDerivedStats.AttackDice;
				combatant.D.AttackDiceLarge = petDerivedStats.AttackDice;
				combatant.D.ArmorClass = petDerivedStats.ArmorClass;
				combatant.D.DamageReduction = petDerivedStats.DamageReduction;
				combatant.D.EvasionRating = petDerivedStats.EvasionRating;
				combatant.D.MagicResist = petDerivedStats.MagicResist;
				combatant.D.Str = petDerivedStats.Strength;
				combatant.D.Con = petDerivedStats.Constitution;
				combatant.D.Dex = petDerivedStats.Dexterity;
				combatant.D.Int = petDerivedStats.Intelligence;
				combatant.D.Wis = petDerivedStats.Wisdom;
				combatant.D.ItemSpellPower = petDerivedStats.SpellPower;
				combatant.D.UsesRangedAttack = petDerivedStats.UsesRangedAttack;
				combatant.MoveSpeed = petDerivedStats.MoveSpeed;
				combatant.AttackRange = petDerivedStats.AttackRange;
				combatant.BasicProjectileKind = petDerivedStats.ProjectileKind;
				combatant.MobHealthRegenIntervalSeconds = petDerivedStats.HealthRegenIntervalSeconds;
				combatant.MobHealthRegenAmount = petDerivedStats.HealthRegen;
				combatant.MobManaRegenIntervalSeconds = petDerivedStats.ManaRegenIntervalSeconds;
				combatant.MobManaRegenAmount = petDerivedStats.ManaRegen;
				petInstance.Hp = combatant.Hp;
				petInstance.Mp = combatant.Mp;
				num++;
			}
		}
		return num;
	}

	public int DismissPets(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		Combatant[] array = (from pair in _petOwners
			where pair.Value == owner
			select pair.Key).Where(_combatants.Contains).ToArray();
		Combatant[] array2 = array;
		foreach (Combatant combatant in array2)
		{
			if (_petInstances.TryGetValue(combatant, out PetInstance value))
			{
				SynchronizePet(value);
			}
			Remove(combatant);
		}
		return array.Length;
	}

	public bool DismissPet(PetInstance instance)
	{
		ArgumentNullException.ThrowIfNull(instance, "instance");
		Combatant key = _petInstances.FirstOrDefault<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => pair.Value == instance).Key;
		if (key == null || !_combatants.Contains(key))
		{
			return false;
		}
		SynchronizePet(instance);
		return Remove(key);
	}

	public Combatant? LiberatePet(PetRoster roster, Combatant owner, PetInstance instance)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(instance, "instance");
		if (_data == null || !string.Equals(instance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			return null;
		}
		Combatant key = _petInstances.FirstOrDefault<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => pair.Value == instance).Key;
		if (key == null || !_combatants.Contains(key) || _petOwners.GetValueOrDefault(key) != owner)
		{
			return null;
		}
		ItemStack itemStack = PetCollarRules.FindCollar(_data, owner.InventoryStacks, instance.Uid);
		if (itemStack == null)
		{
			return null;
		}
		SynchronizePet(instance);
		PetDerivedStats petDerivedStats = _petProfiles[key];
		Combatant combatant = CombatantBuilder.CreateMob(_data, petDerivedStats.MobKey, "released:" + instance.Uid, key.BornSeq, key.Pos);
		combatant.Disp = instance.DisplayName;
		combatant.Level = instance.Level;
		combatant.Experience = instance.Experience;
		combatant.MaxHp = key.MaxHp;
		combatant.Hp = key.Hp;
		combatant.MaxMp = key.MaxMp;
		combatant.Mp = key.Mp;
		combatant.Dead = key.Dead;
		combatant.DropMultiplier = 0.0;
		List<ItemStack> list = (from item in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select item.Copy()).ToList();
		if (!ItemStackInventory.TryRemoveByUid(list, itemStack.Uid, 1L, out ItemStack removed, includeLocked: true))
		{
			return null;
		}
		foreach (ItemStack value in instance.Equipment.Values)
		{
			if (!ItemStackInventory.TryAddOrStack(list, value.Copy(), out removed))
			{
				return null;
			}
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		instance.Equipment.Clear();
		if (!roster.DeletePet(instance.Uid))
		{
			throw new InvalidOperationException("Unable to delete liberated pet record.");
		}
		Remove(key);
		Add(combatant);
		return combatant;
	}

	public Combatant ReloadPet(Combatant owner, PetInstance instance)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(instance, "instance");
		Combatant key = _petInstances.FirstOrDefault<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => pair.Value == instance).Key;
		if (key != null)
		{
			Remove(key);
		}
		PetInstance[] array = _petInstances.Values.Append(instance).Distinct().ToArray();
		return DeployPet(_data ?? throw new InvalidOperationException("Pet reload requires game data."), owner, instance, Math.Max(0, Array.IndexOf(array, instance)), array.Length);
	}

	public double PetReviveRemainingSeconds(Combatant pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		if (pet.Kind != CombatantKind.Pet || !pet.Dead || !_petReviveReadyAt.TryGetValue(pet, out var value))
		{
			return 0.0;
		}
		return Math.Max(0.0, value - CurrentTimeSeconds);
	}

	public bool TryRevivePetWithScroll(Combatant pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		Combatant combatant = PartyLeader();
		if (!CanRevivePet(pet) || !_petOwners.TryGetValue(pet, out Combatant value) || !CombatInventory.TryRemove(combatant ?? value, "scroll_revive", 1L))
		{
			return false;
		}
		RevivePetCore(pet, 0.25);
		return true;
	}

	public int RevivePetsAtTown()
	{
		return 0;
	}

	private void AdvancePetMovement(Combatant pet, double deltaSeconds, double moveSpeed)
	{
		Combatant owner = _petOwners.GetValueOrDefault(pet);
		if (owner == null || !_combatants.Contains(owner) || !_petInstances.TryGetValue(pet, out PetInstance value))
		{
			return;
		}
		Combatant[] array = (from pair in _petOwners
			where pair.Value == owner && _combatants.Contains(pair.Key)
			select pair.Key into candidate
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
		int num = Math.Max(0, Array.IndexOf(array, pet));
		WorldPoint worldPoint = PetRules.FormationPoint(owner, num, array.Length);
		if (value.CommandStatus == PetCommandStatus.Stay)
		{
			ClearPetHate(pet);
			pet.MoveTarget = null;
			return;
		}
		if (value.CommandStatus == PetCommandStatus.Whistle)
		{
			ClearPetHate(pet);
			if (pet.Pos.DistanceSquaredTo(worldPoint) <= 9216.0)
			{
				value.CommandStatus = PetCommandStatus.Stay;
				pet.MoveTarget = null;
			}
			else
			{
				MoveToward(pet, worldPoint, deltaSeconds, 5.0, moveSpeed);
			}
			return;
		}
		if (value.CommandStatus == PetCommandStatus.Extend)
		{
			ClearPetHate(pet);
			double num2 = Math.PI / 2.0 + Math.PI * 2.0 * (double)num / (double)Math.Max(1, array.Length);
			WorldPoint worldPoint2 = new WorldPoint(owner.Pos.X + Math.Cos(num2) * 48.0 * 5.0, owner.Pos.Y + Math.Sin(num2) * 48.0 * 5.0);
			if (pet.Pos.DistanceSquaredTo(worldPoint2) <= 25.0)
			{
				value.CommandStatus = PetCommandStatus.Stay;
				pet.MoveTarget = null;
			}
			else
			{
				MoveToward(pet, worldPoint2, deltaSeconds, 5.0, moveSpeed);
			}
			return;
		}
		if (value.Food < 10)
		{
			pet.MoveTarget = null;
			return;
		}
		Combatant combatant = PetPursuitTarget(pet);
		if (combatant != null)
		{
			MoveToward(pet, combatant.Pos, deltaSeconds, EffectiveRange(pet, combatant, pet.AttackRange), moveSpeed, combatReachStop: true);
			return;
		}
		WorldPoint value2 = worldPoint;
		if (value.CommandStatus == PetCommandStatus.Alert)
		{
			if (!_petAlertHomes.TryGetValue(pet, out value2))
			{
				value2 = pet.Pos;
				_petAlertHomes[pet] = value2;
			}
		}
		else
		{
			_petAlertHomes.Remove(pet);
		}
		if (pet.Pos.DistanceSquaredTo(value2) > 9216.0)
		{
			MoveToward(pet, value2, deltaSeconds, 5.0, moveSpeed);
		}
		else
		{
			pet.MoveTarget = null;
		}
	}

	private Combatant? PetPursuitTarget(Combatant pet)
	{
		PetInstance value;
		bool flag = !_petInstances.TryGetValue(pet, out value) || value.Food < 10;
		if (!flag)
		{
			PetCommandStatus commandStatus = value.CommandStatus;
			bool flag2 = (((uint)(commandStatus - 1) <= 1u || commandStatus == PetCommandStatus.Alert) ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			return null;
		}
		Combatant combatant = MaximumHateTarget(pet);
		if (combatant != null)
		{
			return combatant;
		}
		if (value.CommandStatus == PetCommandStatus.Defensive || !_petOwners.TryGetValue(pet, out Combatant value2))
		{
			return null;
		}
		Combatant combatant2 = SelectNearestEnemy(value2, Math.Max(value2.AggroRange, pet.AggroRange), requireLineOfSight: false, requireReachability: true);
		if (combatant2 != null)
		{
			AddHate(pet, combatant2, 0.0, linkFamily: false);
		}
		return combatant2;
	}

	private Combatant? PetAttackTarget(Combatant pet)
	{
		Combatant combatant = PetPursuitTarget(pet);
		if (combatant == null || !IsWithinRange(pet, combatant, pet.AttackRange) || !HasCombatLineOfSight(pet, combatant))
		{
			return null;
		}
		return combatant;
	}

	private void ClearPetHate(Combatant pet)
	{
		_hate.Remove(pet);
		_receivedFirstHate.Remove(pet);
	}

	private void RegisterPetDefenseHate(Combatant protectedTarget, Combatant attacker)
	{
		PetInstance value2 = default(PetInstance);
		foreach (KeyValuePair<Combatant, Combatant> petOwner in _petOwners)
		{
			petOwner.Deconstruct(out var key, out var value);
			Combatant combatant = key;
			bool flag = value != protectedTarget || !_combatants.Contains(combatant) || !_petInstances.TryGetValue(combatant, out value2);
			if (!flag)
			{
				PetCommandStatus commandStatus = value2.CommandStatus;
				bool flag2 = (((uint)(commandStatus - 1) <= 1u || commandStatus == PetCommandStatus.Alert) ? true : false);
				flag = !flag2;
			}
			if (!flag)
			{
				AddHate(combatant, attacker, 0.0, linkFamily: false);
			}
		}
	}

	private bool TryPerformPetBasicAttack(Combatant attacker, Combatant target)
	{
		if (attacker.Kind != CombatantKind.Pet || !_petProfiles.TryGetValue(attacker, out PetDerivedStats value))
		{
			return false;
		}
		PerformPhysicalHit(attacker, target, value.UsesRangedAttack, forceHeavy: false, forceCritical: false, 0.0, 0.0, forceHit: false, basicAttack: true, 1.0, DirectDamageDelivery.BasicAttack);
		return true;
	}

	private double PetRandomPhysicalReduction(Combatant target)
	{
		return 0.0;
	}

	private double PetIncomingDamageMultiplier(Combatant target, DamageType damageType)
	{
		return 1.0;
	}

	private void AdvancePetRegeneration(Combatant pet)
	{
		if (!_petProfiles.TryGetValue(pet, out PetDerivedStats value) || pet.Dead)
		{
			return;
		}
		double healthRegenIntervalSeconds = value.HealthRegenIntervalSeconds;
		if (healthRegenIntervalSeconds > 0.0 && value.HealthRegen > 0.0)
		{
			double num = _petRegenElapsed.GetValueOrDefault(pet) + 0.1;
			if (num + 1E-09 >= healthRegenIntervalSeconds)
			{
				num = Math.Max(0.0, num - healthRegenIntervalSeconds);
				double num2 = pet.Heal(value.HealthRegen);
				if (num2 > 0.0)
				{
					_events.Add(CombatEvent.Heal(pet, pet, num2));
				}
			}
			_petRegenElapsed[pet] = num;
		}
		double manaRegenIntervalSeconds = value.ManaRegenIntervalSeconds;
		if (!(manaRegenIntervalSeconds > 0.0) || !(value.ManaRegen > 0.0))
		{
			return;
		}
		double num3 = _petManaRegenElapsed.GetValueOrDefault(pet) + 0.1;
		if (num3 + 1E-09 >= manaRegenIntervalSeconds)
		{
			num3 = Math.Max(0.0, num3 - manaRegenIntervalSeconds);
			double mp = pet.Mp;
			pet.RestoreMp(value.ManaRegen);
			double num4 = pet.Mp - mp;
			if (num4 > 0.0)
			{
				_events.Add(CombatEvent.MpChange(pet, num4));
			}
		}
		_petManaRegenElapsed[pet] = num3;
	}

	private void AdvancePetRevives()
	{
		Combatant[] array = _petReviveReadyAt.Keys.ToArray();
		foreach (Combatant combatant in array)
		{
			if (!CanRevivePet(combatant))
			{
				_petReviveReadyAt.Remove(combatant);
			}
		}
	}

	private bool CanRevivePet(Combatant pet)
	{
		if (pet.Kind == CombatantKind.Pet && pet.Dead && _combatants.Contains(pet))
		{
			return _petInstances.ContainsKey(pet);
		}
		return false;
	}

	private void RevivePetCore(Combatant pet, double healthRatio)
	{
		ClearTransientConditions(pet);
		pet.Dead = false;
		pet.Hp = Math.Max(1.0, Math.Floor(pet.MaxHp * Math.Clamp(healthRatio, 0.0, 1.0)));
		pet.AttackCd = 0.0;
		pet.CastCd = 0.0;
		pet.MoveTarget = null;
		pet.VelX = 0.0;
		pet.VelY = 0.0;
		_resolvedDeaths.Remove(pet);
		_petReviveReadyAt.Remove(pet);
		_petRegenElapsed[pet] = 0.0;
		_petManaRegenElapsed[pet] = 0.0;
		if (_petInstances.TryGetValue(pet, out PetInstance value))
		{
			value.Hp = pet.Hp;
			value.Downed = false;
			value.Mp = Math.Max(0.0, pet.Mp);
		}
		_events.Add(CombatEvent.Heal(pet, pet, pet.Hp));
	}

	private void AwardPetExperience(Combatant pet, PetInstance instance, double amount)
	{
		if (_data != null && double.IsFinite(amount) && !(amount <= 0.0) && pet.IsAlive && _combatants.Contains(pet))
		{
			double val = ProgressionRules.ExperienceAtLevel(_data, 51) - 1.0;
			int level = pet.Level;
			pet.Experience = Math.Min(val, pet.Experience + Math.Floor(amount));
			int num = Math.Min(50, ProgressionRules.LevelByExperience(_data, pet.Experience));
			PetDefinition definition = PetRules.Definition(_data, instance.Form);
			while (pet.Level < num)
			{
				pet.Level++;
				instance.Level = pet.Level;
				PetRules.ApplyGrowth(instance, definition, _random);
				pet.MaxHp = instance.MaxHp;
				pet.MaxMp = instance.MaxMp;
				pet.Hp = instance.Hp;
				pet.Mp = instance.Mp;
				_events.Add(CombatEvent.LevelUp(pet, pet.Level));
			}
			instance.Level = pet.Level;
			instance.Experience = pet.Experience;
			instance.ExperiencePercent = ProgressionRules.ExperiencePercentage(_data, pet.Level, pet.Experience);
			if (pet.Level != level)
			{
				pet.Hp = instance.Hp;
				pet.Mp = instance.Mp;
				RefreshPetProfiles();
			}
		}
	}

	private void CleanupPetRuntime(Combatant combatant)
	{
		_petOwners.Remove(combatant);
		_petInstances.Remove(combatant);
		_petProfiles.Remove(combatant);
		_petReviveReadyAt.Remove(combatant);
		_petRegenElapsed.Remove(combatant);
		_petManaRegenElapsed.Remove(combatant);
		_petAlertHomes.Remove(combatant);
	}

	public bool TryCastPetSkill(Combatant pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		return false;
	}

	private double PetHardenDamageReduction(Combatant target, DamageType damageType)
	{
		return 0.0;
	}

	private bool TryBlockUndeadRelicAttack(Combatant attacker, Combatant defender)
	{
		RelicUndeadImmunity? relicUndeadImmunity = RelicConditionalCombatRules.UndeadImmunity(_data, defender, attacker);
		if (relicUndeadImmunity.HasValue)
		{
			RelicUndeadImmunity valueOrDefault = relicUndeadImmunity.GetValueOrDefault();
			if (!(defender.Buffs.GetValueOrDefault("_relicUndeadImmunityCooldown") > 0.0))
			{
				ApplyBuff(defender, "_relicUndeadImmunityCooldown", valueOrDefault.CooldownSeconds);
				_events.Add(CombatEvent.LogLine($"【{valueOrDefault.ItemName}】十字聖印閃耀，{attacker.Disp} 的攻擊被完全無效化。"));
				return true;
			}
		}
		return false;
	}

	private bool TryBlockFireRelicDamage(Combatant attacker, Combatant defender, string element)
	{
		if (!string.Equals(CombatSkill.NormalizeElement(element), "fire", StringComparison.Ordinal) || defender.Buffs.GetValueOrDefault("_relicFireNullifyCooldown") > 0.0 || !RelicConditionalCombatRules.HasFireNullify(_data, defender))
		{
			return false;
		}
		ApplyBuff(defender, "_relicFireNullifyCooldown", 10.0);
		_events.Add(CombatEvent.LogLine($"【火焰化解】{defender.Disp} 免疫了{attacker.Disp} 的火屬性傷害。"));
		return true;
	}

	private void ApplyWeakPointInsight(Combatant attacker, Combatant target)
	{
		if (!target.IsAlive)
		{
			return;
		}
		double num = RelicConditionalCombatRules.WeakPointInsightDamage(_data, attacker, target);
		if (num <= 0.0)
		{
			return;
		}
		double num2 = target.ApplyDamage(num);
		if (!(num2 <= 0.0))
		{
			_events.Add(CombatEvent.Damage(attacker, target, num2, DamageType.True, crit: false, attacker.AttackElement));
			_events.Add(CombatEvent.LogLine($"【弱點洞察】{attacker.Disp} 擊中屬性弱點，額外造成 {num2:0} 點傷害。"));
			if (target.Dead)
			{
				ResolveDeath(target, attacker);
			}
		}
	}

	private double ApplyRelicGatedPhysicalReduction(Combatant defender, double damage, DamageType damageType, DirectDamageDelivery delivery)
	{
		if (damage <= 0.0 || delivery != DirectDamageDelivery.BasicAttack || (damageType != DamageType.Melee && damageType != DamageType.Ranged) || defender.Buffs.GetValueOrDefault("_relicPhysicalReductionCooldown") > 0.0)
		{
			return damage;
		}
		double num = RelicConditionalCombatRules.GatedPhysicalReductionPercent(_data, defender);
		if (num <= 0.0)
		{
			return damage;
		}
		ApplyBuff(defender, "_relicPhysicalReductionCooldown", 3.0);
		return Math.Max(1.0, Math.Floor(damage * (1.0 - num / 100.0)));
	}

	private void TryApplyRelicElementExposure(Combatant attacker, Combatant target)
	{
		if (target.IsAlive)
		{
			if (RelicConditionalCombatRules.OnHitElementVulnerability(_data, attacker) == "fire")
			{
				ApplyBuff(target, "_relicFireVulnerability", 3.0);
			}
			if (RelicConditionalCombatRules.AppliesWetOnHit(_data, attacker))
			{
				ApplyBuff(target, "_relicWet", 10.0);
			}
		}
	}

	private double ApplyRelicElementExposureDamage(Combatant defender, double damage, string element)
	{
		double num = RelicConditionalCombatRules.IncomingElementExposureMultiplier(defender, element);
		if (num <= 1.0)
		{
			return damage;
		}
		if (string.Equals(CombatSkill.NormalizeElement(element), "wind", StringComparison.Ordinal) && defender.Buffs.GetValueOrDefault("_relicWet") > 0.0)
		{
			RemoveBuff(defender, "_relicWet");
		}
		return Math.Max(1.0, Math.Floor(damage * num));
	}

	private void TryApplyRelicBasicHitEffects(Combatant attacker, Combatant target, PhysicalHitResult hit)
	{
		TryApplyManaDrain(attacker, target);
	}

	internal void TryApplyManaDrain(Combatant attacker, Combatant target)
	{
		JsonObject jsonObject = RelicProcRules.MainWeapon(_data, attacker);
		if (jsonObject == null)
		{
			return;
		}
		int num = CombatSkill.ReadInt(jsonObject, "l1jItemId");
		int val;
		if ((uint)(num - 126) <= 1u)
		{
			int num2 = (RelicProcRules.MainWeaponStack(attacker)?.Enhancement ?? 0) + 3;
			if (num2 <= 0)
			{
				return;
			}
			val = Math.Min(9, _random.Roll(1, num2));
		}
		else
		{
			if (num != 259 || EffectiveMagicResist(target) > (double)_random.Roll(1, 100))
			{
				return;
			}
			val = 1;
		}
		val = Math.Min(val, (int)Math.Floor(Math.Max(0.0, target.Mp)));
		if (val > 0)
		{
			target.Mp -= val;
			attacker.Mp = Math.Min(attacker.MaxMp, attacker.Mp + (double)val);
		}
	}

	private void TryApplyRelicAttackProcs(Combatant attacker, Combatant originalTarget, bool attackHit)
	{
		TryApplyMagicDollAttackProcs(attacker, originalTarget);
		if (attackHit)
		{
			TryStartL1jChaser(attacker, originalTarget);
			Combatant combatant = (originalTarget.IsAlive ? originalTarget : SelectRelicProcTarget(attacker));
			if (combatant != null)
			{
				TryApplyWeaponDirectStatus(attacker, combatant);
				TryApplyRelicMagicProcs(attacker, combatant);
			}
		}
	}

	private void TryApplyRelicMagicProcs(Combatant attacker, Combatant target)
	{
		Combatant combatant = (target.IsAlive ? target : SelectRelicProcTarget(attacker));
		if (combatant == null)
		{
			return;
		}
		JsonObject? mainWpn = RelicProcRules.MainWeapon(_data, attacker);
		int num = CombatSkill.ReadInt(mainWpn ?? new JsonObject(), "l1jItemId");
		if ((num == 264 || num == 506) && mainWpn?["spellProc"] == null)
		{
			TryApplyLightningEdge(attacker, combatant);
			return;
		}
		RelicWeaponSpellProc? relicWeaponSpellProc = RelicProcRules.WeaponSpell(_data, attacker);
		if (relicWeaponSpellProc.HasValue)
		{
			RelicWeaponSpellProc valueOrDefault = relicWeaponSpellProc.GetValueOrDefault();
			if (_random.NextDouble() * 100.0 < valueOrDefault.ChancePercent)
			{
				string skillId = valueOrDefault.EffectId;
				if (int.TryParse(skillId, out int numSkillId))
				{
					string mapped = $"sk_{numSkillId}";
					if (_data?.Skill(mapped) != null) skillId = mapped;
				}
				if (skillId == "底比斯追擊" || skillId == "追蹤者" || skillId == "sk_thebes_chaser")
				{
					ApplyL1jChaserStrike(attacker, combatant);
					if (combatant.IsAlive)
					{
						_activeChasers.Add(new ActiveChaser
						{
							Attacker = attacker,
							Target = combatant,
							RemainingStrikes = 2,
							NextStrikeAt = CurrentTimeSeconds + 1.0
						});
					}
				}
				else if (!string.IsNullOrWhiteSpace(skillId) && _data?.Skill(skillId) != null)
				{
					Combatant castTarget = (valueOrDefault.StatusKind == "self") ? attacker : combatant;
					TryCastSkillCore(attacker, skillId, castTarget, freeMp: true, ignoreCastLock: true, automatic: true, ignoreClassRequirement: true);
				}
				else
				{
					ApplyRelicWeaponSpell(attacker, combatant, valueOrDefault);
				}
			}
		}
	}

	internal void TryApplyLightningEdge(Combatant attacker, Combatant target)
	{
		if (!(_random.NextDouble() * 100.0 >= 4.0))
		{
			int num = (int)Math.Floor(Math.Max(0.0, attacker.D.Int));
			int num2 = (int)Math.Floor(Math.Max(0.0, attacker.D.ItemSpellPower));
			double num3 = ((attacker.Buffs.GetValueOrDefault("sk_berserk") > 0.0) ? 0.2 : 0.0);
			int num4 = num + num2;
			double damage = (double)num4 * (2.0 + num3) + (double)(((num4 > 0) ? (_random.Roll(1, num4) - 1) : 0) * 2);
			_events.Add(CombatEvent.Cast(attacker, "雷擊", target));
			damage = L1jWeaponSkillReducedDamage(attacker, target, damage, "wind");
			ApplyWeaponSkillDamage(attacker, target, damage, "wind");
		}
	}

	private void ApplyRelicFreeMagic(Combatant attacker, Combatant target, RelicFreeMagicProc proc, bool useSkillTier = false)
	{
		JsonObject jsonObject = _data?.Skill(proc.SkillId);
		if (jsonObject == null || !CombatSkill.TryRead(proc.SkillId, jsonObject, out CombatSkill skill) || skill == null)
		{
			return;
		}
		Combatant[] array = RelicMagicTargets(attacker, target, skill.TargetsAllEnemies);
		double num = 0.0;
		Combatant[] array2 = array;
		foreach (Combatant combatant in array2)
		{
			bool flag = false;
			if (skill.InstantKill != null)
			{
				if (!combatant.IsBoss && (skill.InstantKill.RequiredTag.Length == 0 || HasTargetTag(combatant, skill.InstantKill.RequiredTag)))
				{
					_events.Add(CombatEvent.Cast(attacker, skill.Id, combatant));
					flag = true;
					if (TryInstantKill(attacker, combatant, skill, skill.InstantKill))
					{
						continue;
					}
				}
				if (skill.DamageDice.Count == 0)
				{
					continue;
				}
			}
			if (!flag)
			{
				_events.Add(CombatEvent.Cast(attacker, skill.Id, combatant));
			}
			double num2 = ApplyRelicMagicDamage(attacker, combatant, skill.DamageDice, skill.DamageBase, skill.Element, useSkillTier ? skill.Tier : proc.WeaponMagicTier, allowCritical: true);
			num += num2;
			if (combatant.IsAlive && skill.Status != null)
			{
				TryApplyStatus(attacker, combatant, skill);
			}
		}
		if (skill.LifeSteal && !(num <= 0.0))
		{
			double num3 = attacker.Heal(num);
			if (num3 > 0.0)
			{
				_events.Add(CombatEvent.Heal(attacker, attacker, num3));
			}
		}
	}

	internal void ApplyRelicWeaponSpell(Combatant attacker, Combatant target, RelicWeaponSpellProc proc)
	{
		double num = proc.FixDamage + ((proc.DiceCount > 0 && proc.DiceSides > 0) ? _random.Roll(proc.DiceCount, proc.DiceSides) : ((proc.RandomDamage > 0) ? (_random.Roll(1, proc.RandomDamage) - 1) : 0));
		if (proc.AreaCells != 0)
		{
			double radius = ((proc.AreaCells < 0) ? double.PositiveInfinity : ((double)proc.AreaCells * 48.0));
			Combatant[] array = (from candidate in _combatants
				where candidate.IsAlive && candidate != attacker && candidate != target && IsEnemy(attacker, candidate) && CombatRangeRules.DiamondDistance(target.Pos, candidate.Pos) <= radius + 1E-06
				orderby candidate.BornSeq, _combatants.IndexOf(candidate)
				select candidate).ToArray();
			foreach (Combatant combatant in array)
			{
				num = L1jWeaponSkillReducedDamage(attacker, combatant, num, proc.Element);
				if (!(num <= 0.0))
				{
					_events.Add(CombatEvent.Cast(attacker, proc.EffectId, combatant));
					ApplyWeaponSkillDamage(attacker, combatant, num, proc.Element);
					TryApplyRelicWeaponSpellStatus(combatant, proc);
				}
			}
		}
		_events.Add(CombatEvent.Cast(attacker, proc.EffectId, target));
		num = L1jWeaponSkillReducedDamage(attacker, target, num, proc.Element);
		ApplyWeaponSkillDamage(attacker, target, num, proc.Element);
		TryApplyRelicWeaponSpellStatus(target, proc);
	}

	private void TryApplyRelicWeaponSpellStatus(Combatant target, RelicWeaponSpellProc proc)
	{
		if (target.IsAlive && proc.StatusKind.Length != 0 && proc.StatusDurationTicks > 0 && !StatusRules.IsImmune(target, proc.StatusKind) && !(_random.NextDouble() * 100.0 >= proc.StatusChancePercent))
		{
			TryApplyStatusCore(target, proc.StatusKind, proc.StatusDurationTicks, null, resistanceChecked: true);
		}
	}

	internal double L1jWeaponSkillReducedDamage(Combatant attacker, Combatant target, double damage, string element)
	{
		if (damage <= 0.0)
		{
			return 0.0;
		}
		if (target.HasStatus("freeze"))
		{
			return 0.0;
		}
		int num = (int)Math.Floor(Math.Max(0.0, EffectiveMagicResist(target)));
		int num2 = Math.Max(0, attacker.D.OriginalMagicHit);
		double num4;
		if (num <= 100)
		{
			int num3 = (num - num2) / 2;
			num4 = 1.0 - 0.01 * (double)num3;
		}
		else
		{
			int num3 = (num - num2) / 10;
			num4 = 0.6 - 0.01 * (double)num3;
		}
		damage *= num4;
		double value = CombatSkill.NormalizeElement(element) switch
		{
			"earth" => target.D.ResistEarth, 
			"fire" => target.D.ResistFire, 
			"water" => target.D.ResistWater, 
			"wind" => target.D.ResistWind, 
			_ => 0.0, 
		};
		double num5 = (double)((int)(0.32 * Math.Abs(value)) * Math.Sign(value)) / 32.0;
		return (1.0 - num5) * damage;
	}

	private double ApplyWeaponSkillDamage(Combatant attacker, Combatant target, double damage, string element)
	{
		damage = Math.Floor(damage);
		if (damage <= 0.0 || !target.IsAlive)
		{
			return 0.0;
		}
		bool blocked;
		double num = ApplyDirectDamage(attacker, target, damage, DamageType.Magic, DirectDamageDelivery.SecondaryEffect, out blocked, critical: false, CombatSkill.NormalizeElement(element));
		if (blocked || num <= 0.0)
		{
			return 0.0;
		}
		ConsumeMagicResistanceReduction(target, num);
		if (target.Dead)
		{
			ResolveDeath(target, attacker);
		}
		return num;
	}

	private double ApplyRelicMagicDamage(Combatant attacker, Combatant target, IReadOnlyList<DiceTerm> diceTerms, double flatDamage, string element, int weaponMagicTier, bool allowCritical)
	{
		if (!target.IsAlive || diceTerms.Count == 0)
		{
			return 0.0;
		}
		bool flag = allowCritical && _random.NextDouble() * 100.0 < attacker.D.MagicCritical;
		double num = CombatMath.MagicDamageCoefficient(attacker.D.IntelligenceSpellPower, attacker.D.ItemSpellPower, AttributeDefense(target, element), weaponMagicTier);
		double num2 = (flag ? (1.0 + attacker.D.MagicCriticalDamage / 100.0) : 1.0);
		double num3 = (RelicConditionalCombatRules.IgnoresSpellMagicResistance(_data, attacker) ? 1.0 : CombatMath.MagicResistanceMultiplier(EffectiveMagicResist(target)));
		double num4 = 0.0;
		for (int i = 0; i < diceTerms.Count; i++)
		{
			DiceTerm diceTerm = diceTerms[i];
			double num5 = CombatMath.MagicBaseDamage(_random.Roll(diceTerm.Count, diceTerm.Sides), (i == diceTerms.Count - 1) ? flatDamage : 0.0, attacker.D.MagicDamage + CombatModifierRules.ActiveMagicDamageBonus(attacker)) * num * num2;
			double val = Math.Max(1.0, Math.Floor(num5 * num3));
			num4 += Math.Max(1.0, val);
		}
		num4 = Math.Max(1.0, num4 + (double)RollElementCounterDamage(element, target));
		num4 = Math.Max(1.0, Math.Floor(num4 * TeamPreciseTargetDamageMultiplier(attacker)));
		bool blocked;
		double num6 = ApplyDirectDamage(attacker, target, num4, DamageType.Magic, DirectDamageDelivery.SecondaryEffect, out blocked, flag, element);
		if (blocked || num6 <= 0.0)
		{
			return 0.0;
		}
		ConsumeMagicResistanceReduction(target, num6);
		if (target.Dead)
		{
			ResolveDeath(target, attacker);
		}
		return num6;
	}

	private Combatant[] RelicMagicTargets(Combatant attacker, Combatant target, bool targetsAllEnemies)
	{
		if (!targetsAllEnemies)
		{
			if (!target.IsAlive || !IsEnemy(attacker, target))
			{
				return Array.Empty<Combatant>();
			}
			return new Combatant[1] { target };
		}
		return (from candidate in _combatants
			where candidate.IsAlive && IsEnemy(attacker, candidate)
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	internal PhysicalHitResult ApplyDiceDaggerDamage(Combatant attacker, Combatant target, PhysicalHitResult hit)
	{
		if (!hit.Hit || !target.IsAlive)
		{
			return hit;
		}
		JsonObject jsonObject = RelicProcRules.MainWeapon(_data, attacker);
		if (jsonObject == null || CombatSkill.ReadInt(jsonObject, "l1jItemId") != 2)
		{
			return hit;
		}
		double num = _random.NextDouble() * 100.0;
		CombatantKind kind = target.Kind;
		bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
		if (flag || HostilePlayerRules.IsHostilePlayer(target))
		{
			if (num >= 3.0)
			{
				return hit;
			}
			double damage = Math.Floor(Math.Max(0.0, target.Hp) * 2.0 / 3.0);
			ConsumeMainWeapon(attacker);
			return hit with
			{
				Damage = damage
			};
		}
		if (num >= 5.0)
		{
			return hit;
		}
		double num2 = Math.Floor(Math.Max(0.0, target.Hp) / 2.0);
		ConsumeMainWeapon(attacker);
		return hit with
		{
			Damage = hit.Damage + num2
		};
	}

	private void ConsumeMainWeapon(Combatant attacker)
	{
		ItemStack itemStack = RelicProcRules.MainWeaponStack(attacker);
		if (itemStack != null)
		{
			if (itemStack.Quantity > 1)
			{
				itemStack.Quantity--;
			}
			else
			{
				attacker.EquippedItems.Remove("wpn");
			}
			CombatEquipment.SyncLegacyView(attacker);
			_events.Add(CombatEvent.LogLine(attacker.Disp + " 的骰子匕首消失了。"));
		}
	}

	private Combatant? SelectRelicProcTarget(Combatant attacker)
	{
		Combatant[] array = (from candidate in _combatants
			where candidate.IsAlive && IsEnemy(attacker, candidate)
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
		if (array.Length == 0)
		{
			return null;
		}
		int num = Math.Clamp((int)Math.Floor(_random.NextDouble() * (double)array.Length), 0, array.Length - 1);
		return array[num];
	}

	private double AdjustIncomingDamage(Combatant target, double damage)
	{
		return Math.Max(1.0, Math.Floor(damage * StatusRules.IncomingDamageMultiplier(target)));
	}

	private void CaptureRenderPreviousPositions()
	{
		_renderPreviousPositions.Clear();
		foreach (Combatant combatant in _combatants)
		{
			_renderPreviousPositions[combatant] = combatant.Pos;
		}
	}

	public bool TryGetRenderStep(Combatant combatant, out WorldPoint anchor, out double progress)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (_isometricSteps.TryGetValue(combatant, out IsometricStepState value))
		{
			anchor = value.Start;
			double num = Math.Clamp(_fixedStepAccumulator / (1.0 / 60.0), 0.0, 1.0);
			progress = Math.Clamp(((double)(value.CompletedFrames - 1) + num) / (double)value.TotalFrames, 0.0, 1.0);
			return true;
		}
		anchor = combatant.Pos;
		progress = 0.0;
		return false;
	}

	public WorldPoint RenderPositionOf(Combatant combatant)
	{
		ArgumentNullException.ThrowIfNull(combatant, "combatant");
		if (!_renderPreviousPositions.TryGetValue(combatant, out var value))
		{
			return combatant.Pos;
		}
		if (combatant.Pos.DistanceSquaredTo(value) > 4096.0)
		{
			return combatant.Pos;
		}
		double num = Math.Clamp(_fixedStepAccumulator / (1.0 / 60.0), 0.0, 1.0);
		return new WorldPoint(value.X + (combatant.Pos.X - value.X) * num, value.Y + (combatant.Pos.Y - value.Y) * num);
	}

	private bool TryCastReturnToNature(Combatant caster, JsonObject source, Combatant? requestedTarget, bool freeMp, bool ignoreCastLock)
	{
		if ((ignoreCastLock || !(caster.CastCd > 0.0)) && requestedTarget != null && requestedTarget.IsAlive)
		{
			if (requestedTarget.Kind == CombatantKind.Summon && _combatants.Contains(requestedTarget) && _summonOwners.GetValueOrDefault(requestedTarget) == caster && !IsSafeZone(caster.Pos) && !IsSafeZone(requestedTarget.Pos))
			{
				double range = CombatRangeRules.ConfiguredCastRange(source) ?? 480.0;
				if (!IsWithinRange(caster, requestedTarget, range) || !HasCombatLineOfSight(caster, requestedTarget))
				{
					return false;
				}
				L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject) ?? throw new InvalidDataException("釋放元素缺少 L1J-TW skills.sql 欄位。");
				if (l1jSkillFields.OfficialId != 145)
				{
					throw new InvalidDataException($"釋放元素綁到錯誤的 L1J skill id {l1jSkillFields.OfficialId}。");
				}
				int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, "sk_elf_release", CombatModifierRules.SkillMpCost(caster, source, "sk_elf_release")) : 0);
				int num2 = CombatModifierRules.SkillHpCost(caster, source, "sk_elf_release");
				if (caster.Mp < (double)num || caster.Hp <= (double)num2)
				{
					return false;
				}
				caster.Mp -= num;
				if (num > 0)
				{
					_events.Add(CombatEvent.MpChange(caster, -num));
				}
				if (num2 > 0)
				{
					caster.Hp = Math.Max(1.0, caster.Hp - (double)num2);
				}
				if (!ignoreCastLock)
				{
					caster.CastCd = Math.Max(NextCastCooldownSeconds(caster, support: true), (double)Math.Max(0, l1jSkillFields.ReuseDelayMilliseconds) / 1000.0);
				}
				_events.Add(CombatEvent.Cast(caster, "sk_elf_release", requestedTarget));
				ICombatRandom random = _random;
				int probabilityDice = l1jSkillFields.ProbabilityDice;
				int probabilityValue = l1jSkillFields.ProbabilityValue;
				int level = caster.Level;
				int level2 = caster.Level;
				int magicBonus = L1jMagicFormulas.MagicBonus((int)Math.Floor(Math.Max(0.0, caster.D.Int)));
				CombatantKind kind = caster.Kind;
				bool flag = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
				int num3 = L1jMagicFormulas.Probability(random, L1jMagicFormulas.ProbabilityBranch.ElementalControl, probabilityDice, probabilityValue, level, level2, magicBonus, (flag || HostilePlayerRules.IsHostilePlayer(caster)) ? ClassGrowthRules.MagicLevel(caster.ClassId, caster.Level) : L1jMagicFormulas.MagicLevel(caster.Level), 0, Math.Max(0, caster.D.OriginalMagicHit), 10, casterIsWizard: false, 0);
				num3 = Math.Min(100, num3 + (int)SkillAbnormalMasteryBonus(caster, "sk_elf_release"));
				if (!L1jMagicFormulas.ProbabilitySucceeds(_random, num3))
				{
					_events.Add(CombatEvent.Miss(caster, requestedTarget));
					return true;
				}
				Remove(requestedTarget);
				return true;
			}
		}
		return false;
	}

	public void SetExplorationTopology(MapTopology? topology)
	{
		_explorationTopology = topology;
		_explorationNavigation = ((topology == null) ? null : new ExplorationNavigationGrid(topology));
		if (topology == null)
		{
			_isometricLatticeOrigin = default(WorldPoint);
		}
		else
		{
			(double X, double Y) tuple = topology.DisplayPixelCenter(0, 0);
			double item = tuple.X;
			double item2 = tuple.Y;
			_isometricLatticeOrigin = new WorldPoint(item, item2);
		}
		_explorationNavigationPaths.Clear();
		_staticSolidBodies.Clear();
		foreach (Combatant combatant in _combatants)
		{
			combatant.Pos = SnapToExplorationWalkablePoint(combatant.Pos);
			WorldPoint? moveTarget = combatant.MoveTarget;
			if (moveTarget.HasValue)
			{
				WorldPoint valueOrDefault = moveTarget.GetValueOrDefault();
				combatant.MoveTarget = SnapToExplorationWalkablePoint(valueOrDefault);
			}
		}
	}

	public bool IsSafeZone(WorldPoint point)
	{
		MapTopology explorationTopology = _explorationTopology;
		if (explorationTopology == null || !explorationTopology.TryLocalCellAtDisplayPixel(point.X, point.Y, out var localX, out var localY))
		{
			return false;
		}
		return explorationTopology.IsSafeCell(localX, localY);
	}

	public bool IsHostileInteractionAllowed(Combatant source, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(target, "target");
		return HostilePlayerEngagementAllowed(source, target);
	}

	private bool CanHostileInteract(Combatant source, Combatant target)
	{
		return IsHostileInteractionAllowed(source, target);
	}

	private void AdvanceSatiety(double deltaSeconds)
	{
		foreach (Combatant combatant in _combatants)
		{
			if (!(SatietyRules.Tick(combatant, deltaSeconds) <= 0.0) && _data != null)
			{
				CombatantBuilder.RefreshPlayer(combatant, _data);
			}
		}
	}

	public bool TryFindSpawnPointAround(ICombatRandom placementRandom, Combatant anchor, double spawnRadius, double minimumDistance, double maximumDistance, out WorldPoint point, int randomAttempts = 24, double separation = 2.0, WorldBounds? hiddenFrom = null)
	{
		ArgumentNullException.ThrowIfNull(placementRandom, "placementRandom");
		ArgumentNullException.ThrowIfNull(anchor, "anchor");
		if (!_combatants.Contains(anchor))
		{
			throw new InvalidOperationException("The spawn anchor must be added to the engine first.");
		}
		double npcBodyRadius = 24.0;
		WorldOccupant[] occupied = (from combatant in _combatants
			where combatant.IsAlive
			select new WorldOccupant(combatant.Pos, Math.Max(0.0, combatant.Radius))).Concat(_staticSolidBodies.Select((WorldPoint body) => new WorldOccupant(body, npcBodyRadius))).ToArray();
		return WorldSpawnRules.TryFindPoint(placementRandom, anchor.Pos, spawnRadius, minimumDistance, maximumDistance, _worldBounds, _collisionGrid, occupied, out point, randomAttempts, separation, hiddenFrom);
	}

	private void ApplySpellbladeBuff(Combatant caster, CombatSkill skill, int consumedMana)
	{
		SpellbladeProfile? spellbladeProfile = SpellbladeRules.Profile(_data, caster, skill.Tier, skill.Element, skill.IsMagicDamage, consumedMana);
		if (spellbladeProfile.HasValue && _data != null)
		{
			SpellbladeRules.Store(caster, spellbladeProfile.Value);
			CombatantBuilder.RefreshPlayer(caster, _data);
		}
	}

	private bool TryResolveStormCast(Combatant caster, string skillId, JsonObject source, Combatant? requestedTarget, out Combatant? castTarget, out WorldPoint? centre)
	{
		if (CombatRangeRules.ConfiguredTargetMode(source) == SkillTargetMode.SelfArea)
		{
			castTarget = caster;
			centre = caster.Pos;
			return true;
		}
		double range = CombatRangeRules.ConfiguredCastRange(source) ?? 72.0;
		castTarget = ((requestedTarget != null && requestedTarget.IsAlive && IsEnemy(caster, requestedTarget) && IsWithinRange(caster, requestedTarget, range) && HasCombatLineOfSight(caster, requestedTarget)) ? requestedTarget : ((requestedTarget == null) ? SelectNearestEnemy(caster, range, requireLineOfSight: true) : null));
		centre = castTarget?.Pos;
		return castTarget != null;
	}

	private void SetStormCentre(Combatant caster, string skillId, WorldPoint centre)
	{
		_stormCentres[(caster, skillId)] = centre;
	}

	private void CleanupStormRuntime(Combatant combatant)
	{
		(Combatant, string)[] array = _stormCentres.Keys.Where<(Combatant, string)>(((Combatant Caster, string SkillId) tuple) => tuple.Caster == combatant).ToArray();
		foreach ((Combatant, string) key in array)
		{
			_stormCentres.Remove(key);
		}
	}

	private void AdvanceStormBuffs()
	{
		if (_data == null)
		{
			return;
		}
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant in array)
		{
			bool flag = !combatant.IsAlive;
			if (!flag)
			{
				CombatantKind kind = combatant.Kind;
				bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
				flag = !flag2 && !HostilePlayerRules.IsHostilePlayer(combatant);
			}
			if (flag)
			{
				continue;
			}
			foreach (string skillId in StormBuffRules.SkillIds)
			{
				if (combatant.Buffs.GetValueOrDefault(skillId) <= 0.0)
				{
					_stormCentres.Remove((combatant, skillId));
				}
				else if (StormBuffRules.ShouldTick(_data, skillId, CurrentTick))
				{
					CombatSkill combatSkill = StormBuffRules.TickSkill(_data, skillId);
					if (combatSkill != null)
					{
						StormBuffTick(combatant, skillId, combatSkill);
					}
				}
			}
		}
	}

	private void StormBuffTick(Combatant caster, string skillId, CombatSkill skill)
	{
		double num = StormBuffRules.DamageMultiplier(_data, caster, skillId);
		double? num2 = StormBuffRules.FreezeHitOffset(_data, skillId);
		WorldPoint a = (skill.CentersOnCaster ? caster.Pos : _stormCentres.GetValueOrDefault((caster, skillId), caster.Pos));
		double num3 = ((skill.EffectRadius > 0.0) ? skill.EffectRadius : 72.0);
		Combatant[] array = _combatants.ToArray();
		foreach (Combatant combatant in array)
		{
			if (!combatant.IsAlive || !IsEnemy(caster, combatant) || !CanHostileInteract(caster, combatant) || CombatRangeRules.DiamondDistance(a, combatant.Pos) > num3)
			{
				continue;
			}
			double hp = combatant.Hp;
			ApplyMagicSkillDamage(caster, combatant, skill, automatic: false);
			if (num > 1.0 && combatant.IsAlive)
			{
				double num4 = hp - combatant.Hp;
				double num5 = Math.Max(0.0, Math.Floor(num4 * num) - num4);
				if (num5 > 0.0)
				{
					ApplyDirectDamage(caster, combatant, num5, DamageType.Dot, DirectDamageDelivery.ActiveSkill, out var _, critical: false, skill.Element);
					if (combatant.Dead)
					{
						ResolveDeath(combatant, caster);
					}
				}
			}
			if (num2.HasValue)
			{
				double valueOrDefault = num2.GetValueOrDefault();
				if (combatant.IsAlive)
				{
					TryApplyNamedStatus(caster, combatant, "freeze", 60, valueOrDefault);
				}
			}
		}
	}

	public IReadOnlyList<Combatant> ActiveSummonsOf(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return (from candidate in _combatants
			where candidate.IsAlive && _summonOwners.GetValueOrDefault(candidate) == owner
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	public Combatant? SummonOwnerOf(Combatant summon)
	{
		ArgumentNullException.ThrowIfNull(summon, "summon");
		return _summonOwners.GetValueOrDefault(summon);
	}

	public int ActiveSummonPetCostOf(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return _summonOwners.Where<KeyValuePair<Combatant, Combatant>>((KeyValuePair<Combatant, Combatant> pair) => pair.Value == owner && pair.Key.IsAlive && _combatants.Contains(pair.Key)).Sum((KeyValuePair<Combatant, Combatant> pair) => Math.Max(0, _summonPetCosts.GetValueOrDefault(pair.Key)));
	}

	public IReadOnlyList<SummonFormInfo> AvailableSummonForms(Combatant caster, string skillId)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = _data?.Skill(skillId);
		if (_data == null || jsonObject == null || !SummonRules.IsSummonSkill(skillId, jsonObject))
		{
			return Array.Empty<SummonFormInfo>();
		}
		return SummonRules.AvailableForms(_data, caster, skillId);
	}

	public bool TryCastSummonSkill(Combatant caster, string skillId, string? preferredForm = null)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = _data?.Skill(skillId);
		if (jsonObject == null || !SummonRules.IsSummonSkill(skillId, jsonObject))
		{
			return false;
		}
		return TryCastSkillAsSharedAction(caster, skillId, null, automatic: false, preferredForm);
	}

	public int DismissSummons(Combatant owner, bool removeContractBuff = true)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		Combatant[] array = (from pair in _summonOwners
			where pair.Value == owner && !string.Equals(_summonSkillIds.GetValueOrDefault(pair.Key), "_necro_skeleton", StringComparison.Ordinal)
			select pair.Key).Where(_combatants.Contains).ToArray();
		Combatant[] array2 = array;
		foreach (Combatant summon in array2)
		{
			ExpireSummon(summon);
		}
		if (removeContractBuff && _combatants.Contains(owner))
		{
			foreach (string skillId in SummonRules.SkillIds)
			{
				RemoveBuff(owner, skillId);
			}
		}
		return array.Length;
	}

	private bool TryCastSummonSkillCore(Combatant caster, string skillId, JsonObject source, string? preferredForm, bool freeMp, bool ignoreCastLock)
	{
		if (NecromancyRules.ReplacesAnimateDead(_data, caster, skillId))
		{
			return true;
		}
		if (!SummonRules.IsSummonSkill(skillId, source) || (!ignoreCastLock && caster.CastCd > 0.0) || _data == null || !SummonRules.TryCreatePlan(_data, caster, skillId, preferredForm, out SummonPlan plan, _petInstances.Where<KeyValuePair<Combatant, PetInstance>>((KeyValuePair<Combatant, PetInstance> pair) => _petOwners.GetValueOrDefault(pair.Key) == caster).Sum((KeyValuePair<Combatant, PetInstance> pair) => (int)Math.Max(0.0, pair.Value.ActiveCharmCost))) || (object)plan == null)
		{
			return false;
		}
		int num = ((!freeMp) ? RelicConditionalCombatRules.SkillManaCost(_data, caster, skillId, CombatModifierRules.SkillMpCost(caster, source, skillId)) : 0);
		int num2 = CombatModifierRules.SkillHpCost(caster, source, skillId);
		L1jSkillFields l1jSkillFields = L1jSkillFields.TryRead(source["l1j"] as JsonObject);
		if (caster.Mp < (double)num || caster.Hp <= (double)(num2 + 5))
		{
			return false;
		}
		foreach (string skillId2 in SummonRules.SkillIds)
		{
			RemoveBuff(caster, skillId2);
		}
		DismissSummons(caster, removeContractBuff: false);
		caster.Mp -= num;
		if (num > 0)
		{
			_events.Add(CombatEvent.MpChange(caster, -num));
		}
		caster.Hp = Math.Max(1.0, caster.Hp - (double)num2);
		ApplyBuff(caster, skillId, plan.DurationSeconds);
		if (!ignoreCastLock)
		{
			caster.CastCd = Math.Max(NextCastCooldownSeconds(caster, support: true), (double)(l1jSkillFields?.ReuseDelayMilliseconds ?? 0) / 1000.0);
		}
		_events.Add(CombatEvent.Cast(caster, skillId, caster));
		int count = plan.Units.Count;
		int num3 = _combatants.Select((Combatant actor) => actor.BornSeq).DefaultIfEmpty(0).Max();
		for (int num4 = 0; num4 < count; num4++)
		{
			SummonUnitPlan summonUnitPlan = plan.Units[num4];
			WorldPoint worldPoint = SummonRules.FormationPoint(caster, num4, count);
			Combatant combatant = SummonRules.CreateCombatant(summonUnitPlan, caster, $"summon:{caster.Key}:{++_nextSummonId}", num3 + num4 + 1, _worldBounds?.Clamp(worldPoint) ?? worldPoint);
			_summonOwners[combatant] = caster;
			_summonSkillIds[combatant] = skillId;
			_summonPetCosts[combatant] = Math.Max(0, plan.PetCostPerUnit);
			_summonExpiresAt[combatant] = CurrentTimeSeconds + plan.DurationSeconds;
			if ((object)summonUnitPlan.MagicAttack != null)
			{
				_summonMagicAttacks[combatant] = summonUnitPlan.MagicAttack;
			}
			if (summonUnitPlan.Procs.Count > 0)
			{
				_summonProcs[combatant] = summonUnitPlan.Procs;
			}
			if ((object)summonUnitPlan.AoeAttack != null)
			{
				_summonAoeAttacks[combatant] = summonUnitPlan.AoeAttack;
			}
			Add(combatant);
		}
		return true;
	}

	private void AdvanceSummonLifetimes()
	{
		Combatant[] array = _summonOwners.Keys.ToArray();
		foreach (Combatant combatant in array)
		{
			if (!_combatants.Contains(combatant))
			{
				continue;
			}
			Combatant valueOrDefault = _summonOwners.GetValueOrDefault(combatant);
			string text = _summonSkillIds.GetValueOrDefault(combatant) ?? string.Empty;
			bool num;
			if (!string.Equals(text, "_necro_skeleton", StringComparison.Ordinal))
			{
				if (valueOrDefault != null && _combatants.Contains(valueOrDefault) && valueOrDefault.IsAlive && text.Length > 0 && valueOrDefault.Buffs.GetValueOrDefault(text) > 0.0)
				{
					num = CurrentTimeSeconds < _summonExpiresAt.GetValueOrDefault(combatant, double.NegativeInfinity);
					goto IL_00ca;
				}
			}
			else if (valueOrDefault != null)
			{
				num = NecromancyContractActive(valueOrDefault);
				goto IL_00ca;
			}
			goto IL_00cc;
			IL_00cc:
			ExpireSummon(combatant);
			continue;
			IL_00ca:
			if (num)
			{
				continue;
			}
			goto IL_00cc;
		}
	}

	private void AdvanceMagicDollMovement(Combatant doll, Combatant owner, double deltaSeconds, double moveSpeed)
	{
		if (owner == null || !owner.IsAlive || !_combatants.Contains(owner))
		{
			return;
		}
		WorldPoint worldPoint = SummonRules.FormationPoint(owner, 0, 1);
		double distSq = doll.Pos.DistanceSquaredTo(owner.Pos);
		if (distSq > 202500.0)
		{
			WorldPoint snapPoint = ClampAndSnapPlacement(worldPoint, doll.Radius);
			doll.Pos = (CanReachExplorationPoint(owner.Pos, snapPoint) ? snapPoint : ClampAndSnapPlacement(owner.Pos, doll.Radius));
			_navigationPaths.Remove(doll);
			doll.MoveTarget = null;
			return;
		}
		double targetDist = CombatRangeRules.DiamondDistance(doll.Pos, worldPoint);
		if (targetDist > 28.0)
		{
			double ownerSpeed = CombatModifierRules.EffectiveMoveSpeed(owner, _data);
			double dollSpeed = Math.Max(ownerSpeed * 1.35, IsometricMovementRules.BaseMoveSpeed * 1.5);
			MoveToward(doll, worldPoint, deltaSeconds, 20.0, dollSpeed);
		}
	}

	private void AdvanceSummonMovement(Combatant summon, double deltaSeconds, double moveSpeed)
	{
		Combatant valueOrDefault = _summonOwners.GetValueOrDefault(summon);
		if (valueOrDefault == null || !valueOrDefault.IsAlive || !_combatants.Contains(valueOrDefault))
		{
			return;
		}
		Combatant[] array = ActiveSummonsOf(valueOrDefault).ToArray();
		int index = Math.Max(0, Array.IndexOf(array, summon));
		WorldPoint worldPoint = SummonRules.FormationPoint(valueOrDefault, index, array.Length);
		if (summon.Pos.DistanceSquaredTo(valueOrDefault.Pos) > 810000.0)
		{
			summon.Pos = ClampAndSnapPlacement(worldPoint, summon.Radius);
			_navigationPaths.Remove(summon);
			summon.MoveTarget = null;
			return;
		}
		Combatant combatant = SelectNearestEnemy(summon, summon.AggroRange, requireLineOfSight: false, requireReachability: true);
		if (combatant != null && CombatRangeRules.DiamondDistance(combatant.Pos, valueOrDefault.Pos) <= 520.0)
		{
			ResetIdleWander(summon);
			MoveToward(summon, combatant.Pos, deltaSeconds, EffectiveRange(summon, combatant, summon.AttackRange), moveSpeed, combatReachStop: true);
		}
		else if (summon.Pos.DistanceSquaredTo(worldPoint) > 7744.0)
		{
			ResetIdleWander(summon);
			MoveToward(summon, worldPoint, deltaSeconds, 5.0, moveSpeed);
		}
		else
		{
			AdvanceIdleWander(summon, deltaSeconds, moveSpeed, worldPoint);
		}
	}

	private bool TryPerformSummonMagicAttack(Combatant attacker, Combatant target)
	{
		if (attacker.Kind != CombatantKind.Summon || !_summonMagicAttacks.TryGetValue(attacker, out SummonMagicAttackProfile value))
		{
			return false;
		}
		double num = value.HitValue - (double)attacker.Level;
		int num2 = (int)Math.Floor(target.D.ArmorClass + StatusRules.ArmorClassAdjustment(target));
		if (Math.Clamp((int)((num + (double)((num2 >= 0) ? num2 : ((int)(_random.NextDouble() * ((double)num2 * 1.5)) - 1))) * 5.0), 5, 95) < _random.Roll(1, 100))
		{
			_events.Add(CombatEvent.Miss(attacker, target));
			return true;
		}
		double num3 = ((double)_random.Roll(Math.Max(1, value.DiceCount), Math.Max(1, value.DiceSides)) + value.FlatDamage) * Math.Max(0.0, value.DamageMultiplier);
		double magicResistance = Math.Max(0.0, EffectiveMagicResist(target) - value.MagicResistancePenetration);
		double num4 = Math.Max(1.0, Math.Floor(num3 * CombatMath.MagicResistanceMultiplier(magicResistance)));
		num4 = Math.Max(1.0, num4 + (double)RollElementCounterDamage(value.Element, target));
		num4 = Math.Max(1.0, Math.Floor(num4 * TeamPreciseTargetDamageMultiplier(attacker)));
		CreateProjectile(attacker, target, "bolt", basicAttack: false, magicWeaponAttack: true, default(PhysicalHitResult), num4);
		return true;
	}

	private void TryTriggerSummonPhysicalProcs(Combatant attacker, Combatant originalTarget)
	{
		if (attacker.Kind != CombatantKind.Summon || !originalTarget.IsAlive || !_summonProcs.TryGetValue(attacker, out IReadOnlyList<SummonProcProfile> value))
		{
			return;
		}
		foreach (SummonProcProfile item in value)
		{
			if (!originalTarget.IsAlive || _random.NextDouble() >= item.Chance)
			{
				continue;
			}
			_events.Add(CombatEvent.Cast(attacker, ProcEventSkillId(item), originalTarget));
			_events.Add(CombatEvent.LogLine(attacker.Disp + " 發動 " + item.Name + "。"));
			SummonProcKind kind = item.Kind;
			bool flag = ((kind == SummonProcKind.PoisonAll || kind == SummonProcKind.MagicAll) ? true : false);
			Combatant[] array = (flag ? AreaEnemies(attacker, originalTarget.Pos, item.AreaRadius) : new Combatant[1] { originalTarget });
			foreach (Combatant combatant in array)
			{
				if (combatant.IsAlive)
				{
					kind = item.Kind;
					if ((uint)kind <= 1u)
					{
						ApplySummonPoison(attacker, combatant, item.FlatDamage);
					}
					else
					{
						ApplySummonMagicProc(attacker, combatant, item);
					}
				}
			}
		}
	}

	private void TryTriggerSummonAoeAttack(Combatant attacker, Combatant originalTarget)
	{
		if (attacker.Kind == CombatantKind.Summon && _summonAoeAttacks.TryGetValue(attacker, out SummonAoeAttackProfile value) && !(_random.NextDouble() >= value.Chance))
		{
			_events.Add(CombatEvent.Cast(attacker, "summon_spirit_aoe_" + value.Element, originalTarget));
			_events.Add(CombatEvent.LogLine(attacker.Disp + " 釋放 " + value.Name + "。"));
			Combatant[] array = AreaEnemies(attacker, originalTarget.Pos, value.AreaRadius);
			foreach (Combatant target in array)
			{
				ApplySummonMagicDamage(attacker, target, value.DiceCount, value.DiceSides, value.FlatDamage, value.DamageMultiplier, value.MagicResistancePenetration, value.Element);
			}
		}
	}

	private void ApplySummonPoison(Combatant attacker, Combatant target, double damage)
	{
		TryApplyStatusCore(target, "poison", 150, new PeriodicEffect
		{
			TickEvery = 30,
			TicksUntilNext = 30,
			Damage = Math.Max(1.0, Math.Floor(damage)),
			DamageType = DamageType.Dot,
			Element = "none",
			Source = attacker
		}, resistanceChecked: true);
	}

	private void ApplySummonMagicProc(Combatant attacker, Combatant target, SummonProcProfile profile)
	{
		ApplySummonMagicDamage(attacker, target, profile.DiceCount, profile.DiceSides, profile.FlatDamage, profile.DamageMultiplier, 0.0, profile.Element);
		if (target.IsAlive)
		{
			double num = Math.Max(0.0, (100.0 - EffectiveMagicResist(target)) / 200.0);
			if (profile.Slow && _random.NextDouble() < num)
			{
				TryApplyStatusCore(target, "slow", 80, null, resistanceChecked: true);
			}
			if (profile.Stun && _random.NextDouble() < num)
			{
				TryApplyStatusCore(target, "stun", 30, null, resistanceChecked: true);
			}
		}
	}

	private void ApplySummonMagicDamage(Combatant attacker, Combatant target, int diceCount, int diceSides, double flatDamage, double damageMultiplier, double magicResistancePenetration, string element)
	{
		if (!target.IsAlive)
		{
			return;
		}
		double num = ((double)_random.Roll(Math.Max(1, diceCount), Math.Max(1, diceSides)) + flatDamage) * Math.Max(0.0, damageMultiplier);
		double magicResistance = Math.Max(0.0, EffectiveMagicResist(target) - magicResistancePenetration);
		double num2 = Math.Max(1.0, Math.Floor(num * CombatMath.MagicResistanceMultiplier(magicResistance)));
		num2 = Math.Max(1.0, num2 + (double)RollElementCounterDamage(element, target));
		num2 = Math.Max(1.0, Math.Floor(num2 * TeamPreciseTargetDamageMultiplier(attacker)));
		bool blocked;
		double appliedDamage = ApplyDirectDamage(attacker, target, num2, DamageType.Magic, DirectDamageDelivery.SecondaryEffect, out blocked, critical: false, element);
		if (!blocked)
		{
			ConsumeMagicResistanceReduction(target, appliedDamage);
			if (target.Dead)
			{
				ResolveDeath(target, attacker);
			}
		}
	}

	private Combatant[] AreaEnemies(Combatant attacker, WorldPoint center, double radius)
	{
		double safeRadius = Math.Max(0.0, radius);
		return (from candidate in _combatants
			where candidate.IsAlive && IsEnemy(attacker, candidate) && HasCombatLineOfSight(attacker, candidate) && CombatRangeRules.DiamondDistance(candidate.Pos, center) <= safeRadius
			orderby candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
	}

	private static string ProcEventSkillId(SummonProcProfile profile)
	{
		SummonProcKind kind = profile.Kind;
		if ((uint)kind <= 1u)
		{
			return "summon_proc_poison";
		}
		return "summon_proc_magic_" + profile.Element;
	}

	private void ExpireSummon(Combatant summon)
	{
		_summonExpiresAt.Remove(summon);
		if (_combatants.Contains(summon) && !summon.Dead)
		{
			summon.Hp = 0.0;
			summon.Dead = true;
			ResolveDeath(summon, null);
		}
	}

	private void CleanupSummonRuntime(Combatant summon)
	{
		_summonOwners.Remove(summon);
		_summonSkillIds.Remove(summon);
		_summonPetCosts.Remove(summon);
		_summonExpiresAt.Remove(summon);
		_summonMagicAttacks.Remove(summon);
		_summonProcs.Remove(summon);
		_summonAoeAttacks.Remove(summon);
	}

	public IReadOnlyList<Combatant> RelocateTeleportGroup(Combatant leader, WorldPoint destination, bool takePets = true)
	{
		ArgumentNullException.ThrowIfNull(leader, "leader");
		if (leader.Kind != CombatantKind.Player || !_combatants.Contains(leader))
		{
			throw new InvalidOperationException("The teleport leader must be an active player.");
		}
		if (!double.IsFinite(destination.X) || !double.IsFinite(destination.Y))
		{
			throw new ArgumentOutOfRangeException("destination", "Teleport destination must be finite.");
		}
		WorldPoint pos = SnapToExplorationWalkablePoint(ClampAndSnapPlacement(destination, leader.Radius));
		Combatant[] array = (from candidate in _combatants
			where candidate.IsAlive && (candidate == leader || (candidate.Kind == CombatantKind.Ally && !IsEnemy(leader, candidate)) || (candidate.Kind == CombatantKind.Pet && !IsEnemy(leader, candidate)) || (takePets && candidate.Kind == CombatantKind.Summon && !IsEnemy(leader, candidate)))
			orderby (candidate != leader) ? 1 : 0, candidate.BornSeq, _combatants.IndexOf(candidate)
			select candidate).ToArray();
		Combatant[] array2 = array;
		foreach (Combatant combatant in array2)
		{
			combatant.Pos = pos;
			combatant.MoveTarget = null;
			combatant.VelX = 0.0;
			combatant.VelY = 0.0;
			_navigationPaths.Remove(combatant);
			_explorationNavigationPaths.Remove(combatant);
			_isometricSteps.Remove(combatant);
			_sidestepOrigins.Remove(combatant);
			_renderPreviousPositions.Remove(combatant);
			ResetIdleWander(combatant);
			_events.Add(CombatEvent.Move(combatant));
		}
		return array;
	}

	public void ApplyL1jTrap(Combatant target, L1jTrapDefinition trap)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(trap, "trap");
		if (!_combatants.Contains(target) || !target.IsAlive)
		{
			return;
		}
		switch (trap.Kind)
		{
		case L1jTrapKind.Damage:
		{
			double num2 = target.ApplyDamage(trap.Base + _random.Roll(trap.DiceCount, trap.Dice));
			if (num2 > 0.0)
			{
				_events.Add(CombatEvent.Damage(target, target, num2, DamageType.True));
			}
			if (target.Dead)
			{
				ResolveDeath(target, target);
			}
			break;
		}
		case L1jTrapKind.Healing:
		{
			double num = target.Heal(trap.Base + _random.Roll(trap.DiceCount, trap.Dice));
			if (num > 0.0)
			{
				_events.Add(CombatEvent.Heal(target, target, num));
			}
			break;
		}
		case L1jTrapKind.Poison:
			ApplyTrapPoison(target, trap);
			break;
		case L1jTrapKind.Skill:
			ApplyTrapSkill(target, trap);
			break;
		}
		_events.Add(CombatEvent.LogLine("觸發陷阱：" + trap.Note));
	}

	private void ApplyTrapPoison(Combatant target, L1jTrapDefinition trap)
	{
		if (!L1jPoisonAttackRules.CanInfect(_data, target))
		{
			return;
		}
		switch (trap.PoisonType)
		{
		case "d":
		{
			int num = Math.Max(1, trap.PoisonTimeMs / 100);
			TryApplyStatusCore(target, "poison", 300, new PeriodicEffect
			{
				TickEvery = num,
				TicksUntilNext = num,
				Damage = trap.PoisonDamage,
				DamageType = DamageType.Dot,
				Element = "none",
				Source = target
			}, resistanceChecked: true);
			break;
		}
		case "s":
			TryApplyStatusCore(target, "poisonsilence", int.MaxValue, null, resistanceChecked: true);
			break;
		case "p":
			if (target.Kind == CombatantKind.Player)
			{
				int durationTicks = Math.Max(1, trap.PoisonDelayMs / 100);
				target.Counters["_trap_poison_paralysis_ticks"] = Math.Max(1, trap.PoisonTimeMs / 100);
				TryApplyStatusCore(target, "poisonparalyzing", durationTicks, null, resistanceChecked: true);
			}
			break;
		}
	}

	private void ApplyTrapSkill(Combatant target, L1jTrapDefinition trap)
	{
		JsonObject jsonObject = _data?.Skill(trap.SkillKey ?? "");
		if (jsonObject == null)
		{
			return;
		}
		double num = trap.SkillTimeSeconds;
		if (num <= 0.0)
		{
			num = CombatSkill.ReadDouble(jsonObject, "dur");
			if (jsonObject["status"] is JsonObject source)
			{
				num = CombatSkill.ReadDouble(source, "dur");
			}
		}
		num = Math.Max(0.1, num);
		if (jsonObject["status"] is JsonObject jsonObject2)
		{
			string text = jsonObject2["kind"]?.GetValue<string>();
			if (text != null && text.Length > 0)
			{
				ApplyStatus(target, text, Math.Max(1, (int)Math.Round(num * 10.0)));
				goto IL_00f4;
			}
		}
		ApplyBuff(target, trap.SkillKey, num);
		goto IL_00f4;
		IL_00f4:
		_events.Add(CombatEvent.Cast(target, trap.SkillKey, target));
	}

	private int TrapParalysisDuration(Combatant target)
	{
		if (!target.Counters.Remove("_trap_poison_paralysis_ticks", out var value))
		{
			return 450;
		}
		return Math.Max(1, value);
	}

	private int RollElementCounterDamage(string? attackElement, Combatant target)
	{
		return CounterDamageRules.RollElementBonus(_random, attackElement, target.Element);
	}

	private int RollWeaponCounterDamage(Combatant attacker, Combatant target)
	{
		return CounterDamageRules.RollWeaponAttackBonus(_data, _random, attacker, target);
	}
}
