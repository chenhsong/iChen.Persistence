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
		public static async Task<ushort[]> GetMoldSettingsAsync (int moldId)
		{
			IList<MoldSetting> data;

			using (var db = new ConfigDB(m_Schema)) {
				data = await db.MoldSettings.AsNoTracking().Where(s => s.MoldId == moldId).OrderBy(s => s.Offset).ToListAsync().ConfigureAwait(false);
			}

			var list = new ushort[(data.Count <= 0) ? 0 : data[data.Count - 1].Offset + 1];
			foreach (var s in data) { list[s.Offset] = s.RawData; }
			return list;
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ICollection<MoldSetting>> GetMoldSettingsDataAsync (int moldId)
		{
			using (var db = new ConfigDB(m_Schema)) {
				return await db.MoldSettings.AsNoTracking().Where(s => s.MoldId == moldId).OrderBy(s => s.Offset).ToListAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<ushort> GetMoldSettingValueAsync (int moldId, int offset)
		{
			using (var db = new ConfigDB(m_Schema)) {
				var setting = await db.MoldSettings.AsNoTracking().FirstOrDefaultAsync(ms => ms.MoldId == moldId && ms.Offset == offset).ConfigureAwait(false);
				return setting?.RawData ?? 0;
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task<MoldSetting> GetMoldSettingAsync (int moldId, int offset)
		{
			using (var db = new ConfigDB(m_Schema)) {
				return await db.MoldSettings.AsNoTracking().FirstOrDefaultAsync(ms => ms.MoldId == moldId && ms.Offset == offset).ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task AddMoldSettingsAsync (int moldId, IList<ulong> data)
		{
			var dict = new Dictionary<ushort, ulong>();
			for (var x = 0; x < data.Count; x++) {
				(var value, _) = UnpackMoldSettingValue(data[x]);
				// Skip zeros, but make sure the last item is always stored to keep the accurate length of the whole data set
				if (x >= data.Count - 1 || value != 0) dict[(ushort) x] = data[x];
			}
			await AddMoldSettingsAsync(moldId, dict).ConfigureAwait(false);
		}


		/// <remarks>This method is thread-safe.</remarks>
		public static async Task AddMoldSettingsAsync (int moldId, IReadOnlyDictionary<ushort, ulong> data)
		{
			if (moldId <= 0) throw new ArgumentOutOfRangeException(nameof(moldId));
			if (data == null) throw new ArgumentNullException(nameof(data));

			using (var db = new ConfigDB(m_Schema)) {
				var existing = await db.MoldSettings.AsNoTracking().Where(mx => mx.MoldId == moldId).ToListAsync().ConfigureAwait(false);

				if (existing.Any(s => data.ContainsKey((ushort) s.Offset)))
					throw new ApplicationException($"Some MoldId/Offset already exist.");

				int maxkey = (data.Count > 0) ? data.Keys.Max() : 0;

				foreach (var kv in data) {
					(var value, var variable) = UnpackMoldSettingValue(kv.Value);

					// Skip zeros but always keep the last item
					if (value == 0 && kv.Key != maxkey) continue;

					db.MoldSettings.Add(new MoldSetting()
					{
						MoldId = moldId,
						Offset = (short) kv.Key,
						RawData = value,
						Variable = variable
					});
				}

				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task ReplaceMoldSettingsAsync (int moldId, IList<ulong> data)
		{
			var dict = new Dictionary<ushort, ulong>();
			for (var x = 0; x < data.Count; x++) {
				(var value, _) = UnpackMoldSettingValue(data[x]);
				// Skip zeros, but make sure the last item is always stored to keep the accurate length of the whole data set
				if (x >= data.Count - 1 || value != 0) dict[(ushort) x] = data[x];
			}
			await ReplaceMoldSettingsAsync(moldId, dict).ConfigureAwait(false);
		}

		/// <remarks>This method is thread-safe.</remarks>
		public static async Task ReplaceMoldSettingsAsync (int moldId, IReadOnlyDictionary<ushort, ulong> data)
		{
			if (moldId <= 0) throw new ArgumentOutOfRangeException(nameof(moldId));
			if (data == null) throw new ArgumentNullException(nameof(data));

			using (var db = new ConfigDB(m_Schema)) {
				var existing = await db.MoldSettings.Where(mx => mx.MoldId == moldId).ToListAsync().ConfigureAwait(false);
				db.MoldSettings.RemoveRange(existing);
				await db.SaveChangesAsync().ConfigureAwait(false);

				foreach (var kv in data) {
					(var value, var variable) = UnpackMoldSettingValue(kv.Value);

					db.MoldSettings.Add(new MoldSetting()
					{
						MoldId = moldId,
						Offset = (short) kv.Key,
						RawData = value,
						Variable = variable
					});
				}

				await db.SaveChangesAsync().ConfigureAwait(false);
			}
		}
	}
}