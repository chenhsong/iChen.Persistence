using System;
using System.ComponentModel;
using iChen.OpenProtocol;

namespace iChen.Persistence.Server
{
	public partial class User
	{
		public int ID { get; set; }
		[DefaultValue(DataStore.DefaultOrgId)]
		public string OrgId { get; set; } = DataStore.DefaultOrgId;
		public string Password { get; set; }
		public string Name { get; set; }
		public bool IsEnabled { get; set; } = true;
		public Filters Filters { get; set; } = Filters.None;
		public byte AccessLevel { get; set; } = 0;
		public DateTime Created { get; set; } = DateTime.Now;
		public DateTime? Modified { get; set; }
	}
}
