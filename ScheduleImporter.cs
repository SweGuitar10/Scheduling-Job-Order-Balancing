using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;


namespace thesis_project;

internal class ScheduleImporter
{
	const string worksheet = "Schedule";
	const string batchPatternFile = "batchPattern";
	const string fileending = ".xlsx";
	public string Filename { get; set; }
	public List<BatchGroup> BatchGroups { get => batchGroups; }
	private List<Job> Jobs;
	private List<BatchGroup> batchGroups;

	public ScheduleImporter(string filename)
	{
		this.Filename = filename;
		Jobs = new List<Job>();
		batchGroups = new List<BatchGroup>();
	}
	public ScheduleImporter()
	{
		Jobs = new List<Job>();
		batchGroups = new List<BatchGroup>();
	}

	public Schedule ImportSchedule()
	{
		return ImportSchedule(Filename);
	}

	public Schedule ImportSchedule(string file)
	{
		XLWorkbook wb = new XLWorkbook(file);
		IXLWorksheet ws = wb.Worksheet(worksheet);

		IXLRow titleRow = ws.FirstRowUsed();

		IXLRow dataRow = titleRow.RowBelow();

		// Get all jobs[]
		while (!dataRow.Cell(1).IsEmpty())
		{
			BuildJobs(dataRow.Cell(2).Value, dataRow.Cell(3).Value, dataRow.Cell(4).Value, dataRow.Cell(5).Value);
			dataRow = dataRow.RowBelow();
		}
		BuildBatchGroups();
		AddCooldowns();

		// Apply jobs to Schedule (put jobs in Schedule.Timeslots)
		return new Schedule(Jobs);

	}

	private void BuildBatchGroups()
	{
		XLWorkbook wb = new XLWorkbook(batchPatternFile + fileending);
		IXLWorksheet ws = wb.Worksheet("Batch pattern");

		IXLRow titleRow = ws.FirstRowUsed();
		IXLRow lastRow = ws.LastRowUsed();
		IXLRow dataRow = titleRow.RowBelow();

		while (!dataRow.Equals(lastRow))
		{
			if (dataRow.Cell(2).IsEmpty())
			{
				dataRow = dataRow.RowBelow();
				continue;
			}
			BuildBatchGroup(dataRow.Cell(1).Value, dataRow.Cell(2).Value, dataRow.Cell(3).Value, dataRow.Cell(4).Value, dataRow.Cell(5).Value, dataRow.Cell(6).Value);
			dataRow = dataRow.RowBelow();
		}
	}

	private void BuildBatchGroup(XLCellValue cellBatchOrder, XLCellValue cellBatchGroup, XLCellValue cellRule,
		XLCellValue cellBatchQty, XLCellValue cellSorting, XLCellValue cellWeight)
	{
		// Create a batch 
		string batchOrder = cellBatchOrder.ToString(); 
		string batchGroupString = cellBatchGroup.GetText();
		string rule = cellRule.GetText();
		int batchQty = cellBatchQty.ToString() == "All" ? 0 : (int)cellBatchQty.GetNumber();
		string sorting = cellSorting.GetText();
		int weight = (int)cellWeight.GetNumber();

		// Sets priority 
		Priority priority = (batchGroupString.Contains("BG01")) ? Priority.HIGH_PRIO : Priority.NORMAL;
		priority = (batchGroupString.Contains("BG11")) ? Priority.LOW_PRIO : priority;

		BatchGroup batchGroup = new BatchGroup(batchGroupString, "", batchQty, weight, batchOrder, rule, priority);

		// add jobs to batch
		foreach (Job job in Jobs)
		{
			if (job.BatchGroupId.Equals("BG01, BG08") && batchGroup.BatchGroupId.Equals("BG08"))
			{
				continue;
			}

			if (job.BatchGroupId.Contains(batchGroup.BatchGroupId))
			{
				batchGroup.AddJobb(job);
			}
		}
		batchGroups.Add(batchGroup);
	}

	private void BuildJobs(XLCellValue cellOrderId, XLCellValue cellCustomerId, XLCellValue cellDeliverySeq, XLCellValue cellMatchBatchGroupId)
	{
		// Handle datatypes (extract string/int/etc...)

		string orderId = cellOrderId.GetText();
		string customerId = cellCustomerId.GetText();
		int customerDeliverySequence = (int)cellDeliverySeq.GetNumber();
		string batchGroupId = cellMatchBatchGroupId.GetText();

		// splits string into array and trims away spaces
		List<string> groupIds = batchGroupId.Split(',').Select(p => p.Trim()).ToList();

		// Create instances of classes
		// add to class list
		Jobs.Add(new Job(orderId, customerId, customerDeliverySequence, groupIds));
	}
	private void AddCooldowns()
	{
		int totalProductionCapacity = 50;// Jobs.Count
		BatchGroups[1].Cooldown = (int)(totalProductionCapacity / (totalProductionCapacity * 0.1f));
		BatchGroups[2].Cooldown = (int)(totalProductionCapacity / (totalProductionCapacity * 0.5f));
		//							50	/ (50*0,5) == 50 / 25 == 2
		// 1 job == 2%
		//Batches[3].Cooldown = (int)(totalProductionCapacity * 0.5f);
		//Batches[4].Cooldown = (int)(totalProductionCapacity * 0.5f);
		//Batches[5].Cooldown = (int)(totalProductionCapacity * 0.5f);

	}
}
