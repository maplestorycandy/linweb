using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class L1jTrapRuntime
{
	private sealed class Instance
	{
		public int CellX;

		public int CellY;

		public bool Enabled = true;

		public long EnableAtMs;

		public required L1jTrapPlacement Placement { get; init; }

		public required L1jTrapDefinition Definition { get; init; }

		public required int Index { get; init; }
	}

	private readonly List<Instance> _instances = new List<Instance>();

	private readonly Func<int, int, bool> _isWalkable;

	private readonly Random _random;

	public int InstanceCount => _instances.Count;

	public L1jTrapRuntime(L1jTrapCatalog catalog, string mapKey, Func<int, int, bool> isWalkable, int seed = 6778)
	{
		ArgumentNullException.ThrowIfNull(catalog, "catalog");
		ArgumentException.ThrowIfNullOrWhiteSpace(mapKey, "mapKey");
		_isWalkable = isWalkable ?? throw new ArgumentNullException("isWalkable");
		_random = new Random(seed);
		foreach (L1jTrapPlacement item in catalog.PlacementsFor(mapKey))
		{
			L1jTrapDefinition definition = catalog.RequireDefinition(item.TrapId);
			for (int i = 0; i < item.Count; i++)
			{
				Instance instance = new Instance
				{
					Placement = item,
					Definition = definition,
					Index = i
				};
				ResetLocation(instance);
				_instances.Add(instance);
			}
		}
	}

	public int EnabledCount(long nowMs)
	{
		Refresh(nowMs);
		return _instances.Count((Instance instance) => instance.Enabled);
	}

	public IReadOnlyList<L1jTrapActivation> OnPlayerMoved(int cellX, int cellY, long nowMs)
	{
		Refresh(nowMs);
		List<L1jTrapActivation> list = new List<L1jTrapActivation>();
		foreach (Instance instance in _instances)
		{
			if (instance.Enabled && instance.CellX == cellX && instance.CellY == cellY)
			{
				list.Add(Disable(instance, nowMs, detected: false));
			}
		}
		return list;
	}

	public IReadOnlyList<L1jTrapActivation> Detect(int centerX, int centerY, int radius, long nowMs)
	{
		if (radius < 0)
		{
			throw new ArgumentOutOfRangeException("radius");
		}
		Refresh(nowMs);
		List<L1jTrapActivation> list = new List<L1jTrapActivation>();
		foreach (Instance instance in _instances)
		{
			if (instance.Enabled && Math.Max(Math.Abs(instance.CellX - centerX), Math.Abs(instance.CellY - centerY)) <= radius)
			{
				list.Add(Disable(instance, nowMs, detected: true));
			}
		}
		return list;
	}

	private L1jTrapActivation Disable(Instance instance, long nowMs, bool detected)
	{
		instance.Enabled = false;
		instance.EnableAtMs = checked(nowMs + instance.Placement.SpanMs);
		return new L1jTrapActivation(instance.Placement.SpawnId, instance.Index, instance.CellX, instance.CellY, instance.Definition, detected);
	}

	private void Refresh(long nowMs)
	{
		foreach (Instance instance in _instances)
		{
			if (!instance.Enabled && nowMs >= instance.EnableAtMs)
			{
				ResetLocation(instance);
				instance.Enabled = true;
			}
		}
	}

	private void ResetLocation(Instance instance)
	{
		for (int i = 0; i < 50; i++)
		{
			int num = Randomized(instance.Placement.CellX, instance.Placement.RandomX);
			int num2 = Randomized(instance.Placement.CellY, instance.Placement.RandomY);
			if (_isWalkable(num, num2))
			{
				instance.CellX = num;
				instance.CellY = num2;
				return;
			}
		}
		if (_isWalkable(instance.Placement.CellX, instance.Placement.CellY))
		{
			instance.CellX = instance.Placement.CellX;
			instance.CellY = instance.Placement.CellY;
		}
	}

	private int Randomized(int origin, int range)
	{
		if (range <= 0)
		{
			return origin;
		}
		int num = _random.Next(range);
		if (_random.Next(2) != 0)
		{
			return origin - num;
		}
		return origin + num;
	}
}
