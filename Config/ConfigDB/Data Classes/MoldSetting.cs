using System;
using Newtonsoft.Json;

namespace iChen.Persistence.Server
{
	public partial class MoldSetting
	{
		public int MoldId { get; set; }
		public short Offset { get; set; }
		public short Value { get; set; }
		public DateTime Created { get; set; } = DateTime.Now;
		public DateTime? Modified { get; set; }

		[JsonIgnore]
		public ushort RawData
		{
			get { return (ushort) this.Value; }
			set { this.Value = (short) value; }
		}

		[JsonIgnore]
		public virtual Mold Mold { get; set; }
	}
}
