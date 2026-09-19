namespace IdleLineage.Combat;

public sealed record NpcActionKillProgress(string TargetName, int Required, int Killed)
{
	public bool Complete => Killed >= Required;
}
