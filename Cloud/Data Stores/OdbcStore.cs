using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace iChen.Persistence.Cloud
{
	public partial class OdbcStore : IDisposable
	{
		public const int RefreshInterval = 500;
		public const int ErrorRefreshInterval = 15000;  // Retry every 15 seconds
		public const uint MaxBufferSize = 100000;

		private readonly uint m_MaxMessages = MaxBufferSize;
		private readonly ConcurrentQueue<EntryBase> m_DataUploadQueue = new ConcurrentQueue<EntryBase>();

		private readonly Func<DbConnection> connectionFactory = null;
		private readonly Func<string, DbType, int, object, DbParameter> createSqlParameter = null;

		private Task m_RefreshLoop = null;
		private bool m_IsRunning = false;
		private DateTime m_NextTryTime = DateTime.MinValue;

		public event Action<string> OnLog;
		public event Action<string> OnDebug;
		public event Action<string> OnSQL;
		public event Action<EntryBase, string> OnUploadSuccess;
		public event Action<EntryBase, Exception, string> OnUploadError;
		public event Action<Exception, string> OnError;

		public OdbcStore (Func<DbConnection> connectionFactory, Func<string, DbType, int, object, DbParameter> createSqlParameter, uint maxbuffer = MaxBufferSize)
		{
			if (connectionFactory == null) throw new ArgumentNullException(nameof(connectionFactory));
			if (createSqlParameter == null) throw new ArgumentNullException(nameof(createSqlParameter));

			this.connectionFactory = connectionFactory;
			this.createSqlParameter = createSqlParameter;
			m_MaxMessages = maxbuffer;

			OnDebug?.Invoke("Archive database started.");

			m_IsRunning = true;

			m_RefreshLoop = Task.Run(async () => {
				OnDebug?.Invoke("Archive database refresh loop started.");

				while (true) {
					if (m_NextTryTime < DateTime.Now) {
						m_NextTryTime = DateTime.MinValue;

						await RefreshAsync();
					}

					if (m_IsRunning) await Task.Delay(RefreshInterval).ConfigureAwait(false);
					if (!m_IsRunning && m_DataUploadQueue.Count <= 0) break;
				}

				OnDebug?.Invoke("Archive database refresh loop ended.");
			});
		}

		public void Close ()
		{
			OnLog?.Invoke("Archive database terminating...");

			m_IsRunning = false;
			m_RefreshLoop.Wait(15000);

			OnLog?.Invoke("Archive database terminated.");
		}

		public void Dispose () => Close();

		/// <remarks>This method is thread-safe.</remarks>
		public void Enqueue (EntryBase entry)
		{
			if (entry == null) throw new ArgumentNullException(nameof(entry));
			m_DataUploadQueue.Enqueue(entry);
		}

		private string MapTable (EntryBase entry)
		{
			switch (entry) {
				case null: throw new ArgumentNullException(nameof(entry));
				case CycleData _: return Storage.CycleDataTable;
				case Alarm _: return Storage.AlarmsTable;
				case AuditTrail _: return Storage.AuditTrailTable;
				case Event _: return Storage.EventsTable;
				default: return null;
			}
		}

		public int OutBufferCount => m_DataUploadQueue.Count;

		private async Task RefreshAsync ()
		{
			// Prune the queues

			while (OutBufferCount > m_MaxMessages) {
				if (m_DataUploadQueue.TryDequeue(out var _)) continue;
			}

			//OnDebug?.Invoke($"{m_DataUploadQueue.Count} in queue.");

			// Upload the data

			if (!m_DataUploadQueue.TryPeek(out var entry)) return;

			var table = MapTable(entry);
			if (table == null) {
				OnDebug?.Invoke($"Skipping {entry.GetType().Name} for archive database...");
				// Remove the message from the queue
				m_DataUploadQueue.TryDequeue(out entry);
				return;
			}

			// The base SQL statement should be as similar as possible so that it will be cached in the database server
			// This is why ValuesListForInsertStatement uses parameters instead of actual values
			var sql = $"INSERT INTO {table} {entry.InsertStatement}";

			OnSQL?.Invoke("Database SQL = " + sql);

			// Store to database

			try {
				using (var conn = connectionFactory()) {
					await conn.OpenAsync();

					using (var cmd = conn.CreateCommand()) {
						// Use a transaction
						using (var transaction = conn.BeginTransaction()) {
							// Must assign both transaction object and connection to Command object
							// for a pending local transaction (as per MSDN)
							cmd.Connection = conn;
							cmd.Transaction = transaction;

							try {
								// Prepare the SQL statement
								cmd.CommandType = CommandType.Text;
								cmd.CommandText = sql;
								cmd.Parameters.Clear();

								entry.AddSqlParameters(cmd.Parameters, createSqlParameter);

								await cmd.ExecuteNonQueryAsync();

								// Special treatment for cycle data
								if (entry is CycleData cycledata) {
									if (cycledata.Data.Count > 0) {
										sql = $"SELECT MAX(ID) FROM {Storage.CycleDataTable}";
										OnSQL?.Invoke(sql);

										cmd.CommandText = sql;
										var id = (int) await cmd.ExecuteScalarAsync();
										OnSQL?.Invoke("New Cycle Data ID = " + id);

										sql = $"INSERT INTO {Storage.CycleDataValuesTable} (ID, VariableName, Value)\nVALUES";
										sql += string.Join(",\n", cycledata.Data.Select(kv => $" ({id}, '{kv.Key}', {(float) kv.Value})"));
										OnSQL?.Invoke(sql);

										cmd.CommandText = sql;
										await cmd.ExecuteNonQueryAsync();
									}
								}

								transaction.Commit();
							} catch {
								transaction.Rollback();
								throw;
							}
						}

						OnUploadSuccess?.Invoke(entry, $"Successfully uploaded {entry.GetType().Name} to archive database.");
					}
				}
			} catch (DbException ex) {
				// Do not remove the message from the queue
				OnUploadError?.Invoke(entry, ex, $"Cannot upload {entry.GetType().Name} to archive database!\n{sql}");
				m_NextTryTime = DateTime.Now.AddMilliseconds(ErrorRefreshInterval);
				return;
			} catch (Exception ex) {
				// Do not remove the message from the queue
				OnError?.Invoke(ex, $"Error when uploading {entry.GetType().Name} to archive database!");
				m_NextTryTime = DateTime.Now.AddMilliseconds(ErrorRefreshInterval);
				return;
			}

			// Remove the message from the queue

			m_DataUploadQueue.TryDequeue(out entry);
		}
	}
}