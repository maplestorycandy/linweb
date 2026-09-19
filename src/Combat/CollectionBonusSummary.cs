namespace IdleLineage.Combat;

public readonly record struct CollectionBonusSummary(double MaxHp = 0.0, double MaxMp = 0.0, double DamageReduction = 0.0, double MagicResist = 0.0, double HealthRegen = 0.0, double ManaRegen = 0.0, double Evasion = 0.0, double ArmorClassReduction = 0.0, double WeightCapacity = 0.0, double PetHit = 0.0, double PotionHealingPercent = 0.0, double ItemSpellPower = 0.0, double ExtraDamage = 0.0, double ExtraHit = 0.0, double ResistFire = 0.0, double ResistWater = 0.0, double ResistWind = 0.0, double ResistEarth = 0.0)
{
	internal CollectionBonusSummary Add(string stat, double value)
	{
		return stat switch
		{
			"mhp" => this with
			{
				MaxHp = MaxHp + value
			}, 
			"mmp" => this with
			{
				MaxMp = MaxMp + value
			}, 
			"dr" => this with
			{
				DamageReduction = DamageReduction + value
			}, 
			"mr" => this with
			{
				MagicResist = MagicResist + value
			}, 
			"hpR" => this with
			{
				HealthRegen = HealthRegen + value
			}, 
			"mpR" => this with
			{
				ManaRegen = ManaRegen + value
			}, 
			"er" => this with
			{
				Evasion = Evasion + value
			}, 
			"ac" => this with
			{
				ArmorClassReduction = ArmorClassReduction + value
			}, 
			"weight" => this with
			{
				WeightCapacity = WeightCapacity + value
			}, 
			"petHit" => this with
			{
				PetHit = PetHit + value
			}, 
			"potion" => this with
			{
				PotionHealingPercent = PotionHealingPercent + value
			}, 
			"extraMp" => this with
			{
				ItemSpellPower = ItemSpellPower + value
			}, 
			"extraDmg" => this with
			{
				ExtraDamage = ExtraDamage + value
			}, 
			"extraHit" => this with
			{
				ExtraHit = ExtraHit + value
			}, 
			"resFire" => this with
			{
				ResistFire = ResistFire + value
			}, 
			"resWater" => this with
			{
				ResistWater = ResistWater + value
			}, 
			"resWind" => this with
			{
				ResistWind = ResistWind + value
			}, 
			"resEarth" => this with
			{
				ResistEarth = ResistEarth + value
			}, 
			_ => this, 
		};
	}
}
