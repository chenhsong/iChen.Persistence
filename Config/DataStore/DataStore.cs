using System;

namespace iChen.Persistence.Server
{
	public static partial class DataStore
	{
		public const string DefaultOrgId = "default";

		private static string m_Schema = null;
		private static ushort m_Version = 1;

		public static void SetSchema (string schema)
		{
			if (schema != null && string.IsNullOrWhiteSpace(schema)) throw new ArgumentNullException(nameof(schema));
			m_Schema = schema?.Trim();
		}

		public static void SetVersion (ushort version) => m_Version = version;
	}
}