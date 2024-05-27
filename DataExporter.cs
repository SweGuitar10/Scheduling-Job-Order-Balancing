using ClosedXML.Excel;
namespace thesis_project;
/// <summary>
/// Exports a schedule to .xlsx
/// </summary>
internal class DataExporter
{
	const string ColumnA = "Scheduling Sequence";
	const string ColumnB = "Production Order ID";
	const string ColumnC = "Customer ID";
	const string ColumnD = "Customer Delivery Sequence";
	const string ColumnE = "Matching Batch Group ID's";

	const string fileending = ".xlsx";


	public static void ExportSchedule(Schedule schedule, string filename = "schedule")
	{
		XLWorkbook wb = new XLWorkbook();
		IXLWorksheet ws = wb.Worksheets.Add("Schedule");

		IXLRow firstRow = ws.FirstRow();
		firstRow.Cell(1).Value = ColumnA;
		firstRow.Cell(2).Value = ColumnB;
		firstRow.Cell(3).Value = ColumnC;
		firstRow.Cell(4).Value = ColumnD;
		firstRow.Cell(5).Value = ColumnE;

		IXLRow row = firstRow.RowBelow();
		int i = 1;


		List<TimeSlot> timeSlots = schedule.TimeSlots;

		foreach (TimeSlot slot in timeSlots)
		{
			Job job = slot.Job;

			row.Cell(1).Value = i++;
			row.Cell(2).Value = job.ProductionOrderID;
			row.Cell(3).Value = job.CustomerID;
			row.Cell(4).Value = job.CustomerDeliverySequence;
			row.Cell(5).Value = string.Join(",", job.BatchGroupId);

			HandleColor(row.Cell(5), HandleBatchGroupId(job.BatchGroupId));
			row = row.RowBelow();
		}
		System.IO.Directory.CreateDirectory("result");

		wb.SaveAs("result/"+filename + fileending);
	}

	private static void HandleColor(IXLCell cell, string batch)
	{
		int[] colors = { 0xfbe5d6, 0xe2f0d9, 0x000000, 0xdae3f3, 0xfff2cc, 0xffccff, 0xcbb9ef, 0x99ff99, 0xffff66, 0xd0cece, 0x66ffff };
		int number = int.Parse(batch.Substring(2)); // Assume BGXX, extract XX (where XX is ALWAYS a number)

		if (number <= 0)
			number = 5;
		else if (number > 11)
			number = 6;

		cell.Style.Fill.BackgroundColor = XLColor.FromArgb(colors[number - 1]);
	}

	private static string HandleBatchGroupId(List<string> batchGroupId)
	{
		if (batchGroupId.Count == 1)
		{
			return batchGroupId.FirstOrDefault();
		}

		if (batchGroupId.First().Equals("BG01") || batchGroupId.First().Equals("BG02"))
		{
			return batchGroupId.First();
		}
		return batchGroupId[1];
	}
}
