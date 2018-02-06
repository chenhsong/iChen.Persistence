using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using iChen.Persistence.Cloud;

namespace iChen.Analytics
{
	public partial class AnalyticsEngine
	{
		public class TimeSplit<T>
		{
			public DateTimeOffset StartTime { get; set; }
			public DateTimeOffset EndTime { get; set; }
			public IDictionary<T, double> Data { get; } = new Dictionary<T, double>();

			public TimeSplit ()
			{
			}

			public TimeSplit (IReadOnlyDictionary<T, double> dict) : this()
			{
				foreach (var kv in dict) Data.Add(kv);
			}
		}

		public async Task<IReadOnlyDictionary<uint, IList<TimeSplit<T>>>> GetEventsTimeSplit<T> (DateTimeOffset from, DateTimeOffset to, Func<EventX, T> grouping, string field, T nullValue, bool skipNulls, TimeSpan step, TimeSpan timezone, string orgId, ICollection<uint> controllers)
		{
			if (from > to) throw new ArgumentOutOfRangeException(nameof(from), "Start date must not be later than the end date.");
			if (grouping == null) throw new ArgumentNullException(nameof(grouping));

			var clist = await Utils.GetValidControllersAsync(m_DB, orgId).ConfigureAwait(false);
			if (clist == null) return null;

			if (controllers == null || controllers.Contains(0)) {
				controllers = clist.Select(c => (uint) c.ID).ToList();
			} else {
				var newlist = clist.Select(c => (uint) c.ID).Intersect(controllers).ToList();

				// No controllers selected
				if (newlist.Count <= 0 || newlist.Count < controllers.Count) return null;

				controllers = newlist;
			}

			var incrementdate = Utils.GetIncrementDateFunc(step, timezone);

			var result = new ConcurrentDictionary<uint, ConcurrentBag<EventX>>(controllers.Select(id => new KeyValuePair<uint, ConcurrentBag<EventX>>(id, new ConcurrentBag<EventX>())));

			foreach (var controllerId in controllers) {
				// Start selecting one day before the lower bound
				var r = await GetDataAsync<EventX>(Storage.EventsTable, from.AddDays(-1), to, null, Sorting.None, orgId, controllerId, field, skipNulls).ConfigureAwait(false);
				if (r == null) return null;

				foreach (var ev in r) {
					result.AddOrUpdate(ev.Controller, new ConcurrentBag<EventX>() { ev }, (k, v) => { v.Add(ev); return v; });
				}
			}

			// Process all the controllers in parallel
			var dict = new ConcurrentDictionary<uint, List<Dictionary<T, double>>>();

			var slots = 1;

			result.AsParallel().ForAll(ctrl => {
				var xlist = ctrl.Value.OrderBy(ev => ev.Time).ThenBy(ev => ev.RowKey).ToList();
				var ctrldictlist = dict.GetOrAdd(ctrl.Key, x => new List<Dictionary<T, double>>());

				if (step == default(TimeSpan)) step = to - from;
				var lastdate = from;
				var laststate = default(T);
				var hasvalue = false;
				var slot = 0;

				for (var lower = from; lower < to; lower = incrementdate(lower, step), slot++) {
					var upper = incrementdate(lower, step);
					if (upper > to) upper = to;

					if (slots <= slot) slots = slot + 1;

					var ctrldict = new Dictionary<T, double>();
					ctrldictlist.Add(ctrldict);

					// Add a dummy record at the end to make sure we get the very last segment
					foreach (var entry in xlist.Concat(new[] { new EventX() })) {
						var newstate = grouping(entry);

						if (newstate == null) newstate = nullValue;

						if (entry.Time < lastdate) {
							// Value below the lower bound
							laststate = newstate;
							hasvalue = true;
							continue;
						} else if (!hasvalue) {
							// Value within range but there is still no state - try to estimate
							if (typeof(T) == typeof(bool) || newstate is bool)
								laststate = (T) Convert.ChangeType(!((bool) Convert.ChangeType(newstate, typeof(bool))), typeof(T));
							else
								laststate = nullValue;

							hasvalue = true;
						}

						// Value in range
						var interval = entry.Time - lastdate;

						if (entry.Time >= upper) {
							interval = upper - lastdate;
							lastdate = upper;
						} else {
							lastdate = entry.Time;
						}

						if (ctrldict.ContainsKey(laststate)) {
							ctrldict[laststate] += interval.TotalMilliseconds;
						} else {
							ctrldict[laststate] = interval.TotalMilliseconds;
						}

						if (entry.Time >= upper) {
							break;
						} else {
							laststate = newstate;
						}
					}
				}
			});

			return dict.ToDictionary(kv => kv.Key, kv => {
				var startdate = from;

				return kv.Value.Select((entry, x) => {
					var enddate = (step == TimeSpan.Zero || x >= slots - 1) ? to : incrementdate(startdate, step);
					var timerange = (enddate - startdate).TotalMilliseconds;

					var r = new TimeSplit<T>(entry.ToDictionary(t => t.Key, t => t.Value / timerange));

					r.StartTime = startdate;
					r.EndTime = enddate;

					startdate = incrementdate(startdate, step);

					return r;
				}).ToList() as IList<TimeSplit<T>>;
			});
		}

		public static IList<TimeSplit<T>> CalcEventsTimeSplitAggregate<T> (IReadOnlyDictionary<uint, IList<TimeSplit<T>>> dict)
		{
			// Calculate aggregate percentages

			return dict.Values
							.SelectMany(_ => _)
							.GroupBy(ctrldict => ctrldict.StartTime)
							.OrderBy(g => g.Key)
							.AsParallel()
							.Select(g => {
								var starttime = g.Key;
								var endtime = g.FirstOrDefault()?.EndTime ?? default(DateTimeOffset);
								var num = g.Count();

								var r = new TimeSplit<T>(
									g.SelectMany(t => t.Data)
										.GroupBy(kv => kv.Key, kv => kv.Value)
										.ToDictionary(x => x.Key, x => x.Sum(p => p) / num)
								);

								r.StartTime = starttime;
								r.EndTime = endtime;

								return r;
							}).OrderBy(x => x.StartTime)
							.ToList();
		}
	}
}