using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;
using Microsoft.WindowsAzure.Storage.Table;

namespace iChen.Analytics
{
	public partial class AnalyticsEngine
	{
		private static T ConvertAzureTableEntityTo<T> (IReadOnlyDictionary<uint, Controller> dict, DynamicTableEntity x)
			where T : EntryBase, IDataFileFormatConverter
		{
			if (typeof(T) == typeof(AlarmX)) return new AlarmX(dict, x) as T;
			if (typeof(T) == typeof(CycleDataX)) return new CycleDataX(dict, x) as T;
			if (typeof(T) == typeof(AuditTrailX)) return new AuditTrailX(dict, x) as T;
			if (typeof(T) == typeof(EventX)) return new EventX(dict, x) as T;
			throw new ApplicationException($"Invalid data type: {typeof(T).Name}");
		}

		public async Task<IEnumerable<T>> GetAzureTableDataAsync<T> (string tableName, DateTimeOffset from, DateTimeOffset to, IPredicate<T> filter = null, Sorting sort = Sorting.ByTime, string orgId = DataStore.DefaultOrgId, uint controllerId = 0, string field = null, bool skipNulls = true)
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

			var table = m_CloudStore.GetTableReference(tableName);

			// NOTE - We must use the old style of table query because all of our classes are dynamic entities.
			//        With the fluent query method, there is no way to refer to a particular property.

			var tfilters = TableQuery.CombineFilters(
				TableQuery.GenerateFilterConditionForDate(Storage.Time, QueryComparisons.GreaterThanOrEqual, from),
				TableOperators.And,
				TableQuery.GenerateFilterConditionForDate(Storage.Time, QueryComparisons.LessThan, to)
			);

			if (typeof(T) == typeof(CycleDataX) && !string.IsNullOrWhiteSpace(field)) {
				// Handle variable name compression
				if (field.StartsWith("Z_QD")) field = field.Substring(4);
			}

			if (skipNulls && !string.IsNullOrWhiteSpace(field)) {
				tfilters = TableQuery.CombineFilters(tfilters,
					TableOperators.And,
					TableQuery.GenerateFilterCondition(field, QueryComparisons.NotEqual, "<NULL>")
				);
			}

			// Treat many controllers vs one controller differently due to partition key structure

			if (controllers.Count > 1) {
				// Get many controllers

				var to2 = new DateTime(to.Year, to.Month, 1).AddMonths(1);

				// Format partition keys
				var fromstr = from.ToString("yyMM");
				var tostr = to2.ToString("yyMM");

				if (!orgId.Equals(DataStore.DefaultOrgId, StringComparison.OrdinalIgnoreCase)) {
					fromstr = orgId + "-" + fromstr;
					tostr = orgId + "-" + tostr;
				}

				// Partition key range = year-month
				tfilters = TableQuery.CombineFilters(tfilters,
					TableOperators.And,
					TableQuery.CombineFilters(
						TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.PartitionKey), QueryComparisons.GreaterThanOrEqual, fromstr),
						TableOperators.And,
						TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.PartitionKey), QueryComparisons.LessThan, tostr)
					)
				);

				if (from.Year == to.Year && from.Month == to.Month) {
					// Row key range = day-hour-minute
					tfilters = TableQuery.CombineFilters(tfilters,
						TableOperators.And,
						TableQuery.CombineFilters(
							TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.RowKey), QueryComparisons.GreaterThanOrEqual, from.ToString(Storage.RowKeyPrefixFormat)),
							TableOperators.And,
							TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.RowKey), QueryComparisons.LessThan, to.AddMinutes(1).ToString(Storage.RowKeyPrefixFormat))
						)
					);
				} else {
					// Unfortunately must process all data in the range, cannot refine it further
				}

				// Get all the records within the date/time range
				var query = new TableQuery<DynamicTableEntity>().Where(tfilters);

				if (!string.IsNullOrWhiteSpace(field)) query = query.Select(new[] { Storage.Time, field });

				var list = await table.ExecuteQueryAsync<DynamicTableEntity>(query).ConfigureAwait(false);

				var filtered = (filter == null) ? list.AsEnumerable() : list.Where(filter.GetLinqFilter());

				var stream = filtered.AsParallel().Select(entity => ConvertAzureTableEntityTo<T>(controllers, entity));

				// Sorting
				switch (sort) {
					case Sorting.ByController: return stream.OrderBy(x => x.Controller).ThenBy(x => x.Time).ThenBy(x => x.RowKey).ToList();
					case Sorting.ByTime: return stream.OrderBy(x => x.Time).ThenBy(x => x.Controller).ThenBy(x => x.RowKey).ToList();
					case Sorting.None: return stream.ToList();
					default: throw new ArgumentOutOfRangeException(nameof(sort));
				}
			} else {
				// Get a specific controller

				var id = controllers.Single().Key;

				// Calculate date/time range
				var from2 = new DateTimeOffset(from.Year, from.Month, 1, 0, 0, 0, from.Offset);
				var to2 = new DateTimeOffset(to.Year, to.Month, 1, 0, 0, 0, from.Offset).AddMonths(1);

				var partitions = new List<DateTimeOffset>();

				for (var date = from2; date < to2;) {
					partitions.Add(date);
					date = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, from.Offset).AddMonths(1);
				}

				// Get monthly data - cannot do it in parallel as we only have one filter
				var list = new List<T>();

				for (var x = 0; x < partitions.Count; x++) {
					var date = partitions[x];

					// Partition key = year-month
					var filter2 = TableQuery.CombineFilters(tfilters,
						TableOperators.And,
						TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.PartitionKey), QueryComparisons.Equal, Storage.MakePartitionKey(orgId, id, date))
					);

					var min = (x <= 0) ? from.ToString(Storage.RowKeyPrefixFormat) : from2.ToString(Storage.RowKeyPrefixFormat);
					var max = (x >= partitions.Count - 1) ? to.ToString(Storage.RowKeyPrefixFormat) : "312400-";

					// Row key range = day-hour-minute
					filter2 = TableQuery.CombineFilters(filter2,
						TableOperators.And,
						TableQuery.CombineFilters(
							TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.RowKey), QueryComparisons.GreaterThan, min),
							TableOperators.And,
							TableQuery.GenerateFilterCondition(nameof(DynamicTableEntity.RowKey), QueryComparisons.LessThan, max)
						)
					);

					var query = new TableQuery<DynamicTableEntity>().Where(filter2);

					if (!string.IsNullOrWhiteSpace(field)) query = query.Select(new[] { Storage.Time, field });

					var result = (await table.ExecuteQueryAsync<DynamicTableEntity>(query).ConfigureAwait(false));

					var rquery = (filter == null) ? result.AsParallel() : result.Where(filter.GetLinqFilter()).AsParallel();

					list.AddRange(rquery.Select(entity => ConvertAzureTableEntityTo<T>(controllers, entity)));
				}

				// Sorting
				switch (sort) {
					case Sorting.ByController: return list.OrderBy(x => x.Controller).ThenBy(x => x.Time).ThenBy(x => x.RowKey).ToList();
					case Sorting.ByTime: return list.OrderBy(x => x.Time).ThenBy(x => x.Controller).ThenBy(x => x.RowKey).ToList();
					case Sorting.None: return list.ToList();
					default: throw new ArgumentOutOfRangeException(nameof(sort));
				}
			}
		}
	}
}