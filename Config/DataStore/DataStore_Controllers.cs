﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public static partial class DataStore
	{
		public static readonly Regex IPRegex = new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(\:\d{1-5})?", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

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
				if (orgId == null) {
					return await db.Controllers.AsNoTracking().ToListAsync().ConfigureAwait(false);
				} else {
					return await db.Controllers.AsNoTracking().Where(c => c.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)).ToListAsync().ConfigureAwait(false);
				}
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Controller> GetControllerAsync (int ID)
		{
			using (var db = new ConfigDB(m_Schema, m_Version)) {
				return await db.Controllers.AsNoTracking().FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task AddControllerAsync (int ID, string orgId, string name, ControllerTypes type, string version, string model, string IPAddress, bool enabled = true)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (type == ControllerTypes.Unknown) throw new ArgumentOutOfRangeException(nameof(type));
			if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
			if (string.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));
			if (string.IsNullOrWhiteSpace(IPAddress)) throw new ArgumentNullException(nameof(IPAddress));

			orgId = orgId.Trim();
			name = name.Trim();
			version = version.Trim();
			model = model.Trim();
			IPAddress = IPAddress.Trim();
			if (!IPRegex.IsMatch(IPAddress)) throw new ArgumentOutOfRangeException(nameof(IPAddress));

			var ctrl = new Controller()
			{
				ID = ID,
				OrgId = orgId,
				IsEnabled = enabled,
				Name = name,
				Type = type,
				Version = version,
				Model = model,
				IP = IPAddress
			};

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				var cx = await db.Controllers.AsNoTracking().FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
				if (cx != null) throw new ApplicationException("ID already exists: " + ID);

				db.Controllers.Add(ctrl);
				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task UpdateControllerAsync (int ID, bool enabled, string name, ControllerTypes type, string version, string model, string IPAddress)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (type == ControllerTypes.Unknown) throw new ArgumentOutOfRangeException(nameof(type));
			if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
			if (string.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));
			if (string.IsNullOrWhiteSpace(IPAddress)) throw new ArgumentNullException(nameof(IPAddress));

			name = name.Trim();
			version = version.Trim();
			model = model.Trim();
			IPAddress = IPAddress.Trim();
			if (!IPRegex.IsMatch(IPAddress)) throw new ArgumentOutOfRangeException(nameof(IPAddress));

			using (var db = new ConfigDB(m_Schema, m_Version)) {
				var ctrl = await db.Controllers.FirstOrDefaultAsync(c => c.ID == ID).ConfigureAwait(false);
				if (ctrl == null) throw new ArgumentOutOfRangeException(nameof(ID));

				ctrl.IsEnabled = enabled;
				ctrl.Name = name;
				ctrl.Type = type;
				ctrl.Version = version;
				ctrl.Model = model;
				ctrl.IP = IPAddress;
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