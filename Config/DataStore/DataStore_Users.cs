using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using iChen.OpenProtocol;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public static partial class DataStore
	{
		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<User>> GetAllUsersAsync (string orgId)
		{
			if (orgId == null && string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));

			using (var db = new ConfigDB(m_Schema)) {
				if (orgId == null) {
					return await db.Users.AsNoTracking().ToListAsync().ConfigureAwait(false);
				} else {
					return await db.Users.AsNoTracking().Where(user => user.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase)).ToListAsync().ConfigureAwait(false);
				}
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<User> GetUserAsync (string orgId, string password)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));

			password = password.Trim();

			using (var db = new ConfigDB(m_Schema)) {
				return await db.Users.AsNoTracking()
													.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
													.FirstOrDefaultAsync(x => x.Password.Equals(password, StringComparison.OrdinalIgnoreCase))
													.ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task AddUserAsync (string orgId, string password, string name, Filters filters, byte level, bool enabled)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

			orgId = orgId.Trim();
			password = password.Trim();
			name = name.Trim();

			var user = new User()
			{
				OrgId = orgId,
				Password = password,
				IsEnabled = enabled,
				Name = name,
				Filters = filters,
				AccessLevel = level
			};

			using (var db = new ConfigDB(m_Schema)) {
				var ux = await db.Users.AsNoTracking()
														.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.FirstOrDefaultAsync(x => x.Password.Equals(password, StringComparison.OrdinalIgnoreCase))
														.ConfigureAwait(false);
				if (ux != null) throw new ApplicationException($"Password already exists in org {orgId}: {password}");

				ux = await db.Users.AsNoTracking()
												.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
												.FirstOrDefaultAsync(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
												.ConfigureAwait(false);
				if (ux != null) throw new ApplicationException($"User name already exists in org {orgId}: {name}");

				db.Users.Add(user);
				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task UpdateUserAsync (string orgId, string password, bool enabled, string name, Filters filters, byte level)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

			orgId = orgId.Trim();
			password = password.Trim();
			name = name.Trim();

			using (var db = new ConfigDB(m_Schema)) {
				var user = await db.Users
															.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
															.FirstOrDefaultAsync(x => x.Password.Equals(password, StringComparison.OrdinalIgnoreCase))
															.ConfigureAwait(false);
				if (user == null) throw new ArgumentOutOfRangeException(nameof(password));

				var ux = await db.Users.AsNoTracking()
														.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
														.FirstOrDefaultAsync(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
														.ConfigureAwait(false);
				if (ux != null) throw new ApplicationException($"User name already exists in org {orgId}: {name}");

				user.Name = name;
				user.IsEnabled = enabled;
				user.Filters = filters;
				user.AccessLevel = level;
				user.Modified = DateTime.Now;

				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<User> DeleteUserAsync (string orgId, string password)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));

			orgId = orgId.Trim();
			password = password.Trim();

			using (var db = new ConfigDB(m_Schema)) {
				var user = await db.Users
															.Where(x => x.OrgId.Equals(orgId, StringComparison.OrdinalIgnoreCase))
															.FirstOrDefaultAsync(x => x.Password.Equals(password, StringComparison.OrdinalIgnoreCase))
															.ConfigureAwait(false);
				if (user == null) throw new ArgumentOutOfRangeException(nameof(password));

				db.Users.Remove(user);
				await db.SaveChangesAsync().ConfigureAwait(false);

				return user;
			}
		}
	}
}