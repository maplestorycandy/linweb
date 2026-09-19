using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetRosterSave
{
	private sealed class PetRosterSaveData
	{
		public int Version { get; set; }

		public PetSaveData[]? Pets { get; set; }
	}

	private sealed class PetSaveData
	{
		public string Uid { get; set; } = "";

		public string Form { get; set; } = "";

		public string? Name { get; set; }

		public int Level { get; set; }

		public double Experience { get; set; }

		public double MaxHp { get; set; }

		public double MaxMp { get; set; }

		public double Hp { get; set; }

		public double Mp { get; set; }

		public string? OwnerKey { get; set; }

		public bool Locked { get; set; }

		public bool Downed { get; set; }

		public int? Food { get; set; }

		public int? Lawful { get; set; }

		public PetCommandStatus? CommandStatus { get; set; }

		public ItemSaveData[]? Equipment { get; set; }
	}

	private sealed class ItemSaveData
	{
		public string Slot { get; set; } = "";

		public string Uid { get; set; } = "";

		public string ItemKey { get; set; } = "";

		public int Enhancement { get; set; }

		public ItemBlessing Blessing { get; set; }

		public bool IsIdentified { get; set; } = true;

		public string? Attribute { get; set; }

		public string? AttributeMagic { get; set; }

		public int BrokenBladeStacks { get; set; }

		public bool Locked { get; set; }

		public bool Downed { get; set; }
	}

	public const int CurrentVersion = 3;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Capture(PetRoster roster)
	{
		ArgumentNullException.ThrowIfNull(roster, "roster");
		return JsonSerializer.Serialize(new PetRosterSaveData
		{
			Version = 3,
			Pets = roster.Pets.Select(CapturePet).ToArray()
		}, JsonOptions);
	}

	public static PetRoster Restore(IGameData data, string blob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(blob, "blob");
		PetRosterSaveData petRosterSaveData;
		try
		{
			petRosterSaveData = JsonSerializer.Deserialize<PetRosterSaveData>(blob, JsonOptions) ?? throw new InvalidDataException("Pet roster save is empty.");
		}
		catch (JsonException innerException)
		{
			throw new InvalidDataException("Pet roster save is not valid JSON.", innerException);
		}
		int version = petRosterSaveData.Version;
		bool flag = (uint)(version - 1) <= 2u;
		if (!flag || petRosterSaveData.Pets == null)
		{
			throw new InvalidDataException("Pet roster save has an unsupported version or size.");
		}
		PetRoster petRoster = new PetRoster();
		PetSaveData[] pets = petRosterSaveData.Pets;
		version = 0;
		while (version < pets.Length)
		{
			PetSaveData petSaveData = pets[version];
			var (num, num2) = RestoreExperience(data, petRosterSaveData.Version, petSaveData);
			if (!string.IsNullOrWhiteSpace(petSaveData.Uid) && !string.IsNullOrWhiteSpace(petSaveData.Form) && num >= 1 && num <= 50 && !(petSaveData.MaxHp <= 0.0) && !(petSaveData.MaxMp < 0.0) && double.IsFinite(num2))
			{
				int? food = petSaveData.Food;
				if ((!food.HasValue || food.GetValueOrDefault() >= 0) && (!petSaveData.CommandStatus.HasValue || Enum.IsDefined(petSaveData.CommandStatus.Value)))
				{
					PetRules.Definition(data, petSaveData.Form);
					PetInstance petInstance = new PetInstance(petSaveData.Uid, petSaveData.Form, num, petSaveData.MaxHp, petSaveData.MaxMp)
					{
						Name = (petSaveData.Name ?? ""),
						Experience = num2,
						ExperiencePercent = ProgressionRules.ExperiencePercentage(data, num, num2),
						Hp = Math.Clamp(petSaveData.Hp, 0.0, petSaveData.MaxHp),
						Mp = Math.Clamp(petSaveData.Mp, 0.0, petSaveData.MaxMp),
						OwnerKey = "",
						Locked = false,
						Downed = petSaveData.Downed,
						Food = (petSaveData.Food ?? 20),
						Lawful = petSaveData.Lawful.GetValueOrDefault(),
						CommandStatus = PetCommandStatus.Stay
					};
					petInstance.ActiveCharmCost = 0.0;
					ItemSaveData[] array = petSaveData.Equipment ?? Array.Empty<ItemSaveData>();
					foreach (ItemSaveData itemSaveData in array)
					{
						string slot = itemSaveData.Slot;
						if ((!(slot == "petwpn") && !(slot == "petarm")) || string.IsNullOrWhiteSpace(itemSaveData.Uid) || string.IsNullOrWhiteSpace(itemSaveData.ItemKey) || itemSaveData.BrokenBladeStacks < 0 || !L1jPetItemCatalog.Load(data).TryGet(itemSaveData.ItemKey, out L1jPetItemDefinition definition) || definition.Slot != itemSaveData.Slot || petInstance.Equipment.ContainsKey(itemSaveData.Slot))
						{
							throw new InvalidDataException("Pet roster save contains invalid equipment.");
						}
						petInstance.Equipment[itemSaveData.Slot] = new ItemStack(itemSaveData.Uid, itemSaveData.ItemKey, 1L)
						{
							Enhancement = itemSaveData.Enhancement,
							Blessing = itemSaveData.Blessing,
							IsIdentified = itemSaveData.IsIdentified,
							BrokenBladeStacks = itemSaveData.BrokenBladeStacks,
							Locked = itemSaveData.Locked
						};
					}
					petRoster.Restore(petInstance);
					version++;
					continue;
				}
			}
			throw new InvalidDataException("Pet roster save contains invalid pet state.");
		}
		return petRoster;
	}

	private static (int Level, double Experience) RestoreExperience(IGameData data, int version, PetSaveData source)
	{
		if (!double.IsFinite(source.Experience) || source.Experience < 0.0)
		{
			throw new InvalidDataException("Pet roster save contains invalid pet experience.");
		}
		int num = ((version == 1) ? 100 : 50);
		if (source.Level < 1 || source.Level > num)
		{
			throw new InvalidDataException("Pet roster save contains an invalid pet level.");
		}
		if (source.Level > 50)
		{
			return (Level: 50, Experience: ProgressionRules.ExperienceAtLevel(data, 51) - 1.0);
		}
		int level = source.Level;
		double num2 = ProgressionRules.ExperienceAtLevel(data, level);
		double num3 = ProgressionRules.ExperienceAtLevel(data, level + 1);
		double value = ((version == 1 || (version == 2 && source.Experience < num2)) ? (num2 + Math.Floor(source.Experience)) : Math.Floor(source.Experience));
		return (Level: level, Experience: Math.Clamp(value, num2, num3 - 1.0));
	}

	private static PetSaveData CapturePet(PetInstance pet)
	{
		return new PetSaveData
		{
			Uid = pet.Uid,
			Form = pet.Form,
			Name = pet.Name,
			Level = pet.Level,
			Experience = pet.Experience,
			MaxHp = pet.MaxHp,
			MaxMp = pet.MaxMp,
			Hp = pet.Hp,
			Mp = pet.Mp,
			Downed = pet.Downed,
			Food = pet.Food,
			Lawful = pet.Lawful,
			Equipment = pet.Equipment.Select<KeyValuePair<string, ItemStack>, ItemSaveData>((KeyValuePair<string, ItemStack> pair) => new ItemSaveData
			{
				Slot = pair.Key,
				Uid = pair.Value.Uid,
				ItemKey = pair.Value.ItemKey,
				Enhancement = pair.Value.Enhancement,
				Blessing = pair.Value.Blessing,
				IsIdentified = pair.Value.IsIdentified,
				BrokenBladeStacks = pair.Value.BrokenBladeStacks,
				Locked = pair.Value.Locked
			}).ToArray()
		};
	}
}
