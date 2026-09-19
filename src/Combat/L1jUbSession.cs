using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class L1jUbSession
{
	private readonly IReadOnlyList<(L1jUbStep Step, double WaitAfter)> _schedule;

	private int _index;

	private double _timer;

	public L1jUbArena Arena { get; }

	public int Pattern { get; }

	public int Round
	{
		get
		{
			if (_index >= _schedule.Count)
			{
				return 0;
			}
			return _schedule[_index].Step.Round;
		}
	}

	public bool IsFinished { get; private set; }

	public double SecondsToNextStep => Math.Max(0.0, _timer);

	public int StepCount => _schedule.Count;

	public L1jUbSession(L1jUltimateBattleCatalog catalog, L1jUbArena arena, int pattern, double countdownSeconds)
	{
		ArgumentNullException.ThrowIfNull(catalog, "catalog");
		ArgumentNullException.ThrowIfNull(arena, "arena");
		if (countdownSeconds < 0.0)
		{
			throw new ArgumentOutOfRangeException("countdownSeconds");
		}
		Arena = arena;
		Pattern = pattern;
		List<(L1jUbStep, double)> list = new List<(L1jUbStep, double)>();
		for (int i = 1; i <= 4; i++)
		{
			foreach (L1jUbWaveGroup item in L1jUltimateBattleRules.Wave(arena, pattern, i))
			{
				list.Add((new L1jUbStep(L1jUbStepKind.SpawnGroup, i, item), item.SpawnDelaySeconds));
			}
			list.Add((new L1jUbStep(L1jUbStepKind.Supplies, i), catalog.RoundWaitSeconds(i)));
		}
		list.Add((new L1jUbStep(L1jUbStepKind.Finished, 4), 0.0));
		_schedule = list;
		_timer = countdownSeconds;
	}

	public IReadOnlyList<L1jUbStep> Advance(double seconds)
	{
		if (IsFinished || seconds <= 0.0)
		{
			return Array.Empty<L1jUbStep>();
		}
		List<L1jUbStep> list = new List<L1jUbStep>();
		double num = seconds;
		while (!IsFinished && num >= _timer)
		{
			num -= _timer;
			var (item, timer) = _schedule[_index];
			_index++;
			_timer = timer;
			list.Add(item);
			if (item.Kind == L1jUbStepKind.Finished)
			{
				IsFinished = true;
			}
		}
		if (!IsFinished)
		{
			_timer -= num;
		}
		return list;
	}
}
