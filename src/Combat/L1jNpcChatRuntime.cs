using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class L1jNpcChatRuntime
{
	private sealed class Schedule
	{
		public required Combatant Speaker { get; init; }

		public required L1jNpcChatDefinition Definition { get; init; }

		public required double SequenceStartSeconds { get; set; }

		public required double DueSeconds { get; set; }

		public int LineIndex { get; set; }
	}

	private const double Epsilon = 1E-09;

	private readonly L1jNpcChatCatalog _catalog;

	private readonly Dictionary<(Combatant, L1jNpcChatTiming), Schedule> _schedules = new Dictionary<(Combatant, L1jNpcChatTiming), Schedule>();

	public int ActiveScheduleCount => _schedules.Count;

	public L1jNpcChatRuntime(L1jNpcChatCatalog catalog)
	{
		_catalog = catalog ?? throw new ArgumentNullException("catalog");
	}

	public bool Start(Combatant speaker, L1jNpcChatTiming timing, double nowSeconds)
	{
		ArgumentNullException.ThrowIfNull(speaker, "speaker");
		if (!double.IsFinite(nowSeconds) || nowSeconds < 0.0)
		{
			throw new ArgumentOutOfRangeException("nowSeconds");
		}
		if (speaker.Kind != CombatantKind.Mob || !StateAllows(speaker, timing))
		{
			return false;
		}
		L1jNpcChatDefinition l1jNpcChatDefinition = _catalog.Find(speaker.Avatar, timing);
		if ((object)l1jNpcChatDefinition == null)
		{
			return false;
		}
		if (timing == L1jNpcChatTiming.Death)
		{
			_schedules.Remove((speaker, L1jNpcChatTiming.Appearance));
			_schedules.Remove((speaker, L1jNpcChatTiming.Hide));
			_schedules.Remove((speaker, L1jNpcChatTiming.GameTime));
		}
		double num = nowSeconds + (double)l1jNpcChatDefinition.StartDelayMs / 1000.0;
		_schedules[(speaker, timing)] = new Schedule
		{
			Speaker = speaker,
			Definition = l1jNpcChatDefinition,
			SequenceStartSeconds = num,
			DueSeconds = num,
			LineIndex = 0
		};
		return true;
	}

	public void Cancel(Combatant speaker)
	{
		ArgumentNullException.ThrowIfNull(speaker, "speaker");
		(Combatant, L1jNpcChatTiming)[] array = _schedules.Keys.Where<(Combatant, L1jNpcChatTiming)>(((Combatant, L1jNpcChatTiming) tuple) => tuple.Item1 == speaker).ToArray();
		foreach ((Combatant, L1jNpcChatTiming) key in array)
		{
			_schedules.Remove(key);
		}
	}

	public IReadOnlyList<L1jNpcChatEmission> Advance(double nowSeconds, IReadOnlyCollection<Combatant> liveActors)
	{
		if (!double.IsFinite(nowSeconds) || nowSeconds < 0.0)
		{
			throw new ArgumentOutOfRangeException("nowSeconds");
		}
		ArgumentNullException.ThrowIfNull(liveActors, "liveActors");
		List<L1jNpcChatEmission> list = new List<L1jNpcChatEmission>();
		KeyValuePair<(Combatant, L1jNpcChatTiming), Schedule>[] array = _schedules.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<(Combatant, L1jNpcChatTiming), Schedule> keyValuePair = array[i];
			Schedule value = keyValuePair.Value;
			if (!liveActors.Contains(value.Speaker) || !StateAllows(value.Speaker, value.Definition.Timing))
			{
				_schedules.Remove(keyValuePair.Key);
				continue;
			}
			int num = 0;
			while (value.DueSeconds <= nowSeconds + 1E-09)
			{
				if (++num > 1000)
				{
					throw new InvalidOperationException("NPC chat schedule exceeded 1000 due emissions in one advance.");
				}
				string chatToken = value.Definition.ChatTokens[value.LineIndex];
				list.Add(new L1jNpcChatEmission(value.Speaker, chatToken, value.Definition.Timing, value.Definition.Shout, value.Definition.WorldChat));
				value.LineIndex++;
				if (value.LineIndex < value.Definition.ChatTokens.Count)
				{
					value.DueSeconds += (double)value.Definition.ChatIntervalMs / 1000.0;
					continue;
				}
				if (!value.Definition.Repeat)
				{
					_schedules.Remove(keyValuePair.Key);
					break;
				}
				value.LineIndex = 0;
				value.SequenceStartSeconds += (double)value.Definition.RepeatIntervalMs / 1000.0;
				value.DueSeconds = value.SequenceStartSeconds;
			}
		}
		return list;
	}

	private static bool StateAllows(Combatant actor, L1jNpcChatTiming timing)
	{
		return timing switch
		{
			L1jNpcChatTiming.Death => actor.Dead, 
			L1jNpcChatTiming.Appearance => !actor.Dead, 
			L1jNpcChatTiming.Hide => !actor.Dead, 
			L1jNpcChatTiming.GameTime => !actor.Dead, 
			_ => false, 
		};
	}
}
