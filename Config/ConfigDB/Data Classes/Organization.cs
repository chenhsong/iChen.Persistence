using System;

namespace iChen.Persistence.Server
{
	public partial class Organization
	{
		public string ID { get; set; }
		public string Name { get; set; }
		public float? TimeZoneOffset { get; set; }
		public bool RestrictMoldsToJobCards { get; set; } = false;
		public DateTime Created { get; set; } = DateTime.Now;
		public DateTime? Modified { get; set; }
	}
}