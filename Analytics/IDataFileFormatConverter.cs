using NPOI.SS.UserModel;

namespace iChen.Analytics
{
	public interface IDataFileFormatConverter
	{
		string ToCSVDataLine (string[] headers, double timezone, string delimiter = ",", bool escape = true);

		void FillXlsRow (IWorkbook workbook, ISheet sheet, string[] headers, IRow row, ICellStyle datestyle, double timezone);
	}
}