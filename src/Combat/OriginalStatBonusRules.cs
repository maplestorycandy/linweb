namespace IdleLineage.Combat;

public static class OriginalStatBonusRules
{
	private static string Branch(string? classId)
	{
		string text = ClassKitRegistry.NormalizeClassId(classId);
		if (!(text == "warrior"))
		{
			return text;
		}
		return "knight";
	}

	public static int HpUp(string? classId, int originalCon)
	{
		switch (Branch(classId))
		{
		case "royal":
		{
			int result;
			if (originalCon < 16)
			{
				switch (originalCon)
				{
				case 12:
				case 13:
					result = 1;
					break;
				case 14:
				case 15:
					result = 2;
					break;
				default:
					result = 0;
					break;
				}
			}
			else
			{
				result = 3;
			}
			return result;
		}
		case "knight":
			return (originalCon >= 17) ? 3 : (((uint)(originalCon - 15) <= 1u) ? 1 : 0);
		case "elf":
		{
			int result;
			switch (originalCon)
			{
			case 13:
			case 14:
			case 15:
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dark":
			return (originalCon >= 12) ? 2 : (((uint)(originalCon - 10) <= 1u) ? 1 : 0);
		case "mage":
			return (originalCon >= 16) ? 2 : (((uint)(originalCon - 14) <= 1u) ? 1 : 0);
		case "dragon":
			return (originalCon >= 17) ? 3 : (((uint)(originalCon - 15) <= 1u) ? 1 : 0);
		case "illusion":
			return (originalCon >= 15) ? 2 : (((uint)(originalCon - 13) <= 1u) ? 1 : 0);
		default:
			return 0;
		}
	}

	public static int MpUp(string? classId, int originalWis)
	{
		return Branch(classId) switch
		{
			"royal" => (originalWis >= 16) ? 1 : 0, 
			"elf" => (originalWis >= 14) ? ((originalWis <= 16) ? 1 : 2) : 0, 
			"dark" => (originalWis >= 12) ? 1 : 0, 
			"mage" => (originalWis >= 13) ? ((originalWis <= 16) ? 1 : 2) : 0, 
			"dragon" => (originalWis >= 13) ? ((originalWis <= 15) ? 1 : 2) : 0, 
			"illusion" => (originalWis >= 13) ? ((originalWis <= 15) ? 1 : 2) : 0, 
			_ => 0, 
		};
	}

	public static int StrWeightReduction(string? classId, int originalStr)
	{
		switch (Branch(classId))
		{
		case "royal":
		{
			int result;
			switch (originalStr)
			{
			case 14:
			case 15:
			case 16:
				result = 1;
				break;
			case 17:
			case 18:
			case 19:
				result = 2;
				break;
			case 20:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "elf":
			return (originalStr >= 16) ? 2 : 0;
		case "dark":
			return (originalStr >= 13) ? ((originalStr > 15) ? 3 : 2) : 0;
		case "mage":
			return (originalStr >= 9) ? 1 : 0;
		case "dragon":
			return (originalStr >= 16) ? 1 : 0;
		case "illusion":
			return (originalStr == 18) ? 1 : 0;
		default:
			return 0;
		}
	}

	public static int DmgUp(string? classId, int originalStr)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalStr >= 15) ? ((originalStr <= 17) ? 1 : 2) : 0;
		case "knight":
		{
			int result;
			switch (originalStr)
			{
			case 18:
			case 19:
				result = 2;
				break;
			case 20:
				result = 4;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "elf":
			return (originalStr >= 14) ? 2 : (((uint)(originalStr - 12) <= 1u) ? 1 : 0);
		case "dark":
		{
			int result;
			switch (originalStr)
			{
			case 14:
			case 15:
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "mage":
			return (originalStr >= 12) ? 2 : (((uint)(originalStr - 10) <= 1u) ? 1 : 0);
		case "dragon":
			return (originalStr >= 15) ? ((originalStr <= 17) ? 1 : 3) : 0;
		case "illusion":
			return (originalStr >= 15) ? 2 : (((uint)(originalStr - 13) <= 1u) ? 1 : 0);
		default:
			return 0;
		}
	}

	public static int ConWeightReduction(string? classId, int originalCon)
	{
		return Branch(classId) switch
		{
			"royal" => (originalCon >= 11) ? 1 : 0, 
			"knight" => (originalCon >= 15) ? 1 : 0, 
			"elf" => (originalCon >= 15) ? 2 : 0, 
			"dark" => (originalCon >= 9) ? 1 : 0, 
			"mage" => (originalCon >= 15) ? 2 : (((uint)(originalCon - 13) <= 1u) ? 1 : 0), 
			"illusion" => originalCon switch
			{
				17 => 1, 
				18 => 2, 
				_ => 0, 
			}, 
			_ => 0, 
		};
	}

	public static int BowDmgUp(string? classId, int originalDex)
	{
		return Branch(classId) switch
		{
			"royal" => (originalDex >= 13) ? 1 : 0, 
			"elf" => (originalDex >= 14) ? ((originalDex > 16) ? 3 : 2) : 0, 
			"dark" => (originalDex == 18) ? 2 : 0, 
			_ => 0, 
		};
	}

	public static int HitUp(string? classId, int originalStr)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalStr >= 16) ? ((originalStr <= 18) ? 1 : 2) : 0;
		case "knight":
			return (originalStr >= 19) ? 4 : (((uint)(originalStr - 17) <= 1u) ? 2 : 0);
		case "elf":
			return (originalStr >= 15) ? 2 : (((uint)(originalStr - 13) <= 1u) ? 1 : 0);
		case "dark":
		{
			int result;
			switch (originalStr)
			{
			case 15:
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "mage":
			return (originalStr >= 13) ? 2 : (((uint)(originalStr - 11) <= 1u) ? 1 : 0);
		case "dragon":
			return (originalStr >= 14) ? ((originalStr <= 16) ? 1 : 3) : 0;
		case "illusion":
		{
			int result;
			if (originalStr < 17)
			{
				switch (originalStr)
				{
				case 12:
				case 13:
					result = 1;
					break;
				case 14:
				case 15:
					result = 2;
					break;
				case 16:
					result = 3;
					break;
				default:
					result = 0;
					break;
				}
			}
			else
			{
				result = 4;
			}
			return result;
		}
		default:
			return 0;
		}
	}

	public static int BowHitUp(string? classId, int originalDex)
	{
		string text = Branch(classId);
		if (!(text == "elf"))
		{
			if (text == "dark")
			{
				return originalDex switch
				{
					17 => 1, 
					18 => 2, 
					_ => 0, 
				};
			}
			return 0;
		}
		return (originalDex >= 13) ? ((originalDex > 15) ? 3 : 2) : 0;
	}

	public static int Mr(string? classId, int originalWis)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalWis >= 14) ? 2 : (((uint)(originalWis - 12) <= 1u) ? 1 : 0);
		case "knight":
			return (originalWis >= 12) ? 2 : (((uint)(originalWis - 10) <= 1u) ? 1 : 0);
		case "elf":
			return (originalWis >= 13) ? ((originalWis <= 15) ? 1 : 2) : 0;
		case "dark":
			return (originalWis >= 11) ? ((originalWis <= 13) ? 1 : (originalWis switch
			{
				14 => 2, 
				15 => 3, 
				_ => 4, 
			})) : 0;
		case "mage":
			return (originalWis >= 15) ? 1 : 0;
		case "dragon":
			return (originalWis >= 14) ? 2 : 0;
		case "illusion":
		{
			int result;
			switch (originalWis)
			{
			case 15:
			case 16:
			case 17:
				result = 2;
				break;
			case 18:
				result = 4;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		default:
			return 0;
		}
	}

	public static int MagicHit(string? classId, int originalInt)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalInt >= 14) ? 2 : (((uint)(originalInt - 12) <= 1u) ? 1 : 0);
		case "knight":
		{
			int result;
			switch (originalInt)
			{
			case 10:
			case 11:
				result = 1;
				break;
			case 12:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "elf":
			return (originalInt >= 15) ? 2 : (((uint)(originalInt - 13) <= 1u) ? 1 : 0);
		case "dark":
			return (originalInt >= 14) ? 2 : (((uint)(originalInt - 12) <= 1u) ? 1 : 0);
		case "mage":
			return (originalInt >= 14) ? 1 : 0;
		case "dragon":
		{
			int result;
			if (originalInt < 16)
			{
				switch (originalInt)
				{
				case 12:
				case 13:
					result = 2;
					break;
				case 14:
				case 15:
					result = 3;
					break;
				default:
					result = 0;
					break;
				}
			}
			else
			{
				result = 4;
			}
			return result;
		}
		case "illusion":
			return (originalInt >= 13) ? 1 : 0;
		default:
			return 0;
		}
	}

	public static int MagicCritical(string? classId, int originalInt)
	{
		string text = Branch(classId);
		if (!(text == "elf"))
		{
			if (text == "mage")
			{
				return originalInt switch
				{
					15 => 2, 
					16 => 4, 
					17 => 6, 
					18 => 8, 
					_ => 0, 
				};
			}
			return 0;
		}
		return (originalInt >= 16) ? 4 : (((uint)(originalInt - 14) <= 1u) ? 2 : 0);
	}

	public static int MagicConsumeReduction(string? classId, int originalInt)
	{
		return Branch(classId) switch
		{
			"royal" => (originalInt >= 13) ? 2 : (((uint)(originalInt - 11) <= 1u) ? 1 : 0), 
			"knight" => (originalInt >= 11) ? 2 : (((uint)(originalInt - 9) <= 1u) ? 1 : 0), 
			"dark" => (originalInt >= 15) ? 2 : (((uint)(originalInt - 13) <= 1u) ? 1 : 0), 
			"illusion" => (originalInt >= 15) ? 2 : ((originalInt == 14) ? 1 : 0), 
			_ => 0, 
		};
	}

	public static int MagicDamage(string? classId, int originalInt)
	{
		switch (Branch(classId))
		{
		case "mage":
			return (originalInt >= 13) ? 1 : 0;
		case "dragon":
		{
			int result;
			switch (originalInt)
			{
			case 13:
			case 14:
				result = 1;
				break;
			case 15:
			case 16:
				result = 2;
				break;
			case 17:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "illusion":
			return originalInt switch
			{
				16 => 1, 
				17 => 2, 
				_ => 0, 
			};
		default:
			return 0;
		}
	}

	public static int Ac(string? classId, int originalDex)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalDex >= 12) ? ((originalDex <= 14) ? 1 : (((uint)(originalDex - 15) > 1u) ? 3 : 2)) : 0;
		case "knight":
			return (originalDex >= 15) ? 3 : (((uint)(originalDex - 13) <= 1u) ? 1 : 0);
		case "elf":
		{
			int result;
			switch (originalDex)
			{
			case 15:
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dark":
			return (originalDex >= 17) ? 1 : 0;
		case "mage":
			return (originalDex >= 10) ? 2 : (((uint)(originalDex - 8) <= 1u) ? 1 : 0);
		case "dragon":
			return (originalDex >= 14) ? 2 : (((uint)(originalDex - 12) <= 1u) ? 1 : 0);
		case "illusion":
			return (originalDex >= 13) ? 2 : (((uint)(originalDex - 11) <= 1u) ? 1 : 0);
		default:
			return 0;
		}
	}

	public static int Er(string? classId, int originalDex)
	{
		switch (Branch(classId))
		{
		case "royal":
		{
			int result;
			switch (originalDex)
			{
			case 14:
			case 15:
				result = 1;
				break;
			case 16:
			case 17:
				result = 2;
				break;
			case 18:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "knight":
		{
			int result;
			switch (originalDex)
			{
			case 14:
			case 15:
				result = 1;
				break;
			case 16:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dark":
			return (originalDex >= 16) ? 2 : 0;
		case "mage":
		{
			int result;
			switch (originalDex)
			{
			case 9:
			case 10:
				result = 1;
				break;
			case 11:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dragon":
			return (originalDex >= 15) ? 2 : (((uint)(originalDex - 13) <= 1u) ? 1 : 0);
		case "illusion":
			return (originalDex >= 14) ? 2 : (((uint)(originalDex - 12) <= 1u) ? 1 : 0);
		default:
			return 0;
		}
	}

	public static int Hpr(string? classId, int originalCon)
	{
		switch (Branch(classId))
		{
		case "royal":
		{
			int result;
			switch (originalCon)
			{
			case 13:
			case 14:
				result = 1;
				break;
			case 15:
			case 16:
				result = 2;
				break;
			case 17:
				result = 3;
				break;
			case 18:
				result = 4;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "knight":
		{
			int result;
			switch (originalCon)
			{
			case 16:
			case 17:
				result = 2;
				break;
			case 18:
				result = 4;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "elf":
		{
			int result;
			if (originalCon < 17)
			{
				switch (originalCon)
				{
				case 14:
				case 15:
					result = 1;
					break;
				case 16:
					result = 2;
					break;
				default:
					result = 0;
					break;
				}
			}
			else
			{
				result = 3;
			}
			return result;
		}
		case "dark":
			return (originalCon >= 13) ? 2 : (((uint)(originalCon - 11) <= 1u) ? 1 : 0);
		case "mage":
			return originalCon switch
			{
				17 => 1, 
				18 => 2, 
				_ => 0, 
			};
		case "dragon":
		{
			int result;
			switch (originalCon)
			{
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "illusion":
			return (originalCon >= 16) ? 2 : (((uint)(originalCon - 14) <= 1u) ? 1 : 0);
		default:
			return 0;
		}
	}

	public static int Mpr(string? classId, int originalWis)
	{
		switch (Branch(classId))
		{
		case "royal":
			return (originalWis >= 15) ? 2 : (((uint)(originalWis - 13) <= 1u) ? 1 : 0);
		case "knight":
		{
			int result;
			switch (originalWis)
			{
			case 11:
			case 12:
				result = 1;
				break;
			case 13:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "elf":
		{
			int result;
			switch (originalWis)
			{
			case 15:
			case 16:
			case 17:
				result = 1;
				break;
			case 18:
				result = 2;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dark":
			return (originalWis >= 13) ? 1 : 0;
		case "mage":
		{
			int result;
			switch (originalWis)
			{
			case 14:
			case 15:
				result = 1;
				break;
			case 16:
			case 17:
				result = 2;
				break;
			case 18:
				result = 3;
				break;
			default:
				result = 0;
				break;
			}
			return result;
		}
		case "dragon":
			return (originalWis >= 17) ? 2 : (((uint)(originalWis - 15) <= 1u) ? 1 : 0);
		case "illusion":
			return (originalWis >= 14) ? ((originalWis <= 16) ? 1 : 2) : 0;
		default:
			return 0;
		}
	}
}
