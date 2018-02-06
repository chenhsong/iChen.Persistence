using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace iChen.Persistence.Server
{
	/// <remarks>This type is thread-safe.</remarks>
	public class TextMapCache
	{
		public const int NumRetries = 3;
		public const int RetryWait = 100;

		private readonly ConcurrentDictionary<int, string> m_IdToText;
		private readonly ConcurrentDictionary<string, int> m_TextToId;

		public TextMapCache ()
		{
			using (var db = new ConfigDB()) {
				m_IdToText = new ConcurrentDictionary<int, string>(db.TextMaps.AsNoTracking().AsEnumerable().Select(tm => new KeyValuePair<int, string>(tm.ID, tm.Text)));
			}

			m_TextToId = new ConcurrentDictionary<string, int>(m_IdToText.Select(kv => new KeyValuePair<string, int>(kv.Value, kv.Key)), StringComparer.OrdinalIgnoreCase);
		}

		public async Task<string> GetTextAsync (int id)
		{
			string text;
			var retries = NumRetries;

			// Retry the operation a few times while waiting a short interval in between
			// This is to prevent concurrency problems when a record is being written and the two indices are out-of-sync

			do {
				if (m_IdToText.TryGetValue(id, out text)) return text;
				await Task.Delay(RetryWait);
			} while (--retries > 0);

			return null;
		}

		public string this[int id] { get { return GetTextAsync(id).Result; } }

		public async Task<int> GetTextIdAsync (string text)
		{
			if (string.IsNullOrWhiteSpace(text)) throw new ArgumentNullException(nameof(text));

			if (m_TextToId.TryGetValue(text, out var id)) return id;

			// Not found in cache, add it

			using (var db = new ConfigDB()) {
				var map = await db.TextMaps.SingleOrDefaultAsync(tm => tm.Text.Equals(text, StringComparison.OrdinalIgnoreCase));

				if (map == null) {
					try {
						db.TextMaps.Add(new TextMap() { Text = text });
						await db.SaveChangesAsync();
					} catch (DbUpdateException) {
						// Probably already added by some other process
					}
				}

				map = db.TextMaps.Single(tm => tm.Text.Equals(text, StringComparison.OrdinalIgnoreCase));

				m_TextToId.AddOrUpdate(map.Text, map.ID, (x, v) => v);
				m_IdToText.AddOrUpdate(map.ID, map.Text, (x, v) => v);

				return map.ID;
			}
		}
	}
}