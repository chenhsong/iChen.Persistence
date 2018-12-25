using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoubleDictX = System.Collections.Concurrent.ConcurrentDictionary<string, double>;
using ObjDictX = System.Collections.Concurrent.ConcurrentDictionary<string, object>;

namespace iChen.Persistence
{
	/// <remarks>This type is thread-safe.</remarks>
	public class InMemoryCache : ISharedCache
	{
		private readonly ConcurrentDictionary<uint, (ObjDictX dict, DateTimeOffset timestamp)> m_Cache
			= new ConcurrentDictionary<uint, (ObjDictX, DateTimeOffset)>();

		public InMemoryCache () { }

		public virtual void Dispose () { }

		private ObjDictX GetPartition (uint id) => m_Cache.TryGetValue(id, out var entry)
			? entry.dict
			: throw new ArgumentOutOfRangeException($"Invalid id: {id}");

		public virtual void Clear () => m_Cache.Clear();

		public virtual async Task<(IReadOnlyDictionary<string, object> dict, DateTimeOffset timestamp)> GetAsync (uint id) =>
			m_Cache.TryGetValue(id, out var entry)
				? await Task.FromResult((entry.dict.ToDictionary(kv => kv.Key, kv => (kv.Value is DoubleDictX ddict)
						? new Dictionary<string, double>(ddict, StringComparer.InvariantCultureIgnoreCase)
						: kv.Value, StringComparer.InvariantCultureIgnoreCase), entry.timestamp))
				: throw new ArgumentOutOfRangeException($"Invalid id: {id}");

		public virtual async Task<double> GetAsync (uint id, string key, string field)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (string.IsNullOrWhiteSpace(field)) throw new ArgumentNullException(nameof(field));

			if (!GetPartition(id).TryGetValue(key, out var result)) throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}/{field}");

			if (!(result is DoubleDictX dict)) throw new ArgumentOutOfRangeException($"Key {id}:{key} is not a hash.");

			return dict.TryGetValue(field, out var value) ?
								await Task.FromResult(value) :
								throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}/{field}");
		}

		public virtual async Task<T> GetAsync<T> (uint id, string key)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

			if (!GetPartition(id).TryGetValue(key, out var result)) throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}");

			if (typeof(T) == typeof(IReadOnlyDictionary<string, double>))
				return (result is DoubleDictX ddict)
					? (T) (IReadOnlyDictionary<string, double>) new Dictionary<string, double>(ddict, StringComparer.InvariantCultureIgnoreCase)
					: throw new InvalidCastException("{id}:{key} is not a hash.");

			return await Task.FromResult((T) Convert.ChangeType(result, typeof(T)));
		}

		private Task SetValueAsync (uint id, string key, object value)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

			m_Cache.AddOrUpdate(id,
				(new ObjDictX(StringComparer.OrdinalIgnoreCase) { [key] = value }, DateTimeOffset.Now),
				(k, v) => {
					v.dict.AddOrUpdate(key, value, (k2, v2) => value);
					return (v.dict, timestamp: DateTimeOffset.Now);
				});

			return Task.CompletedTask;
		}

		public virtual Task SetAsync (uint id, IReadOnlyDictionary<string, object> values)
		{
			if (values == null) throw new ArgumentNullException(nameof(values));

			var (dict, timestamp) = m_Cache.AddOrUpdate(id, (
				dict: new ObjDictX(StringComparer.OrdinalIgnoreCase),
				timestamp: DateTimeOffset.Now
			), (k, v) => (v.dict, DateTimeOffset.Now));

			foreach (var kv in values) {
				if (string.IsNullOrWhiteSpace(kv.Key)) throw new ArgumentNullException(nameof(values), "One or more of the keys is missing.");

				switch (kv.Value) {
					case bool _:
					case double _:
					case string _:
					case int _:
					case uint _: {
							dict.AddOrUpdate(kv.Key, kv.Value, (k, v) => kv.Value);
							break;
						}

					case IReadOnlyDictionary<string, double> ddict: {
							ddict = new DoubleDictX(ddict, StringComparer.InvariantCultureIgnoreCase);
							dict.AddOrUpdate(kv.Key, ddict, (k, v) => ddict);
							break;
						}

					default: throw new ArgumentOutOfRangeException(nameof(values), $"Invalid value type for key [{kv.Key}]: {kv.Value}");
				}
			}

			return Task.CompletedTask;
		}

		public virtual Task SetAsync (uint id, string key, bool value) => SetValueAsync(id, key, value);

		public virtual Task SetAsync (uint id, string key, double value) => SetValueAsync(id, key, value);

		public virtual Task SetAsync (uint id, string key, string value) => SetValueAsync(id, key, value);

		public virtual Task SetAsync (uint id, string key, int value) => SetValueAsync(id, key, value);

		public virtual Task SetAsync (uint id, string key, uint value) => SetValueAsync(id, key, value);

		public virtual Task SetAsync (uint id, string key, IReadOnlyDictionary<string, double> values)
			=> SetValueAsync(id, key, new DoubleDictX(values, StringComparer.InvariantCultureIgnoreCase));    // Copy the dictionary

		public virtual async Task UpdateAsync (uint id, string key, IReadOnlyDictionary<string, double> values)
		{
			foreach (var kv in values) {
				await SetAsync(id, key, kv.Key, kv.Value);
			}
		}

		public virtual async Task SetAsync (uint id, string key, string field, double value)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (string.IsNullOrWhiteSpace(field)) throw new ArgumentNullException(nameof(field));

			if (!GetPartition(id).TryGetValue(key, out var result)) {
				await SetValueAsync(id, key, new Dictionary<string, double>() { [field] = value });
				return;
			}

			if (!(result is DoubleDictX dict)) throw new ArgumentOutOfRangeException($"Key {id}:{key} is not a hash.");

			dict[field] = value;
		}

		public virtual Task<bool> HasAsync (uint id, string key)
			=> Task.FromResult(m_Cache.TryGetValue(id, out var entry) ? entry.dict.ContainsKey(key) : false);

		public virtual Task<DateTimeOffset> GetTimeStampAsync (uint id)
			=> Task.FromResult(m_Cache.TryGetValue(id, out var entry) ? entry.timestamp : default);

		public virtual Task MarkActiveAsync (uint id)
		{
			m_Cache.AddOrUpdate(id,
				k => throw new ArgumentOutOfRangeException($"Invalid id: {k}"),
				(k, v) => (v.dict, DateTimeOffset.Now)
			);

			return Task.CompletedTask;
		}

		public virtual IEnumerable<(uint id, string key, string field, object value, DateTimeOffset timestamp)> Dump ()
		{
			foreach (var kv in m_Cache) {
				uint id = kv.Key;
				var (dict, timestamp) = kv.Value;

				foreach (var kv2 in dict) {
					switch (kv2.Value) {
						case DoubleDictX ddict: {
								foreach (var kv3 in ddict) yield return (id, kv2.Key, kv3.Key, kv3.Value, timestamp);
								break;
							}
						default: yield return (id, kv2.Key, null, kv2.Value, timestamp); break;
					}
				}
			}
		}
	}
}