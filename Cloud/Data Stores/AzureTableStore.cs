using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;

namespace iChen.Persistence.Cloud
{
	public partial class AzureTableStore : IDisposable
	{
		public const uint RefreshInterval = 1000;
		public const uint MaxBatchSize = 99;
		public const uint MaxUploadInterval = 5 * 60000;
		public const uint MaxBufferSize = 100000;

		public readonly uint BatchSize = 5;
		public readonly uint CycleDataBatchSize = 5;

		private readonly CloudStorageAccount m_StorageAccount = null;
		private readonly CloudTableClient m_TableClient = null;
		private readonly CloudTable m_CycleDataTable = null;
		private readonly CloudTable m_MoldDataTable = null;
		private readonly CloudTable m_AlarmsTable = null;
		private readonly CloudTable m_AuditTrailTable = null;
		private readonly CloudTable m_EventsTable = null;
		private readonly CloudTable m_LinksTable = null;

		private readonly uint m_MaxMessages = MaxBufferSize;
		private readonly ConcurrentQueue<CycleData> m_CycleDataQueue = new ConcurrentQueue<CycleData>();
		private readonly ConcurrentQueue<MoldData> m_MoldDataQueue = new ConcurrentQueue<MoldData>();
		private readonly ConcurrentQueue<Alarm> m_AlarmsQueue = new ConcurrentQueue<Alarm>();
		private readonly ConcurrentQueue<AuditTrail> m_AuditTrailQueue = new ConcurrentQueue<AuditTrail>();
		private readonly ConcurrentQueue<Event> m_EventsQueue = new ConcurrentQueue<Event>();

		private readonly string m_RowKeyBase = GuidEncoder.Encode(Guid.NewGuid());
		private ulong m_Seq = 1;
		private readonly Task m_RefreshLoop = null;
		private bool m_IsRunning = false;
		private readonly List<EntryBase> m_Buffer = new List<EntryBase>();
		private DateTime m_LastEnqueueTime = DateTime.MinValue;
		private DateTime m_LastDebugMessage = DateTime.MinValue;

		public event Action<string> OnLog;
		public event Action<string> OnDebug;
		public event Action<int, ICollection<EntryBase>, string> OnUploadSuccess;
		public event Action<int, ICollection<EntryBase>, string> OnUploadError;
		public event Action<Exception, string> OnError;

		public AzureTableStore (string account, string signature, uint batch = 0, uint cycle_data_batch = 0, bool useHttps = true, uint maxbuffer = MaxBufferSize, Action<string> onDebug = null)
		{
			if (string.IsNullOrWhiteSpace(account)) throw new ArgumentNullException(nameof(account));
			if (string.IsNullOrWhiteSpace(signature)) throw new ArgumentNullException(nameof(signature));

			if (batch > 0) CycleDataBatchSize = BatchSize = batch;
			if (cycle_data_batch > 0) CycleDataBatchSize = cycle_data_batch;

			m_MaxMessages = maxbuffer;
			OnDebug += onDebug;

			//var conn = $"DefaultEndpointsProtocol=https;AccountName={account};AccountKey={password}";

			OnDebug?.Invoke($"Connecting to Azure Storage account {account} using {(useHttps ? "HTTPS" : "HTTP")}...");
			OnDebug?.Invoke($"Upload batch size = {BatchSize}, Cycle data upload batch size = {CycleDataBatchSize}");

			m_StorageAccount = new CloudStorageAccount(new StorageCredentials(signature), account, null, useHttps);

			OnDebug?.Invoke("Getting Azure Table Store client...");
			m_TableClient = m_StorageAccount.CreateCloudTableClient();

			OnDebug?.Invoke("Getting Azure tables...");
			m_CycleDataTable = m_TableClient.GetTableReference(Storage.CycleDataTable);
			m_MoldDataTable = m_TableClient.GetTableReference(Storage.MoldDataTable);
			m_AlarmsTable = m_TableClient.GetTableReference(Storage.AlarmsTable);
			m_AuditTrailTable = m_TableClient.GetTableReference(Storage.AuditTrailTable);
			m_EventsTable = m_TableClient.GetTableReference(Storage.EventsTable);
			m_LinksTable = m_TableClient.GetTableReference(Storage.LinksTable);

			OnDebug?.Invoke("All tables obtained from Azure Table Store.");

			m_IsRunning = true;

			m_RefreshLoop = RunRefreshLoopAsync();
		}

		private async Task RunRefreshLoopAsync ()
		{
			OnDebug?.Invoke("Azure Table Store refresh loop started.");

			while (true) {
				if (m_Buffer.Count > 0 || OutBufferCount > 0) {
					try {
						await RefreshAsync();
					} catch (Exception ex) {
						OnError?.Invoke(ex, "Error when uploading to Azure Table Store.");
					}
				} else if (!m_IsRunning) {
					break;
				}

				await Task.Delay((int) RefreshInterval).ConfigureAwait(false);
			}

			OnDebug?.Invoke("Azure Table Store refresh loop ended.");
		}

		public void Close ()
		{
			OnDebug?.Invoke("Azure Table Store terminating...");

			m_IsRunning = false;
			m_RefreshLoop?.Wait((int) RefreshInterval);

			OnLog?.Invoke("Azure Table Store terminated.");
		}

		public void Dispose () => Close();

		/// <remarks>This method is thread-safe.</remarks>
		public void Enqueue (EntryBase entry)
		{
			if (entry == null) throw new ArgumentNullException(nameof(entry));

			switch (entry) {
				case CycleData cycle: m_CycleDataQueue.Enqueue(cycle); break;
				case MoldData mold: m_MoldDataQueue.Enqueue(mold); break;
				case Alarm alarm: m_AlarmsQueue.Enqueue(alarm); break;
				case AuditTrail audit: m_AuditTrailQueue.Enqueue(audit); break;
				case Event ev: m_EventsQueue.Enqueue(ev); break;
				default: throw new ApplicationException();
			}

			m_LastEnqueueTime = DateTime.Now;
			m_LastDebugMessage = DateTime.MinValue;
		}

		private CloudTable MapTable (EntryBase entry)
		{
			switch (entry) {
				case null: throw new ArgumentNullException(nameof(entry));
				case CycleData _: return m_CycleDataTable;
				case MoldData _: return m_MoldDataTable;
				case Alarm _: return m_AlarmsTable;
				case AuditTrail _: return m_AuditTrailTable;
				case Event _: return m_EventsTable;
				default: throw new ApplicationException();
			}
		}

		private void TakeIntoBuffer<T> (ConcurrentQueue<T> queue, uint max = MaxBatchSize) where T : EntryBase
		{
			string partition = null;

			while (m_Buffer.Count < max) {
				T entry;

				if (partition != null) {
					if (!queue.TryPeek(out entry)) break;
					if (entry.GeneratedPartitionKey != partition) break;
				}

				if (!queue.TryDequeue(out entry)) break;

				if (partition == null) partition = entry.GeneratedPartitionKey;
				m_Buffer.Add(entry);
			}
		}

		public int OutBufferCount => m_CycleDataQueue.Count + m_MoldDataQueue.Count + m_AlarmsQueue.Count + m_AuditTrailQueue.Count + m_EventsQueue.Count;

		private async Task RefreshAsync ()
		{
			// Prune the queues in order of least importance

			while (OutBufferCount > m_MaxMessages) {
				if (m_MoldDataQueue.Count > 0) { if (m_MoldDataQueue.TryDequeue(out _)) continue; }
				if (m_EventsQueue.Count > 0) { if (m_EventsQueue.TryDequeue(out _)) continue; }
				if (m_AuditTrailQueue.Count > 0) { if (m_AuditTrailQueue.TryDequeue(out _)) continue; }
				if (m_AlarmsQueue.Count > 0) { if (m_AlarmsQueue.TryDequeue(out _)) continue; }
				if (m_CycleDataQueue.Count > 0) { if (m_CycleDataQueue.TryDequeue(out _)) continue; }
			}

			if ((DateTime.Now - m_LastDebugMessage).TotalMilliseconds > MaxUploadInterval) {
				OnDebug?.Invoke($"Azure - CYCLE:{m_CycleDataQueue.Count},AUDIT:{m_AuditTrailQueue.Count},ALARM:{m_AlarmsQueue.Count},MOLD:{m_MoldDataQueue.Count},EVENT:{m_EventsQueue.Count}" + (m_Buffer.Count > 0 ? $",BUF:{m_Buffer.Count}" : null));
				m_LastDebugMessage = DateTime.Now;
			}

			if (m_Buffer.Count <= 0) {
				// See if we have anything interesting
				var minitems = BatchSize;
				var mincycledata = CycleDataBatchSize;

				// Not uploaded for a while, upload data anyway
				if (!m_IsRunning) {
					minitems = mincycledata = 1;
				} else {
					if (m_LastEnqueueTime == DateTime.MinValue || (DateTime.Now - m_LastEnqueueTime).TotalMilliseconds > MaxUploadInterval) minitems = mincycledata = 1;
				}

				// Check cycle data first
				if (m_CycleDataQueue.Count() >= mincycledata) {
					TakeIntoBuffer(m_CycleDataQueue);
				} else if (m_EventsQueue.Count() >= minitems) {
					TakeIntoBuffer(m_EventsQueue);
				} else if (m_AuditTrailQueue.Count() >= minitems) {
					TakeIntoBuffer(m_AuditTrailQueue);
				} else if (m_AlarmsQueue.Count() >= minitems) {
					TakeIntoBuffer(m_AlarmsQueue);
				} else if (m_MoldDataQueue.Count() >= 1) {
					TakeIntoBuffer(m_MoldDataQueue, 1);
				}
			}

			await UploadBufferAsync();
		}

		private async Task UploadBufferAsync ()
		{
			if (m_Buffer.Count > 1 && m_Buffer[0].UseBatches) {
				// Batch upload
				var table = MapTable(m_Buffer[0]);
				var uploads = new TableBatchOperation();
				var links = new TableBatchOperation();
				var id = m_Buffer[0].Controller;

				foreach (var data in m_Buffer) {
					var entity = data.ToEntity(data.ID ?? m_RowKeyBase + "-" + m_Seq++);
					uploads.Insert(entity);
					if (data.ID != null) links.Insert(new Link(table.Name, data.ID, entity.PartitionKey, entity.RowKey));
				}

				OnDebug?.Invoke($"Batch uploading {uploads.Count} records to Azure table storage {table.Name} for controller [{id}]...");

				try {
					if (links.Count > 0) await m_LinksTable.ExecuteBatchAsync(links);

					var r = await table.ExecuteBatchAsync(uploads);

					// Check for errors
					var errors = m_Buffer
												.Where((entry, x) => r.Count <= x || (r[x].HttpStatusCode != 201 && r[x].HttpStatusCode != 204))
												.ToList();

					var successes = m_Buffer
														.Where((entry, x) => r.Count > x && (r[x].HttpStatusCode == 201 || r[x].HttpStatusCode == 204))
														.ToList();

					OnUploadSuccess?.Invoke(201, successes, $"{m_Buffer.Count - errors.Count} record(s) out of {m_Buffer.Count} for controller [{id}] successfully uploaded to Azure table storage {table.Name}.");

					m_Buffer.Clear();

					if (errors.Count > 0) {
						m_Buffer.AddRange(errors);
						OnUploadError?.Invoke(0, errors, $"{errors.Count} record(s) for controller [{id}] failed to upload to Azure table storage {table.Name}.");
					}
				} catch (StorageException ex) {
					var status = ex.RequestInformation.HttpStatusCode;
					var errmsg = ex.RequestInformation.ExtendedErrorInformation?.ErrorMessage ?? ex.RequestInformation.HttpStatusMessage ?? ex.Message;

					switch (status) {
						case 0: {
								OnError?.Invoke(ex, $"Azure table storage batch upload to {table.Name} for controller [{id}] failed.");
								break;
							}
						case 401:
						case 403: {
								OnUploadError?.Invoke(status, m_Buffer, $"Azure table storage batch upload to {table.Name} for controller [{id}] forbidden: {errmsg}");
								break;
							}
						default: {
								OnUploadError?.Invoke(status, m_Buffer, $"Azure table storage batch upload to {table.Name} for controller [{id}] failed: {errmsg}");
								break;
							}
					}
				} catch (Exception ex) {
					OnError?.Invoke(ex, $"Azure table storage batch upload to {table.Name} for controller [{id}] failed.");
				}
			} else if (m_Buffer.Count > 0) {
				// Single upload
				var data = m_Buffer[0];
				var id = data.Controller;
				var table = MapTable(data);
				var entity = data.ToEntity(data.ID ?? m_RowKeyBase + "-" + m_Seq++);
				var insert = TableOperation.Insert(entity);
				var link = (data.ID != null) ? TableOperation.Insert(new Link(table.Name, data.ID, entity.PartitionKey, entity.RowKey)) : null;

				OnDebug?.Invoke($"Uploading record to Azure table storage {table.Name} for controller [{id}]...");

				try {
					TableResult r;

					if (link != null) r = await m_LinksTable.ExecuteAsync(link);

					r = await table.ExecuteAsync(insert);

					OnUploadSuccess?.Invoke(r.HttpStatusCode, new[] { data }, $"Azure table storage upload to {table.Name} for controller [{id}] succeeded, result = {r.HttpStatusCode}.");
					if (m_Buffer.Count <= 1) m_Buffer.Clear(); else m_Buffer.RemoveAt(0);
				} catch (StorageException ex) {
					var status = ex.RequestInformation.HttpStatusCode;
					var errmsg = ex.RequestInformation.ExtendedErrorInformation?.ErrorMessage ?? ex.RequestInformation.HttpStatusMessage ?? ex.Message;

					switch (status) {
						case 0: {
								OnError?.Invoke(ex, $"Azure table storage upload to {table.Name} for controller [{id}] failed.");
								break;
							}
						case 401:
						case 403: {
								OnUploadError?.Invoke(status, new[] { data }, $"Azure table storage upload to {table.Name} for controller [{id}] forbidden: {errmsg}");
								break;
							}
						default: {
								OnUploadError?.Invoke(status, new[] { data }, $"Azure table storage upload to {table.Name} for controller [{id}] failed: {errmsg}");
								break;
							}
					}
				} catch (Exception ex) {
					OnError?.Invoke(ex, $"Azure table storage upload to {table.Name} for controller [{id}] failed.");
				}
			}
		}
	}
}