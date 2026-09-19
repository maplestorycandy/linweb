using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed class EngineAdapter
{
	public readonly CombatEngine Engine;

	private readonly IGameData _data;

	private readonly ICombatRandom _place;

	private readonly ICombatRandom _mobStats;

	private int _bornSeq;

	public static Action<double, double>? SpawnCostProbe;

	public Combatant Player { get; private set; }

	public IReadOnlyList<Combatant> Combatants => Engine.Combatants;

	public IReadOnlyList<CombatProjectile> Projectiles => Engine.Projectiles;

	public EngineAdapter(IGameData? data = null, int seed = 20260726)
	{
		_data = data ?? GameDataProvider.Shared;
		Engine = new CombatEngine(new SeededCombatRandom(seed), _data);
		_place = new SeededCombatRandom(seed);
		_mobStats = new SeededCombatRandom(seed ^ 0x4D4F42);
	}

	public static Vector2 ToVec(WorldPoint p)
	{
		return new Vector2((float)p.X, (float)p.Y);
	}

	public static WorldPoint ToWorld(Vector2 v)
	{
		return new WorldPoint(v.X, v.Y);
	}

	public IReadOnlyList<CombatEvent> Advance(double delta)
	{
		double safeDelta = (!double.IsFinite(delta) || delta <= 0.0) ? 0.0 : Math.Min(delta, 0.25);
		return Engine.Advance(safeDelta);
	}

	public Vector2 RenderPos(Combatant c)
	{
		return ToVec(Engine.RenderPositionOf(c));
	}

	public (Vector2 Anchor, float Progress, bool Stepping) RenderWalk(Combatant c)
	{
		WorldPoint anchor;
		double progress;
		bool item = Engine.TryGetRenderStep(c, out anchor, out progress);
		return (Anchor: ToVec(anchor), Progress: (float)progress, Stepping: item);
	}

	public void SetPlayerPathTarget(Vector2 world)
	{
		Engine.SetMoveTarget(Player, ToWorld(world), isManual: true);
	}

	public void SetPlayerMoveTarget(Vector2 world)
	{
		SetPlayerPathTarget(world);
	}

	public void SetPlayerMoveDirection(Vector2 dir)
	{
		Engine.SetMoveDirection(Player, dir.X, dir.Y);
	}

	public void ReleasePlayerMoveDirection()
	{
		Engine.ReleaseMoveDirection(Player);
	}

	public void StopPlayer()
	{
		Engine.ClearMoveTarget(Player);
	}

	public void BuildPrototype(PlayerBuild build, Rect2 field)
	{
		BuildWithPlayer(MakePlayer(build), field);
	}

	public void BuildWithPlayer(Combatant player, Rect2 field)
	{
		Engine.SetWorldBounds(new WorldBounds(field.Position.X, field.Position.Y, field.End.X, field.End.Y));
		Player = player;
		player.Pos = new WorldPoint((double)field.Position.X + (double)field.Size.X * 0.5, (double)field.Position.Y + (double)field.Size.Y * 0.5);
		player.MoveTarget = null;
		player.VelX = 0.0;
		player.VelY = 0.0;
		player.Dead = false;
		player.Hp = player.MaxHp;
		player.AttackCd = 0.0;
		player.OffhandCd = 0.0;
		player.CastCd = 0.0;
		player.HitstunUntil = 0;
		player.Statuses.Clear();
		player.PeriodicEffects.Clear();
		player.Bleeds.Clear();
		player.Counters.Clear();
		Engine.Add(player);
	}

	public IReadOnlyList<string> GetMapMobKeys(string mapKey)
	{
		if (MapSpawnCatalog.TryGetMobKeys(_data, mapKey, out var mobKeys))
		{
			return mobKeys;
		}
		return Array.Empty<string>();
	}

	public Combatant SpawnMob(string mobKey, WorldPoint pos)
	{
		Action<double, double> spawnCostProbe = SpawnCostProbe;
		long num = ((spawnCostProbe == null) ? 0 : Stopwatch.GetTimestamp());
		Combatant combatant = CombatantBuilder.CreateMob(_data, mobKey, null, ++_bornSeq, pos, _mobStats);
		long num2 = ((spawnCostProbe == null) ? 0 : Stopwatch.GetTimestamp());
		Engine.Add(combatant);
		if (spawnCostProbe != null)
		{
			long timestamp = Stopwatch.GetTimestamp();
			double num3 = 1000.0 / (double)Stopwatch.Frequency;
			spawnCostProbe((double)(num2 - num) * num3, (double)(timestamp - num2) * num3);
		}
		return combatant;
	}

	public Combatant SpawnPainwandMob(string mobKey)
	{
		Combatant combatant = CombatantBuilder.CreateMob(_data, mobKey, null, _bornSeq + 1, Player.Pos, _mobStats);
		if (!Engine.TryFindSpawnPointAround(_place, Player, Math.Max(0.0, combatant.Radius), 48.0, 144.0, out var point, 32) && !Engine.TryFindSpawnPointAround(_place, Player, Math.Max(0.0, combatant.Radius), 48.0, 480.0, out point, 64))
		{
			point = Player.Pos;
		}
		combatant.Pos = point;
		_bornSeq++;
		Engine.Add(combatant);
		Engine.MarkPainwandMob(combatant);
		return combatant;
	}

	public Combatant? SpawnMobAroundPlayer(string mobKey)
	{
		Combatant combatant = CombatantBuilder.CreateMob(_data, mobKey, null, _bornSeq + 1, Player.Pos, _mobStats);
		if (!Engine.TryFindSpawnPointAround(_place, Player, Math.Max(0.0, combatant.Radius), 380.0, 700.0, out var point))
		{
			return null;
		}
		combatant.Pos = point;
		_bornSeq++;
		Engine.Add(combatant);
		return combatant;
	}

	public Combatant? SpawnMobHiddenFromView(string mobKey, WorldBounds view)
	{
		Combatant combatant = CombatantBuilder.CreateMob(_data, mobKey, null, _bornSeq + 1, Player.Pos, _mobStats);
		if (!Engine.TryFindSpawnPointAround(_place, Player, Math.Max(0.0, combatant.Radius), 380.0, 1600.0, out var point, 48, 2.0, view))
		{
			return null;
		}
		combatant.Pos = point;
		_bornSeq++;
		Engine.Add(combatant);
		return combatant;
	}

	private Combatant MakePlayer(PlayerBuild build)
	{
		return CreatePlayerCombatant(build, _data);
	}

	public static Combatant CreatePlayerCombatant(PlayerBuild build, IGameData? data = null)
	{
		if (data == null)
		{
			data = GameDataProvider.Shared;
		}
		PlayerCombatantSpec spec = new PlayerCombatantSpec("player", build.DisplayName, build.ClassId, build.Level)
		{
			Avatar = build.Avatar,
			BornSeq = 0,
			Allocations = build.Allocations,
			CurrentGold = build.StartingGold
		};
		return CombatantBuilder.CreatePlayer(data, spec);
	}
}
