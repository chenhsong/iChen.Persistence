using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using iChen.Persistence.Server;
using Microsoft.EntityFrameworkCore;

namespace iChen.Analytics
{
	internal static class Utils
	{
		public static async Task<ICollection<Controller>> GetValidControllersAsync (ConfigDB db, string orgId, uint controllerId = 0)
		{
			Organization org = null;

			if (db.Version >= ConfigDB.Version_Organization) {
				org = await db.Organizations.SingleOrDefaultAsync(x => x.ID.Equals(orgId, StringComparison.OrdinalIgnoreCase));
				if (org == null) return null;   // Wrong Org
			}

			if (controllerId <= 0) {
				var ctrls = await db.Controllers.AsNoTracking()
														.Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.ToListAsync()
														.ConfigureAwait(false);

				foreach (var c in ctrls) { c.TimeZoneOffset = c.TimeZoneOffset ?? org?.TimeZoneOffset; }

				return ctrls;
			} else {
				var ctrl = await db.Controllers.AsNoTracking().SingleOrDefaultAsync(c => c.ID == controllerId).ConfigureAwait(false);
				if (ctrl == null) return null;    // No such controller

				if (!ctrl.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)) return null;    // Org of controller doesn't match

				ctrl.TimeZoneOffset = ctrl.TimeZoneOffset ?? org?.TimeZoneOffset;

				return new[] { ctrl };
			}
		}

		public static Func<DateTimeOffset, TimeSpan, DateTimeOffset> GetIncrementDateFunc (TimeSpan step, TimeSpan timezone)
		{
			if (step.TotalDays >= 0) return (d, s) => d + s;

			switch ((int) step.TotalDays) {
				case -7:
					return (d, s) => {
						// Starts from Sunday
						switch (d.DayOfWeek) {
							case DayOfWeek.Sunday: return d.AddDays(7);

							case DayOfWeek.Monday: return d.AddDays(6);
							case DayOfWeek.Tuesday: return d.AddDays(5);
							case DayOfWeek.Wednesday: return d.AddDays(4);
							case DayOfWeek.Thursday: return d.AddDays(3);
							case DayOfWeek.Friday: return d.AddDays(2);
							case DayOfWeek.Saturday: return d.AddDays(1);
						}
						return d.AddDays(7);
					};

				case -30:
					return (d, s) => {
						// Starts from first of month
						var d2 = d.ToOffset(timezone);
						d2 = new DateTimeOffset(d2.Year, d2.Month, 1, d2.Hour, d2.Minute, d2.Second, d2.Millisecond, timezone);
						return d2.AddMonths(1).ToOffset(d.Offset);
					};

				case -90:
					return (d, s) => {
						// Starts from first of quarter
						var d2 = d.ToOffset(timezone);
						d2 = new DateTimeOffset(d2.Year, d2.Month, 1, d2.Hour, d2.Minute, d2.Second, d2.Millisecond, timezone);
						switch (d2.Month % 3) {
							case 0: return d2.AddMonths(1).ToOffset(d.Offset);
							case 1: return d2.AddMonths(3).ToOffset(d.Offset);
							case 2: return d2.AddMonths(2).ToOffset(d.Offset);
						}
						return d2.AddMonths(3).ToOffset(d.Offset);
					};

				case -180:
					return (d, s) => {
						// Starts from first of half year
						var d2 = d.ToOffset(timezone);
						d2 = new DateTimeOffset(d2.Year, d2.Month, 1, d2.Hour, d2.Minute, d2.Second, d2.Millisecond, timezone);
						switch (d2.Month % 6) {
							case 0: return d2.AddMonths(1).ToOffset(d.Offset);
							case 1: return d2.AddMonths(6).ToOffset(d.Offset);
							case 2: return d2.AddMonths(5).ToOffset(d.Offset);
							case 3: return d2.AddMonths(4).ToOffset(d.Offset);
							case 4: return d2.AddMonths(3).ToOffset(d.Offset);
							case 5: return d2.AddMonths(2).ToOffset(d.Offset);
						}
						return d2.AddMonths(6).ToOffset(d.Offset);
					};
			}

			throw new ArgumentOutOfRangeException(nameof(step));
		}
	}
}