using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class CollectionState
{
	private readonly HashSet<string> _equipment = new HashSet<string>(StringComparer.Ordinal);

	private readonly HashSet<string> _misc = new HashSet<string>(StringComparer.Ordinal);

	private readonly HashSet<string> _relic = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, int> _killProgress = new Dictionary<string, int>(StringComparer.Ordinal);

	private CollectionBonusSummary? _cachedBonuses;

	public string Key { get; }

	public IGameData Data { get; }

	internal CollectionCatalog Catalog { get; }

	public IReadOnlyCollection<string> EquipmentItems => _equipment;

	public IReadOnlyCollection<string> MiscItems => _misc;

	public IReadOnlyCollection<string> RelicItems => _relic;

	public IReadOnlyDictionary<string, int> KillProgress => _killProgress;

	public CollectionBonusSummary Bonuses
	{
		get
		{
			CollectionBonusSummary valueOrDefault = _cachedBonuses.GetValueOrDefault();
			if (!_cachedBonuses.HasValue)
			{
				valueOrDefault = CalculateBonuses();
				_cachedBonuses = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
	}

	public CollectionState(IGameData data, string key = "standard")
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(key, "key");
		Data = data;
		Key = key;
		Catalog = new CollectionCatalog(data);
	}

	public bool Contains(CollectionBookKind book, string key)
	{
		return book switch
		{
			CollectionBookKind.Equipment => _equipment.Contains(key), 
			CollectionBookKind.Misc => _misc.Contains(key), 
			CollectionBookKind.Relic => _relic.Contains(key), 
			CollectionBookKind.Card => Kills(key) > 0, 
			_ => false, 
		};
	}

	public int Kills(string mobKey)
	{
		if (string.IsNullOrWhiteSpace(mobKey) || !_killProgress.TryGetValue(mobKey, out var value))
		{
			return 0;
		}
		return value;
	}

	public bool RegisterKill(string mobKey)
	{
		if (string.IsNullOrWhiteSpace(mobKey) || !Catalog.CardMobKeys.Contains(mobKey))
		{
			return false;
		}
		int num = Kills(mobKey);
		if (num >= 100)
		{
			return false;
		}
		_killProgress[mobKey] = num + 1;
		_cachedBonuses = null;
		return true;
	}

	public IReadOnlyList<string> RequiredItems(CollectionBookKind book, string categoryKey)
	{
		return Catalog.RequiredItems(book, categoryKey);
	}

	public IReadOnlyList<CollectionCategoryProgress> Categories(CollectionBookKind book)
	{
		return (from category in Catalog.Categories(book)
			select BuildProgress(book, category)).ToArray();
	}

	internal bool RegisterItem(string itemKey)
	{
		if (string.IsNullOrWhiteSpace(itemKey) || Data.Item(itemKey) == null)
		{
			return false;
		}
		if (Catalog.IsCardItem(itemKey))
		{
			return false;
		}
		bool flag = false;
		if (Catalog.EquipmentCategoryByItem.ContainsKey(itemKey))
		{
			flag |= _equipment.Add(itemKey);
		}
		if (Catalog.RelicCategoryByItem.ContainsKey(itemKey))
		{
			flag |= _relic.Add(itemKey);
		}
		if (Catalog.TryClassifyMisc(itemKey, out string _))
		{
			flag |= _misc.Add(itemKey);
		}
		if (flag)
		{
			_cachedBonuses = null;
		}
		return flag;
	}

	internal void SetKills(string mobKey, int kills)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mobKey, "mobKey");
		if (!Catalog.CardMobKeys.Contains(mobKey))
		{
			throw new InvalidDataException("Collection kill progress mob '" + mobKey + "' is not a card-book mob.");
		}
		if ((kills < 0 || kills > 100) ? true : false)
		{
			throw new InvalidDataException("Collection kill progress for '" + mobKey + "' must be between 0 and 100.");
		}
		if (kills == 0)
		{
			_killProgress.Remove(mobKey);
		}
		else
		{
			_killProgress[mobKey] = kills;
		}
		_cachedBonuses = null;
	}

	internal void RestoreItems(IEnumerable<string> equipment, IEnumerable<string> misc, IEnumerable<string> relic, IReadOnlyDictionary<string, int> kills)
	{
		RestoreSet(_equipment, equipment, CollectionBookKind.Equipment);
		RestoreSet(_misc, misc, CollectionBookKind.Misc);
		RestoreSet(_relic, relic, CollectionBookKind.Relic);
		_killProgress.Clear();
		foreach (var (mobKey, kills2) in kills)
		{
			SetKills(mobKey, kills2);
		}
		_cachedBonuses = null;
	}

	private void RestoreSet(HashSet<string> destination, IEnumerable<string> values, CollectionBookKind book)
	{
		destination.Clear();
		foreach (string value in values)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}
			if (Data.Item(value) == null || !Catalog.BelongsToBook(book, value))
			{
				Godot.GD.PushWarning($"Collection {book} save contains unrecognized or unclassified item '{value}', preserving entry.");
			}
			destination.Add(value);
		}
	}

	private CollectionCategoryProgress BuildProgress(CollectionBookKind book, CollectionCategory category)
	{
		HashSet<string> hashSet;
		switch (book)
		{
		case CollectionBookKind.Card:
		{
			int num = ((category.Items.Count != 0) ? category.Items.Min((string mobKey) => KillTier(Kills(mobKey))) : 0);
			double num2 = ((num == 0) ? 0.0 : category.TierValues[Math.Min(num - 1, category.TierValues.Count - 1)]);
			return new CollectionCategoryProgress(book, category.Key, category.Name, category.Group, category.Items.Count((string mobKey) => Kills(mobKey) > 0), category.Items.Count, num, category.BonusStat, num2, (num2 == 0.0) ? "" : $"{category.BonusStat} +{num2}", num > 0);
		}
		case CollectionBookKind.Equipment:
			hashSet = _equipment;
			break;
		case CollectionBookKind.Misc:
			hashSet = _misc;
			break;
		case CollectionBookKind.Relic:
			hashSet = _relic;
			break;
		default:
			throw new ArgumentOutOfRangeException("book");
		}
		HashSet<string> hashSet2 = hashSet;
		IReadOnlyList<string> readOnlyList = Catalog.RequiredItems(book, category.Key);
		int num3 = readOnlyList.Count(hashSet2.Contains);
		bool flag = readOnlyList.Count > 0 && num3 == readOnlyList.Count;
		return new CollectionCategoryProgress(book, category.Key, category.Name, category.Group, num3, readOnlyList.Count, flag ? 1 : 0, category.BonusStat, flag ? category.BonusValue : 0.0, category.BonusLabel, flag && !string.IsNullOrEmpty(category.BonusStat));
	}

	private CollectionBonusSummary CalculateBonuses()
	{
		CollectionBonusSummary result = default(CollectionBonusSummary);
		CollectionBookKind[] array = new CollectionBookKind[3]
		{
			CollectionBookKind.Equipment,
			CollectionBookKind.Misc,
			CollectionBookKind.Card
		};
		foreach (CollectionBookKind book in array)
		{
			foreach (CollectionCategoryProgress item in Categories(book))
			{
				if (item.BonusActive)
				{
					result = result.Add(item.BonusStat, item.BonusValue);
				}
			}
		}
		return result;
	}

	internal static int KillTier(int kills)
	{
		if (kills < 100)
		{
			if (kills < 10)
			{
				return (kills >= 1) ? 1 : 0;
			}
			return 2;
		}
		return 3;
	}
}
