namespace IdleLineage.Combat;

public enum PetRosterFailure
{
	None,
	InvalidOwner,
	UnknownPet,
	StorageFull,
	AlreadyAssigned,
	AssignedToAnotherOwner,
	InsufficientCharm,
	Locked
}
