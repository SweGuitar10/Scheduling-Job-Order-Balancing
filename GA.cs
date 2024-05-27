using System;
using GeneticSharp;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Diagrams;

namespace thesis_project
{
	internal class GA : IScheduler
	{
		public List<Job> Jobs { get; set; }
		public List<BatchGroup> BatchGroups { get; set; }
		public List<TimeSlot> TimeSlots { get; set; }
		public List<Batch> Batches { get; set; }
		public int populationSize { get; set; }

		public double SortedScheduleFitnessCheck(List<Job> jobs, List<BatchGroup> groupBatches, List<TimeSlot> timeSlots) 
		{
			Jobs = jobs;
			BatchGroups = groupBatches;
			Batches = new List<Batch>();
			BuildBatches();
			ScheduleChromosome adamChromosome = new ScheduleChromosome(jobs, Batches, groupBatches, true);
			ScheduleFitness fitness = new ScheduleFitness();
			double schedScore = fitness.Evaluate(adamChromosome);
			return schedScore; 
		}
		public double SortedScheduleFitnessCheck(Schedule schedule, List<BatchGroup> batchGroups)
		{
			List<Job> jobs = schedule.GetAllJobs();
			Jobs = jobs;
			BatchGroups = batchGroups;
			Batches = new List<Batch>();
			BuildBatches();
			ScheduleChromosome adamChromosome = new ScheduleChromosome(jobs, Batches, batchGroups, true);
			ScheduleFitness fitness = new ScheduleFitness();
			return fitness.Evaluate(adamChromosome);
		}
		
		public Schedule ScheduleJobs(List<Job> jobs, List<BatchGroup> groupBatches, List<TimeSlot> timeSlots) 
		{
			Jobs = jobs;
			BatchGroups = groupBatches;
			Batches = new List<Batch>();
			BuildBatches();

			Schedule schedule = new Schedule(jobs.Count);

			ScheduleChromosome adamChromosome = new ScheduleChromosome(jobs, Batches, groupBatches);

			Population population = new Population(populationSize, populationSize, adamChromosome);
			population.GenerationStrategy = new PerformanceGenerationStrategy();
			ScheduleFitness fitness = new ScheduleFitness();
			var selection = new TournamentSelection(2);
			var crossover = new OrderBasedCrossover();  // used in schedule problems see Genetic algorithm wiki
			var mutation = new TworsMutation();
			//FitnessStagnationTermination termination = new FitnessStagnationTermination(400); // can be used for shorter executions, but is less accurate
			TimeEvolvingTermination termination = new TimeEvolvingTermination(TimeSpan.FromMinutes(5));
			ParallelTaskExecutor parallelTaskExecutor = new ParallelTaskExecutor();
			parallelTaskExecutor.MinThreads = 300;
			parallelTaskExecutor.MaxThreads = 300;

			GeneticAlgorithm ga = new GeneticAlgorithm(
										population,
										fitness,
										selection,
										crossover,
										mutation
										);

			ga.MutationProbability = 0.75f;  // this changes the mutation rate
			ga.CrossoverProbability = 0.7f; 
			ga.OperatorsStrategy = new TplOperatorsStrategy();
			ga.Termination = termination;
			ga.TaskExecutor = parallelTaskExecutor;
			

			Console.WriteLine("Generation: 1");

			var latestFitness = 0.0;
			
			
			
			ga.GenerationRan += (sender, e) =>
			{
				var bestChromosome = ga.BestChromosome as ScheduleChromosome;
				var bestFitness = bestChromosome.Fitness.Value;

				if (bestFitness != latestFitness)
				{
					latestFitness = bestFitness;

					Console.WriteLine(
						"Generation {0}:  = {1}",
						ga.GenerationsNumber,
						bestFitness
					);
				}
			};

			Console.WriteLine("GA running...");

			ga.Start();
			Console.WriteLine("Best solution found has {0} fitness. generations run: {1}", ga.BestChromosome.Fitness, ga.GenerationsNumber);

			Gene[] genes = ga.BestChromosome.GetGenes();

			for (int i = 0; i < genes.Length; i++)
			{
				schedule.AddJob(genes[i].Value as Job, i);
			}

			return schedule;
		}

		private void BuildBatches()
		{
			List<Job> currentJobs = Jobs.OrderBy(x => x.ProductionOrderID).ToList();

			// 1. Foreach job in jobs
			// 2. Extract: BatchGroup(s), Cooldown, BatchQuantity
			// 3. Add to new Batch

			// "BGXX", {job1, job2,...}
			Dictionary<string, IEnumerable<Job>> batchJobPair = new Dictionary<string, IEnumerable<Job>>();

			// Find all jobs sorted by BGXX
			foreach (Job job in currentJobs)
			{
				string bg = string.Join(",", job.BatchGroupId);

				if (!batchJobPair.ContainsKey(bg))
				{
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
				int cooldown = 1;
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
					if (Priority.HIGH_PRIO == group.Priority)
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
	}
}
