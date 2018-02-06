using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Threading.Tasks;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;

namespace iChen.Analytics
{
	public partial class AnalyticsEngine
	{
		private const string ResultTable = "Results";
		private const string DetailsTable = "Details";
		private const string IsoDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.ffff";

		private static T ConvertDataRowTo<T> (IReadOnlyDictionary<uint, Controller> dict, DataRow x) where T : EntryBase, IDataFileFormatConverter
		{
			if (typeof(T) == typeof(AlarmX)) return new AlarmX(dict, x) as T;
			if (typeof(T) == typeof(CycleDataX)) return new CycleDataX(dict, x) as T;
			if (typeof(T) == typeof(AuditTrailX)) return new AuditTrailX(dict, x) as T;
			if (typeof(T) == typeof(EventX)) return new EventX(dict, x) as T;
			throw new ApplicationException($"Invalid data type: {typeof(T).Name}");
		}

		public async Task<IEnumerable<T>> GetOdbcDatabaseDataAsync<T> (string tableName, DateTimeOffset from, DateTimeOffset to, IPredicate<T> filter = null, Sorting sort = Sorting.ByTime, string orgId = DataStore.DefaultOrgId, uint controllerId = 0, string field = null, bool skipNulls = true)
			where T : EntryBase, IDataFileFormatConverter
		{
			if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

			from = from.ToUniversalTime();
			to = to.ToUniversalTime();

			// Check controller org's
			var clist = await Utils.GetValidControllersAsync(m_DB, orgId, controllerId).ConfigureAwait(false);
			if (clist == null) return null;

			var controllers = clist.ToDictionary(c => (uint) c.ID);
			if (controllers.Count <= 0) return Enumerable.Empty<T>();

			// Build SQL statement
			m_OdbcConnection.SelectCommand.Parameters.Clear();

			// FROM-WHERE clause
			var statement = $" FROM {tableName} WHERE (Time BETWEEN ? AND ?)";

			m_OdbcConnection.SelectCommand.Parameters.Add("@fromDate", OdbcType.DateTime, 2).Value = from.UtcDateTime;
			m_OdbcConnection.SelectCommand.Parameters.Add("@toDate", OdbcType.DateTime, 2).Value = to.UtcDateTime;

			if (filter != null) {
				statement += $" AND ({filter.GetSqlWhereClause()})";
				filter.AddSqlParameters(m_OdbcConnection.SelectCommand.Parameters);
			}

			if (controllerId > 0) {
				statement += $" AND (Controller=?)";
				m_OdbcConnection.SelectCommand.Parameters.Add("@Controller", OdbcType.Int).Value = controllerId;
			}

			if (skipNulls && field != null && typeof(T) != typeof(CycleDataX)) statement += $" AND ({field} IS NOT NULL)";

			// Columns list
			var colslist = "*";

			if (!string.IsNullOrWhiteSpace(field)) {
				colslist = "OrgId, Controller, Time, ID";

				if (typeof(T) != typeof(CycleDataX)) colslist += ", " + field;
			}

			var sql = "SELECT " + colslist + statement;

			// Sorting
			switch (sort) {
				case Sorting.ByController: sql += $" ORDER BY OrgId, Controller, Time, ID"; break;
				case Sorting.ByTime: sql += $" ORDER BY TIme, OrgId, Controller, ID"; break;
				case Sorting.None: break;
				default: throw new ArgumentOutOfRangeException(nameof(sort));
			}

			m_OdbcConnection.SelectCommand.CommandText = sql;

			// Process the dataset
			using (var dset = new DataSet()) {
				try {
					m_OdbcConnection.Fill(dset, ResultTable);
				} catch (Exception ex) {
					throw new ApplicationException("SQL=" + sql, ex);
				}

				var table = dset.Tables[ResultTable];
				var datarows = table.Rows.Cast<DataRow>().ToList();

				if (typeof(T) == typeof(CycleDataX)) {
					// Get cycle data details
					sql = $"SELECT * FROM {Storage.CycleDataValuesTable} WHERE ID IN (SELECT DISTINCT ID {statement})";

					if (!string.IsNullOrWhiteSpace(field)) {
						sql += $" AND (VariableName=?)";
						m_OdbcConnection.SelectCommand.Parameters.Add("@Field", OdbcType.VarChar).Value = field;
					}

					m_OdbcConnection.SelectCommand.CommandText = sql;
					m_OdbcConnection.Fill(dset, DetailsTable);

					table = dset.Tables[DetailsTable];
					var details = table.Rows.Cast<DataRow>().ToLookup(row => (int) row["ID"]);
					if (details.Count <= 0) return datarows.Select(row => ConvertDataRowTo<T>(controllers, row));

					var results = new List<T>();

					foreach (var row in datarows) {
						var id = (int) row["ID"];
						if (!details.Contains(id)) {
							results.Add(ConvertDataRowTo<T>(controllers, row));
						} else {
							var data = details[id].ToDictionary(drow => drow["VariableName"].ToString(), drow => (double) (float) drow["Value"]);
							results.Add(new CycleDataX(controllers, row, data) as T);
						}
					}

					return results;
				}

				// Return the data
				return datarows.Select(row => ConvertDataRowTo<T>(controllers, row));
			}
		}
	}
}