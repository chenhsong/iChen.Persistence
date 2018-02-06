using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Threading.Tasks;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace iChen.Analytics
{
	public partial class AnalyticsEngine : IDisposable
	{
		public static bool IsInitialized { get; private set; } = false;

		public static string DefaultOdbcConnectionString { get; private set; }
		public static string DefaultAzureStorageAccount { get; private set; } = "ichen";    // Replace this with customer-specific storage account name
		public static string DefaultAzureStorageKey { get; private set; }

		private readonly OdbcDataAdapter m_OdbcConnection = null;
		private readonly CloudTableClient m_CloudStore = null;

		private readonly ConfigDB m_DB = null;
		private readonly bool m_PrivateDB = false;

		private bool UseDatabase { get { return m_OdbcConnection != null; } }
		private bool UseAzure { get { return m_CloudStore != null; } }

		public AnalyticsEngine (ConfigDB db = null, string odbcConnStr = null, string azureStorage = null, string azureStorageKey = null)
		{
			if (!IsInitialized) throw new ApplicationException("The analytics engine has not been initialized.");

			if (db == null) {
				m_DB = new ConfigDB();
				m_PrivateDB = true;
			} else {
				m_DB = db;
			}

			// Archive database via ODBC
			odbcConnStr = odbcConnStr ?? DefaultOdbcConnectionString;

			if (!string.IsNullOrWhiteSpace(odbcConnStr)) {
				this.m_OdbcConnection = new OdbcDataAdapter(null, odbcConnStr.Trim());
				return;
			}

			// Try Azure if set...
			azureStorage = azureStorage ?? DefaultAzureStorageAccount;
			azureStorageKey = azureStorageKey ?? DefaultAzureStorageKey;

			if (string.IsNullOrWhiteSpace(azureStorage)) throw new ArgumentNullException(nameof(azureStorage));
			if (string.IsNullOrWhiteSpace(azureStorageKey)) throw new ArgumentNullException(nameof(azureStorageKey));

			var azureconnstr = $"DefaultEndpointsProtocol=https;AccountName={azureStorage.Trim()};AccountKey={azureStorageKey.Trim()}";
			var azurestorage = CloudStorageAccount.Parse(azureconnstr);

			this.m_CloudStore = azurestorage.CreateCloudTableClient();
		}

		public void Dispose ()
		{
			if (m_OdbcConnection != null) m_OdbcConnection.Dispose();
			if (m_PrivateDB) m_DB.Dispose();
		}

		public static void Init (string dbConnStr, string azureStorage, string azureStorageKey)
		{
			if (string.IsNullOrWhiteSpace(dbConnStr)) {
				if (string.IsNullOrWhiteSpace(azureStorage)) throw new ArgumentNullException(nameof(azureStorage));
				if (string.IsNullOrWhiteSpace(azureStorageKey)) throw new ArgumentNullException(nameof(azureStorageKey));
			}

			DefaultOdbcConnectionString = dbConnStr?.Trim();
			DefaultAzureStorageAccount = azureStorage?.Trim();
			DefaultAzureStorageKey = azureStorageKey?.Trim();

			IsInitialized = true;
		}

		public async Task<IEnumerable<T>> GetDataAsync<T> (string tableName, DateTimeOffset from, DateTimeOffset to, IPredicate<T> filter = null, Sorting sort = Sorting.ByTime, string orgId = DataStore.DefaultOrgId, uint controllerId = 0, string field = null, bool skipNulls = true) where T : EntryBase, IDataFileFormatConverter
		{
			if (UseAzure) return await GetAzureTableDataAsync<T>(tableName, from, to, filter, sort, orgId, controllerId, field, skipNulls);
			if (UseDatabase) return await GetOdbcDatabaseDataAsync<T>(tableName, from, to, filter, sort, orgId, controllerId, field, skipNulls);
			throw new ApplicationException();
		}
	}
}