
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Spreadsheet;
using GeneticSharp;
using System.Diagnostics;
using System.Text;
using thesis_project.test;


using CsvHelper;
using System.IO;
using System.Globalization;
using System.Threading;


namespace thesis_project;

class Program
{

	static void Main(string[] args)
	{
		var csvPath = Path.Combine(Environment.CurrentDirectory, "test.csv");
		
		System.IO.Directory.CreateDirectory("result");
		if (args[0].ToLower() == "cp")
		{
			int timeout = 0;
			if (args.Length > 1)
			{
				try
				{
					timeout = int.Parse(args[1]);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}

			RunCP(1, timeout);
		}
		else if (args[0].ToLower() == "ga")
		{
			int populationSize = 5000;
			if (args.Length > 1)
			{
				try
				{
					populationSize = int.Parse(args[1]);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}

			RunGA(populationSize);
		}
		else if (args[0].ToLower() == "check")
		{
			if(args.Length <= 1)
			{
				Console.WriteLine("Specify a file as second argument");
				return;
			}
			string filename = args[1];
			ScheduleImporter importer = new ScheduleImporter(filename);
			double fitness = GetFitness(importer.ImportSchedule(filename), importer.BatchGroups);
			Console.WriteLine($"--------------------------------------------------------------------------");
			Console.WriteLine($"File: {filename}\nFitness: {fitness}");
			Console.WriteLine($"--------------------------------------------------------------------------");
		}
	}

	public static void RunFitness(Schedule schedule, List<BatchGroup> bgs) 
	{
		double sortedSchedScore = new GA().SortedScheduleFitnessCheck(schedule, bgs);
		Console.WriteLine("Fitness: {0} ", sortedSchedScore);
	}

	public static double GetFitness(Schedule schedule, List<BatchGroup> bgs) 
	{
		return new GA().SortedScheduleFitnessCheck(schedule, bgs);
	}

	public static void RunGA(int populationSize)
	{
		// 3.B - Run data against GA
		Console.WriteLine($"Default population size: {populationSize}");

		DataImport import = new DataImport("data.xlsx");
		List<BatchGroup> batcheGroupsGA = import.BatchGroups;
		List<Job> jobsGA = import.Jobs;
		int maxSlots = jobsGA.Count;

		var csvPath = Path.Combine(Environment.CurrentDirectory, $"result/GA_Experiment_{DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss")}.csv");
		List<TestValues> testValues = new List<TestValues>();

		for (int i = 0; i < 3; i++)
		{
			List<Job> sortedJobList = new List<Job>();

			Schedule scheduleGa = new Schedule(maxSlots);
			GA ga = new GA();
			ga.populationSize= populationSize;
			Stopwatch gaTimer = new Stopwatch();

			gaTimer.Start();
			List<TimeSlot> timeSlotsGA = new List<TimeSlot>(); // only here so that i can test ga, might not even be necesery  

			scheduleGa = ga.ScheduleJobs(jobsGA, batcheGroupsGA, timeSlotsGA);   
			gaTimer.Stop();

			foreach (TimeSlot timeSlot in scheduleGa.TimeSlots)
			{
				sortedJobList.Add(timeSlot.Job);
			}
			DataExporter.ExportSchedule(scheduleGa, $"JP_{i}_TestScheduel_{DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss")}");

			GA sortedFitGA = new GA();
			double sortedScore = sortedFitGA.SortedScheduleFitnessCheck(sortedJobList, batcheGroupsGA, timeSlotsGA); 
			testValues.Add(new TestValues(sortedScore, gaTimer.ElapsedMilliseconds));
		}

		using (var writer = new StreamWriter(csvPath))
		{
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(testValues);
			}
		}
	}

	public static void RunCP(int times, int timeout = 0)
	{
		DataImport import = new DataImport("data.xlsx");
		List<BatchGroup> batcheGroupsCP = import.BatchGroups;
		List<Job> jobsCP = import.Jobs;

		//3.A - Run data against CP
		List<TimeSlot> timeslots = new List<TimeSlot>();
		for (int i = 0; i < 50; i++)
		{
			timeslots.Add(new TimeSlot(i));
		}
		IScheduler cp = new CP();
		(cp as CP).Timeout = timeout;

		List<string> timesElapsed = new List<string>();
		for (int i = 0; i < times; i++)
		{
			Console.WriteLine($"----------Constraint Programming Solution {i}---------");
			Console.WriteLine(DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss") + $"\nTimeout: {timeout}");
			Stopwatch timer = new Stopwatch();
			timer.Start();
			Schedule scheduleCP = cp.ScheduleJobs(jobsCP, batcheGroupsCP, timeslots);
			timer.Stop();
			TimeSpan timeTaken = timer.Elapsed;
			Console.WriteLine($"Time: {timeTaken.ToString(@"m\:ss\.fffff")}");
			Console.WriteLine($"----------Printing CP_Schedule_{i}.xlsx---------");
			DataExporter.ExportSchedule(scheduleCP, $"CP_Schedule_{i}_{DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss")}");
			timesElapsed.Add($"CP_{i}: {timeTaken.ToString(@"m\:ss\.fffff")}");
			RunFitness(scheduleCP, batcheGroupsCP);
		}

		using (FileStream fs = File.Create($"result/CP_Experiment_{DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss")}.txt"))
		{
			fs.Write(new UTF8Encoding(true).GetBytes($"Timeout: {timeout / 60 / 60}\n"));
			foreach (string str in timesElapsed)
			{
				fs.Write(new UTF8Encoding(true).GetBytes(str + "\n"));
			}
		}
	}
}
