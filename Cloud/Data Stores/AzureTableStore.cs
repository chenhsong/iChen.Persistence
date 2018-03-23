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
		public const int RefreshInterval = 60000;
		public const int MaxBatchSize = 99;
		public const int BatchSize = 5;
		public const int MaxUploadInterval = 5 * 60000;
		public const uint MaxBufferSize = 100000;

		private readonly CloudStorageAccount m_StorageAccount = null;
		private readonly CloudTableClient m_TableClient = null;
		private readonly CloudTable m_CycleDataTable = null;
		private readonly CloudTable m_MoldDataTable = null;
		private readonly CloudTable m_AlarmsTable = null;
		private readonly CloudTable m_AuditTrailTable = null;
		private readonly CloudTable m_EventsTable = null;

		private readonly uint m_MaxMessages = MaxBufferSize;
		private readonly ConcurrentQueue<CycleData> m_CycleDataQueue = new ConcurrentQueue<CycleData>();
		private readonly ConcurrentQueue<MoldData> m_MoldDataQueue = new ConcurrentQueue<MoldData>();
		private readonly ConcurrentQueue<Alarm> m_AlarmsQueue = new ConcurrentQueue<Alarm>();
		private readonly ConcurrentQueue<AuditTrail> m_AuditTrailQueue = new ConcurrentQueue<AuditTrail>();
		private readonly ConcurrentQueue<Event> m_EventsQueue = new ConcurrentQueue<Event>();

		private readonly string m_RowKeyBase = GuidEncoder.Encode(Guid.NewGuid());
		private long m_Seq = 1;
		private Task m_RefreshLoop = null;
		private bool m_IsRunning = false;
		private List<EntryBase> m_Buffer = new List<EntryBase>();
		private DateTime m_LastDataTime = DateTime.MinValue;

		public event Action<string> OnLog;
		public event Action<string> OnDebug;
		public event Action<int, ICollection<EntryBase>, string> OnUploadSuccess;
		public event Action<int, ICollection<EntryBase>, string> OnUploadError;
		public event Action<Exception, string> OnError;

		public AzureTableStore (string account, string signature, uint maxbuffer = MaxBufferSize)
		{
			if (string.IsNullOrWhiteSpace(account)) throw new ArgumentNullException(nameof(account));
			if (string.IsNullOrWhiteSpace(signature)) throw new ArgumentNullException(nameof(signature));
			m_MaxMessages = maxbuffer;

			//var conn = $"DefaultEndpointsProtocol=https;AccountName={account};AccountKey={password}";

			OnDebug?.Invoke($"Connecting to Azure storage account {account}...");
			m_StorageAccount = new CloudStorageAccount(new StorageCredentials(signature), account, null, false);

			OnDebug?.Invoke("Getting Azure storage table client...");
			m_TableClient = m_StorageAccount.CreateCloudTableClient();

			OnDebug?.Invoke("Getting Azure tables...");
			m_CycleDataTable = m_TableClient.GetTableReference(Storage.CycleDataTable);
			m_MoldDataTable = m_TableClient.GetTableReference(Storage.MoldDataTable);
			m_AlarmsTable = m_TableClient.GetTableReference(Storage.AlarmsTable);
			m_AuditTrailTable = m_TableClient.GetTableReference(Storage.AuditTrailTable);
			m_EventsTable = m_TableClient.GetTableReference(Storage.EventsTable);

			OnDebug?.Invoke("Azure storage started.");

			m_IsRunning = true;

			m_RefreshLoop = Task.Run(async () => {
				OnDebug?.Invoke("Azure storage refresh loop started.");

				while (true) {
					await RefreshAsync();

					if (m_IsRunning) await Task.Delay(RefreshInterval).ConfigureAwait(false);
					if (!m_IsRunning && m_Buffer.Count <= 0 && m_CycleDataQueue.Count <= 0 && m_MoldDataQueue.Count <= 0 && m_AuditTrailQueue.Count <= 0 && m_AlarmsQueue.Count <= 0) break;
				}

				OnDebug?.Invoke("Azure storage refresh loop ended.");
			});
		}

		public void Close ()
		{
			OnLog?.Invoke("Azure storage terminating...");

			m_IsRunning = false;
			m_RefreshLoop.Wait(RefreshInterval);

			OnLog?.Invoke("Azure storage terminated.");
		}

		public void Dispose ()
		{
			Close();
		}

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

			this.m_LastDataTime = DateTime.Now;
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

		private void TakeIntoBuffer<T> (ConcurrentQueue<T> queue, int max = MaxBatchSize) where T : EntryBase
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

		public int OutBufferCount { get { return m_CycleDataQueue.Count + m_MoldDataQueue.Count + m_AlarmsQueue.Count + m_AuditTrailQueue.Count + m_EventsQueue.Count; } }

		private async Task RefreshAsync ()
		{
			// Prune the queues

			while (OutBufferCount > m_MaxMessages) {
				if (m_MoldDataQueue.Count > 0) { if (m_MoldDataQueue.TryDequeue(out _)) continue; }
				if (m_EventsQueue.Count > 0) { if (m_EventsQueue.TryDequeue(out _)) continue; }
				if (m_AuditTrailQueue.Count > 0) { if (m_AuditTrailQueue.TryDequeue(out _)) continue; }
				if (m_AlarmsQueue.Count > 0) { if (m_AlarmsQueue.TryDequeue(out _)) continue; }
				if (m_CycleDataQueue.Count > 0) { if (m_CycleDataQueue.TryDequeue(out _)) continue; }
			}

			OnDebug?.Invoke($"Azure - CYCLE:{m_CycleDataQueue.Count},AUDIT:{m_AuditTrailQueue.Count},ALARM:{m_AlarmsQueue.Count},MOLD:{m_MoldDataQueue.Count},EVENT:{m_EventsQueue.Count}" + (m_Buffer.Count > 0 ? $",BUF:{m_Buffer.Count}" : null));

			if (m_Buffer.Count <= 0) {
				// See if we have anything interesting
				var minitems = BatchSize;

				// Not uploaded for a while, upload data anyway
				if (!m_IsRunning) {
					minitems = 1;
				} else {
					if (m_LastDataTime == DateTime.MinValue || (DateTime.Now - m_LastDataTime).TotalMilliseconds > MaxUploadInterval) minitems = 1;
				}

				// Check cycle data first
				if (m_CycleDataQueue.Count() >= minitems) {
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
				var batch = new TableBatchOperation();
				foreach (var data in m_Buffer) batch.Insert(data.ToEntity(m_RowKeyBase + "-" + m_Seq++));

				OnDebug?.Invoke($"Batch uploading {batch.Count} records to Azure storage...");

				try {
					var r = await MapTable(m_Buffer[0]).ExecuteBatchAsync(batch);

					// Check for errors
					var errors = m_Buffer
												.Where((entry, x) => r.Count <= x || (r[x].HttpStatusCode != 201 && r[x].HttpStatusCode != 204))
												.ToList();

					var successes = m_Buffer
														.Where((entry, x) => r.Count > x && (r[x].HttpStatusCode == 201 || r[x].HttpStatusCode == 204))
														.ToList();

					OnUploadSuccess?.Invoke(201, successes, $"{m_Buffer.Count - errors.Count} record(s) out of {m_Buffer.Count} successfully uploaded to Azure storage.");

					m_Buffer.Clear();

					if (errors.Count > 0) {
						m_Buffer.AddRange(errors);
						OnUploadError?.Invoke(0, errors, $"{errors.Count} record(s) failed to upload to Azure storage.");
					}
				} catch (StorageException ex) {
					var status = ex.RequestInformation.HttpStatusCode;
					var errmsg = ex.RequestInformation.ExtendedErrorInformation?.ErrorMessage ?? ex.RequestInformation.HttpStatusMessage ?? ex.Message;

					switch (status) {
						case 0: {
								OnError?.Invoke(ex, $"Azure storage batch upload failed.");
								break;
							}
						case 401:
						case 403: {
								OnUploadError?.Invoke(status, m_Buffer, $"Azure storage batch upload forbidden: {errmsg}");
								break;
							}
						default: {
								OnUploadError?.Invoke(status, m_Buffer, $"Azure storage batch upload failed: {errmsg}");
								break;
							}
					}
				} catch (Exception ex) {
					OnError?.Invoke(ex, $"Azure storage batch upload failed.");
				}
			} else if (m_Buffer.Count > 0) {
				// Single upload
				var data = m_Buffer[0];
				var insert = TableOperation.Insert(data.ToEntity(m_RowKeyBase + "-" + m_Seq++));

				OnDebug?.Invoke($"Uploading record to Azure storage...");

				try {
					var r = await MapTable(data).ExecuteAsync(insert);

					OnUploadSuccess?.Invoke(r.HttpStatusCode, new[] { data }, $"Azure storage upload succeeded, result = {r.HttpStatusCode}.");
					if (m_Buffer.Count <= 1) m_Buffer.Clear(); else m_Buffer.RemoveAt(0);
				} catch (StorageException ex) {
					var status = ex.RequestInformation.HttpStatusCode;
					var errmsg = ex.RequestInformation.ExtendedErrorInformation?.ErrorMessage ?? ex.RequestInformation.HttpStatusMessage ?? ex.Message;

					switch (status) {
						case 0: {
								OnError?.Invoke(ex, $"Azure storage upload failed.");
								break;
							}
						case 401:
						case 403: {
								OnUploadError?.Invoke(status, new[] { data }, $"Azure storage upload forbidden: {errmsg}");
								break;
							}
						default: {
								OnUploadError?.Invoke(status, new[] { data }, $"Azure storage upload failed: {errmsg}");
								break;
							}
					}
				} catch (Exception ex) {
					OnError?.Invoke(ex, "Azure storage upload failed.");
				}
			}
		}
	}
}