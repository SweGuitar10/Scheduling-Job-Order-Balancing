using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace thesis_project.test;

internal class Test
{

	public static void MockTestSmallSpreadOutCP()
	{
		int prodId = 10;
		int custId = 10;
		int custDelSeq = 100;

		BatchGroup bg1 = new BatchGroup("BG00", "CUSTOMER A", 5, 50, "", "Spread evenly");
		BatchGroup bg2 = new BatchGroup("BG33", "CUSTOMER B", 5, 50, "", "Spread evenly");
		List<BatchGroup> bgs = new List<BatchGroup> { bg1, bg2 };

		List<Job> jobs = new List<Job>
		{
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} )
		};
		//Schedule before = new Schedule(jobs);
		//DataExporter.ExportSchedule(before, "CP_SMALL_MOCK_Keep_before");


		IScheduler cp = new CP();

		List<TimeSlot> slots = new List<TimeSlot>();
		for (int i = 0; i < jobs.Count; i++)
		{
			slots.Add(new TimeSlot(i));
		}

		Console.WriteLine("----------Small Spread Out Test---------");
		Schedule scheduleCP = cp.ScheduleJobs(jobs, bgs, slots);
		Console.WriteLine("----------Printing---------");
		DataExporter.ExportSchedule(scheduleCP, "CP_SMALL_MOCK_Spread");
		Console.WriteLine("-------------------------------------------");
	}

	public static void MockTestSmallCooldownCP()
	{
		int prodId = 10;
		int custId = 10;
		int custDelSeq = 100;

		BatchGroup bg1 = new BatchGroup("BG00", "CUSTOMER A", 5, 50, "", "");
		BatchGroup bg2 = new BatchGroup("BG33", "CUSTOMER B", 5, 50, "", "");
		bg1.Cooldown = 2;
		bg2.Cooldown = 2;
		List<BatchGroup> bgs = new List<BatchGroup> { bg1, bg2 };

		List<Job> jobs = new List<Job>
		{
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} )
		};
		//Schedule before = new Schedule(jobs);
		//DataExporter.ExportSchedule(before, "CP_SMALL_MOCK_Cooldown_before");

		IScheduler cp = new CP();

		List<TimeSlot> slots = new List<TimeSlot>();
		for (int i = 0; i < jobs.Count; i++)
		{
			slots.Add(new TimeSlot(i));
		}
		Console.WriteLine("----------Small Cooldown Test---------");
		Schedule scheduleCP = cp.ScheduleJobs(jobs, bgs, slots);
		Console.WriteLine("----------Printing---------");
		DataExporter.ExportSchedule(scheduleCP, "CP_SMALL_MOCK_Cooldown");
	}
	public static void MockTestSmallKeepTogetherCP()
	{
		int prodId = 10;
		int custId = 10;
		int custDelSeq = 100;

		BatchGroup bg1 = new BatchGroup("BG00", "CUSTOMER A", 5, 50, "", "Keep together");
		BatchGroup bg2 = new BatchGroup("BG33", "CUSTOMER B", 5, 50, "", "Keep together");
		List<BatchGroup> bgs = new List<BatchGroup> { bg1, bg2 };

		List<Job> jobs = new List<Job>
		{
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg1.BatchGroupId} ),
			new Job($"PO_{prodId++}", $"CUSTOMERID_{custId++}",custDelSeq++, new List<string>  { bg2.BatchGroupId} )
		};
		IScheduler cp = new CP();

		List<TimeSlot> slots = new List<TimeSlot>();
		for (int i = 0; i < jobs.Count; i++)
		{
			slots.Add(new TimeSlot(i));
		}
		Console.WriteLine("----------Small Keep Together Test---------");
		Schedule scheduleCP = cp.ScheduleJobs(jobs, bgs, slots);
		Console.WriteLine("----------Printing---------");
		DataExporter.ExportSchedule(scheduleCP, "CP_SMALL_MOCK_Keep_Schedule");
		Console.WriteLine("-------------------------------------------");
	}
}
