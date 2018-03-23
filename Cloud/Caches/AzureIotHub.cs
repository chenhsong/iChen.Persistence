using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;

namespace iChen.Persistence.Cloud
{
	public class AzureIoTHub : ISharedCache
	{
		public readonly RegistryManager m_Server = null;
		public readonly string DeviceConnectionStringFormat;
		public readonly ConcurrentDictionary<uint, DeviceClient> m_Clients = new ConcurrentDictionary<uint, DeviceClient>();

		private static readonly IReadOnlyDictionary<string, object> m_TextValueMaps = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase)
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

		public void Dispose ()
		{
			foreach (var client in m_Clients.Values) client.Dispose();
			m_Clients.Clear();

			if (m_Server != null) m_Server.Dispose();
		}

		private DeviceClient GetDeviceClient (uint id) =>
			m_Clients.GetOrAdd(id, k => DeviceClient.CreateFromConnectionString(string.Format(DeviceConnectionStringFormat, k), Microsoft.Azure.Devices.Client.TransportType.Amqp));

		private double DecodeJObject (JObject jval, string field)
		{
			if (!jval.TryGetValue(field, out var value)) throw new ArgumentOutOfRangeException(nameof(field), $"Invalid field: [{field}].");

			if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
				throw new ArgumentOutOfRangeException(nameof(field), $"Invalid data type for field: [{field}].");

			return value.Value<double>();
		}

		private object DecodeValue (object value)
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
				lock (m_Server) { return m_Server.GetTwinAsync(id.ToString()).Result; }
			} else {
				var client = GetDeviceClient(id);

				lock (client) { return client.GetTwinAsync().Result; }
			}
		}

		public async Task<IReadOnlyDictionary<string, object>> GetAsync (uint id)
		{
			Twin twin = await GetTwinAsync(id);

			return twin.Properties.Reported
								.Cast<KeyValuePair<string, object>>()
								.ToDictionary(kv => kv.Key, kv => DecodeValue(kv.Value));
		}

		public async Task<T> GetAsync<T> (uint id, string key)
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

		public async Task<double> GetAsync (uint id, string key, string field)
		{
			Twin twin = await GetTwinAsync(id);

			if (!twin.Properties.Reported.Contains(key)) throw new ArgumentOutOfRangeException(nameof(key), $"Invalid property: [{key}].");

			switch (twin.Properties.Reported[key]) {
				case JObject jval: return DecodeJObject(jval, field);

				case IReadOnlyDictionary<string, double> dict: {
						if (!dict.TryGetValue(field, out var dval)) throw new ArgumentOutOfRangeException(nameof(field), $"Invalid field: [{field}].");
						return dval;
					}

				default: throw new ArgumentOutOfRangeException(nameof(key), $"Key [{key}] is not a hash dictionary.");
			}
		}

		public async Task<bool> HasAsync (uint id, string key)
		{
			Twin twin = await GetTwinAsync(id);

			return twin.Properties.Reported.Contains(key);
		}

		private async Task UpdateValueAsync (uint id, string key, dynamic value)
		{
			var col = new TwinCollection();
			col[key] = value;

			var client = GetDeviceClient(id);

			lock (client) { client.UpdateReportedPropertiesAsync(col).Wait(); }
		}

		public Task SetAsync (uint id, string key, uint value) => UpdateValueAsync(id, key, value);
		public Task SetAsync (uint id, string key, int value) => UpdateValueAsync(id, key, value);
		public Task SetAsync (uint id, string key, string value) => UpdateValueAsync(id, key, value);
		public Task SetAsync (uint id, string key, double value) => UpdateValueAsync(id, key, value);
		public Task SetAsync (uint id, string key, bool value) => UpdateValueAsync(id, key, value);

		public async Task SetAsync (uint id, IReadOnlyDictionary<string, object> values)
		{
			var col = new TwinCollection();

			foreach (var kv in values) {
				switch (kv.Value) {
					case IReadOnlyDictionary<string, double> hashvalues: {
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

			var client = GetDeviceClient(id);

			lock (client) { client.UpdateReportedPropertiesAsync(col).Wait(); }
		}

		public async Task SetAsync (uint id, string key, IReadOnlyDictionary<string, double> value)
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

			var client = GetDeviceClient(id);

			lock (client) { client.UpdateReportedPropertiesAsync(root).Wait(); }
		}

		public async Task SetAsync (uint id, string key, string field, double value)
		{
			var root = new TwinCollection() { [key] = new TwinCollection { [field] = value } };

			var client = GetDeviceClient(id);

			lock (client) { client.UpdateReportedPropertiesAsync(root).Wait(); }
		}

		public async Task SendMessageAsync (uint id, string message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));

			var msg = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(message));

			var client = GetDeviceClient(id);

			lock (client) { client.SendEventAsync(msg).Wait(); }
		}
	}
}
