using System;
using System.ComponentModel;

namespace iChen.Persistence.Server
{
	public partial class TerminalConfig
	{
		[DefaultValue(DataStore.DefaultOrgId)]
		public string OrgId { get; set; } = DataStore.DefaultOrgId;
		public string Text { get; set; }
		public DateTime Created { get; set; } = DateTime.Now;
		public DateTime? Modified { get; set; }
	}
}
