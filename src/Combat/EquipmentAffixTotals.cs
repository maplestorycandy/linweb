using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class EquipmentAffixTotals
{
	private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);

	public double this[string stat] => _values.GetValueOrDefault(stat);

	internal void Add(string stat, double value)
	{
		_values[stat] = _values.GetValueOrDefault(stat) + value;
	}

	internal void Cap(string stat, double maximum)
	{
		if (_values.TryGetValue(stat, out var value))
		{
			_values[stat] = Math.Min(value, maximum);
		}
	}
}
