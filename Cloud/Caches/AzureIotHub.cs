using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;

using DoubleDict = System.Collections.Generic.IReadOnlyDictionary<string, double>;
using ObjDict = System.Collections.Generic.IReadOnlyDictionary<string, object>;

namespace iChen.Persistence.Cloud
{
	public class AzureIoTHub : ISharedCache
	{
		public const string HeartBeatProperty = "LastHeartBeatTime";

		public readonly string DeviceConnectionStringFormat;
		public readonly RegistryManager m_Server = null;
		private readonly SemaphoreSlim m_ServerSyncLock = new SemaphoreSlim(1, 1);
		public readonly ConcurrentDictionary<uint, (DeviceClient device, SemaphoreSlim sync)> m_Clients
			= new ConcurrentDictionary<uint, (DeviceClient, SemaphoreSlim)>();


		private static readonly ObjDict m_TextValueMaps = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase)
		{
			["NaN"] = double.NaN,
			["PositiveInfinity"] = double.PositiveInfinity,
			["NegativeInfinity"] = double.NegativeInfinity
		};

		public AzureIoTHub (string device_conn, string server_conn = null)
		{
			if (string.IsNullOrWhiteSpace(device_conn)) throw new ArgumentNullException(nameof(device_conn));
			if (server_conn != null && string.IsNullOrWhiteSpace(server_conn)) throw new ArgumentNullException(nameof(server_conn));

			this.DeviceConnectionStringFormat = device_conn;
			this.m_Server = RegistryManager.CreateFromConnectionString(server_conn);
		}

		public virtual void Dispose ()
		{
			foreach (var (device, sync) in m_Clients.Values) {
				sync.Wait();
				try { device.Dispose(); } finally { sync.Release(); }
				sync.Dispose();
			}

			m_Clients.Clear();

			if (m_Server != null) {
				m_ServerSyncLock.Wait();
				try { m_Server.Dispose(); } finally { m_ServerSyncLock.Release(); }
				m_ServerSyncLock.Dispose();
			}
		}

		private (DeviceClient device, SemaphoreSlim sync) GetDeviceClient (uint id) =>
			m_Clients.GetOrAdd(id, k => (
				device: DeviceClient.CreateFromConnectionString(string.Format(DeviceConnectionStringFormat, k), Microsoft.Azure.Devices.Client.TransportType.Amqp),
				sync: new SemaphoreSlim(1, 1)
			));

		private static double DecodeJObject (JObject jval, string field)
		{
			if (!jval.TryGetValue(field, out var value)) throw new ArgumentOutOfRangeException(nameof(field), $"Invalid field: [{field}].");

			if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
				throw new ArgumentOutOfRangeException(nameof(field), $"Invalid data type for field: [{field}].");

			return value.Value<double>();
		}

		private static object DecodeValue (object value)
		{
			switch (value) {
				case int _:
				case uint _:
				case bool _:
				case double _: return value;

				case string str: return m_TextValueMaps.TryGetValue(str, out var fval) ? fval : value;

				case float _: return Convert.ToDouble(value);
				case long _: return Convert.ToInt32(value);
				case ulong _: return Convert.ToUInt32(value);

				case DateTime dval: return dval.ToString("o");

				case JValue jval: {
						var val = jval.Value;

						switch (val) {
							case int _:
							case uint _:
							case bool _:
							case double _: return val;

							case string str: return m_TextValueMaps.TryGetValue(str, out var fval2) ? fval2 : val;

							case float _: return Convert.ToDouble(val);
							case long _: return Convert.ToInt32(val);
							case ulong _: return Convert.ToUInt32(val);

							case DateTime dval: return dval.ToString("o");

							default: throw new ApplicationException($"Invalid data type in JSON document: [{val.GetType().Name}].");
						}
					}

				case TwinCollection tcol: {
						// Sometimes the JSON document comes back with objects that are TwinCollection
						return tcol
										.Cast<KeyValuePair<string, object>>()
										.Select(kv => new KeyValuePair<string, object>(kv.Key, DecodeValue(kv.Value)))
										.Where(kv => kv.Value is double)
										.ToDictionary(kv => kv.Key, kv => (double) kv.Value);
					}

				case JObject jval: {
						return jval.Properties()
										.Where(prop => prop.Value.Type == JTokenType.Float || prop.Value.Type == JTokenType.Integer)
										.ToDictionary(prop => prop.Name, prop => prop.Value.Value<double>());
					}

				default: throw new ApplicationException($"Invalid node in JSON document: [{value.GetType().Name}].");
			}

		}

		private async Task<Twin> GetTwinAsync (uint id)
		{
			if (m_Server != null) {
				await m_ServerSyncLock.WaitAsync().ConfigureAwait(false);

				try {
					return await m_Server.GetTwinAsync(id.ToString()).ConfigureAwait(false);
				} finally { m_ServerSyncLock.Release(); }
			} else {
				var (device, sync) = GetDeviceClient(id);

				await sync.WaitAsync().ConfigureAwait(false);

				try {
					return await device.GetTwinAsync().ConfigureAwait(false);
				} finally { sync.Release(); }
			}
		}

		public virtual async Task<(ObjDict dict, DateTimeOffset timestamp)> GetAsync (uint id)
		{
			Twin twin = await GetTwinAsync(id);

			var dict = twin.Properties.Reported
										.Cast<KeyValuePair<string, object>>()
										.ToDictionary(kv => kv.Key, kv => DecodeValue(kv.Value));

			var timestamp = new DateTimeOffset(twin.Properties.Reported.GetLastUpdated(), TimeSpan.Zero);

			return (dict, timestamp);
		}

		public virtual async Task<T> GetAsync<T> (uint id, string key)
		{
			Twin twin = await GetTwinAsync(id);

			if (!twin.Properties.Reported.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key), $"Invalid property: [{key}].");

			object val = DecodeValue(twin.Properties.Reported[key]);
			if (val == null) throw new ArgumentOutOfRangeException(nameof(key), $"Invalid property: [{key}].");

			if (typeof(T).IsInterface) {
				if (!typeof(T).IsAssignableFrom(val.GetType())) throw new ArgumentOutOfRangeException(nameof(T), $"Data type ({val.GetType().Name}) does not implement interface ({typeof(T).Name}).");
			} else {
				if (val.GetType() != typeof(T)) throw new ArgumentOutOfRangeException(nameof(T), $"Data type ({val.GetType().Name}) does not match expected ({typeof(T).Name}).");
			}

			return (T) val;
		}

		public virtual async Task<double> GetAsync (uint id, string key, string field)
		{
			Twin twin = await GetTwinAsync(id);

			if (!twin.Properties.Reported.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key), $"Invalid property: [{key}].");

			switch (twin.Properties.Reported[key]) {
				case JObject jval: return DecodeJObject(jval, field);

				case DoubleDict dict: {
						if (!dict.TryGetValue(field, out var dval)) throw new ArgumentOutOfRangeException(nameof(field), $"Invalid field: [{field}].");
						return dval;
					}

				default: throw new ArgumentOutOfRangeException(nameof(key), $"Key [{key}] is not a hash dictionary.");
			}
		}

		public virtual async Task<DateTimeOffset> GetTimeStampAsync (uint id)
		{
			Twin twin = await GetTwinAsync(id);

			return new DateTimeOffset(twin.Properties.Reported.GetLastUpdated(), TimeSpan.Zero);
		}

		public virtual async Task<bool> HasAsync (uint id, string key)
		{
			Twin twin = await GetTwinAsync(id);

			return twin.Properties.Reported.Contains(key);
		}

		public virtual Task MarkActiveAsync (uint id) => SetAsync(id, HeartBeatProperty, DateTimeOffset.Now.ToString("O"));

		private async Task UpdateValueAsync (uint id, string key, dynamic value)
		{
			var col = new TwinCollection { [key] = value };

			var (device, sync) = GetDeviceClient(id);

			await sync.WaitAsync().ConfigureAwait(false);

			try {
				await device.UpdateReportedPropertiesAsync(col).ConfigureAwait(false);
			} finally { sync.Release(); }
		}

		public virtual Task SetAsync (uint id, string key, uint value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, int value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, string value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, double value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, bool value) => UpdateValueAsync(id, key, value);

		public virtual async Task SetAsync (uint id, ObjDict values)
		{
			var col = new TwinCollection();

			foreach (var kv in values) {
				switch (kv.Value) {
					case DoubleDict hashvalues: {
							if (hashvalues.Count > 0) {
								var hash = new TwinCollection();
								foreach (var kv2 in hashvalues) hash[kv2.Key] = kv2.Value;
								col[kv.Key] = hash;
							} else {
								// Remove the property if the entire dictionary is empty
								col[kv.Key] = null;
							}
							break;
						}
					case int _:
					case uint _:
					case bool _:
					case string _:
					case double _: col[kv.Key] = kv.Value; break;

					default: throw new ArgumentOutOfRangeException(nameof(values), $"Invalid data type: {kv.Value.GetType().Name}.");
				}
			}

			var (device, sync) = GetDeviceClient(id);

			await sync.WaitAsync().ConfigureAwait(false);

			try {
				await device.UpdateReportedPropertiesAsync(col).ConfigureAwait(false);
			} finally { sync.Release(); }
		}

		public virtual async Task SetAsync (uint id, string key, DoubleDict value)
		{
			var root = new TwinCollection();

			if (value.Count > 0) {
				var col = new TwinCollection();

				foreach (var kv in value) col[kv.Key] = kv.Value;
				root[key] = col;
			} else {
				// Remove the property if the entire dictionary is empty
				root[key] = null;
			}

			var (device, sync) = GetDeviceClient(id);

			await sync.WaitAsync().ConfigureAwait(false);

			try {
				await device.UpdateReportedPropertiesAsync(root).ConfigureAwait(false);
			} finally { sync.Release(); }
		}

		public virtual async Task SetAsync (uint id, string key, string field, double value)
		{
			var root = new TwinCollection() { [key] = new TwinCollection { [field] = value } };

			var (device, sync) = GetDeviceClient(id);

			await sync.WaitAsync().ConfigureAwait(false);

			try {
				await device.UpdateReportedPropertiesAsync(root).ConfigureAwait(false);
			} finally { sync.Release(); }
		}

		public virtual async Task SendMessageAsync (uint id, string message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));

			var msg = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(message));

			var (device, sync) = GetDeviceClient(id);

			await sync.WaitAsync().ConfigureAwait(false);

			try {
				await device.SendEventAsync(msg).ConfigureAwait(false);
			} finally { sync.Release(); }
		}

		public IEnumerable<(uint id, string key, string field, object value, DateTimeOffset timestamp)> Dump () =>
			throw new NotImplementedException();
	}
}
