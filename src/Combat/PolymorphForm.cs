using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record PolymorphForm(string Name, int Level, string Color, bool ControlOnly, bool KeepClassAppearance, bool ClassMorph, bool Shanna, bool TrueShanna, double? Atk, double? AtkApm, double? CastApm, double? SupportCastApm, double? Cast, double? Stun, double? Wlk, IReadOnlyDictionary<string, double>? ApmByFamily, double Md, double Mh, double Rd, double Rh, double Ed, double Eh, double Mgd, double Sp, double Mpr, double Ac, double Er, double Mr, int Gfx, int MinLevel, int WeaponMask, int ArmorMask, double? CastNoDir, IReadOnlyDictionary<string, double>? AtkByWeapon)
{
	public int RequiredLevel => Math.Max(Level, MinLevel);
}
