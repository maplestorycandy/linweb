namespace IdleLineage.Data;

public sealed record MapMusicZone(string Track, int BeginX, int BeginY, int EndX, int EndY)
{
	public bool Contains(int gameX, int gameY)
	{
		if (gameX >= BeginX && gameX <= EndX && gameY >= BeginY)
		{
			return gameY <= EndY;
		}
		return false;
	}
}
