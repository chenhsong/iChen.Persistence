using System;
using System.Collections.Generic;
using System.Data;
using iChen.Persistence.Cloud;
using iChen.Persistence.Server;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using NPOI.SS.UserModel;

namespace iChen.Analytics
{
	public enum Sorting
	{
		None, ByTime, ByController
	}

	public class EventX : Event, IDataFileFormatConverter
	{
		public static readonly string[] Headers = new[]
		{
			"Machine", "Name", "Time", "Connection", "Operator", "IP", "OpMode", "JobMode", "JobCardId", "MoldId"
		};

		public string DisplayId { get; }

		[JsonProperty(Order = -998)]
		public new DateTimeOffset Time { get; }

		public EventX (IReadOnlyDictionary<uint, Controller> controllers, DynamicTableEntity entity) : base(entity)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public EventX (IReadOnlyDictionary<uint, Controller> controllers, DataRow drow) : base(drow)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public EventX () : base("Dummy", "Dummy")
		{
			this.Time = DateTimeOffset.MaxValue;
		}

		public string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true)
			=> $@"{Controller}{delimiter}{(escape ? "\"" : null)}{(escape ? DisplayId?.Replace("\"", "\"\"") : DisplayId) ?? Controller.ToString()}{(escape ? "\"" : null)}{delimiter}{Time.ToString("o")}{delimiter}{(!Connected.HasValue ? null : Connected.Value ? "Connected" : "Disconnected")}{delimiter}{OperatorId}{delimiter}{IP}{delimiter}{OpMode}{delimiter}{JobMode}{delimiter}{(JobCardId != null ? (escape ? "\"" : null) + (escape ? JobCardId.Replace("\"", "\"\"") : JobCardId) + (escape ? "\"" : null) : null)}{delimiter}{MoldId}";

		public void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone)
		{
			row.CreateCell(0, CellType.String).SetCellValue(Controller.ToString());
			row.CreateCell(1, CellType.String).SetCellValue(DisplayId ?? Controller.ToString());

			var cell = row.CreateCell(2);
			cell.SetCellValue(Time.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
			cell.CellStyle = datestyle;

			if (Connected.HasValue) row.CreateCell(3, CellType.Boolean).SetCellValue(Connected.Value ? "Connected" : "Disconnected");
			if (OperatorId.HasValue) row.CreateCell(4, CellType.Numeric).SetCellValue(OperatorId.Value);
			if (!string.IsNullOrWhiteSpace(IP)) row.CreateCell(5, CellType.String).SetCellValue(IP);
			if (OpMode.HasValue) row.CreateCell(6, CellType.String).SetCellValue(OpMode.ToString());
			if (JobMode.HasValue) row.CreateCell(7, CellType.String).SetCellValue(JobMode.ToString());
			if (JobCardId != null) row.CreateCell(8, CellType.String).SetCellValue(JobCardId);
			if (MoldId.HasValue) row.CreateCell(9, CellType.String).SetCellValue(MoldId.ToString());
		}
	}

	public class AuditTrailX : AuditTrail, IDataFileFormatConverter
	{
		public static readonly string[] Headers = new[]
		{
			"Machine", "Name", "Time", "Operator", "Variable", "NewValue", "OldValue"
		};

		public string DisplayId { get; }

		[JsonProperty(Order = -998)]
		public new DateTimeOffset Time { get; }

		public AuditTrailX (IReadOnlyDictionary<uint, Controller> controllers, DynamicTableEntity entity) : base(entity)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public AuditTrailX (IReadOnlyDictionary<uint, Controller> controllers, DataRow drow) : base(drow)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true)
			=> $@"{Controller}{delimiter}{(escape ? "\"" : null)}{(escape ? DisplayId?.Replace("\"", "\"\"") : DisplayId) ?? Controller.ToString()}{(escape ? "\"" : null)}{delimiter}{Time.ToString("o")}{delimiter}{OperatorId}{delimiter}{Key}{delimiter}{Value}{delimiter}{(OldValue.HasValue ? OldValue : null)}";

		public void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone)
		{
			row.CreateCell(0, CellType.String).SetCellValue(Controller.ToString());
			row.CreateCell(1, CellType.String).SetCellValue(DisplayId ?? Controller.ToString());

			var cell = row.CreateCell(2);
			cell.SetCellValue(Time.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
			cell.CellStyle = datestyle;

			if (OperatorId != 0) row.CreateCell(3, CellType.Numeric).SetCellValue(OperatorId);
			row.CreateCell(4, CellType.String).SetCellValue(Key);
			row.CreateCell(5, CellType.Numeric).SetCellValue(Value);
			if (OldValue.HasValue) row.CreateCell(6, CellType.Numeric).SetCellValue(OldValue.Value);
		}
	}

	public class AlarmX : Alarm, IDataFileFormatConverter
	{
		public static readonly string[] Headers = new[]
		{
			"Machine", "Name", "Time", "Alarm", "State"
		};

		public string DisplayId { get; }

		[JsonProperty(Order = -998)]
		public new DateTimeOffset Time { get; }

		public AlarmX (IReadOnlyDictionary<uint, Controller> controllers, DynamicTableEntity entity) : base(entity)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public AlarmX (IReadOnlyDictionary<uint, Controller> controllers, DataRow row) : base(row)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true)
			=> $@"{Controller}{delimiter}{(escape ? "\"" : null)}{(escape ? DisplayId?.Replace("\"", "\"\"") : DisplayId) ?? Controller.ToString()}{(escape ? "\"" : null)}{delimiter}{Time.ToString("o")}{delimiter}{Key}{delimiter}{(State ? "Active" : "Clear")}";

		public void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone)
		{
			row.CreateCell(0, CellType.String).SetCellValue(Controller.ToString());
			row.CreateCell(1, CellType.String).SetCellValue(DisplayId ?? Controller.ToString());

			var cell = row.CreateCell(2);
			cell.SetCellValue(Time.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
			cell.CellStyle = datestyle;

			row.CreateCell(3, CellType.String).SetCellValue(Key);
			row.CreateCell(4, CellType.String).SetCellValue(State ? "Active" : "Clear");
		}
	}

	public class CycleDataX : CycleData, IDataFileFormatConverter
	{
		public static readonly string[] Headers = new[]
		{
			"Machine", "Name", "Time", "Operator", "OpMode", "JobMode", "JobCardId", "MoldId"
		};

		public string DisplayId { get; }

		[JsonProperty(Order = -998)]
		public new DateTimeOffset Time { get; }

		public CycleDataX (IReadOnlyDictionary<uint, Controller> controllers, DynamicTableEntity entity) : base(entity)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public CycleDataX (IReadOnlyDictionary<uint, Controller> controllers, DataRow drow, IReadOnlyDictionary<string, double> data = null) : base(drow, data)
		{
			if (controllers == null) throw new ArgumentNullException(nameof(controllers));

			if (controllers.ContainsKey(this.Controller)) {
				var controller = controllers[this.Controller];

				this.DisplayId = controller.Name;
				this.Time = base.Time.ToOffset(TimeSpan.FromHours(controller.TimeZoneOffset.GetValueOrDefault((float) DateTimeOffset.Now.Offset.TotalHours)));
			}
		}

		public string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true)
		{
			var datalist = new List<string>();

			for (var i = 8; i < headers.Length; i++) {
				var key = headers[i];
				datalist.Add(Data.ContainsKey(key) ? Data[key].ToString() : "");
			}

			return $@"{Controller}{delimiter}{(escape ? "\"" : null)}{(escape ? DisplayId?.Replace("\"", "\"\"") : DisplayId) ?? Controller.ToString()}{(escape ? "\"" : null)}{delimiter}{Time.ToString("o")}{delimiter}{OperatorId}{delimiter}{OpMode}{delimiter}{JobMode}{delimiter}{(JobCardId != null ? (escape ? "\"" : null) + (escape ? JobCardId.Replace("\"", "\"\"") : JobCardId) + (escape ? "\"" : null) : null)}{delimiter}{MoldId}{delimiter}{string.Join(delimiter, datalist)}";
		}

		public void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone)
		{
			row.CreateCell(0, CellType.String).SetCellValue(Controller.ToString());
			row.CreateCell(1, CellType.String).SetCellValue(DisplayId ?? Controller.ToString());

			var cell = row.CreateCell(2);
			cell.SetCellValue(Time.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
			cell.CellStyle = datestyle;

			if (OperatorId != 0) row.CreateCell(3, CellType.Numeric).SetCellValue(OperatorId);

			row.CreateCell(4, CellType.String).SetCellValue(OpMode.ToString());
			row.CreateCell(5, CellType.String).SetCellValue(JobMode.ToString());
			if (JobCardId != null) row.CreateCell(6, CellType.String).SetCellValue(JobCardId);
			if (MoldId != null) row.CreateCell(7, CellType.String).SetCellValue(MoldId);

			for (var i = 8; i < headers.Length; i++) {
				var key = headers[i];
				if (Data.ContainsKey(key)) row.CreateCell(i, CellType.Numeric).SetCellValue(Data[key]);
			}
		}
	}

	public class TimeValue<T> : IDataFileFormatConverter
	{
		public static readonly string[] Headers = new[]
		{
			"Time", "Value"
		};

		[JsonProperty(Order = 0)]
		public DateTimeOffset Time { get; }

		[JsonProperty(Order = 1)]
		public T Value { get; }

		public TimeValue (DateTimeOffset time, T value)
		{
			this.Time = time;
			this.Value = value;
		}

		public string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true)
		{
			if (typeof(T) == typeof(DateTime)) {
				return $@"{Time.ToString("o")}{delimiter}{Convert.ToDateTime(Value).ToString("o")}";
			} else if (typeof(T) == typeof(DateTimeOffset)) {
				var val = (DateTimeOffset) (object) Value;
				return $@"{Time.ToString("o")}{delimiter}{val.ToString("o")}";
			} else if (typeof(T) == typeof(string)) {
				return $@"{Time.ToString("o")}{delimiter}""{Value.ToString().Replace("\"", "\"\"")}""";
			} else {
				return $@"{Time.ToString("o")}{delimiter}{Value}";
			}
		}

		public void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone)
		{
			var cell = row.CreateCell(0);
			cell.SetCellValue(Time.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
			cell.CellStyle = datestyle;

			if (typeof(T) == typeof(double) || typeof(T) == typeof(float)) {
				row.CreateCell(1, CellType.Numeric).SetCellValue(Convert.ToDouble(Value));
			} else if (typeof(T) == typeof(DateTime)) {
				cell = row.CreateCell(1);
				cell.SetCellValue(Convert.ToDateTime(Value));
				cell.CellStyle = datestyle;
			} else if (typeof(T) == typeof(DateTimeOffset)) {
				cell = row.CreateCell(1);
				var val = (DateTimeOffset) (object) Value;
				cell.SetCellValue(val.ToOffset(TimeSpan.FromMinutes(timezone)).DateTime);
				cell.CellStyle = datestyle;
			} else {
				row.CreateCell(1, CellType.String).SetCellValue(Value.ToString());
			}
		}
	}
}