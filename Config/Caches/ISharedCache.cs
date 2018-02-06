using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iChen.Persistence
{
	public interface ISharedCache : IDisposable
	{
		Task<bool> HasAsync (uint id, string key);

		Task<IReadOnlyDictionary<string, object>> GetAsync (uint id);

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
	}
}