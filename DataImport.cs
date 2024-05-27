
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace thesis_project;
/// <summary>
/// This class is solely for importing specific data from an .xlsx
/// The values could just as well be hardcoded into the code, or imported from another source
/// as long as they are represented with the classes from this project.
/// </summary>
internal class DataImport
{
	public List<Job> Jobs { get; private set; }
	public List<BatchGroup> BatchGroups { get; private set; }
	public List<Batch> Batches { get; private set; }
	public bool Sorted { get; private set; }
	string path;
	public DataImport(string path, bool sorted = false)
	{
		this.path = path;
		Jobs = new List<Job>();
		BatchGroups = new List<BatchGroup>();
		Batches = new List<Batch>();
		Sorted = sorted;
		ExtractData();

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

	private void ExtractData()
	{
		//const string schedulingSequence = "Scheduling Sequence";
		//const string prodcutionOrderId = "Production Order ID";
		//const string customerId = "Customer ID";
		//const string customerDeliverySequence = "Customer Delivery Sequence";
		//const string matchingBatchGroupIds = "Matching Batch Group ID's";

		XLWorkbook wb = new XLWorkbook(path);
		string wsJobExtraction;
		// Extract jobs
		if (Sorted)
		{
			wsJobExtraction = "Example 1 After";
		}
		else
		{
			wsJobExtraction = "Example 1 Before";
		}

		IXLWorksheet ws = wb.Worksheet(wsJobExtraction);

		IXLRow titleRow = ws.FirstRowUsed();

		IXLRow dataRow = titleRow.RowBelow();

		while (!dataRow.Cell(1).IsEmpty())
		{
			BuildJobs(dataRow.Cell(2).Value, dataRow.Cell(3).Value, dataRow.Cell(4).Value, dataRow.Cell(5).Value);
			dataRow = dataRow.RowBelow();
		}

		// Extract batches
		string wsBatchExtraction = "Batch pattern";
		ws = wb.Worksheet(wsBatchExtraction);

		titleRow = ws.FirstRowUsed();

		IXLRow lastRow = ws.LastRowUsed();
		dataRow = titleRow.RowBelow();

		while (!dataRow.Equals(lastRow))
		{
			// TODO: breaks loop at row 8?
			if (dataRow.Cell(2).IsEmpty())
			{
				dataRow = dataRow.RowBelow();
				continue;
			}
			BuildBatchGroups(dataRow.Cell(1).Value, dataRow.Cell(2).Value, dataRow.Cell(3).Value, dataRow.Cell(4).Value, dataRow.Cell(5).Value, dataRow.Cell(6).Value);
			dataRow = dataRow.RowBelow();
		}
		AddCooldowns();
		BuildBatches();
	}

	private void BuildBatches()
	{
		List<Job> currentJobs = Jobs.OrderBy(o => o.ProductionOrderID).ToList();  // IS THIS TO BE USED ? 
		Jobs = currentJobs;

		if (Sorted) // is schedule from the sorted list? // IS THIS STILL RELEVANT? 
		{
			// Sort joblist by PO_ID
			Jobs.OrderBy(o => o.ProductionOrderID).ToList();

		}
		// 1. Foreach job in jobs
		// 2. Extract: BatchGroup(s), Cooldown, BatchQuantity
		// 3. Add to new Batch

		// "BGXX", {job1, job2,...}
		Dictionary<string, IEnumerable<Job>> batchJobPair = new Dictionary<string, IEnumerable<Job>>();

		// Find all jobs sorted by BGXX
		foreach (Job job in Jobs)
		{
			string bg = string.Join(",", job.BatchGroupId);

			if (!batchJobPair.ContainsKey(bg))
			{
				// TODO: Maybe not needed?
				batchJobPair[bg] = new List<Job>();
			}
			batchJobPair[bg] = batchJobPair[bg].Append(job);
		}

		foreach (IEnumerable<Job> jobsInBatch in batchJobPair.Values)
		{
			List<BatchGroup> batchGroups = new List<BatchGroup>();
			foreach (string group in jobsInBatch.First().BatchGroupId)
			{
				batchGroups.Add(BatchGroups.Find(x => x.BatchGroupId.Equals(group)));
			}

			// jobsInBatch {job1,job2 ... , jobn} | job1.BatchGroupId {"BG03", BG04}, job2.BatchGroupId {BG03, BG04}, job3.BatchGroupId {BG03, BG04}

			// Prioritize the cooldown of the highest weighted BatchGroup
			int cooldown = 1; // default value == 1
			BatchGroup maxWeighted = batchGroups.First();

			for (int z = 0; z < batchGroups.Count - 1; z++)
			{
				for (int x = z + 1; x < batchGroups.Count; x++)
				{
					maxWeighted = batchGroups.MaxBy(x => x.Cooldown);
				}
			}
			cooldown = maxWeighted.Cooldown;

			// Prioritize the smallest batch quantity larger than 0
			int quantity = 0;
			foreach (BatchGroup group in batchGroups)
			{
				if(Priority.HIGH_PRIO == group.Priority /*|| Priority.LOW_PRIO == group.Priority*/) // TODO: Consider this?
				{
					quantity = group.BatchQuantity;
					break;
				}
				if (group.BatchQuantity == 0)
				{
					continue;
				}
				if (quantity == 0)
				{
					quantity = group.BatchQuantity;
				}
				else
				{
					quantity = int.Min(quantity, group.BatchQuantity);
				}
			}

			if (quantity == 0)
			{
				Batches.Add(new Batch(batchGroups, jobsInBatch, cooldown));
			}
			else
			{
				IEnumerable<Job[]> batches = jobsInBatch.Chunk(quantity);
				foreach (Job[] batch in batches)
				{
					Batches.Add(new Batch(batchGroups, batch, cooldown));
				}
			}

		}
	}

	private void BuildBatchGroups(XLCellValue cellBatchOrder, XLCellValue cellBatchGroup, XLCellValue cellRule,
		XLCellValue cellBatchQty, XLCellValue cellSorting, XLCellValue cellWeight)
	{
		// Create a batch 
		string batchOrder = cellBatchOrder.ToString(); // TODO: not implemented yet
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

				// TODO: optimization, since all jobs are sorted by batch group,
				// stop loop when first of other type is found. Keep track of last job used globally
			}
		}
		BatchGroups.Add(batchGroup);
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
}
