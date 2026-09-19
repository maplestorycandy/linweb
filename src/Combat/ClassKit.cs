using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public sealed class ClassKit
{
	private readonly Func<JsonObject, int?> _levelResolver;

	private readonly Func<WeaponRuleContext, bool> _weaponRule;

	public string Id { get; }

	public string DefaultAvatar { get; }

	internal ClassKit(string id, string defaultAvatar, Func<JsonObject, int?> levelResolver, Func<WeaponRuleContext, bool> weaponRule)
	{
		Id = id;
		DefaultAvatar = defaultAvatar;
		_levelResolver = levelResolver;
		_weaponRule = weaponRule;
	}

	public bool TryGetRequiredLevel(JsonObject skill, out int requiredLevel)
	{
		int? num = _levelResolver(skill);
		requiredLevel = num.GetValueOrDefault();
		return num.HasValue;
	}

	internal bool CanEquipWeapon(WeaponRuleContext context)
	{
		return _weaponRule(context);
	}
}
