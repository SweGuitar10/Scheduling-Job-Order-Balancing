using Google.OrTools.Sat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace thesis_project;

internal class CP : IScheduler
{
	public List<Job> Jobs { get; set; }
	public List<BatchGroup> BatchGroups { get; set; }
	public List<TimeSlot> TimeSlots { get; set; }
	public List<Batch> Batches { get; set; }

	public int Timeout { get; set; } = 0;

	int[] jobsArr;
	int[] timeSlotsArr;
	Dictionary<Tuple<int, int>, BoolVar> slots;

	const string RULE_SPREAD_EVENLY = "Spread evenly";
	const string RULE_KEEP_TOGETHER = "Keep together";

	public Schedule ScheduleJobs(List<Job> jobs, List<BatchGroup> batcheGroups, List<TimeSlot> timeSlots)
	{
		Jobs = jobs;
		BatchGroups = batcheGroups;
		TimeSlots = timeSlots;
		Batches = new List<Batch>();
		BuildBatches();

		jobsArr = Enumerable.Range(0, jobs.Count).ToArray();
		timeSlotsArr = Enumerable.Range(0, timeSlots.Count).ToArray();

		CpModel model = new CpModel();

		// Create Slots variables
		// slots[(slot,job)]: job j is scheduled on slot s
		slots = new Dictionary<Tuple<int, int>, BoolVar>();
		foreach (int slot in timeSlotsArr)
		{
			foreach (int job in jobsArr)
			{
				slots.Add(Tuple.Create(slot, job), model.NewBoolVar($"timeslots_slot{slot}_job{job}"));
			}
		}

		// Each slot has exactly one job assigned to it
		foreach (int slot in timeSlotsArr)
		{
			List<IntVar> x = new List<IntVar>();
			foreach (int job in jobsArr)
			{
				x.Add(slots[Tuple.Create(slot, job)]);
			}
			model.Add(LinearExpr.Sum(x) == 1);
		}

		// Each job is used at most in one slot
		foreach (int job in jobsArr)
		{
			List<IntVar> x = new List<IntVar>();
			foreach (int slot in timeSlotsArr)
			{
				x.Add(slots[Tuple.Create(slot, job)]);
			}
			model.Add(LinearExpr.Sum(x) <= 1);
		}

		// High Priority first
		IEnumerable<Batch> highPrioBatches = GetBatchesByPriority(Priority.HIGH_PRIO);
		List<int> highPrioJobsIndex = new List<int>();
		int usedJobsCount = 0, highPrioIndex = 0;

		foreach (Batch b in highPrioBatches)
		{
			foreach (Job j in b.Jobs)
			{
				int jobIndex = Jobs.IndexOf(j);
				if (jobIndex >= 0) { highPrioJobsIndex.Add(jobIndex); }
				usedJobsCount++;
				highPrioIndex++;
			}
		}

		// Each of the first N slots must prioritize HIGH_PRIO jobs
		int amountOfHighPrioJobs = highPrioBatches.Sum(x => x.Jobs.Count);
		for (int slot = 0; slot < amountOfHighPrioJobs; slot++)
		{
			List<BoolVar> highPrioX = new List<BoolVar>();
			foreach (int index in highPrioJobsIndex)
			{
				highPrioX.Add(slots[Tuple.Create(slot, index)]);
			}
			model.Add(LinearExpr.Sum(highPrioX) == 1);
		}

		// Low Priority last
		IEnumerable<Batch> lowPrioBatches = GetBatchesByPriority(Priority.LOW_PRIO);
		List<int> lowPrioJobsIndex = new List<int>();
		int lowPrioIndex = jobs.Count - 1;

		foreach (Batch b in lowPrioBatches)
		{
			foreach (Job j in b.Jobs)
			{
				int jobIndex = Jobs.IndexOf(j);
				if (jobIndex >= 0) { lowPrioJobsIndex.Add(jobIndex); }
				usedJobsCount++;
				lowPrioIndex--;
			}
		}

		// Each of the first N slots must prioritize HIGH_PRIO jobs
		int amountOfLowPrioJobs = lowPrioBatches.Sum(x => x.Jobs.Count);
		for (int slot = Jobs.Count - 1; slot > lowPrioIndex; slot--)
		{
			List<BoolVar> lowPrioX = new List<BoolVar>();
			foreach (int index in lowPrioJobsIndex)
			{
				lowPrioX.Add(slots[Tuple.Create(slot, index)]);
			}
			model.Add(LinearExpr.Sum(lowPrioX) == 1);
		}

		LinearExprBuilder obj = LinearExprBuilder.NewBuilder();

		// Cooldown between jobs of same BG
		// Find all [slots,jobs] entries for a BG at a time
		foreach (BatchGroup bg in BatchGroups)
		{
			int cooldown = bg.Cooldown;
			if (cooldown <= 1) { continue; }
			if (bg.Priority != Priority.NORMAL) { continue; }
			int penalty = bg.Weight;
			IEnumerable<Job> jobsInBg = Jobs.Where(x => x.BatchGroupId.Contains(bg.BatchGroupId));

			for (int j = 0; j < jobsInBg.Count(); j++)
			{
				int job1Index = GetJobIndex(jobsInBg.ElementAt(j));

				for (int slot1 = 0; slot1 < timeSlotsArr.Length - cooldown; slot1++)
				{
					List<BoolVar> work = new List<BoolVar>();
					BoolVar firstJob = slots[Tuple.Create(slot1, job1Index)];
					work.Add(firstJob);

					for (int j2 = 0; j2 < jobsInBg.Count(); j2++)
					{
						if (j2 == j) { continue; }

						int job2Index = GetJobIndex(jobsInBg.ElementAt(j2));
						for (int k = 1; k <= cooldown; k++)
						{
							work.Add(slots[Tuple.Create(slot1 + k, job2Index)]);
						}
					}

					var (variables, coeffs) = AddSoftSumConstraint(model, work.ToArray(),
						0, 1, 0, 1, jobsInBg.Count(), penalty,
						$"cooldown BG:{bg}, job1:{job1Index}, cooldown:{cooldown}, slot1:{slot1}");
					obj.AddWeightedSum(variables, coeffs);
				}
			}
		}

		// Spread Out jobs of the same batch over the day
		foreach (Batch batch in Batches)
		{
			List<BatchGroup> batchGroups = batch.BatchGroups;
			List<Job> batchJobs = batch.Jobs;

			// Check the weight of the BG's, get largest weight
			foreach (BatchGroup bg in batchGroups)
			{
				if (bg.Rule != RULE_SPREAD_EVENLY || batch.Jobs.Count <= 1 || bg.Priority != Priority.NORMAL) { continue; }
				// Get jobs in batch
				int batchSize = batch.Jobs.Count;
				// Get total slots
				int slotSize = timeSlotsArr.Length;
				// Ensure that jobs are spread out evenly
				int spreadSize = slotSize / batchSize - 1;
				int penalty = bg.Weight;

				for (int j = 0; j < batchJobs.Count(); j++)
				{
					int job1Index = GetJobIndex(batchJobs.ElementAt(j));

					for (int slot1 = 0; slot1 < timeSlotsArr.Length - spreadSize; slot1++)
					{
						List<BoolVar> work = new List<BoolVar>();
						work.Add(slots[Tuple.Create(slot1, job1Index)]);

						for (int j2 = 0; j2 < batchJobs.Count(); j2++)
						{
							if (j2 == j) { continue; }

							int job2Index = GetJobIndex(batchJobs.ElementAt(j2));
							for (int k = 1; k <= spreadSize; k++)
							{
								work.Add(slots[Tuple.Create(slot1 + k, job2Index)]);
							}
						}
						var (variables, coeffs) = AddSoftSumConstraint(model, work.ToArray(),
						0, 1, 0, 1, batchSize, penalty,
						$"Spread batch:{batch.Id}, job1:{job1Index}, spread:{spreadSize}, slot1:{slot1}");
						obj.AddWeightedSum(variables, coeffs);
					}
				}
			}
		}

		// Keep Together jobs of same batch
		foreach (Batch batch in Batches)
		{
			// Consider the slots from slot1 to slot1 + batch.BestDistance
			//	Foreach "overstep" increase the penalty factor
			List<BatchGroup> batchGroups = batch.BatchGroups;
			List<Job> batchJobs = batch.Jobs;

			// Check the weight of the BG's, get largest weight
			foreach (BatchGroup bg in batchGroups)
			{
				if (bg.Rule != RULE_KEEP_TOGETHER || bg.Priority != Priority.NORMAL) { continue; }
				// Get jobs in batch
				int batchSize = batchJobs.Count;
				// Get total slots
				int slotSize = timeSlotsArr.Length;
				// Value of max steps from starting job
				int bestDistance = batchSize;//batch.BestDistance;
				int penalty = bg.Weight;

				List<int> jobsToCheck = new List<int>();
				foreach (Job j in batchJobs)
				{
					jobsToCheck.Add(GetJobIndex(j));
				}

				for (int slot = 0; slot <= timeSlotsArr.Length - bestDistance; slot++)
				{
					List<BoolVar> x = new List<BoolVar>();
					for (int ji = 0; ji < batchJobs.Count; ji++)
					{
						int jobIndex = GetJobIndex(batchJobs[ji]);
						for (int count = 0; count < bestDistance; count++)
						{
							Tuple<int, int> key = Tuple.Create(slot + count, jobIndex);
							x.Add(slots[key]);
						}
					}
					int min = 0;
					int max = batchSize;
					IntVar amountOfTrue = model.NewIntVar(min, max, "");
					// amount of true values equals amount of true jobs within range [slot, slot + bestDistance]
					model.Add(LinearExpr.Sum(x) == amountOfTrue); 
					
					string name = $"keep_together_penalty(jobs: {string.Join(",", x)}, BG: {bg})";
					IntVar deficit = model.NewIntVar(0, max, name);
					model.Add(deficit == max - amountOfTrue); // deficit contains the amount of lacking true values.
					obj.AddTerm(deficit, penalty);
				}
			}
		}
		model.Minimize(obj);

		CpSolver solver = new CpSolver();

		if (Timeout > 0)
		{
			solver.StringParameters = $"max_time_in_seconds: {Timeout}.0"; 
		}
		CpSolverStatus status = solver.Solve(model);
		Console.WriteLine(solver.ResponseStats());
		Console.WriteLine(solver.SolutionInfo());

		using (FileStream fs = File.Create($"result/CP_{DateTime.Now.ToString("MM-dd-yyyy HH_mm_ss")}.txt"))
		{
			fs.Write(new UTF8Encoding(true).GetBytes(solver.ResponseStats()));
			fs.Write(new UTF8Encoding(true).GetBytes(solver.SolutionInfo()));
		}


		Console.WriteLine(model.Validate());
		List<Job> sortedJobs = ResponseToListJobs(solver);
		List<Job> sortedOrderedJobs = OrderScheduleByDeliverySequence(sortedJobs.ToArray()).ToList();

		return BuildSchedule(sortedOrderedJobs);
	}

	private Job[] OrderScheduleByDeliverySequence(Job[] jobs)
	{
		Job[] result = new Job[jobs.Length];
		// Sort batches orders by delivery sequence, (maybe Stack??)
		Dictionary<List<string>, PriorityQueue<Job, int>> batchJobLists = new Dictionary<List<string>, PriorityQueue<Job, int>>();
		List<List<string>> bgsList = new List<List<string>>();

		// Find BatchGroups combinations
		for (int i = 0; i < Jobs.Count; i++)
		{
			List<string> bg = Jobs[i].BatchGroupId;
			bool isPresent = false;
			foreach (List<string> bgs in bgsList)
			{
				if (Enumerable.SequenceEqual(bg, bgs))
				{
					isPresent = true;
					break;
				}
			}
			if (!isPresent) { bgsList.Add(bg); }
		}

		// Get Jobs by BG-combos
		foreach (List<string> bgs in bgsList)
		{
			PriorityQueue<Job, int> queue = new PriorityQueue<Job, int>();
			foreach (Job j in Jobs)
			{
				if (Enumerable.SequenceEqual(j.BatchGroupId, bgs))
				{
					queue.Enqueue(j, j.CustomerDeliverySequence);
				}
			}
			batchJobLists.Add(bgs, queue);
		}

		// Go through the schedule
		// Each job, find the first of the matching BG
		for (int i = 0; i < jobs.Length; i++)
		{
			// Find BG-combo
			foreach (List<string> bgs in batchJobLists.Keys)
			{
				if (Enumerable.SequenceEqual(bgs, jobs[i].BatchGroupId))
				{
					result[i] = batchJobLists[bgs].Dequeue();
					break;
				}
			}
		}
		return result;
	}

	private Schedule BuildSchedule(List<Job> sortedJobs)
	{
		return new Schedule(sortedJobs);
	}
	private bool CheckedJobs(bool[] checkedJobs, int[] indexes)
	{
		int checks = 0, amount = indexes.Length;
		foreach (int i in indexes)
		{
			if (checkedJobs[i])
				checks++;
		}
		return checks == amount;
	}
	private List<Job> ResponseToListJobs(CpSolver solver)
	{
		List<Job> sortedJobs = new List<Job>();
		if (solver.Response.Status == CpSolverStatus.Infeasible) { return sortedJobs; }

		foreach (int slot in timeSlotsArr)
		{
			foreach (int job in jobsArr)
			{
				Tuple<int, int> key = Tuple.Create(slot, job);
				if (solver.Value(slots[key]) == 1L)
				{
					sortedJobs.Add(GetJob(job));
				}
			}
		}
		return sortedJobs;
	}

	private void BuildBatches()
	{
		List<Job> currentJobs = Jobs;

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
				if (Priority.HIGH_PRIO == group.Priority /*|| Priority.LOW_PRIO == group.Priority*/) // TODO: Consider this?
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
	private Job GetJob(int index)
	{
		return Jobs.ElementAt(index);
	}
	private int GetJobIndex(Job job)
	{
		return Jobs.IndexOf(job);
	}

	private IEnumerable<Batch> GetBatchesByPriority(Priority prio)
	{
		List<Batch> batches = new List<Batch>();
		foreach (Batch batch in Batches)
		{
			foreach (BatchGroup bg in batch.BatchGroups)
			{
				if (bg.Priority == prio)
					batches.Add(batch);
			}
		}

		return batches;
	}


	/// <summary>
	/// Sum constraint with soft and hard bounds.
	/// This constraint counts the variables assigned to true from works.
	/// If forbids sum &lt; hardMin or &gt; hardMax.
	/// Then it creates penalty terms if the sum is &lt; softMin or &gt; softMax.
	/// </summary>
	/// <param name="model">The sequence constraint is built on this
	/// model.</param> <param name="works">A list of Boolean variables.</param>
	/// <param name="hardMin">Any sequence of true variables must have a length of
	/// at least hardMin.</param> <param name="softMin">Any sequence should have a
	/// length of at least softMin, or a linear penalty on the delta will be added
	/// to the objective.</param> <param name="minCost">The coefficient of the
	/// linear penalty if the length is less than softMin.</param> <param
	/// name="softMax">Any sequence should have a length of at most softMax, or a
	/// linear penalty on the delta will be added to the objective.</param> <param
	/// name="hardMax">Any sequence of true variables must have a length of at
	/// most hardMax.</param> <param name="maxCost">The coefficient of the linear
	/// penalty if the length is more than softMax.</param> <param name="prefix">A
	/// base name for penalty literals.</param> <returns>A tuple (costVariables,
	/// costCoefficients) containing the different penalties created by the
	/// sequence constraint.</returns>
	static (IEnumerable<IntVar> costVariables, IEnumerable<int> costCoefficients)
		AddSoftSumConstraint(CpModel model, BoolVar[] works, int hardMin, int softMin, int minCost, int softMax,
							 int hardMax, int maxCost, string prefix, bool negatePenalty = false)
	{
		var costVariables = new List<IntVar>();
		var costCoefficients = new List<int>();
		var sumVar = model.NewIntVar(hardMin, hardMax, "");
		// This adds the hard constraints on the sum.
		model.Add(sumVar == LinearExpr.Sum(works));

		var zero = model.NewConstant(0);

		// Penalize sums below the soft_min target.

		if (softMin > hardMin && minCost > 0)
		{
			var delta = model.NewIntVar(-works.Length, works.Length, "");
			model.Add(delta == (softMin - sumVar));
			var excess = model.NewIntVar(0, works.Length, prefix + ": under_sum");
			model.AddMaxEquality(excess, new[] { delta, zero });
			costVariables.Add(excess);
			costCoefficients.Add(negatePenalty ? -minCost : minCost);
		}

		// Penalize sums above the soft_max target.
		if (softMax < hardMax && maxCost > 0)
		{
			var delta = model.NewIntVar(-works.Length, works.Length, "");
			model.Add(delta == sumVar - softMax);
			var excess = model.NewIntVar(0, works.Length, prefix + ": over_sum");
			model.AddMaxEquality(excess, new[] { delta, zero });
			costVariables.Add(excess);
			costCoefficients.Add(maxCost);
		}

		return (costVariables, costCoefficients);
	}
	/// <summary>
	/// C# equivalent of Python range (start, stop)
	/// </summary>
	/// <param name="start">The inclusive start.</param>
	/// <param name="stop">The exclusive stop.</param>
	/// <returns>A sequence of integers.</returns>
	static IEnumerable<int> Range(int start, int stop)
	{
		foreach (var i in Enumerable.Range(start, stop - start))
			yield return i;
	}

	/// <summary>
	/// C# equivalent of Python range (stop)
	/// </summary>
	/// <param name="stop">The exclusive stop.</param>
	/// <returns>A sequence of integers.</returns>
	static IEnumerable<int> Range(int stop)
	{
		return Range(0, stop);
	}

	// https://stackoverflow.com/a/10630026
	static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
	{
		if (length == 1) return list.Select(t => new T[] { t });

		return GetPermutations(list, length - 1)
			.SelectMany(t => list.Where(e => !t.Contains(e)),
				(t1, t2) => t1.Concat(new T[] { t2 }));
	}
}
