using System;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

internal static class CharacterMorphAnimation
{
	internal readonly record struct Spec(string Group, string Atlas, string WeaponPrefix, bool ThreeDirection)
	{
		public string VisualKey => Group + ":" + Atlas;
	}

	public static Spec Resolve(Combatant actor, IGameData data, string classAvatar, string classWeaponPrefix)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		PolymorphForm polymorphForm = PolymorphRules.ForcedForm(data, actor);
		if ((object)polymorphForm != null)
		{
			return Morph(data, polymorphForm.Name, classAvatar, classWeaponPrefix);
		}
		PolymorphForm polymorphForm2 = PolymorphRules.ActiveForm(data, actor);
		if ((object)polymorphForm2 != null)
		{
			if (polymorphForm2.KeepClassAppearance)
			{
				return Class(classAvatar, classWeaponPrefix);
			}
			if (polymorphForm2.ClassMorph)
			{
				return Class("真夏納" + classAvatar, classWeaponPrefix);
			}
			return Morph(data, polymorphForm2.Name, classAvatar, classWeaponPrefix);
		}
		return Class(classAvatar, classWeaponPrefix);
	}

	private static Spec Class(string atlas, string weaponPrefix)
	{
		return new Spec("classanim", atlas, weaponPrefix, ThreeDirection: false);
	}

	public static Spec ResolveForm(IGameData data, string formName)
	{
		return Morph(data, formName, "男騎士", "");
	}

	private static Spec Morph(IGameData data, string morphId, string fallbackAvatar = "男騎士", string fallbackWeaponPrefix = "")
	{
		if (string.Equals(morphId, "妖魔密使", StringComparison.Ordinal))
		{
			return new Spec("anim", "mob_784", "", ThreeDirection: false);
		}
		if (data.HasTable("L1J_MOB_SPRITES") && data.Table("L1J_MOB_SPRITES") is JsonObject jsonObject && jsonObject["byPolymorph"]?[morphId] is JsonObject jsonObject2 && jsonObject2["atlas"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
		{
			return new Spec("anim", value, "", ThreeDirection: false);
		}
		return Class(fallbackAvatar, fallbackWeaponPrefix);
	}
}
