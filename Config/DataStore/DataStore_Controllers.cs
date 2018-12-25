using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public static partial class DataStore
	{
		private static readonly Regex IPRegex = new Regex(@"^(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(\:(?<port>\d{1,5}))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.Singleline);
		private static readonly Regex TtyRegex = new Regex(@"tty\w+", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
		private static readonly Regex SerialPortRegex = new Regex(@"COM(?<port>\d+)", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

		public static async Task<Organization> GetOrgAsync (string orgId)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));

			if (m_Version < ConfigDB.Version_Organization) return null;

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				return await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.ID.Equals(orgId, StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
			}
		}

		public static async Task<ICollection<Organization>> GetAllOrgsAsync ()
		{
			if (m_Version < ConfigDB.Version_Organization) return new Organization[0];

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				return await db.Organizations.AsNoTracking().ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<Controller>> GetAllControllersAsync (string orgId)
		{
			if (orgId != null && string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				return (orgId == null)
					? await db.Controllers.AsNoTracking().ToListAsync().ConfigureAwait(false)
					: await db.Controllers.AsNoTracking().Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)).ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Controller> GetControllerAsync (int ID)
		{
			using (var db = new ConfigDB(m_Schema, m_Version)) {
				return await db.Controllers.AsNoTracking().FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
			}
		}

		private static string ProcessIPAddress (string ip)
		{
			if (string.IsNullOrWhiteSpace(ip)) return null;

			ip = ip.Trim();

			var match = IPRegex.Match(ip);

			if (match.Success) {
				if (!System.Net.IPAddress.TryParse(match.Groups["ip"].Value, out var addr)) return null;
				return addr.ToString() + (match.Groups["port"].Success ? ":" + match.Groups["port"].Value : null);
			} else {
				match = SerialPortRegex.Match(ip);
				if (match.Success) {
					return "COM" + match.Groups["port"].Value;
				} else {
					match = TtyRegex.Match(ip);
					if (!match.Success) return null;
					return ip;
				}
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task AddControllerAsync (int ID, string orgId, string name, int type, string version, string model, string IPAddress, bool enabled = true, double? geo_latitude = null, double? geo_longitude = null)
		{
			var ctrl = new Controller()
			{
				ID = ID,
				OrgId = !string.IsNullOrWhiteSpace(orgId) ? orgId.Trim() : throw new ArgumentNullException(nameof(orgId)),
				IsEnabled = enabled,
				Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : throw new ArgumentNullException(nameof(name)),
				Type = (type > 0) ? type : throw new ArgumentOutOfRangeException(nameof(type)),
				Version = !string.IsNullOrWhiteSpace(version) ? version.Trim() : throw new ArgumentNullException(nameof(version)),
				Model = !string.IsNullOrWhiteSpace(model) ? model.Trim() : throw new ArgumentNullException(nameof(model)),
				IP = ProcessIPAddress(IPAddress) ?? throw new ArgumentOutOfRangeException(nameof(IPAddress)),
				GeoLatitude = geo_latitude,
				GeoLongitude = geo_longitude
			};

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				var cx = await db.Controllers.AsNoTracking().FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
				if (cx != null) throw new ApplicationException("ID already exists: " + ID);

				db.Controllers.Add(ctrl);
				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task UpdateControllerAsync (int ID, bool enabled, string name, int type, string version, string model, string IPAddress, double? geo_latitude, double? geo_longitude)
		{
			using (var db = new ConfigDB(m_Schema, m_Version)) {
				var ctrl = await db.Controllers.FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
				if (ctrl == null) throw new ArgumentOutOfRangeException(nameof(ID));

				ctrl.IsEnabled = enabled;
				ctrl.Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : throw new ArgumentNullException(nameof(name));
				ctrl.Type = (type >= 0) ? type : throw new ArgumentOutOfRangeException(nameof(type));
				ctrl.Version = !string.IsNullOrWhiteSpace(version) ? version.Trim() : throw new ArgumentNullException(nameof(version));
				ctrl.Model = !string.IsNullOrWhiteSpace(model) ? model.Trim() : throw new ArgumentNullException(nameof(model));
				ctrl.IP = ProcessIPAddress(IPAddress) ?? throw new ArgumentOutOfRangeException(nameof(IPAddress));
				ctrl.GeoLatitude = geo_latitude;
				ctrl.GeoLongitude = geo_longitude;
				ctrl.Modified = DateTime.Now;

				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Controller> DeleteControllerAsync (int ID)
		{
			using (var db = new ConfigDB(m_Schema, m_Version)) {
				var ctrl = await db.Controllers.FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
				if (ctrl == null) throw new ArgumentOutOfRangeException(nameof(ID));

				db.Controllers.Remove(ctrl);
				await db.SaveChangesAsync().ConfigureAwait(false);

				return ctrl;
			}
		}
	}
}