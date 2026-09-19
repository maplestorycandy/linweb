using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Combat;

public sealed class L1jMobGroupRuntime
{
	private sealed class Group
	{
		public required bool RemoveWithLeader { get; init; }

		public required List<Combatant> Members { get; init; }

		public Combatant Leader => Members[0];
	}

	private readonly Dictionary<Combatant, Group> _byMember = new Dictionary<Combatant, Group>();

	public void Attach(Combatant leader, IReadOnlyList<Combatant> minions, bool removeGroupIfLeaderDies)
	{
		ArgumentNullException.ThrowIfNull(leader, "leader");
		ArgumentNullException.ThrowIfNull(minions, "minions");
		if (leader.Kind != CombatantKind.Mob || minions.Any((Combatant member) => member.Kind != CombatantKind.Mob))
		{
			throw new ArgumentException("L1J mob groups may contain mobs only.");
		}
		List<Combatant> list = new List<Combatant>(minions.Count + 1) { leader };
		list.AddRange(minions);
		if (list.Distinct().Count() != list.Count || list.Any(_byMember.ContainsKey))
		{
			throw new InvalidOperationException("A mob cannot be attached to more than one L1J mob group.");
		}
		Group value = new Group
		{
			RemoveWithLeader = removeGroupIfLeaderDies,
			Members = list
		};
		foreach (Combatant item in list)
		{
			_byMember.Add(item, value);
			item.MobGroupLeader = leader;
		}
	}

	public Combatant? LeaderFor(Combatant member)
	{
		if (!_byMember.TryGetValue(member, out Group value))
		{
			return null;
		}
		return value.Leader;
	}

	public IReadOnlyList<Combatant> MembersFor(Combatant member)
	{
		if (!_byMember.TryGetValue(member, out Group value))
		{
			return Array.Empty<Combatant>();
		}
		return value.Members.ToArray();
	}

	public bool SameGroup(Combatant left, Combatant right)
	{
		if (_byMember.TryGetValue(left, out Group value) && _byMember.TryGetValue(right, out Group value2))
		{
			return value == value2;
		}
		return false;
	}

	public L1jMobGroupDeathOutcome RecordDeath(Combatant dead)
	{
		ArgumentNullException.ThrowIfNull(dead, "dead");
		if (!_byMember.TryGetValue(dead, out Group value))
		{
			return new L1jMobGroupDeathOutcome(L1jMobGroupDeathKind.NotMember);
		}
		bool flag = value.Leader == dead;
		_byMember.Remove(dead);
		value.Members.Remove(dead);
		dead.MobGroupLeader = null;
		if (value.Members.Count == 0)
		{
			return new L1jMobGroupDeathOutcome(L1jMobGroupDeathKind.GroupDefeated);
		}
		if (!flag)
		{
			return new L1jMobGroupDeathOutcome(L1jMobGroupDeathKind.MemberRemoved);
		}
		if (value.RemoveWithLeader)
		{
			foreach (Combatant member in value.Members)
			{
				_byMember.Remove(member);
				member.MobGroupLeader = null;
			}
			value.Members.Clear();
			return new L1jMobGroupDeathOutcome(L1jMobGroupDeathKind.LeaderDetached);
		}
		Combatant leader = value.Leader;
		foreach (Combatant member2 in value.Members)
		{
			member2.MobGroupLeader = leader;
		}
		return new L1jMobGroupDeathOutcome(L1jMobGroupDeathKind.LeaderPromoted, leader);
	}

	public IReadOnlyList<Combatant> DetachWholeGroup(Combatant member)
	{
		ArgumentNullException.ThrowIfNull(member, "member");
		if (!_byMember.TryGetValue(member, out Group value))
		{
			return Array.Empty<Combatant>();
		}
		Combatant[] array = value.Members.ToArray();
		Combatant[] array2 = array;
		foreach (Combatant combatant in array2)
		{
			_byMember.Remove(combatant);
			combatant.MobGroupLeader = null;
		}
		value.Members.Clear();
		return array;
	}
}
