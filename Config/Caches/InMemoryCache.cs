using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace iChen.Persistence
{
	/// <remarks>This type is thread-safe.</remarks>
	public class InMemoryCache : ISharedCache
	{
		private readonly ConcurrentDictionary<uint, ConcurrentDictionary<string, object>> m_Cache = new ConcurrentDictionary<uint, ConcurrentDictionary<string, object>>();

		public InMemoryCache ()
		{
		}

		public void Dispose ()
		{
		}

		private ConcurrentDictionary<string, object> GetPartition (uint id) =>
			m_Cache.ContainsKey(id) ? m_Cache[id] : throw new ArgumentOutOfRangeException($"Invalid id: {id}");

		public void Clear () => m_Cache.Clear();

		// Do not include hashes from properties
		public async Task<IReadOnlyDictionary<string, object>> GetAsync (uint id) =>
			GetPartition(id)
				.Where(kv => !(kv.Value is IReadOnlyDictionary<string, double>))
				.ToDictionary(kv => kv.Key, kv => kv.Value);

		public async Task<double> GetAsync (uint id, string key, string field)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (string.IsNullOrWhiteSpace(field)) throw new ArgumentNullException(nameof(field));

			if (!GetPartition(id).TryGetValue(key, out var result)) throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}/{field}");

			var dict = result as IReadOnlyDictionary<string, double>;
			if (dict == null) throw new ArgumentOutOfRangeException($"Key {id}:{key} is not a hash.");

			lock (dict) {
				if (!dict.TryGetValue(field, out var value)) throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}/{field}");
				return value;
			}
		}

		public async Task<T> GetAsync<T> (uint id, string key)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

			if (!GetPartition(id).TryGetValue(key, out var result)) throw new ArgumentOutOfRangeException($"Invalid key: {id}:{key}");

			if (typeof(T) == typeof(IReadOnlyDictionary<string, double>)) {
				return (T) (IReadOnlyDictionary<string, double>) result;
			} else {
				return (T) Convert.ChangeType(result, typeof(T));
			}
		}

		private void SetValue (uint id, string key, object value)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			var dict = m_Cache.GetOrAdd(id, _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
			dict.AddOrUpdate(key, value, (k, v) => value);
		}

		public async Task SetAsync (uint id, IReadOnlyDictionary<string, object> values)
		{
			if (values == null) throw new ArgumentNullException(nameof(values));

			var dict = m_Cache.GetOrAdd(id, _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));

			foreach (var kv in values) {
				if (string.IsNullOrWhiteSpace(kv.Key)) throw new ArgumentNullException(nameof(values), "One or more of the keys is missing.");

				switch (kv.Value) {
					case bool _:
					case double _:
					case string _:
					case int _:
					case uint _: dict.AddOrUpdate(kv.Key, kv.Value, (k, v) => kv.Value); break;

					case IReadOnlyDictionary<string, double> val: {
							val = val.ToDictionary(kv2 => kv2.Key, kv2 => kv2.Value);
							dict.AddOrUpdate(kv.Key, val, (k, v) => val);
							break;
						}

					default: throw new ArgumentOutOfRangeException(nameof(values), $"Invalid value type for key [{kv.Key}]: {kv.Value}");
				}
			}
		}

		public async Task SetAsync (uint id, string key, bool value) => SetValue(id, key, value);

		public async Task SetAsync (uint id, string key, double value) => SetValue(id, key, value);

		public async Task SetAsync (uint id, string key, string value) => SetValue(id, key, value);

		public async Task SetAsync (uint id, string key, int value) => SetValue(id, key, value);

		public async Task SetAsync (uint id, string key, uint value) => SetValue(id, key, value);

		public async Task SetAsync (uint id, string key, IReadOnlyDictionary<string, double> value) =>
			SetValue(id, key, value.ToDictionary(kv => kv.Key, kv => kv.Value));    // Copy the dictionary

		public async Task SetAsync (uint id, string key, string field, double value)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (string.IsNullOrWhiteSpace(field)) throw new ArgumentNullException(nameof(field));

			if (!GetPartition(id).TryGetValue(key, out var result)) {
				SetValue(id, key, new Dictionary<string, double>() { { "field", value } });
			} else {
				var dict = result as IDictionary<string, double>;
				if (dict == null) throw new ArgumentOutOfRangeException($"Key {id}:{key} is not a hash.");

				lock (dict) { dict[field] = value; }
			}
		}

		public async Task<bool> HasAsync (uint id, string key) =>
			m_Cache.ContainsKey(id) ? GetPartition(id).ContainsKey(key) : false;
	}
}