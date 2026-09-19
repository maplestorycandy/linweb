using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class ExplorationSpawnSession
{
	public const int MinimumSpawnSeparationCells = 4;

	public const int NearbyNormalMobRadiusCells = 24;

	public const int MaximumNearbyNormalMobs = 16;

	private const int MainPlacementAttempts = 50;

	private readonly MapTopology _map;

	private readonly ICombatRandom _random;

	private readonly Func<MapSpawnCell, MapSpawnCell, bool>? _connected;

	private readonly IReadOnlyList<MapSpawnPoint> _fixedSpawnPoints;

	private readonly Dictionary<string, MapSpawnPoint> _pointsBySlot = new Dictionary<string, MapSpawnPoint>(StringComparer.Ordinal);

	private readonly HashSet<string> _activeSlots = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, double> _respawnReadyAt = new Dictionary<string, double>(StringComparer.Ordinal);

	private MapSpawnCell? _lastPlayerCell;

	private ExplorationSpawnPlan? _pendingPlan;

	public IReadOnlyList<MapSpawnPoint> CommittedSpawnPoints => _fixedSpawnPoints;

	public ExplorationSpawnSession(MapTopology map, ICombatRandom random, IReadOnlySet<MapSpawnCell>? villageCells = null, Func<MapSpawnCell, MapSpawnCell, bool>? connected = null, IReadOnlyList<MapSpawnPoint>? fixedSpawnPoints = null)
	{
		_map = map ?? throw new ArgumentNullException("map");
		_random = random ?? throw new ArgumentNullException("random");
		_connected = connected;
		List<MapSpawnPoint> list = new List<MapSpawnPoint>();
		if (fixedSpawnPoints != null)
		{
			foreach (MapSpawnPoint fixedSpawnPoint in fixedSpawnPoints)
			{
				if (string.IsNullOrWhiteSpace(fixedSpawnPoint.SlotKey))
				{
					throw new InvalidDataException("A fixed spawn slot must have a stable slot key.");
				}
				if (_pointsBySlot.ContainsKey(fixedSpawnPoint.SlotKey))
				{
					throw new InvalidDataException("Duplicate fixed spawn slot '" + fixedSpawnPoint.SlotKey + "'.");
				}
				MapSpawnCell? mapSpawnCell = ResolveCommittedCell(fixedSpawnPoint);
				if (mapSpawnCell.HasValue)
				{
					MapSpawnCell valueOrDefault = mapSpawnCell.GetValueOrDefault();
					MapSpawnPoint mapSpawnPoint = fixedSpawnPoint with
					{
						Cell = valueOrDefault
					};
					_pointsBySlot.Add(mapSpawnPoint.SlotKey, mapSpawnPoint);
					list.Add(mapSpawnPoint);
				}
			}
		}
		_fixedSpawnPoints = ((list.Count == 0) ? Array.Empty<MapSpawnPoint>() : list.OrderBy<MapSpawnPoint, string>((MapSpawnPoint point) => point.SlotKey, StringComparer.Ordinal).ToArray());
	}

	public static HashSet<MapSpawnCell> BuildVillageCells(MapTopology map)
	{
		ArgumentNullException.ThrowIfNull(map, "map");
		HashSet<MapSpawnCell> hashSet = new HashSet<MapSpawnCell>();
		IReadOnlyList<IntegratedTownDefinition> readOnlyList = IntegratedTownCatalog.FindAllByMap(map.MapKey);
		if (readOnlyList.Count == 0)
		{
			return hashSet;
		}
		if (!map.HasSafeZone)
		{
			for (int i = 0; i < map.HeightCells; i++)
			{
				for (int j = 0; j < map.WidthCells; j++)
				{
					if (map.IsWalkableCell(j, i))
					{
						hashSet.Add(new MapSpawnCell(j, i));
					}
				}
			}
			return hashSet;
		}
		foreach (IntegratedTownDefinition item6 in readOnlyList)
		{
			MapSpawnCell? mapSpawnCell = null;
			(int, int)? entryGameCell = item6.EntryGameCell;
			if (entryGameCell.HasValue)
			{
				(int, int) valueOrDefault = entryGameCell.GetValueOrDefault();
				int item = valueOrDefault.Item1;
				int item2 = valueOrDefault.Item2;
				(int X, int Y) tuple = map.ToLocalCell(item, item2);
				int item3 = tuple.X;
				int item4 = tuple.Y;
				mapSpawnCell = new MapSpawnCell(item3, item4);
			}
			else
			{
				string entryLandmarkId = item6.EntryLandmarkId;
				if (entryLandmarkId != null)
				{
					foreach (MapLandmark landmark in map.Landmarks)
					{
						if (string.Equals(landmark.Id, entryLandmarkId, StringComparison.Ordinal))
						{
							mapSpawnCell = new MapSpawnCell(landmark.LocalX, landmark.LocalY);
							break;
						}
					}
				}
			}
			if (!mapSpawnCell.HasValue)
			{
				continue;
			}
			MapSpawnCell valueOrDefault2 = mapSpawnCell.GetValueOrDefault();
			if (!map.IsSafeCell(valueOrDefault2.X, valueOrDefault2.Y) || !hashSet.Add(valueOrDefault2))
			{
				continue;
			}
			Queue<MapSpawnCell> queue = new Queue<MapSpawnCell>();
			queue.Enqueue(valueOrDefault2);
			while (queue.Count > 0)
			{
				MapSpawnCell mapSpawnCell2 = queue.Dequeue();
				for (int k = -1; k <= 1; k++)
				{
					for (int l = -1; l <= 1; l++)
					{
						if (l != 0 || k != 0)
						{
							MapSpawnCell item5 = new MapSpawnCell(mapSpawnCell2.X + l, mapSpawnCell2.Y + k);
							if (map.IsSafeCell(item5.X, item5.Y) && hashSet.Add(item5))
							{
								queue.Enqueue(item5);
							}
						}
					}
				}
			}
		}
		return hashSet;
	}

	public ExplorationSpawnPlan? PlanStep(MapSpawnCell playerCell, int livingNormalMobCount, bool bossAlive, double currentTimeSeconds = 0.0, bool ignoreDistance = false, int? triggerDistance = null)
	{
		if (livingNormalMobCount < 0)
		{
			throw new ArgumentOutOfRangeException("livingNormalMobCount");
		}
		_lastPlayerCell = playerCell;
		if (!double.IsFinite(currentTimeSeconds) || currentTimeSeconds < 0.0)
		{
			throw new ArgumentOutOfRangeException("currentTimeSeconds");
		}
		MapSpawnSettings spawnSettings = _map.SpawnSettings;
		int maxNormalMobs = Math.Max(spawnSettings.MaximumLivingNormalMobs, _fixedSpawnPoints.Count);
		bool flag = livingNormalMobCount >= maxNormalMobs;
		MapSpawnPoint? mapSpawnPoint = null;
		int num = int.MaxValue;
		foreach (MapSpawnPoint fixedSpawnPoint in _fixedSpawnPoints)
		{
			if (!_activeSlots.Contains(fixedSpawnPoint.SlotKey) && 
			    (!_respawnReadyAt.TryGetValue(fixedSpawnPoint.SlotKey, out var value) || !(currentTimeSeconds + 1E-09 < value)) && 
			    (fixedSpawnPoint.IsBoss ? true : !flag))
			{
				int num2 = Chebyshev(playerCell, fixedSpawnPoint.Cell);
				bool distanceOk;
				if (ignoreDistance || (triggerDistance.HasValue && triggerDistance.Value <= 0))
				{
					distanceOk = true;
				}
				else if (triggerDistance.HasValue)
				{
					distanceOk = (num2 <= triggerDistance.Value);
				}
				else
				{
					distanceOk = (num2 >= spawnSettings.MinimumHiddenDistanceCells && num2 <= spawnSettings.MaximumHiddenDistanceCells);
				}
				bool connectedOk = ignoreDistance || triggerDistance.HasValue || (_connected == null || _connected(playerCell, fixedSpawnPoint.Cell));
				if (distanceOk && connectedOk && (!mapSpawnPoint.HasValue || num2 < num || (num2 == num && string.CompareOrdinal(fixedSpawnPoint.SlotKey, mapSpawnPoint.Value.SlotKey) < 0)))
				{
					mapSpawnPoint = fixedSpawnPoint;
					num = num2;
				}
			}
		}
		if (mapSpawnPoint.HasValue)
		{
			MapSpawnPoint valueOrDefault = mapSpawnPoint.GetValueOrDefault();
			ExplorationSpawnPlan value2 = new ExplorationSpawnPlan(valueOrDefault.IsBoss ? ExplorationSpawnKind.BossMob : ExplorationSpawnKind.NormalMob, valueOrDefault.MobKey, valueOrDefault.Cell, valueOrDefault.SlotKey, valueOrDefault.TargetCell);
			_pendingPlan = value2;
			return value2;
		}
		_pendingPlan = null;
		return null;
	}

	public void NoteSpawnPlaced(ExplorationSpawnPlan plan)
	{
		if (!_pointsBySlot.ContainsKey(plan.SlotKey))
		{
			throw new ArgumentException("Unknown fixed spawn slot '" + plan.SlotKey + "'.", "plan");
		}
		_activeSlots.Add(plan.SlotKey);
		_respawnReadyAt.Remove(plan.SlotKey);
		_pendingPlan = null;
	}

	public void NoteSpawnPlaced(MapSpawnCell cell)
	{
		ExplorationSpawnPlan? pendingPlan = _pendingPlan;
		if (pendingPlan.HasValue)
		{
			ExplorationSpawnPlan valueOrDefault = pendingPlan.GetValueOrDefault();
			if (valueOrDefault.Cell == cell)
			{
				NoteSpawnPlaced(valueOrDefault);
			}
		}
	}

	public void NoteSpawnReleased(string slotKey, double currentTimeSeconds, bool killed)
	{
		if (!_pointsBySlot.TryGetValue(slotKey, out var value))
		{
			return;
		}
		_activeSlots.Remove(slotKey);
		if (!killed)
		{
			_respawnReadyAt.Remove(slotKey);
			return;
		}
		int num = value.RespawnMinimumSeconds;
		int num2 = value.RespawnMaximumSeconds - value.RespawnMinimumSeconds;
		if (num2 > 0)
		{
			num += NextIndex(num2);
		}
		_respawnReadyAt[slotKey] = currentTimeSeconds + (double)num;
	}

	public void ResetAfterTeleport()
	{
		_lastPlayerCell = null;
		_pendingPlan = null;
	}

	private MapSpawnCell? ResolveCommittedCell(MapSpawnPoint point)
	{
		for (int i = 0; i < 50; i++)
		{
			MapSpawnCell value = CandidateFor(point, i);
			if (_map.IsWalkableCell(value.X, value.Y))
			{
				return value;
			}
		}
		if (_map.IsWalkableCell(point.Cell.X, point.Cell.Y))
		{
			return point.Cell;
		}
		int num = Math.Max(_map.WidthCells, _map.HeightCells);
		for (int j = 1; j <= num; j++)
		{
			for (int k = -j; k <= j; k++)
			{
				for (int l = -j; l <= j; l++)
				{
					if (Math.Max(Math.Abs(l), Math.Abs(k)) == j)
					{
						MapSpawnCell value2 = new MapSpawnCell(point.Cell.X + l, point.Cell.Y + k);
						if (_map.IsWalkableCell(value2.X, value2.Y))
						{
							return value2;
						}
					}
				}
			}
		}
		return null;
	}

	private static MapSpawnCell CandidateFor(MapSpawnPoint point, int attempt)
	{
		MapSpawnBounds? area = point.Area;
		if (area.HasValue)
		{
			MapSpawnBounds valueOrDefault = area.GetValueOrDefault();
			int x = valueOrDefault.MinimumX + StableIndex(point.SlotKey, attempt, 0, valueOrDefault.Width);
			int y = valueOrDefault.MinimumY + StableIndex(point.SlotKey, attempt, 1, valueOrDefault.Height);
			return new MapSpawnCell(x, y);
		}
		if (point.RandomX > 0 || point.RandomY > 0)
		{
			int x2 = point.Cell.X + SignedMainOffset(point.SlotKey, attempt, 2, point.RandomX);
			int y2 = point.Cell.Y + SignedMainOffset(point.SlotKey, attempt, 4, point.RandomY);
			return new MapSpawnCell(x2, y2);
		}
		return point.Cell;
	}

	private static int SignedMainOffset(string key, int attempt, int salt, int range)
	{
		if (range <= 0)
		{
			return 0;
		}
		int num = StableIndex(key, attempt, salt, range);
		int num2 = ((StableIndex(key, attempt, salt + 1, 2) != 0) ? 1 : (-1));
		return num * num2;
	}

	private static int StableIndex(string key, int attempt, int salt, int count)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		uint num = 2166136261u;
		foreach (char c in key)
		{
			num ^= c;
			num *= 16777619;
		}
		num ^= (uint)(attempt * -1640531527);
		num *= 16777619;
		num ^= (uint)(salt * -2048144789);
		num *= 16777619;
		return (int)(num % (uint)count);
	}

	private static int Chebyshev(MapSpawnCell left, MapSpawnCell right)
	{
		return Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y));
	}

	public static bool RespectsOrdinaryDensity(MapSpawnCell playerCell, MapSpawnCell candidate, IReadOnlyList<MapSpawnCell> livingNormalMobCells)
	{
		ArgumentNullException.ThrowIfNull(livingNormalMobCells, "livingNormalMobCells");
		int num = 0;
		foreach (MapSpawnCell livingNormalMobCell in livingNormalMobCells)
		{
			if (Math.Max(Math.Abs(candidate.X - livingNormalMobCell.X), Math.Abs(candidate.Y - livingNormalMobCell.Y)) < 4)
			{
				return false;
			}
			if (Math.Max(Math.Abs(playerCell.X - livingNormalMobCell.X), Math.Abs(playerCell.Y - livingNormalMobCell.Y)) <= 24)
			{
				num++;
			}
		}
		if (Math.Max(Math.Abs(playerCell.X - candidate.X), Math.Abs(playerCell.Y - candidate.Y)) <= 24)
		{
			return num < 16;
		}
		return true;
	}

	private int NextIndex(int count)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		double num = _random.NextDouble();
		if (!double.IsFinite(num))
		{
			throw new InvalidOperationException("Combat random returned a non-finite value.");
		}
		return Math.Clamp((int)Math.Floor(Math.Clamp(num, 0.0, Math.BitDecrement(1.0)) * (double)count), 0, count - 1);
	}
}
