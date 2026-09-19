using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace IdleLineage.Combat;

public sealed class LearnedSkillCollection : IEnumerable<string>, IEnumerable
{
	private readonly HashSet<string> _skills = new HashSet<string>(StringComparer.Ordinal);

	public int Count => _skills.Count;

	public bool Add(string skillId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		return _skills.Add(skillId);
	}

	public bool Contains(string skillId)
	{
		if (!string.IsNullOrWhiteSpace(skillId))
		{
			return _skills.Contains(skillId);
		}
		return false;
	}

	public bool Remove(string skillId)
	{
		return _skills.Remove(skillId);
	}

	public void Clear()
	{
		_skills.Clear();
	}

	public void UnionWith(IEnumerable<string> skillIds)
	{
		ArgumentNullException.ThrowIfNull(skillIds, "skillIds");
		foreach (string skillId in skillIds)
		{
			Add(skillId);
		}
	}

	public void Import(IEnumerable<string> skillIds)
	{
		ArgumentNullException.ThrowIfNull(skillIds, "skillIds");
		foreach (string skillId in skillIds)
		{
			if (!Add(skillId))
			{
				throw new InvalidDataException("Learned skill '" + skillId + "' appears more than once.");
			}
		}
	}

	public bool SetEquals(IEnumerable<string> skillIds)
	{
		ArgumentNullException.ThrowIfNull(skillIds, "skillIds");
		return _skills.SetEquals(skillIds);
	}

	public IEnumerator<string> GetEnumerator()
	{
		return _skills.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
