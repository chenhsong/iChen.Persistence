using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	public static partial class DataStore
	{
		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<Mold>> GetAllMoldsAsync ()
		{
			using (var db = new ConfigDB(m_Schema)) {
				return await db.Molds.AsNoTracking().ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<Mold>> GetAllMoldsAsync (int? controller)
		{
			if (controller.HasValue && controller.Value <= 0) throw new ArgumentOutOfRangeException(nameof(controller));

			using (var db = new ConfigDB(m_Schema)) {
				return await db.Molds.AsNoTracking().Where(x => x.ControllerId == controller).ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Mold> GetMoldAsync (int id)
		{
			using (var db = new ConfigDB(m_Schema)) {
				return await db.Molds.AsNoTracking().FirstOrDefaultAsync(m => m.ID == id).ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Mold> GetMoldByControllerAndNameAsync (int controller, string name)
		{
			if (controller <= 0) return null;
			if (string.IsNullOrWhiteSpace(name)) return null;

			using (var db = new ConfigDB(m_Schema)) {
				return await db.Molds.AsNoTracking().FirstOrDefaultAsync(x => x.ControllerId == controller && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<Mold>> SearchMoldsByControllerAndKeywordAsync (int controller, string keyword)
		{
			if (controller <= 0) return null;
			if (string.IsNullOrWhiteSpace(keyword)) return null;

			using (var db = new ConfigDB(m_Schema)) {
				return await db.Molds.AsNoTracking().Where(x => x.ControllerId == controller && x.Name.Contains(keyword)).ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<int> AddMoldAsync (string name, int? controller, bool enabled = true, IList<ushort> data = null)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (controller.HasValue && controller.Value <= 0) throw new ArgumentOutOfRangeException(nameof(controller));

			name = name.Trim();

			var mold = new Mold()
			{
				IsEnabled = enabled,
				Name = name,
				ControllerId = controller,
				Guid = Guid.NewGuid()
			};

			using (var db = new ConfigDB(m_Schema)) {
				var cx = await db.Molds.AsNoTracking().FirstOrDefaultAsync(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.ControllerId == controller).ConfigureAwait(false);
				if (cx != null) throw new ApplicationException($"Controller/Name already exists: {controller}/{name}");

				db.Molds.Add(mold);

				if (data != null) {
					for (var x = 0; x < data.Count; x++) {
						// Make sure the last item is always stored to keep the accurate length of the whole data set
						if (x >= data.Count - 1 || data[x] != 0) {
							db.MoldSettings.Add(new MoldSetting()
							{
								Mold = mold,
								Offset = (short) x,
								RawData = data[x]
							});
						}
					}
				}

				await db.SaveChangesAsync().ConfigureAwait(false);

				return mold.ID;
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<int> AddMoldAsync (string name, int? controller, bool enabled = true, IReadOnlyDictionary<ushort, ushort> data = null)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (controller.HasValue && controller.Value <= 0) throw new ArgumentOutOfRangeException(nameof(controller));

			name = name.Trim();

			var mold = new Mold()
			{
				IsEnabled = enabled,
				Name = name,
				ControllerId = controller,
				Guid = Guid.NewGuid()
			};

			using (var db = new ConfigDB(m_Schema)) {
				var cx = await db.Molds.AsNoTracking().FirstOrDefaultAsync(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.ControllerId == controller).ConfigureAwait(false);
				if (cx != null) throw new ApplicationException($"Controller/Name already exists: {controller}/{name}");

				db.Molds.Add(mold);

				if (data != null) {
					db.MoldSettings.AddRange(data.Select(s => new MoldSetting()
					{
						Mold = mold,
						Offset = (short) s.Key,
						RawData = s.Value
					}));
				}

				await db.SaveChangesAsync().ConfigureAwait(false);

				return mold.ID;
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task UpdateMoldAsync (int ID, bool enabled, string name, int? controller)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (controller.HasValue && controller.Value <= 0) throw new ArgumentOutOfRangeException(nameof(controller));

			name = name.Trim();

			using (var db = new ConfigDB(m_Schema)) {
				var mold = await db.Molds.FirstOrDefaultAsync(m => m.ID == ID).ConfigureAwait(false);
				if (mold == null) throw new ArgumentOutOfRangeException(nameof(ID));

				mold.IsEnabled = enabled;
				mold.Name = name;
				mold.ControllerId = controller;
				mold.Modified = DateTime.Now;

				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<Mold> DeleteMoldAsync (int ID)
		{
			using (var db = new ConfigDB(m_Schema)) {
				var mold = await db.Molds.FirstOrDefaultAsync(m => m.ID == ID).ConfigureAwait(false);
				if (mold == null) throw new ArgumentOutOfRangeException(nameof(ID));

				await db.Entry(mold).Collection(m => m.MoldSettings).LoadAsync().ConfigureAwait(false);

				db.MoldSettings.RemoveRange(mold.MoldSettings);
				db.Molds.Remove(mold);

				await db.SaveChangesAsync().ConfigureAwait(false);

				return mold;
			}
		}
	}
}