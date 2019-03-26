using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using iChen.OpenProtocol;
using iChen.Persistence.Server;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace iChen.Persistence.Cloud
{
	public static class Storage
	{
		public const int DoubleValueMultiplier = 100;
		public const string RowKeyPrefixFormat = "ddHHmm-";

		public const string CycleDataTable = "CycleData";
		public const string CycleDataValuesTable = "CycleDataValues";
		public const string MoldDataTable = "MoldData";
		public const string AlarmsTable = "Alarms";
		public const string AuditTrailTable = "AuditTrail";
		public const string EventsTable = "Events";
		public const string LinksTable = "Links";

		public const string Key = "k";
		public const string OldValue = "x";
		public const string Value = "v";
		public const string DoubleValue = "V";
		public const string Time = "t";
		public const string OpMode = "o";
		public const string JobMode = "j";
		public const string JobCard = "c";
		public const string Mold = "m";
		public const string Operator = "u";

		public const string LinkMarker = "*";

		public static string MakePartitionKey (string orgId, uint controller) =>
			MakePartitionKey(orgId, controller, DateTimeOffset.UtcNow);

		public static string MakePartitionKey (string orgId, uint controller, DateTimeOffset date)
		{
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));

			if (orgId.Equals(DataStore.DefaultOrgId, StringComparison.OrdinalIgnoreCase)) {
				if (controller <= 0) return date.ToUniversalTime().ToString("yyMM");
				return $"{date.ToUniversalTime().ToString("yyMM")}-{controller}";
			} else {
				if (controller <= 0) return $"{orgId}-{date.ToUniversalTime().ToString("yyMM")}";
				return $"{orgId}-{date.ToUniversalTime().ToString("yyMM")}-{controller}";
			}
		}

		public static uint GetControllerFromPartitionKey (string key)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (key.Length < 6) return 0;

			if (char.IsDigit(key[0])) return uint.TryParse(key.Substring(5), out var r1) ? r1 : 0;

			var n = key.IndexOf('-');
			if (n <= 0) throw new ArgumentOutOfRangeException(nameof(key));

			return uint.TryParse(key.Substring(n + 1 + 5), out var r2) ? r2 : 0;
		}

		public static string GetOrgIdFromPartitionKey (string key)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			if (key.Length < 8) return null;

			if (char.IsDigit(key[0])) return null;

			var n = key.IndexOf('-');
			if (n <= 0) throw new ArgumentOutOfRangeException(nameof(key));

			return key.Substring(0, n);
		}

		public static string MakeRowKey (string rowkey, uint controller, DateTimeOffset date)
		{
			if (string.IsNullOrWhiteSpace(rowkey)) throw new ArgumentNullException(nameof(rowkey));
			return date.ToUniversalTime().ToString(RowKeyPrefixFormat) + rowkey;
		}
	}

	public class Link : TableEntity
	{
		public string T { get; set; }
		public string P { get; set; }
		public string R { get; set; }

		public Link (string table, string uniqueId, string partitionkey, string rowkey)
		{
			if (string.IsNullOrWhiteSpace(uniqueId)) throw new ArgumentNullException(nameof(uniqueId));
			uniqueId = uniqueId.Trim();

			this.T = !string.IsNullOrWhiteSpace(table) ? table.Trim() : throw new ArgumentNullException(nameof(table));
			this.P = !string.IsNullOrWhiteSpace(partitionkey) ? partitionkey.Trim() : throw new ArgumentNullException(nameof(partitionkey));
			this.R = !string.IsNullOrWhiteSpace(rowkey) ? rowkey.Trim() : throw new ArgumentNullException(nameof(rowkey));

			if (this.R.EndsWith(uniqueId)) {
				// If prefix, then just leave the prefix
				this.R = this.R.Substring(0, this.R.Length - uniqueId.Length);
			} else if (this.R.Contains(uniqueId)) {
				// Otherwise 
				this.R = this.R.Replace(uniqueId, Storage.LinkMarker);
			}

			this.PartitionKey = "x";
			this.RowKey = uniqueId;
		}

		public string RedirectedPartition => this.P;

		public string RedirectedRowKey => this.R.Contains(Storage.LinkMarker)
										? this.R.Replace(Storage.LinkMarker, this.RowKey)
										: this.R + this.RowKey;
	}

	public abstract class EntryBase
	{
		[JsonIgnore]
		public static string ClassInsertStatement { get; }

		[JsonIgnore]
		public string ID { get; set; }

		[JsonIgnore]
		public string OrgId { get; }

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include, Order = -999)]
		public uint Controller { get; }

		[JsonProperty(Order = -998)]
		public DateTimeOffset Time { get; } = DateTimeOffset.UtcNow;

		[JsonIgnore]
		public int Sequence { get; }

		[JsonIgnore]
		public virtual bool UseBatches => true;

		static EntryBase ()
		{
			ClassInsertStatement = "(OrgId, Controller, Time) VALUES (?, ?, ?)";
		}

		public EntryBase (string uniqueId, string orgId, uint controller, DateTimeOffset time)
		{
			if (uniqueId != null && string.IsNullOrWhiteSpace(uniqueId)) throw new ArgumentNullException(nameof(uniqueId));
			if (orgId == null) orgId = DataStore.DefaultOrgId;
			if (string.IsNullOrWhiteSpace(orgId)) throw new ArgumentNullException(nameof(orgId));
			if (controller < 0) throw new ArgumentOutOfRangeException(nameof(controller));

			this.ID = string.IsNullOrWhiteSpace(uniqueId) ? null : uniqueId.Trim();
			this.OrgId = orgId;
			this.Controller = controller;
			this.Time = time;
		}

		public EntryBase (DynamicTableEntity entity)
		{
			if (entity == null) throw new ArgumentNullException(nameof(entity));

			this.OrgId = Storage.GetOrgIdFromPartitionKey(entity.PartitionKey);
			this.Controller = Storage.GetControllerFromPartitionKey(entity.PartitionKey);
			this.Time = entity.Properties[Storage.Time].DateTimeOffsetValue.Value;
			this.PartitionKey = entity.PartitionKey;
			this.RowKey = entity.RowKey;
		}

		public EntryBase (DataRow drow)
		{
			if (drow == null) throw new ArgumentNullException(nameof(drow));

			this.OrgId = drow["OrgId"].ToString();
			this.Controller = (uint) (int) drow["Controller"];
			//this.Time = DateTime.SpecifyKind((DateTime) drow["Time"], DateTimeKind.Utc);
			this.Time = DateTimeOffset.Parse(drow["Time"].ToString().Replace(" ", "T") + "Z");
			this.PartitionKey = this.GeneratedPartitionKey;

			// Use the sequence number as a unique row key
			var seq = (int) drow["ID"];
			this.Sequence = seq;

			seq += 1000000000;    // Make sure enough there are always the same number of digits in the key

			this.RowKey = seq.ToString();
		}

		protected static bool IsColumnAvailable (DataRow drow, string column)
		{
			if (drow == null) return false;
			if (string.IsNullOrWhiteSpace(column)) return false;
			try {
				return !drow.IsNull(column);
			} catch (ArgumentException) {
				return false;
			}
		}

		[JsonIgnore]
		public virtual string GeneratedPartitionKey { get { return Storage.MakePartitionKey(this.OrgId, this.Controller, this.Time); } }

		[JsonIgnore]
		public virtual string PartitionKey { get; private set; }

		[JsonIgnore]
		public virtual string RowKey { get; private set; }

		internal virtual DynamicTableEntity ToEntity (string rowkey)
		{
			var entity = new DynamicTableEntity(GeneratedPartitionKey, Storage.MakeRowKey(rowkey, this.Controller, this.Time));
			entity.Properties[Storage.Time] = new EntityProperty(this.Time);
			return entity;
		}

		[JsonIgnore]
		internal virtual string InsertStatement { get { return ClassInsertStatement; } }

		internal virtual void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			parameters.Add(makeParam("OrgId", DbType.String, 100, OrgId));
			parameters.Add(makeParam("Controller", DbType.Int32, 0, (int) Controller));
			parameters.Add(makeParam("Time", DbType.DateTime, 2, Time.ToUniversalTime().UtcDateTime));
		}
	}

	public abstract class KeyEntryBase : EntryBase
	{
		[JsonIgnore]
		public new static string ClassInsertStatement { get; }

		[JsonProperty(Order = -899)]
		public string Key { get; }

		protected abstract string DatabaseKeyName { get; }

		static KeyEntryBase ()
		{
			ClassInsertStatement = EntryBase.ClassInsertStatement.Replace(") VALUES (", ", <KeyName>, <ValueName>) VALUES (");
			ClassInsertStatement = ClassInsertStatement.Substring(0, ClassInsertStatement.Length - 1) + ", ?, ?)";
		}

		public KeyEntryBase (string uniqueId, string orgId, uint controller, string key, DateTimeOffset time)
			: base(uniqueId, orgId, controller, time)
		{
			if (controller <= 0) throw new ArgumentOutOfRangeException(nameof(controller));
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
			this.Key = key;
		}

		public KeyEntryBase (DynamicTableEntity entity) : base(entity)
		{
			this.Key = entity.Properties[Storage.Key].StringValue;
		}

		public KeyEntryBase (DataRow drow) : base(drow)
		{
			if (IsColumnAvailable(drow, DatabaseKeyName)) this.Key = drow[DatabaseKeyName].ToString();
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			var entity = base.ToEntity(rowkey);
			entity.Properties[Storage.Key] = new EntityProperty(this.Key);
			return entity;
		}

		[JsonIgnore]
		internal override string InsertStatement { get { return ClassInsertStatement; } }

		internal override void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			base.AddSqlParameters(parameters, makeParam);
			parameters.Add(makeParam("Key", DbType.AnsiString, 50, Key));
		}
	}

	public class Alarm : KeyEntryBase
	{
		[JsonIgnore]
		public new static string ClassInsertStatement { get; }

		public const string DatabaseValueField = "AlarmState";
		public const string DatabaseKeyField = "AlarmName";

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include, Order = 1)]
		public bool State { get; }

		protected override string DatabaseKeyName { get { return DatabaseKeyField; } }

		static Alarm ()
		{
			ClassInsertStatement = KeyEntryBase.ClassInsertStatement.Replace("<KeyName>", DatabaseKeyField).Replace("<ValueName>", DatabaseValueField);
		}

		public Alarm (string uniqueId, string orgId, uint controller, string key, bool state, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, key, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			this.State = state;
		}

		public Alarm (DynamicTableEntity entity) : base(entity)
		{
			this.State = entity.Properties[Storage.Value].BooleanValue.Value;
		}

		public Alarm (DataRow drow) : base(drow)
		{
			if (IsColumnAvailable(drow, DatabaseValueField)) this.State = (bool) drow[DatabaseValueField];
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			var entity = base.ToEntity(rowkey);
			entity.Properties[Storage.Value] = new EntityProperty(this.State);
			return entity;
		}

		[JsonIgnore]
		internal override string InsertStatement { get { return ClassInsertStatement; } }

		internal override void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			base.AddSqlParameters(parameters, makeParam);
			parameters.Add(makeParam("Value", DbType.Boolean, 1, State));
		}
	}

	public class CycleData : EntryBase//, IEnumerable, IEnumerable<KeyValuePair<string, double>>
	{
		[JsonIgnore]
		public new static string ClassInsertStatement { get; }

		[JsonProperty(Order = -990)]
		public int OperatorId { get; } = 0;

		[DefaultValue(OpModes.Unknown)]
		public OpModes OpMode { get; } = OpModes.Unknown;

		[DefaultValue(JobModes.Unknown)]
		public JobModes JobMode { get; } = JobModes.Unknown;

		public string JobCardId { get; }
		public string MoldId { get; }
		public IReadOnlyDictionary<string, double> Data { get { return m_Data; } }

		private readonly Dictionary<string, double> m_Data = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

		static CycleData ()
		{
			ClassInsertStatement = EntryBase.ClassInsertStatement.Replace(") VALUES (", ", Operator, OpMode, JobMode, JobCard, Mold, UniqueID) VALUES (");
			ClassInsertStatement = ClassInsertStatement.Substring(0, ClassInsertStatement.Length - 1) + ", ?, ?, ?, ?, ?, ?)";
		}

		public CycleData (string uniqueId, string orgId, uint controller, OpModes opmode, JobModes jobmode, int user, string jobcard, string mold, IReadOnlyDictionary<string, double> data = null, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			if (controller <= 0) throw new ArgumentOutOfRangeException(nameof(controller));
			if (mold != null && string.IsNullOrWhiteSpace(mold)) throw new ArgumentNullException(nameof(mold));
			if (jobcard != null && string.IsNullOrWhiteSpace(jobcard)) throw new ArgumentNullException(nameof(jobcard));

			OpMode = opmode;
			JobMode = jobmode;
			OperatorId = user;
			JobCardId = jobcard?.Trim();
			MoldId = mold?.Trim();

			if (data != null) {
				foreach (var kv in data.Where(kv => kv.Key != Storage.Time)) m_Data[kv.Key.Trim().ToUpperInvariant()] = kv.Value;
			}
		}

		public CycleData (DynamicTableEntity entity) : base(entity)
		{
			var type = entity.RowKey[Storage.RowKeyPrefixFormat.Length];

			if (entity.Properties.TryGetValue(Storage.OpMode, out var prop) && prop.StringValue != null) OpMode = (OpModes) Enum.Parse(typeof(OpModes), prop.StringValue, true);
			if (entity.Properties.TryGetValue(Storage.JobMode, out prop) && prop.StringValue != null) JobMode = (JobModes) Enum.Parse(typeof(JobModes), prop.StringValue, true);
			if (entity.Properties.TryGetValue(Storage.Operator, out prop)) OperatorId = prop.Int32Value.Value;
			if (entity.Properties.TryGetValue(Storage.JobCard, out prop)) JobCardId = prop.StringValue;
			if (entity.Properties.TryGetValue(Storage.Mold, out prop)) MoldId = prop.StringValue;

			foreach (var kv in entity.Properties) {
				var val = 0.0;

				// Map 0/1 --> boolean to save storage
				if (kv.Value.PropertyType == EdmType.Boolean) {
					val = kv.Value.BooleanValue.Value ? 1.0 : 0;
				} else if (kv.Value.PropertyType == EdmType.Int32) {
					val = kv.Value.Int32Value.Value / ((double) Storage.DoubleValueMultiplier);
				} else {
					continue;
				}

				var key = kv.Key.Trim().ToUpperInvariant();

				switch (type) {
					case 'Z': key = "Z_QD" + key; break;
				}

				m_Data[key] = val;
			}
		}

		public CycleData (DataRow drow, IReadOnlyDictionary<string, double> data = null) : base(drow)
		{
			if (IsColumnAvailable(drow, "Operator")) this.OperatorId = (int) drow["Operator"];
			if (IsColumnAvailable(drow, "OpMode")) this.OpMode = (OpModes) (byte) drow["OpMode"];
			if (IsColumnAvailable(drow, "JobMode")) this.JobMode = (JobModes) (byte) drow["JobMode"];
			if (IsColumnAvailable(drow, "JobCard")) this.JobCardId = drow["JobCard"].ToString();
			if (IsColumnAvailable(drow, "Mold")) this.MoldId = drow["Mold"].ToString();
			if (IsColumnAvailable(drow, "UniqueID")) this.ID = drow["UniqueID"].ToString();

			if (data != null) {
				foreach (var kv in data) m_Data[kv.Key.Trim().ToUpperInvariant()] = kv.Value;
			}
		}

		[JsonIgnore]
		public ICollection<string> Keys => m_Data.Keys;

		[JsonIgnore]
		public ICollection<double> Values => m_Data.Values;

		[JsonIgnore]
		public int Count => m_Data.Count;

		public bool ContainsKey (string key) => m_Data.ContainsKey(key);

		public bool TryGetValue (string key, out double value) => m_Data.TryGetValue(key, out value);

		public IEnumerator<KeyValuePair<string, double>> GetEnumerator () => m_Data.GetEnumerator();

		//IEnumerator IEnumerable.GetEnumerator ()
		//{
		//	return (m_Data as IEnumerable).GetEnumerator();
		//}

		public double this[string key]
		{
			get {
				if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

				return m_Data[key];
			}
			set {
				if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(value));

				m_Data[key.Trim().ToUpperInvariant()] = value;
			}
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			char type = '_';
			if (m_Data.Keys.All(key => key.Trim().ToUpperInvariant().StartsWith("Z_QD"))) type = 'Z';
			rowkey = type + rowkey;

			var entity = base.ToEntity(rowkey);

			if (OpMode != OpModes.Unknown) entity.Properties[Storage.OpMode] = new EntityProperty(this.OpMode.ToString());
			if (JobMode != JobModes.Unknown) entity.Properties[Storage.JobMode] = new EntityProperty(this.JobMode.ToString());
			if (OperatorId > 0) entity.Properties[Storage.Operator] = new EntityProperty(this.OperatorId);
			if (JobCardId != null) entity.Properties[Storage.JobCard] = new EntityProperty(this.JobCardId);
			if (MoldId != null) entity.Properties[Storage.Mold] = new EntityProperty(this.MoldId);

			foreach (var kv in m_Data) {
				EntityProperty val;

				// Map 0/1 --> boolean to save storage
				if (kv.Value == 0.0) val = new EntityProperty(false);
				else if (kv.Value == 1.0) val = new EntityProperty(true);
				else val = new EntityProperty((int) (kv.Value * Storage.DoubleValueMultiplier));

				var key = kv.Key.Trim().ToUpperInvariant();
				switch (type) {
					case 'Z': key = key.Substring(4); break;
				}

				entity.Properties[key] = val;
			}
			return entity;
		}

		[JsonIgnore]
		internal override string InsertStatement { get { return ClassInsertStatement; } }

		internal override void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			base.AddSqlParameters(parameters, makeParam);

			parameters.Add(makeParam("Operator", DbType.Int32, 0, OperatorId));
			parameters.Add(makeParam("OpMode", DbType.Byte, 0, (byte) OpMode));
			parameters.Add(makeParam("JobMode", DbType.Byte, 0, (byte) JobMode));
			parameters.Add(makeParam("JobCard", DbType.String, 100, (object) JobCardId ?? DBNull.Value));
			parameters.Add(makeParam("Mold", DbType.String, 100, (object) MoldId ?? DBNull.Value));
			parameters.Add(makeParam("UniqueID", DbType.String, 100, (object) ID ?? DBNull.Value));
		}
	}

	public class MoldData : EntryBase
	{
		[JsonProperty("id", Order = -899)]
		public Guid GUID { get; }

		[JsonProperty(Order = 1)]
		public string Name { get; }

		[JsonProperty(Order = 10)]
		public IList<ushort> Data { get; }

		[JsonIgnore]
		public override bool UseBatches => false;

		/// <summary>Save (if not existing) or update (if existing) a set of mold data</summary>
		/// <param name="controller">Unique controller ID</param>
		/// <param name="guid">GUID of the mold record (not its numeric ID) - this is to prevent ID collision between multiple instances of the iChen server each with a separate database</param>
		/// <param name="name">Name of the mold</param>
		/// <param name="data">Mold data</param>
		public MoldData (string uniqueId, string orgId, uint controller, Guid guid, string name, IList<ushort> data, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			if (controller <= 0) throw new ArgumentOutOfRangeException(nameof(controller));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
			if (data == null) throw new ArgumentNullException(nameof(data));

			this.GUID = guid;
			this.Name = name;
			this.Data = data.ToList();
		}

		public MoldData (DynamicTableEntity entity) : base(entity)
		{
			GUID = GuidEncoder.Decode(entity.RowKey);
			Name = entity.Properties["Name"].StringValue;

			var bytes = entity.Properties["Data"].BinaryValue;
			var compressed = new List<int>();

			for (var x = 0; x < bytes.Length; x += 4) {
				compressed.Add(BitConverter.ToInt32(bytes, x));
			}

			Data = RunLengthEncoder.Decode(compressed.ToArray());
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			// The row key is ignored
			var entity = new DynamicTableEntity(this.Controller.ToString(), GUID + ":" + rowkey);

			entity.Properties["GUID"] = new EntityProperty(GUID);
			entity.Properties["Name"] = new EntityProperty(Name);

			// Store mold data as RLE-compressed byte stream
			var compressed = RunLengthEncoder.Encode(Data);
			var bytes = new List<byte>();

			foreach (var x in compressed) {
				var buf = BitConverter.GetBytes(x);
				if (!BitConverter.IsLittleEndian) Array.Reverse(buf);
				bytes.AddRange(buf);
			}

			entity.Properties["Data"] = new EntityProperty(bytes.ToArray());
			return entity;
		}
	}

	public class AuditTrail : KeyEntryBase
	{
		public const string DatabaseKeyField = "VariableName";

		[JsonIgnore]
		public new static string ClassInsertStatement { get; }

		[JsonProperty(Order = -997)]
		public int OperatorId { get; } = 0;

		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Include, Order = 1)]
		public double Value { get; }

		[JsonProperty(Order = 2)]
		public double? OldValue { get; }

		protected override string DatabaseKeyName { get { return DatabaseKeyField; } }

		static AuditTrail ()
		{
			ClassInsertStatement = KeyEntryBase.ClassInsertStatement.Replace("<KeyName>", DatabaseKeyField).Replace("<ValueName>", "Value").Replace(") VALUES (", ", OldValue, Operator) VALUES (");
			ClassInsertStatement = ClassInsertStatement.Substring(0, ClassInsertStatement.Length - 1) + ", ?, ?)";
		}

		public AuditTrail (string uniqueId, string orgId, uint controller, string key, double value, double? oldvalue, int user, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, key, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			this.OperatorId = user;
			this.Value = value;
			this.OldValue = oldvalue;
		}

		public AuditTrail (DynamicTableEntity entity) : base(entity)
		{
			if (entity.Properties.TryGetValue(Storage.Operator, out var prop)) OperatorId = prop.Int32Value.Value;
			if (entity.Properties.TryGetValue(Storage.Value, out prop)) Value = prop.Int32Value.Value / (double) Storage.DoubleValueMultiplier;
			if (entity.Properties.TryGetValue(Storage.OldValue, out prop)) OldValue = prop.Int32Value.Value / (double) Storage.DoubleValueMultiplier;
		}

		public AuditTrail (DataRow drow) : base(drow)
		{
			if (IsColumnAvailable(drow, "Operator")) this.OperatorId = (int) drow["Operator"];
			if (IsColumnAvailable(drow, "Value")) this.Value = (float) drow["Value"];
			if (IsColumnAvailable(drow, "OldValue")) this.OldValue = (float) drow[nameof(OldValue)];
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			var entity = base.ToEntity(rowkey);

			if (OperatorId > 0) entity.Properties[Storage.Operator] = new EntityProperty(OperatorId);
			entity.Properties[Storage.Value] = new EntityProperty((int) Value * Storage.DoubleValueMultiplier);
			if (OldValue.HasValue) entity.Properties[Storage.OldValue] = new EntityProperty((int) OldValue.Value * Storage.DoubleValueMultiplier);
			return entity;
		}

		[JsonIgnore]
		internal override string InsertStatement { get { return ClassInsertStatement; } }

		internal override void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			base.AddSqlParameters(parameters, makeParam);

			parameters.Add(makeParam("Value", DbType.Single, 0, (float) Value));
			parameters.Add(makeParam("OldValue", DbType.Single, 0, OldValue.HasValue ? (object) (float) OldValue.Value : DBNull.Value));
			parameters.Add(makeParam("Operator", DbType.Int32, 0, OperatorId));
		}
	}

	public class Event : EntryBase
	{
		[JsonIgnore]
		public new static string ClassInsertStatement { get; }

		[JsonProperty(Order = -897)]
		public int? OperatorId { get; }

		public string IP { get; }
		public double? GeoLatitude { get; }
		public double? GeoLongitude { get; }
		public OpModes? OpMode { get; }
		public JobModes? JobMode { get; }
		public string JobCardId { get; }
		public Guid? MoldId { get; }
		public bool? Connected { get; }
		public string Type { get; }
		public string Message { get; }

		static Event ()
		{
			ClassInsertStatement = EntryBase.ClassInsertStatement.Replace(") VALUES (", ", IP, GeoLatitude, GeoLongitude, Connected, OpMode, JobMode, Operator, JobCard, Mold) VALUES (");
			ClassInsertStatement = ClassInsertStatement.Substring(0, ClassInsertStatement.Length - 1) + ", ?, ?, ?, ?, ?, ?, ?, ?, ?)";
		}

		/// <summary>Store a log message as an event</summary>
		public Event (string uniqueId, string type, string message, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, null, 0, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));

			this.Type = type.Trim();
			this.Message = message.TrimEnd();
		}

		/// <summary>Store a log message as an event</summary>
		public Event (string uniqueId, string orgId, uint controller, string type, string message, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			if (controller <= 0) throw new ArgumentOutOfRangeException(nameof(controller));
			if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
			if (string.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));

			this.Type = type.Trim();
			this.Message = message.TrimEnd();
		}

		/// <param name="jobcard">Use an empty string to clear this value. Null will be interpreted as no change.</param>
		/// <param name="mold">Use Guid.Empty to clear this value. Null will be interpreted as no change.</param>
		public Event (string uniqueId, string orgId, uint controller, bool? connected, string IP, double? geo_latitude, double? geo_longitude, OpModes? opmode, JobModes? jobmode, string jobcard, int? user, Guid? mold, DateTimeOffset time = default(DateTimeOffset))
			: base(uniqueId, orgId, controller, time == default(DateTimeOffset) ? DateTimeOffset.UtcNow : time)
		{
			if (controller <= 0) throw new ArgumentOutOfRangeException(nameof(controller));
			this.IP = IP;
			this.GeoLatitude = geo_latitude;
			this.GeoLongitude = geo_longitude;
			this.OpMode = opmode;
			this.JobMode = jobmode;
			this.JobCardId = jobcard;
			this.OperatorId = user;
			this.MoldId = mold;
			this.Connected = connected;
		}

		public Event (DynamicTableEntity entity) : base(entity)
		{
			if (entity.Properties.TryGetValue("IP", out var prop)) IP = prop.StringValue;
			if (entity.Properties.TryGetValue("GeoLatitude", out prop)) GeoLatitude = prop.DoubleValue;
			if (entity.Properties.TryGetValue("GeoLongitude", out prop)) GeoLongitude = prop.DoubleValue;
			if (entity.Properties.TryGetValue("Connected", out prop)) Connected = prop.BooleanValue;
			if (entity.Properties.TryGetValue("OpMode", out prop) && prop.StringValue != null) OpMode = (OpModes) Enum.Parse(typeof(OpModes), prop.StringValue, true);
			if (entity.Properties.TryGetValue("JobMode", out prop) && prop.StringValue != null) JobMode = (JobModes) Enum.Parse(typeof(JobModes), prop.StringValue, true);
			if (entity.Properties.TryGetValue("Operator", out prop)) OperatorId = prop.Int32Value;
			if (entity.Properties.TryGetValue("JobCard", out prop)) JobCardId = prop.StringValue;
			if (entity.Properties.TryGetValue("Mold", out prop) && prop.StringValue != null) MoldId = string.IsNullOrWhiteSpace(prop.StringValue) ? Guid.Empty : new Guid(prop.StringValue);
			if (entity.Properties.TryGetValue("Type", out prop)) Type = prop.StringValue;
			if (entity.Properties.TryGetValue("Message", out prop)) Message = prop.StringValue;
		}

		public Event (DataRow drow) : base(drow)
		{
			if (IsColumnAvailable(drow, "IP")) this.IP = drow["IP"].ToString();
			if (IsColumnAvailable(drow, "GeoLatitude")) this.GeoLatitude = (double) drow["GeoLatitude"];
			if (IsColumnAvailable(drow, "GeoLongitude")) this.GeoLongitude = (double) drow["GeoLongitude"];
			if (IsColumnAvailable(drow, "Connected")) this.Connected = (bool) drow["Connected"];
			if (IsColumnAvailable(drow, "OpMode")) this.OpMode = (OpModes) (byte) drow["OpMode"];
			if (IsColumnAvailable(drow, "JobMode")) this.JobMode = (JobModes) (byte) drow["JobMode"];
			if (IsColumnAvailable(drow, "Operator")) this.OperatorId = (int) drow["Operator"];
			if (IsColumnAvailable(drow, "JobCard")) this.JobCardId = drow["JobCard"].ToString();
			if (IsColumnAvailable(drow, "Mold")) this.MoldId = (Guid) drow["Mold"];
		}

		internal override DynamicTableEntity ToEntity (string rowkey)
		{
			var entity = base.ToEntity(rowkey);
			if (IP != null) entity.Properties["IP"] = new EntityProperty(IP);
			if (GeoLatitude.HasValue) entity.Properties["GeoLatitude"] = new EntityProperty(GeoLatitude.Value);
			if (GeoLongitude.HasValue) entity.Properties["GeoLongitude"] = new EntityProperty(GeoLongitude.Value);
			if (Connected.HasValue) entity.Properties["Connected"] = new EntityProperty(Connected);
			if (OpMode.HasValue) entity.Properties["OpMode"] = new EntityProperty(OpMode.Value.ToString());
			if (JobMode.HasValue) entity.Properties["JobMode"] = new EntityProperty(JobMode.Value.ToString());
			if (OperatorId.HasValue) entity.Properties["Operator"] = new EntityProperty(OperatorId.Value);
			if (JobCardId != null) entity.Properties["JobCard"] = new EntityProperty(JobCardId.Trim());
			if (MoldId.HasValue) entity.Properties["Mold"] = new EntityProperty(MoldId == Guid.Empty ? "" : MoldId.Value.ToString());
			if (!string.IsNullOrWhiteSpace(Type)) entity.Properties["Type"] = new EntityProperty(Type);
			if (!string.IsNullOrWhiteSpace(Message)) entity.Properties["Message"] = new EntityProperty(Message);

			return entity;
		}

		[JsonIgnore]
		internal override string InsertStatement { get { return ClassInsertStatement; } }

		internal override void AddSqlParameters (DbParameterCollection parameters, Func<string, DbType, int, object, DbParameter> makeParam)
		{
			base.AddSqlParameters(parameters, makeParam);

			parameters.Add(makeParam("IP", DbType.AnsiString, 25, (object) IP ?? DBNull.Value));
			parameters.Add(makeParam("GeoLatitude", DbType.Double, 0, GeoLatitude.HasValue ? (object) GeoLatitude.Value : DBNull.Value));
			parameters.Add(makeParam("GeoLongitude", DbType.Double, 0, GeoLongitude.HasValue ? (object) GeoLongitude.Value : DBNull.Value));
			parameters.Add(makeParam("Connected", DbType.Boolean, 0, Connected.HasValue ? (object) Connected.Value : DBNull.Value));
			parameters.Add(makeParam("OpMode", DbType.Byte, 0, OpMode.HasValue ? (object) (byte) OpMode.Value : DBNull.Value));
			parameters.Add(makeParam("JobMode", DbType.Byte, 0, JobMode.HasValue ? (object) (byte) JobMode.Value : DBNull.Value));
			parameters.Add(makeParam("Operator", DbType.Int32, 0, OperatorId.HasValue ? (object) OperatorId.Value : DBNull.Value));
			parameters.Add(makeParam("JobCard", DbType.String, 100, (object) JobCardId ?? DBNull.Value));
			parameters.Add(makeParam("Mold", DbType.Guid, 0, (object) MoldId ?? DBNull.Value));
		}
	}
}