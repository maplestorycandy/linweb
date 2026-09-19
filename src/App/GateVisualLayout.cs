using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IdleLineage.App;

public static class GateVisualLayout
{
	public readonly record struct Candidate(MapLinks.Gate Gate, Vector2 Position);

	public static bool ShouldRender(string sourceMapKey, MapLinks.Gate gate)
	{
		return !string.Equals(sourceMapKey, gate.TargetKey, StringComparison.Ordinal);
	}

	public static IReadOnlyList<Candidate> Consolidate(IReadOnlyList<Candidate> candidates)
	{
		if (candidates.Count < 2)
		{
			return candidates.ToArray();
		}
		float mergeDistanceSquared = 6400f;
		List<List<Candidate>> list = new List<List<Candidate>>();
		foreach (Candidate candidate in candidates)
		{
			List<int> list2 = new List<int>();
			for (int i = 0; i < list.Count; i++)
			{
				List<Candidate> list3 = list[i];
				if (SameDestination(list3[0].Gate, candidate.Gate) && list3.Any((Candidate member) => member.Position.DistanceSquaredTo(candidate.Position) <= mergeDistanceSquared))
				{
					list2.Add(i);
				}
			}
			if (list2.Count == 0)
			{
				list.Add(new List<Candidate> { candidate });
				continue;
			}
			int index = list2[0];
			list[index].Add(candidate);
			for (int num = list2.Count - 1; num >= 1; num--)
			{
				int index2 = list2[num];
				list[index].AddRange(list[index2]);
				list.RemoveAt(index2);
			}
		}
		List<Candidate> list4 = new List<Candidate>(list.Count);
		foreach (List<Candidate> item in list)
		{
			Vector2 zero = Vector2.Zero;
			foreach (Candidate item2 in item)
			{
				zero += item2.Position;
			}
			zero /= (float)item.Count;
			list4.Add(new Candidate(item[0].Gate, zero));
		}
		return list4;
	}

	private static bool SameDestination(MapLinks.Gate left, MapLinks.Gate right)
	{
		if (left.ToTown != right.ToTown || !string.Equals(left.TargetKey, right.TargetKey, StringComparison.Ordinal))
		{
			return false;
		}
		(int, int)? destinationGameCell = left.DestinationGameCell;
		if (destinationGameCell.HasValue)
		{
			(int, int) valueOrDefault = destinationGameCell.GetValueOrDefault();
			destinationGameCell = right.DestinationGameCell;
			if (destinationGameCell.HasValue)
			{
				(int, int) valueOrDefault2 = destinationGameCell.GetValueOrDefault();
				(int, int) tuple = valueOrDefault;
				(int, int) tuple2 = valueOrDefault2;
				if (tuple.Item1 != tuple2.Item1 || tuple.Item2 != tuple2.Item2)
				{
					return false;
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(left.DestinationLandmarkId) && !string.IsNullOrWhiteSpace(right.DestinationLandmarkId) && !string.Equals(left.DestinationLandmarkId, right.DestinationLandmarkId, StringComparison.Ordinal))
		{
			return false;
		}
		return true;
	}
}
