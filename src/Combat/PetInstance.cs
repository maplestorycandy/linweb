using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed class PetInstance
{
	public string Uid { get; }

	public string Form { get; internal set; }

	public string Name { get; set; } = "";

	public int Level { get; internal set; }

	public double Experience { get; internal set; }

	public int ExperiencePercent { get; internal set; }

	public double MaxHp { get; internal set; }

	public double MaxMp { get; internal set; }

	public double Hp { get; internal set; }

	public double Mp { get; internal set; }

	public string OwnerKey { get; internal set; } = "";

	public double ActiveCharmCost { get; internal set; }

	public bool Locked { get; set; }

	public bool Downed { get; internal set; }

	public int Food { get; internal set; } = 20;

	public int Lawful { get; internal set; }

	public PetCommandStatus CommandStatus { get; internal set; } = PetCommandStatus.Stay;

	public Dictionary<string, ItemStack> Equipment { get; } = new Dictionary<string, ItemStack>(StringComparer.Ordinal);

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Name))
			{
				return Name + "（" + Form + "）";
			}
			return Form;
		}
	}

	internal PetInstance(string uid, string form, int level, double maxHp, double maxMp)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentException.ThrowIfNullOrWhiteSpace(form, "form");
		Uid = uid;
		Form = form;
		Level = Math.Max(1, level);
		MaxHp = Math.Max(1.0, maxHp);
		MaxMp = Math.Max(0.0, maxMp);
		Hp = MaxHp;
		Mp = MaxMp;
	}
}
