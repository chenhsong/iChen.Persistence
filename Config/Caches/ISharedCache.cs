using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iChen.Persistence
{
	public interface ISharedCache : IDisposable
	{
		Task<bool> HasAsync (uint id, string key);

		Task<DateTimeOffset> GetTimeStampAsync (uint id);

		Task MarkActiveAsync (uint id);

		Task<(IReadOnlyDictionary<string, object> dict, DateTimeOffset timestamp)> GetAsync (uint id);

		Task<T> GetAsync<T> (uint id, string key);

		Task<double> GetAsync (uint id, string key, string field);

		Task SetAsync (uint id, string key, uint value);

		Task SetAsync (uint id, string key, int value);

		Task SetAsync (uint id, string key, string value);

		Task SetAsync (uint id, string key, double value);

		Task SetAsync (uint id, string key, bool value);

		Task SetAsync (uint id, IReadOnlyDictionary<string, object> values);

		Task SetAsync (uint id, string key, IReadOnlyDictionary<string, double> value);

		Task SetAsync (uint id, string key, string field, double value);

		Task UpdateAsync (uint id, string key, IReadOnlyDictionary<string, double> value);

		IEnumerable<(uint id, string key, string field, object value, DateTimeOffset timestamp)> Dump ();
	}
}