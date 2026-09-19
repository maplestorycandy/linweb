using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal static class ClassicSkillSidebarCatalog
{
	public static ClassicSkillSidebarSpec For(string? classId)
	{
		return ClassKitRegistry.NormalizeClassId(classId) switch
		{
			"royal" => new ClassicSkillSidebarSpec(2058, 7, 2, "prince"), 
			"elf" => new ClassicSkillSidebarSpec(2075, 11, 6, "elf"), 
			"dark" => new ClassicSkillSidebarSpec(2086, 5, 2, "darkelf"), 
			"warrior" => new ClassicSkillSidebarSpec(2065, 10, 10, "general"), 
			_ => new ClassicSkillSidebarSpec(2065, 10, 10, "general"), 
		};
	}

	public static List<ClassSkillEntry>[] Group(IReadOnlyList<ClassSkillEntry> skills, ClassicSkillSidebarSpec spec)
	{
		List<ClassSkillEntry>[] array = (from _ in Enumerable.Range(0, spec.SlotCount)
			select new List<ClassSkillEntry>()).ToArray();
		int[] array2 = (from level in (from skill in skills
				where skill.Tier <= 0
				select skill.RequiredLevel).Distinct()
			orderby level
			select level).ToArray();
		foreach (ClassSkillEntry skill in skills)
		{
			int value;
			if (skill.Tier > 0)
			{
				value = skill.Tier - 1;
			}
			else
			{
				int val = Array.IndexOf(array2, skill.RequiredLevel);
				value = spec.GeneralSlots + Math.Max(0, val);
			}
			value = Math.Clamp(value, 0, spec.SlotCount - 1);
			array[value].Add(skill);
		}
		List<ClassSkillEntry>[] array3 = array;
		for (int num = 0; num < array3.Length; num++)
		{
			array3[num].Sort(delegate(ClassSkillEntry left, ClassSkillEntry right)
			{
				int num2 = left.RequiredLevel.CompareTo(right.RequiredLevel);
				return (num2 == 0) ? string.Compare(left.SkillId, right.SkillId, StringComparison.Ordinal) : num2;
			});
		}
		return array;
	}
}
