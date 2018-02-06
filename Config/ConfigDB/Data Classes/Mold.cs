using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace iChen.Persistence.Server
{
	public partial class Mold
	{
		public int ID { get; set; }
		public string Name { get; set; }
		public int? ControllerId { get; set; }
		public bool IsEnabled { get; set; } = true;
		public DateTime Created { get; set; } = DateTime.Now;
		public DateTime? Modified { get; set; }
		public Guid Guid { get; set; }

		[JsonIgnore]
		public virtual ICollection<MoldSetting> MoldSettings { get; set; } = new HashSet<MoldSetting>();

		[JsonIgnore]
		public virtual Controller Controller { get; set; }
	}
}
