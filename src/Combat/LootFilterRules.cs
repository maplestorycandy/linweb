using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class LootFilterRules
{
	[CompilerGenerated]
	private sealed class _003CSearchableNames_003Ed__6 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string canonical;

		public string _003C_003E3__canonical;

		private JsonObject item;

		public JsonObject _003C_003E3__item;

		private string[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CSearchableNames_003Ed__6(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E7__wrap1 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = canonical;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E7__wrap1 = new string[2] { "l1jIdentifiedName", "l1jUnidentifiedName" };
				_003C_003E7__wrap2 = 0;
				goto IL_00b5;
			case 2:
				{
					_003C_003E1__state = -1;
					goto IL_00a7;
				}
				IL_00b5:
				if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
				{
					string field = _003C_003E7__wrap1[_003C_003E7__wrap2];
					string text = ReadText(item, field);
					if (text != null && !string.Equals(text, canonical, StringComparison.Ordinal))
					{
						_003C_003E2__current = text;
						_003C_003E1__state = 2;
						return true;
					}
					goto IL_00a7;
				}
				_003C_003E7__wrap1 = null;
				return false;
				IL_00a7:
				_003C_003E7__wrap2++;
				goto IL_00b5;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CSearchableNames_003Ed__6 _003CSearchableNames_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CSearchableNames_003Ed__ = this;
			}
			else
			{
				_003CSearchableNames_003Ed__ = new _003CSearchableNames_003Ed__6(0);
			}
			_003CSearchableNames_003Ed__.item = _003C_003E3__item;
			_003CSearchableNames_003Ed__.canonical = _003C_003E3__canonical;
			return _003CSearchableNames_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	public static bool ShouldDropToGround(IReadOnlySet<string>? itemKeys, string? itemKey)
	{
		if (itemKeys != null && !string.IsNullOrWhiteSpace(itemKey))
		{
			return itemKeys.Contains(itemKey);
		}
		return false;
	}

	public static IReadOnlyList<LootFilterCandidate> Search(IGameData data, string? query, int maximumResults = 40)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		string needle = query?.Trim() ?? string.Empty;
		if (needle.Length == 0 || maximumResults <= 0)
		{
			return Array.Empty<LootFilterCandidate>();
		}
		return (from candidate in data.Items.Where<KeyValuePair<string, JsonNode>>((KeyValuePair<string, JsonNode> entry) => entry.Value is JsonObject).Select(delegate(KeyValuePair<string, JsonNode> entry)
			{
				JsonObject item = (JsonObject)entry.Value;
				string text = ReadText(item, "n") ?? ReadText(item, "l1jIdentifiedName") ?? ReadText(item, "l1jUnidentifiedName") ?? entry.Key;
				return (!SearchableNames(item, text).Any((string name) => name.Contains(needle, StringComparison.OrdinalIgnoreCase))) ? null : new LootFilterCandidate(entry.Key, text);
			})
			where (object)candidate != null
			select (candidate)).OrderBy<LootFilterCandidate, string>((LootFilterCandidate candidate) => candidate.DisplayName, StringComparer.Ordinal).ThenBy<LootFilterCandidate, string>((LootFilterCandidate candidate) => candidate.ItemKey, StringComparer.Ordinal).Take(maximumResults)
			.ToArray();
	}

	public static string DisplayName(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return string.Empty;
		}
		JsonObject item = data.Item(itemKey);
		return ReadText(item, "n") ?? ReadText(item, "l1jIdentifiedName") ?? ReadText(item, "l1jUnidentifiedName") ?? itemKey;
	}

	public static string Serialize(IEnumerable<string>? itemKeys)
	{
		return JsonSerializer.Serialize(Normalize(itemKeys));
	}

	public static bool TryParseSaved(string? saved, out string[] itemKeys)
	{
		itemKeys = Array.Empty<string>();
		if (string.IsNullOrWhiteSpace(saved))
		{
			return true;
		}
		try
		{
			string[] array = JsonSerializer.Deserialize<string[]>(saved);
			if (array == null || array.Any((string key) => string.IsNullOrWhiteSpace(key) || key.Length > 256))
			{
				return false;
			}
			string[] array2 = Normalize(array);
			if (array2.Length != array.Length)
			{
				return false;
			}
			itemKeys = array2;
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static string[] Normalize(IEnumerable<string>? itemKeys)
	{
		return (from key in itemKeys ?? Array.Empty<string>()
			select key?.Trim() ?? string.Empty into key
			where key.Length > 0
			select key).Distinct<string>(StringComparer.Ordinal).OrderBy<string, string>((string key) => key, StringComparer.Ordinal).ToArray();
	}

	[IteratorStateMachine(typeof(_003CSearchableNames_003Ed__6))]
	private static IEnumerable<string> SearchableNames(JsonObject item, string canonical)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CSearchableNames_003Ed__6(-2)
		{
			_003C_003E3__item = item,
			_003C_003E3__canonical = canonical
		};
	}

	private static string? ReadText(JsonObject? item, string field)
	{
		if (!(item?[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		return value.Trim();
	}
}
