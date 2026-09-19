using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class PetRoster
{
	private readonly List<PetInstance> _pets = new List<PetInstance>();

	public IReadOnlyList<PetInstance> Pets => _pets;

	public PetRosterResult TryAdd(IGameData data, string form, string uid, int? level = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(form, "form");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		if (_pets.Any((PetInstance pet) => string.Equals(pet.Uid, uid, StringComparison.Ordinal)))
		{
			return PetRosterResult.Failed(PetRosterFailure.UnknownPet);
		}
		PetInstance petInstance;
		try
		{
			petInstance = PetRules.CreateInstance(data, form, uid, level);
		}
		catch (KeyNotFoundException)
		{
			return PetRosterResult.Failed(PetRosterFailure.UnknownPet);
		}
		_pets.Add(petInstance);
		return new PetRosterResult(Success: true, PetRosterFailure.None, petInstance);
	}

	public PetRosterResult TryDeploy(IGameData data, Combatant owner, string uid, double otherPetCost = 0.0)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetRosterResult.Failed(PetRosterFailure.InvalidOwner);
		}
		PetInstance petInstance = Find(uid);
		if (petInstance == null)
		{
			return PetRosterResult.Failed(PetRosterFailure.UnknownPet);
		}
		if (string.Equals(petInstance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			return PetRosterResult.Failed(PetRosterFailure.AlreadyAssigned);
		}
		if (!string.IsNullOrEmpty(petInstance.OwnerKey))
		{
			return PetRosterResult.Failed(PetRosterFailure.AssignedToAnotherOwner);
		}
		PetInstance[] source = ActiveFor(owner).ToArray();
		double num = Math.Max(0.0, otherPetCost) + source.Sum((PetInstance activePet) => Math.Max(0.0, activePet.ActiveCharmCost));
		double num2 = CharmCostFor(data, owner, petInstance.Form);
		if (num + num2 > PetRules.MainCharmCapacity(owner))
		{
			return PetRosterResult.Failed(PetRosterFailure.InsufficientCharm);
		}
		petInstance.OwnerKey = owner.Key;
		petInstance.ActiveCharmCost = num2;
		petInstance.CommandStatus = PetCommandStatus.Stay;
		petInstance.ExperiencePercent = ProgressionRules.ExperiencePercentage(data, petInstance.Level, petInstance.Experience);
		if (!petInstance.Downed)
		{
			petInstance.Hp = Math.Max(1.0, petInstance.Hp);
		}
		return new PetRosterResult(Success: true, PetRosterFailure.None, petInstance);
	}

	public static double CharmCostFor(IGameData data, Combatant owner, string form)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return PetRules.CompanionDeploymentCharmCost(owner, PetRules.Definition(data, form).CharmCost);
	}

	public PetCommandResult CommandActivePets(Combatant owner, PetCommandStatus status)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (status == PetCommandStatus.Dismiss)
		{
			throw new ArgumentOutOfRangeException("status", "Dismiss remains the roster's explicit release workflow.");
		}
		int num = 0;
		int num2 = 0;
		foreach (PetInstance item in ActiveFor(owner))
		{
			if (owner.Level < item.Level)
			{
				num2++;
				continue;
			}
			if (status != PetCommandStatus.Collect)
			{
				item.CommandStatus = status;
			}
			num++;
		}
		return new PetCommandResult(status, num, num2);
	}

	public bool Recall(string ownerKey, string uid)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey, "ownerKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		PetInstance petInstance = Find(uid);
		if (petInstance == null || !string.Equals(petInstance.OwnerKey, ownerKey, StringComparison.Ordinal))
		{
			return false;
		}
		petInstance.OwnerKey = "";
		petInstance.ActiveCharmCost = 0.0;
		petInstance.CommandStatus = PetCommandStatus.Stay;
		return true;
	}

	public IReadOnlyList<PetInstance> ActiveFor(Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return _pets.Where((PetInstance pet) => string.Equals(pet.OwnerKey, owner.Key, StringComparison.Ordinal)).ToArray();
	}

	public IReadOnlyList<Combatant> DeployAll(IGameData data, CombatEngine engine, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(engine, "engine");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		if (owner.Kind != CombatantKind.Player || !engine.Combatants.Contains(owner))
		{
			throw new InvalidOperationException("The pet owner must be the active player in the combat engine.");
		}
		PetInstance[] array = ActiveFor(owner).ToArray();
		List<Combatant> list = new List<Combatant>(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			list.Add(engine.DeployPet(data, owner, array[i], i, array.Length));
		}
		return list;
	}

	public void Synchronize(CombatEngine engine)
	{
		ArgumentNullException.ThrowIfNull(engine, "engine");
		foreach (PetInstance pet in _pets)
		{
			engine.SynchronizePet(pet);
		}
	}

	public PetInstance? Find(string uid)
	{
		return _pets.FirstOrDefault((PetInstance pet) => string.Equals(pet.Uid, uid, StringComparison.Ordinal));
	}

	public IReadOnlyList<PetEvolutionOption> EvolutionOptions(IGameData data, string uid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		PetInstance petInstance = Find(uid);
		if (petInstance == null)
		{
			return Array.Empty<PetEvolutionOption>();
		}
		try
		{
			PetRules.Definition(data, petInstance.Form);
		}
		catch (KeyNotFoundException)
		{
			return Array.Empty<PetEvolutionOption>();
		}
		if (L1jPetTypeCatalog.Load(data).TryGet(petInstance.Form, out L1jPetTypeDefinition definition))
		{
			if (definition.EvolutionItemKey == null || definition.EvolutionForm == null)
			{
				return Array.Empty<PetEvolutionOption>();
			}
			return new PetEvolutionOption[1]
			{
				new PetEvolutionOption(definition.EvolutionItemKey, definition.EvolutionForm)
			};
		}
		return Array.Empty<PetEvolutionOption>();
	}

	public PetEvolutionResult TryEvolve(IGameData data, Combatant owner, string uid, string fruitItemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		ArgumentException.ThrowIfNullOrWhiteSpace(fruitItemKey, "fruitItemKey");
		if (owner.Kind != CombatantKind.Player || string.IsNullOrWhiteSpace(owner.Key))
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.InvalidOwner);
		}
		PetInstance petInstance = Find(uid);
		if (petInstance == null)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.UnknownPet);
		}
		if (petInstance.OwnerKey.Length > 0 && !string.Equals(petInstance.OwnerKey, owner.Key, StringComparison.Ordinal))
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.ForeignOwner, petInstance);
		}
		if (petInstance.Level < 30)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.LevelTooLow, petInstance);
		}
		IReadOnlyList<PetEvolutionOption> readOnlyList = EvolutionOptions(data, uid);
		if (readOnlyList.Count == 0)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.FinalForm, petInstance);
		}
		PetEvolutionOption petEvolutionOption = readOnlyList.FirstOrDefault((PetEvolutionOption option) => string.Equals(option.FruitItemKey, fruitItemKey, StringComparison.Ordinal));
		if (string.IsNullOrWhiteSpace(petEvolutionOption.FruitItemKey))
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.InvalidFruit, petInstance);
		}
		if (CombatInventory.AvailableCount(owner, fruitItemKey) < 1)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.MissingFruit, petInstance);
		}
		PetDefinition petDefinition;
		try
		{
			petDefinition = PetRules.Definition(data, petEvolutionOption.TargetForm);
		}
		catch (KeyNotFoundException)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.FinalForm, petInstance);
		}
		if (!CombatInventory.TryRemove(owner, fruitItemKey, 1L))
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.MissingFruit, petInstance);
		}
		string form = petInstance.Form;
		petInstance.Form = petDefinition.Form;
		petInstance.Level = 1;
		petInstance.Experience = 0.0;
		petInstance.ExperiencePercent = 0;
		petInstance.MaxHp = Math.Max(1.0, Math.Floor(petInstance.MaxHp * 0.5));
		petInstance.MaxMp = Math.Max(0.0, Math.Floor(petInstance.MaxMp * 0.5));
		petInstance.Hp = petInstance.MaxHp;
		petInstance.Mp = petInstance.MaxMp;
		petInstance.Downed = false;
		petInstance.Food = 20;
		petInstance.CommandStatus = PetCommandStatus.Stay;
		petInstance.Equipment.Clear();
		return new PetEvolutionResult(Success: true, PetEvolutionFailure.None, petInstance, form, petInstance.Form);
	}

	public PetEvolutionResult TryGiveEvolutionItem(IGameData data, Combatant owner, string petUid, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(petUid, "petUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.MissingFruit, Find(petUid));
		}
		string itemKey = itemStack.ItemKey;
		if (!ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, itemUid, 1L, out ItemStack _))
		{
			return PetEvolutionResult.Failed(PetEvolutionFailure.MissingFruit, Find(petUid));
		}
		CombatInventory.SyncLegacyView(owner);
		return TryEvolveTransferred(data, owner, petUid, itemKey);
	}

	private PetEvolutionResult TryEvolveTransferred(IGameData data, Combatant owner, string uid, string fruitItemKey)
	{
		ItemStack incoming = new ItemStack($"pet-evolution-transfer:{Guid.NewGuid():N}", fruitItemKey, 1L);
		if (!ItemStackInventory.TryAddOrStack(owner.InventoryStacks, incoming, out ItemStack stored))
		{
			throw new InvalidOperationException("進化果實的虛擬單位無法入包。");
		}
		CombatInventory.SyncLegacyView(owner);
		PetEvolutionResult result = TryEvolve(data, owner, uid, fruitItemKey);
		if (!result.Success)
		{
			ItemStackInventory.TryRemoveByUid(owner.InventoryStacks, stored.Uid, 1L, out ItemStack _, includeLocked: true);
		}
		CombatInventory.SyncLegacyView(owner);
		return result;
	}

	internal bool DeletePet(string uid)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uid, "uid");
		PetInstance petInstance = Find(uid);
		if (petInstance != null)
		{
			return _pets.Remove(petInstance);
		}
		return false;
	}

	internal void Restore(PetInstance pet)
	{
		ArgumentNullException.ThrowIfNull(pet, "pet");
		if (Find(pet.Uid) != null)
		{
			throw new InvalidDataException("Pet roster contains a duplicate pet.");
		}
		_pets.Add(pet);
	}
}
