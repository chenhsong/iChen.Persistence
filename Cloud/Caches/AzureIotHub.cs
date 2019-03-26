using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;
using DoubleDict = System.Collections.Generic.IReadOnlyDictionary<string, double>;
using ObjDict = System.Collections.Generic.IReadOnlyDictionary<string, object>;

namespace iChen.Persistence.Cloud
{
	public class AzureIoTHub : ISharedCache
	{
		private const string HeartBeatProperty = "LastHeartBeatTime";

		// Careful - twin reads/writes may be throttled at 10/sec.
		protected uint ReadThrottle = 200;
		protected uint WriteThrottle = 500;
		protected uint MessageThrottle = 100;
		protected uint TimeOutInterval = 30000;
		protected uint WaitInterval = 1000;
		protected int Retry = 5;

		private bool m_IsRunning = false;
		private readonly Task m_RefreshTask;

		private readonly string m_DeviceConnectionStringFormat;
		private readonly RegistryManager m_Server = null;
		private readonly SemaphoreSlim m_Lock = new SemaphoreSlim(1, 1);
		private readonly ConcurrentDictionary<uint, DeviceClient> m_Clients = new ConcurrentDictionary<uint, DeviceClient>();
		private readonly ConcurrentDictionary<uint, (TwinCollection Twin, DateTime Time)> m_Updates = new ConcurrentDictionary<uint, (TwinCollection Twin, DateTime Time)>();

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

			m_DeviceConnectionStringFormat = device_conn;

			if (!string.IsNullOrWhiteSpace(server_conn)) m_Server = RegistryManager.CreateFromConnectionString(server_conn);

			m_IsRunning = true;
			m_RefreshTask = RunRefreshLoopAsync();
		}

		public virtual void Dispose ()
		{
			m_IsRunning = false;

			foreach (var kv in m_Clients) {
				var device = kv.Value;

				m_Lock.Wait();
				try { device.Dispose(); } finally { m_Lock.Release(); }
				m_Lock.Dispose();
			}

			m_Clients.Clear();

			if (m_Server != null) {
				m_Lock.Wait();
				try { m_Server.Dispose(); } finally { m_Lock.Release(); }
				m_Lock.Dispose();
			}
		}

		private Exception TranslateUnauthorizedAccessException (uint id, IotHubException ex)
		{
			switch (ex) {
				case DeviceNotFoundException _: return new UnauthorizedAccessException($"Device {id} is not registered on Azure IOT Hub!", ex);
				case DeviceDisabledException _: return new UnauthorizedAccessException($"Device {id} is disabled!", ex);
				case DeviceMaximumQueueDepthExceededException _: return new UnauthorizedAccessException($"Too many messages pending Device {id}: {ex.Message}", ex);
				case MessageTooLargeException _: return new UnauthorizedAccessException($"Message sent to Device {id} is too large: {ex.Message}", ex);
				case QuotaExceededException _: return new UnauthorizedAccessException($"Azure IOT Hub quota exceeded: {ex.Message}", ex);
				case IotHubThrottledException _: return new UnauthorizedAccessException($"Azure IOT Hub bandwidth exceeded: {ex.Message}", ex);
				case UnauthorizedException _:
					return new UnauthorizedAccessException(m_Server != null
												? $"Access to Azure IOT Hub is refused!"
												: $"Device {id} is not enabled on Azure IOT Hub or access to Azure IOT Hub is refused!", ex);

				default: return null;
			}
		}

		private async Task RunRefreshLoopAsync ()
		{
			for (; m_IsRunning;) {
				uint id = 0;
				TwinCollection twin = null;

				if (m_Updates.Count > 0) {
					var entry = m_Updates
												.OrderBy(kv => kv.Value.Time)
												.Where(kv => (DateTime.Now - kv.Value.Time).TotalMilliseconds > WaitInterval)
												.FirstOrDefault();

					if (entry.Value != default) {
						id = entry.Key;
						twin = entry.Value.Twin;
						m_Updates.TryRemove(entry.Key, out _);
					}
				}

				if (twin != null) {
					var retry = Retry;

					for (; ; ) {
						Exception failure = null;

						await m_Lock.WaitAsync().ConfigureAwait(false);

						BeforeTwinUpdate(id, retry < Retry);

						try {
							//if (m_Server != null) {
							//	await m_Server.UpdateTwinAsync(id.ToString(), twin, null).ConfigureAwait(false);
							//} else {
							DeviceClient device = GetDeviceClient(id);
							await device.UpdateReportedPropertiesAsync(twin).ConfigureAwait(false);
							//}
						} catch (IotHubException ex) {
							// Azure IOT Hub exception
							failure = TranslateUnauthorizedAccessException(id, ex);

							if (failure != null) {
								// Unauthorized access, don't bother to retry
								OnError(id, failure);
								break;
							} else if (retry <= 0) {
								// No more retries
								failure = ex;
								OnError(id, new ApplicationException($"Error when updating Azure IOT Hub device {id}!", ex));
								break;
							} else {
								failure = ex;
								// Wait for retry...
							}
						} catch (Exception ex) {
							failure = ex;

							if (retry <= 0) {
								// No more retries
								OnError(id, new ApplicationException($"Error when updating Azure IOT Hub device {id}!", ex));
								break;
							}

							// Wait for retry...
						} finally { m_Lock.Release(); }

						if (failure == null) {
							AfterTwinUpdate(id, retry < Retry);
							await Task.Delay((int) WriteThrottle).ConfigureAwait(false);

							// Exit loop after a wait
							break;
						} else {
							await Task.Delay((new Random()).Next(3000) + 2000).ConfigureAwait(false);

							// Retry after a wait
							retry--;
							OnRetry(id, Retry - retry, failure);
						}
					}
				}

				await Task.Delay(100);
			}
		}

		protected virtual void BeforeTwinUpdate (uint id, bool retry) { }

		protected virtual void AfterTwinUpdate (uint id, bool retry) { }

		protected virtual void OnError (uint id, Exception ex) { }

		protected virtual void OnRetry (uint id, int count, Exception ex) { }

		private DeviceClient GetDeviceClient (uint id) =>
			m_Clients.GetOrAdd(id, k => {
				DeviceClient device = DeviceClient.CreateFromConnectionString(string.Format(m_DeviceConnectionStringFormat, k), Microsoft.Azure.Devices.Client.TransportType.Amqp);
				device.OperationTimeoutInMilliseconds = TimeOutInterval;
				return device;
			});

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
						return tcol.Cast<KeyValuePair<string, object>>()
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
			await m_Lock.WaitAsync().ConfigureAwait(false);

			try {
				Twin twin;

				if (m_Server != null) {
					twin = await m_Server.GetTwinAsync(id.ToString()).ConfigureAwait(false);
					if (twin == null) throw new UnauthorizedAccessException($"Device {id} is not registered on Azure IOT Hub!");
				} else {
					var device = GetDeviceClient(id);
					twin = await device.GetTwinAsync().ConfigureAwait(false);
				}

				await Task.Delay((int) ReadThrottle).ConfigureAwait(false);
				return twin;
			} catch (IotHubException ex) {
				var ex2 = TranslateUnauthorizedAccessException(id, ex);
				if (ex2 == ex) throw; else throw ex2;
			} finally { m_Lock.Release(); }
		}

		public virtual async Task<(ObjDict dict, DateTimeOffset timestamp)> GetAsync (uint id)
		{
			var twin = await GetTwinAsync(id);

			var dict = twin.Properties.Reported
											.Cast<KeyValuePair<string, object>>()
											.ToDictionary(kv => kv.Key, kv => DecodeValue(kv.Value));

			if (twin.Properties.Reported.Contains(HeartBeatProperty))
				return (dict, DateTimeOffset.Parse(DecodeValue(twin.Properties.Reported[HeartBeatProperty])));

			try {
				var timestamp = twin.Properties.Reported.GetLastUpdated();
				return (dict, new DateTimeOffset(timestamp, TimeSpan.Zero));
			} catch (NullReferenceException) {
				// No authority to get timestamp
				return (dict, DateTimeOffset.UtcNow);
			}
		}

		public virtual async Task<T> GetAsync<T> (uint id, string key)
		{
			var twin = await GetTwinAsync(id);

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
			var twin = await GetTwinAsync(id);

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
			var twin = await GetTwinAsync(id);

			if (twin.Properties.Reported.Contains(HeartBeatProperty))
				return DateTimeOffset.Parse(DecodeValue(twin.Properties.Reported[HeartBeatProperty]));

			try {
				return new DateTimeOffset(twin.Properties.Reported.GetLastUpdated(), TimeSpan.Zero);
			} catch (NullReferenceException) {
				// No authority to get timestamp
				return DateTimeOffset.UtcNow;
			}
		}

		public virtual async Task<bool> HasAsync (uint id, string key)
		{
			var twin = await GetTwinAsync(id);

			return twin.Properties.Reported.Contains(key);
		}

		public virtual Task MarkActiveAsync (uint id) => SetAsync(id, HeartBeatProperty, DateTimeOffset.Now.ToString("O"));

		private async Task UpdateValueAsync (uint id, string key, dynamic value)
		{
			var col = m_Updates.AddOrUpdate(id, (Twin: new TwinCollection(), Time: DateTime.Now),
																					(_, v) => (Twin: v.Twin, Time: DateTime.Now));

			lock (col.Twin) {
				col.Twin[key] = value;
				col.Twin[HeartBeatProperty] = DateTimeOffset.Now.ToString("O");
			}

			await Task.CompletedTask;
		}

		public virtual Task SetAsync (uint id, string key, uint value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, int value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, string value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, double value) => UpdateValueAsync(id, key, value);
		public virtual Task SetAsync (uint id, string key, bool value) => UpdateValueAsync(id, key, value);

		public virtual async Task SetAsync (uint id, ObjDict values)
		{
			var col = m_Updates.AddOrUpdate(id, (Twin: new TwinCollection(), Time: DateTime.Now),
																					(_, v) => (Twin: v.Twin, Time: DateTime.Now));

			lock (col.Twin) {
				foreach (var kv in values) {
					switch (kv.Value) {
						case DoubleDict hashvalues: {
								if (hashvalues.Count > 0) {
									var hash = new TwinCollection();
									foreach (var kv2 in hashvalues) hash[kv2.Key] = kv2.Value;
									col.Twin[kv.Key] = hash;
								} else {
									// Remove the property if the entire dictionary is empty
									col.Twin[kv.Key] = null;
								}
								break;
							}
						case int _:
						case uint _:
						case bool _:
						case string _:
						case double _: col.Twin[kv.Key] = kv.Value; break;

						default: throw new ArgumentOutOfRangeException(nameof(values), $"Invalid data type: {kv.Value.GetType().Name}.");
					}
				}

				col.Twin[HeartBeatProperty] = DateTimeOffset.Now.ToString("O");
			}

			await Task.CompletedTask;
		}

		public virtual async Task SetAsync (uint id, string key, DoubleDict value)
		{
			var root = m_Updates.AddOrUpdate(id, (Twin: new TwinCollection(), Time: DateTime.Now),
																						(_, v) => (Twin: v.Twin, Time: DateTime.Now));

			lock (root.Twin) {
				if (value.Count > 0) {
					var col = new TwinCollection();

					foreach (var kv in value) col[kv.Key] = kv.Value;
					root.Twin[key] = col;
				} else {
					// Remove the property if the entire dictionary is empty
					root.Twin[key] = null;
				}

				root.Twin[HeartBeatProperty] = DateTimeOffset.Now.ToString("O");
			}

			await Task.CompletedTask;
		}

		public virtual async Task UpdateAsync (uint id, string key, DoubleDict value)
		{
			var root = m_Updates.AddOrUpdate(id, (Twin: new TwinCollection(), Time: DateTime.Now),
																						(_, v) => (Twin: v.Twin, Time: DateTime.Now));

			lock (root.Twin) {
				if (value.Count > 0) {
					var col = new TwinCollection();

					foreach (var kv in value) col[kv.Key] = kv.Value;
					root.Twin[key] = col;
				} else {
					// Remove the property if the entire dictionary is empty
					root.Twin[key] = null;
				}

				root.Twin[HeartBeatProperty] = DateTimeOffset.Now.ToString("O");
			}

			await Task.CompletedTask;
		}

		public virtual async Task SetAsync (uint id, string key, string field, double value)
		{
			var root = m_Updates.AddOrUpdate(id, (Twin: new TwinCollection(), Time: DateTime.Now),
																						(_, v) => (Twin: v.Twin, Time: DateTime.Now));

			lock (root.Twin) {
				root.Twin[key] = new TwinCollection { [field] = value };
				root.Twin[HeartBeatProperty] = DateTimeOffset.Now.ToString("O");
			}

			await Task.CompletedTask;
		}

		public virtual async Task SendMessageAsync (uint id, byte[] message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));
			if (message.Length <= 0) return;

			var msg = new Microsoft.Azure.Devices.Client.Message(message);

			var device = GetDeviceClient(id);

			await m_Lock.WaitAsync().ConfigureAwait(false);

			try {
				await device.SendEventAsync(msg).ConfigureAwait(false);
				await Task.Delay((int) MessageThrottle);
			} finally { m_Lock.Release(); }
		}

		public IEnumerable<(uint id, string key, string field, object value, DateTimeOffset timestamp)> Dump () =>
			throw new NotImplementedException();
	}
}
