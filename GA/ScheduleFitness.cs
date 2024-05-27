using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Spreadsheet;
using GeneticSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace thesis_project;

internal class ScheduleFitness : IFitness
{
	public List<BatchGroup> BatchGroups { get; private set; }
	public List<Batch> Batches { get; private set; }
	public Dictionary<string, Dictionary<int, Batch>> BatchesInBatchGroups { get; private set; }

	// Forced part of the Interface, converts IChromosome to ScheduelChromosome 
	public double Evaluate(IChromosome chromosome)
	{
		ScheduleChromosome convertedChromosome = chromosome as ScheduleChromosome;
		double check = Evaluate(convertedChromosome);
		return check;
	}

	
	// there is NO template for the fitness function 
	public double Evaluate(ScheduleChromosome chromosome)
	{
		BatchGroups = chromosome.BatchGroups;
		Batches = chromosome.Batches;
		BatchesInBatchGroups = batchChunking();

		double f1;
		double n1 = 0;
		double n2 = 0;
		double n3 = 0;
		double n4 = 0;
		double n5 = 0;
		double n6 = 0;

		n1 = n1 + checkOrder(chromosome);                       // (numberOfViolations, weight)^2 
		n2 = n2 + checkKeepTogetherRule(chromosome);            // (numberOfViolations, weight)^2
		n3 = n3 + checkHighestPriorityPlacement(chromosome);    // (numberOfViolations, weight)^2
		n4 = n4 + checkLowestPriorityPlacement(chromosome);     // (numberOfViolations, weight)^2
		n5 = n5 + checkSpreadEvenlyRule(chromosome);            // lowPenelty = numberOfViolations, weight 
																// highPenelty = (numberOfViolations, weight)^2 
		n6 = n6 + checkBatchOrder(chromosome);                  // checkRepeatingPattern = (numberOfViolations, 10)^2
																// checkBatchesPlacedInASCOrder = (numberOfViolations, 50)^2

		f1 = n1 + n2 + n3 + n4 + n5 + n6;

		return f1;
	}

	// Builds a Dictionery that keeps track of the instances of batches of the same type
	private Dictionary<string, Dictionary<int, Batch>> batchChunking()
	{
		Queue<Batch> batchQueue = new Queue<Batch>();

		Dictionary<string, Dictionary<int, Batch>> batchesOfSameType = new Dictionary<string, Dictionary<int, Batch>>();

		batchQueue = placeBatchesInQueue(Batches);

		int queueSize = batchQueue.Count;

		for (int i = 0; i < queueSize; i++)
		{
			Batch batch1 = batchQueue.Dequeue();
			string batchGroupIDs = extractBatchGroupIDs(batch1);

			if (!batchesOfSameType.ContainsKey(batchGroupIDs))
			{
				Dictionary<int, Batch> batchList = new Dictionary<int, Batch>();
				batchList.Add(0, batch1);
				batchesOfSameType.Add(batchGroupIDs, batchList);
			}


			if (batchQueue.Count != 0)
			{
				Batch batch2 = batchQueue.Peek();

				if (isBatchSameType(batch1, batch2))
				{
					// start value 0, if an element already exist index for new = 1, if two = 2
					int batchIndex = batchesOfSameType[batchGroupIDs].Count;
					batchesOfSameType[batchGroupIDs].Add(batchIndex, batch2);
				}
			}
		}

		return batchesOfSameType;
	}
	
	// Compares two batches to see if they are of the same type (i.e. if they belong to the exact same batchGroups)
	private bool isBatchSameType(Batch batch1, Batch batch2)
	{
		string id1 = extractBatchGroupIDs(batch1);
		string id2 = extractBatchGroupIDs(batch2);
		if (id2.Equals(id1))
		{
			return true;
		}
		return false;
	}
	
	// checks how well the jobs in a batch are placed in an ASC order 
	// Violation calculation (numberOfViolations, weight)^2
	private double checkOrder(ScheduleChromosome chromosome)
	{
		int numberOfViolations = 0;
		int exponent = 2;
		int weight = 1;
		List<int> violationSum = new List<int>();
		Queue<Job> currentJobOrder = new Queue<Job>();

		foreach (Batch batch in Batches)
		{
			// Places every job from the batch/batchGroup that were found in the
			// list of all jobs, into a Queue in the order of FIFO. 
			for (int i = 0; i < chromosome.Length; i++)
			{
				foreach (Job job in batch.Jobs)
				{
					if ((chromosome.GetGene(i).Value as Job).Equals(job))
					{
						currentJobOrder.Enqueue(job);
					}
				}
			}
			violationSum.Add(checkBatchSortOrder(batch.Jobs, currentJobOrder));
			currentJobOrder.Clear();
		}

		numberOfViolations = violationSum.Sum(x => x);

		return -QuadraticPenaltyCalculation(numberOfViolations, weight);
	}
	
	// Compares current order of Jobs against expected order of jobs
	private int checkBatchSortOrder(List<Job> jobs, Queue<Job> currentJobOrder)
	{
		int numberOfViolations = 0;

		// Original batch order is presumed to be ordered 
		for (int i = 0; i < jobs.Count; i++)
		{
			string job1 = currentJobOrder.Dequeue().ToString();
			string job2 = jobs[i].ToString();

			if (!job1.Equals(job2))
			{
				numberOfViolations++;
			}
		}

		return numberOfViolations;
	}

	// Checks how well the repeating patterns of the batch pattern is followed 
	/*--------------------------------------------------
	 * Repeating patterns containe more then one batchgroup, where a batch from each batch group is placed
	 * in the schedule before staring from the first batch group again. This continues untill there are no more batches left.
	 * If a batchgroup has a higher number of batches then the other batchgroups, then the final batches might only represent by this batchgroup.
	 * -------------------------------------------------*/
	private int checkRepeatingPattern(ScheduleChromosome chromosome)
	{
		List<int> numberOfViolationSum = new List<int>();
		Dictionary<string, Queue<Batch>> singleBatchGroupRepeatingPattern = new Dictionary<string, Queue<Batch>>();
		bool hasBatchGroupRepeatingPattern = false;

		foreach (BatchGroup batchGroup in BatchGroups)
		{
			bool hasParentBatchGroupRepeatingPattern = false;
			Dictionary<string, Queue<Batch>> repeatingGroupPattern = new Dictionary<string, Queue<Batch>>();

			// builds a Dictionery with all "Parent" batchGroups, and their associated batches 
			string batchOrderParent = batchGroup.BatchOrder + ".";

			foreach (BatchGroup batchGroup1 in BatchGroups)
			{
				int batchNumber = 1;  // might not be used
				
				Queue<Batch> repeatingGroupbatchQueue = new Queue<Batch>();
				
				if (batchGroup1.BatchOrder.Contains(batchOrderParent))
				{
					string combinedBatchGroupID = batchGroup.BatchGroupId + "," + batchGroup1.BatchGroupId; 
					
					foreach (KeyValuePair<int, Batch> batch in BatchesInBatchGroups[combinedBatchGroupID])  
					{
						repeatingGroupbatchQueue.Enqueue(batch.Value);
					} 

					repeatingGroupPattern.Add(combinedBatchGroupID, repeatingGroupbatchQueue);
					hasParentBatchGroupRepeatingPattern= true;
				}
			}

			if (hasParentBatchGroupRepeatingPattern)
			{
				int numberOfViolations = calculateRepeatingPatternViolations(chromosome, repeatingGroupPattern, batchGroup.BatchGroupId);
				numberOfViolationSum.Add(numberOfViolations);
			}

			// builds a Dictionery with all batchGroups not associated with "spread out" or High/Low prioirity rules, and their associated batches
			Queue<Batch> singleBatchGroupQueue = new Queue<Batch>();
			string singleBatchGroupParent = batchGroup.BatchGroupId;

			if (BatchesInBatchGroups.ContainsKey(singleBatchGroupParent))
			{
				bool wasHighOrLowPriority = true; 

				foreach (KeyValuePair<int, Batch> batch in BatchesInBatchGroups[singleBatchGroupParent])
				{
					BatchGroup currentBatchGroup = BatchGroups.Find(x => x.BatchGroupId.Equals(singleBatchGroupParent));
					Priority batchGroupPriority = currentBatchGroup.Priority;

					if ((!batchGroupPriority.Equals(Priority.LOW_PRIO)) &&
						(!batchGroupPriority.Equals(Priority.HIGH_PRIO)))
					{
						singleBatchGroupQueue.Enqueue(batch.Value);
						wasHighOrLowPriority = false;
					}
				}

				if (!wasHighOrLowPriority)
				{
					singleBatchGroupRepeatingPattern.Add(singleBatchGroupParent, singleBatchGroupQueue);
					hasBatchGroupRepeatingPattern = true;
				}
			}		
		}

		if (hasBatchGroupRepeatingPattern)
		{
			int numberOfViolations = calculateRepeatingPatternViolations(chromosome, singleBatchGroupRepeatingPattern, null);
			numberOfViolationSum.Add(numberOfViolations);
		}

		return numberOfViolationSum.Sum(x => x);
	}

	// Calculates how often the repeating pattern is violated
	// Can be performed on specifik "Parent" batch Groups, or a collection of "single" batchGroups
	private int calculateRepeatingPatternViolations(ScheduleChromosome chromosome, Dictionary<string, Queue<Batch>> repeatingGroupPattern, string batchGroupID)
	{
		Queue<Job> optimalJobOrder = new Queue<Job>();
		List<string> batchGroupIDList = new List<string>();

		// finds the highest number of queues within the dictionery 
		int highestNumberOfQueues = 0;

		foreach (KeyValuePair<string, Queue<Batch>> batchGroupsbatchQueues in repeatingGroupPattern)
		{
			int numberOfQueues = batchGroupsbatchQueues.Value.Count();

			if (highestNumberOfQueues < numberOfQueues)
			{
				highestNumberOfQueues = numberOfQueues;
			}
		}

		// fill upp the optimumJobOrder
		for (int i = 0; i < highestNumberOfQueues; i++)
		{
			foreach (KeyValuePair<string, Queue<Batch>> batchGroupsbatchQueues in repeatingGroupPattern)
			{
				// filles up the list with batchGroupIDs, is used when a collection of "single" batchGroups is checked 
				if (!batchGroupIDList.Contains(batchGroupsbatchQueues.Key))
				{
					batchGroupIDList.Add(batchGroupsbatchQueues.Key);
				}

				if (batchGroupsbatchQueues.Value.Count() > 0)
				{
					Batch batch = batchGroupsbatchQueues.Value.Dequeue();

					foreach (Job job in batch.Jobs)
					{
						optimalJobOrder.Enqueue(job);
					}
				}
			}
		}

		// Retrives the current order of the jobs for the relevant batch pattern 
		Queue<Job> chromosomeJobOrder = new Queue<Job>();
		if (batchGroupID != null)
		{
			chromosomeJobOrder = jobOrderFromSpecificGroup(chromosome, batchGroupID, null);
		} 
		else
		{
			chromosomeJobOrder = jobOrderFromSpecificGroup(chromosome, null, batchGroupIDList);
		}

		// Calculates the number of violations (how many jobs are in the wrong place)
		int numberOfJobs = chromosomeJobOrder.Count();
		int numberOfViolations = 0;

		for (int i = 0; i < numberOfJobs; i++)
		{
			Job job1 = optimalJobOrder.Dequeue();
			Job job2 = chromosomeJobOrder.Dequeue();
			List<string> job1BatchGroupIDs = job1.BatchGroupId;
			List<string> job2BatchGroupIDs = job2.BatchGroupId;
			int customerDeliverySequence1 = job1.CustomerDeliverySequence;  
			int customerDeliverySequence2 = job2.CustomerDeliverySequence;  


			if ((!job1BatchGroupIDs.SequenceEqual(job2BatchGroupIDs)) ||
				(!customerDeliverySequence1.Equals(customerDeliverySequence2)))
			{
				numberOfViolations++;
			}
		}

		return numberOfViolations;
	}

	// Makes a list with only jobs from a specified batch group, in the order they currently are in the chromosome 
	private Queue<Job> jobOrderFromSpecificGroup(ScheduleChromosome chromosome, string batchgroupID = null, List<string> batchGroupIDs = null)
	{
		Queue<Job> jobFromSameParentBatchGroup = new Queue<Job>();


		for (int i = 0; i < chromosome.Length; i++)
		{
			// collects a string containing all batchGroupIDs associated with the Job
			Job job = chromosome.GetGene(i).Value as Job;
			string jobGroupIDs = extractJobBatchGroupIDs(job);

			if (batchgroupID != null)
			{
				if (jobGroupIDs.Contains(batchgroupID))
				{
					jobFromSameParentBatchGroup.Enqueue(job);
				}
			} 
			else
			{
				foreach (string bGID in batchGroupIDs)
				{
					if (bGID.Equals(jobGroupIDs))
					{
						jobFromSameParentBatchGroup.Enqueue(job);
					}
				}
			}
		}
		return jobFromSameParentBatchGroup;
	}


	// Performes checks of the expected order patterns on the scheduel 
	 /*---------------------------------------------------------------------
	 * Violation calculation:
	 * checkRepeatingPattern = (numberOfViolations, 10)^2
	 * checkBatchesPlacedInASCOrder = (numberOfViolations, 50)^2
	 * --------------------------------------------------------------------*/
	private double checkBatchOrder(ScheduleChromosome chromosome)
	{
		int numberOfRepeatingPatternViolations = checkRepeatingPattern(chromosome);
		double totalRepeatingPatternPenelty = QuadraticPenaltyCalculation(numberOfRepeatingPatternViolations, 10);
	

		List<int> numberOfViolationsBatchASCOrder = new List<int>();
		int batchASCOrderViolationSum = 0;

		foreach (Dictionary<int, Batch> batchList in BatchesInBatchGroups.Values)
		{
			if (batchList.Count > 1)
			{
				numberOfViolationsBatchASCOrder.Add(checkBatchesPlacedInASCOrder(chromosome, batchList));
			}
		}

		batchASCOrderViolationSum = numberOfViolationsBatchASCOrder.Sum(x => x);
		double totalBatchASCOrderPenelty = QuadraticPenaltyCalculation(batchASCOrderViolationSum, 50); 

		return -( totalBatchASCOrderPenelty + totalRepeatingPatternPenelty );

	}


	
	// Extracts GroupIDs from a batch
	private string extractBatchGroupIDs(Batch batch)
	{
		List<string> ids = new List<string>();

		foreach (BatchGroup batchGroup in batch.BatchGroups)
		{
			ids.Add(batchGroup.BatchGroupId);
		}

		string batchIDs = string.Join(",", ids);

		return batchIDs;
	}

	// Extracts GroupIDs from a Job
	private string extractJobBatchGroupIDs(Job job)
	{
		List<string> ids = new List<string>();

		foreach (string batchGroupID in job.BatchGroupId)
		{
			ids.Add(batchGroupID);
		}

		string batchIDs = string.Join(",", ids);

		return batchIDs;
	}

		
	// Places all batches in a Queue in FIFO order
	private Queue<Batch> placeBatchesInQueue(List<Batch> batches)
	{
		Queue<Batch> batchQueue = new Queue<Batch>();

		foreach (Batch batch in batches)
		{
			batchQueue.Enqueue(batch);
		}

		return batchQueue;
	}

	// Checks that the Batches of same type are placed in an ASC order (jobs go from lowest in first batch and highest in last batch)
	private int checkBatchesPlacedInASCOrder(ScheduleChromosome chromosome, Dictionary<int, Batch> batchList)
	{
		List<int> violationSum = new List<int>();

		Dictionary<int, int[]> batches = new Dictionary<int, int[]>();

		int batchNumber = 0;
		/* ------------------------------------------------------------------------
		 * Collects the index of the fist and last instance of a job from a batch.
		 * [i] first braket: specifies which batch
		 * [i] second braket: 0 = first, 1 = last
		 --------------------------------------------------------------------------*/
		foreach (Batch batch in batchList.Values)
		{
			int[] startAndEndIndex = new int[2];
			bool firstIndexSet = false;
			bool secondIndexSet = false;

			for (int i = 0; i < chromosome.Length; i++)
			{
				Job job = chromosome.GetGene(i).Value as Job;
				List<string> batchGroupIDs2 = job.BatchGroupId;

				if (firstIndexSet.Equals(false) && doesJobExistInBatch(batch, job))
				{
					firstIndexSet = true;
					startAndEndIndex[0] = i;
				}
				else if (doesJobExistInBatch(batch, job))
				{
					startAndEndIndex[1] = i;
				}
			}
			batches.Add(batchNumber, startAndEndIndex);
			batchNumber++;
		}


		// If the second batch's first instance has a lower index then the last instance
		// of the first batch, then that would mean that the second batch overlaps with the firt batch
		int numberOfViolations = 0; 
		
		for (int i = 0; i < batches.Count; i++)
		{
			int firstBatchLastIndex = batches[0][1];
			int secondBatchFistIndex = batches[1][0];

			numberOfViolations = secondBatchFistIndex - firstBatchLastIndex; 

			if (numberOfViolations < 0) 
			{ 
				numberOfViolations *= -1;
				violationSum.Add(numberOfViolations);
			}
		}

		int totalViolations = violationSum.Sum(x => x); 

		return totalViolations;
	}


	// Checks if a specifik job is part of supplied batch 
	private bool doesJobExistInBatch(Batch batch, Job job)
	{
		foreach(Job batchJob in batch.Jobs)
		{
			if (job.Equals(batchJob))
			{
				return true;
			}
		}
		return false;
	}

	private double LinearPenaltyCalculation(int numberOfViolations, double weight)
	{
		return numberOfViolations * weight;
	}

	private double QuadraticPenaltyCalculation(int numberOfViolations, int weight)
	{
		return Math.Pow(LinearPenaltyCalculation(numberOfViolations, weight),2);
	}


	// Initiates the Spread-out constraint check on relevant batches.

	/*-------------------------------------------------------------------------- 
	 * Penalties are handled differently depending on the severity of the spread.
	 *
	 * Violation calculation:
	 * lowPenelty = numberOfViolations, weight 
	 * highPenelty = (numberOfViolations, weight)^2 
	 *-----------------------------------------------------------------------------*/
	private double checkSpreadEvenlyRule(ScheduleChromosome chromosome)
	{
		string HIGH = "high";
		string LOW = "low";
		int exponent = 2;

		double totalBatchPenelty = 0;
		double totalBatchGroupPenelty = 0;

		List<double> batchPeneltyPointSum = new List<double>();
		List<double> batchGroupPeneltyPointSum = new List<double>();

		foreach (Batch batch in Batches)
		{
			foreach (BatchGroup batchGroup in batch.BatchGroups)
			{
				string batchGroupID = batchGroup.BatchGroupId;
				Priority batchPriority = batchGroup.Priority;

				// Any other batchGroup rules then "keep together" is not relevent if the batchGroup is low or high priority
				if (batchPriority.Equals(Priority.LOW_PRIO) || batchPriority.Equals(Priority.HIGH_PRIO))
				{
					break;
				}

				// checks if the rule applies to the specific groupBatchID
				if (checkIfSpreadEvenly(batchGroupID))
				{
					int weight = batchGroup.Weight;

					// calculate violations for the individual batches
					Dictionary<string, int> highLowViolationsNumber = checkBatchSpreadEvenly(chromosome, batch);
					double lowPenelty = LinearPenaltyCalculation(highLowViolationsNumber[LOW], weight);
					double highPenelty = QuadraticPenaltyCalculation(highLowViolationsNumber[HIGH], weight);

					batchPeneltyPointSum.Add(lowPenelty + highPenelty);

					// calculetes violations for the entire batchGroup
					highLowViolationsNumber.Clear();
					highLowViolationsNumber = checkBatchSpreadEvenly(chromosome, null, batchGroup);
					double lowBatchGroupPenelty = LinearPenaltyCalculation(highLowViolationsNumber[LOW], weight);
					double highBatchGroupPenelty = QuadraticPenaltyCalculation(highLowViolationsNumber[HIGH], weight);

					batchGroupPeneltyPointSum.Add(lowBatchGroupPenelty + highBatchGroupPenelty);
				}
			}
		}

		totalBatchPenelty = batchPeneltyPointSum.Sum(x => x);
		totalBatchGroupPenelty = batchGroupPeneltyPointSum.Sum(x => x);


		return -(totalBatchPenelty + totalBatchGroupPenelty);
	}


	// Checks if a specific batch Group has the rule Spread-Evenly applyed to it
	private bool checkIfSpreadEvenly(String groupID)
	{
		foreach (BatchGroup batchGroup in BatchGroups)
		{
			if (batchGroup.BatchGroupId.Equals(groupID) &&
				batchGroup.Rule.Contains("Spread evenly"))
			{
				return true;
			}
		}
		return false;
	}

	// Calculates number of violations of the constraints, and if it is a hard or soft violation 
	private Dictionary<string, int> checkBatchSpreadEvenly(ScheduleChromosome chromosome, Batch batch = null, BatchGroup batchGroup = null)
	{ 
		string HIGH = "high";
		string LOW = "low";

		List<Job> jobs = new List<Job>();
		int cooldown = 0;

		if (batch == null)
		{
			jobs = batchGroup.GetJobs();
			cooldown = batchGroup.Cooldown;
		}
		else
		{
			jobs = batch.Jobs;
			cooldown = batch.Cooldown;
		}

		double maxDistance = cooldown + 1;

		List<int> highViolations = new List<int>();
		List<int> lowViolations = new List<int>();

		Dictionary<string, int> singleInstanceViolations = new Dictionary<string, int>();
		Dictionary<string, int> highLowViolations = new Dictionary<string, int>();

		Queue<Job> currentJobOrder = new Queue<Job>();
		
		// Only performs the calculation if there are more then a single job 
		if(jobs.Count > 2) 
		{
			// Places every job from the batch/batchGroup that were found in the
			// list of all jobs, into a Queue in the order of FIFO. 
			for (int i = 0; i < chromosome.Length; i++)
			{
				foreach (Job job in jobs)
				{
					if ((chromosome.GetGene(i).Value as Job).Equals(job))
					{
						currentJobOrder.Enqueue(job);
					}
				}
			}

			// Gathers the number of violations regestered
			// Two jobs at a time, untill Queue only has a single job left
			int numberOfcurrentJobOrder = currentJobOrder.Count();

			for (int i = 0; i < numberOfcurrentJobOrder - 1; i++)
			{
				Job firstJob = currentJobOrder.Dequeue();
				Job SecondJob = currentJobOrder.Peek();

				singleInstanceViolations = calculateSpreadViolations(chromosome, firstJob, SecondJob, cooldown, maxDistance);

				if (singleInstanceViolations[HIGH] > 0)
				{
					highViolations.Add(singleInstanceViolations[HIGH]);
				}

				if (singleInstanceViolations[LOW] > 0)
				{
					lowViolations.Add(singleInstanceViolations[LOW]);
				}
				singleInstanceViolations.Clear();
			}

			int lowSum = lowViolations.Sum(x => x);
			int highSum = highViolations.Sum(x => x);

			highLowViolations.Add(LOW, lowSum);
			highLowViolations.Add(HIGH, highSum);
		}
		else // assures that there is no null values when returnd  
		{
			highLowViolations.Add(LOW, 0);
			highLowViolations.Add(HIGH, 0);
		}

		return highLowViolations;
	}

	// Calculates the number of violations of the "spread out" rule that are performed on a single instance of two jobs 
	private Dictionary<string, int> calculateSpreadViolations(ScheduleChromosome chromosome, Job firstJob, 
																	Job secondJob, int cooldown, double maxDistance)
	{
		string HIGH = "high";
		string LOW = "low";

		int indexOfJob1 = -1;

		int stepsToJobInSameBatch = 0;
		bool secondJobFound = false;
		int numberOfViolations = 0;

		List<int> highViolations = new List<int>();
		List<int> lowViolations = new List<int>();
		Dictionary<string, int> highLowViolations = new Dictionary<string, int>();
		Queue<int> firstLastIndex = new Queue<int>();


		// search for index of first and second instances of a jobs in current batch 
		firstLastIndex = firstLastPosition(firstJob, secondJob, chromosome);

		if (firstLastIndex.Count() > 1)
		{
			secondJobFound= true;
			indexOfJob1 = firstLastIndex.Dequeue();
			stepsToJobInSameBatch = firstLastIndex.Dequeue();
		}
		

		// if more then one instance of jobs found from current batch  
		if (secondJobFound)
		{
			// HARD RULE violation // soft rule HIGH constraint weight
			if ((stepsToJobInSameBatch < cooldown) || (stepsToJobInSameBatch > maxDistance))
			{
				numberOfViolations = cooldown - stepsToJobInSameBatch;    
			
				if (numberOfViolations < 0)
				{
					numberOfViolations = numberOfViolations * -1;
				}
				highViolations.Add(numberOfViolations);
			}
			// SOFT RULE violation // soft rule LOW constraint weight
			else
			{
				numberOfViolations = stepsToJobInSameBatch - cooldown;
				lowViolations.Add(numberOfViolations);
			}
		}

		int lowSum = lowViolations.Sum(x => x);
		int highSum = highViolations.Sum(x => x);

		highLowViolations.Add(LOW, lowSum);
		highLowViolations.Add(HIGH, highSum);

		return highLowViolations;
	}



	// Checkes how well the Keep-together rule is followed on batches where it applies 
	// Violation calculation (numberOfViolations, weight)^2
	private double checkKeepTogetherRule(ScheduleChromosome chromosome)
	{
		int totalPenelty = 0;
		List<double> penaltyValues = new List<double>();
		List<double> numberOfViolationsList = new List<double>();

		foreach (Batch batch in Batches)
		{
			
			foreach (BatchGroup batchGroup in batch.BatchGroups)
			{
				string batchGroupID = batchGroup.BatchGroupId;
				Priority batchPriority = batchGroup.Priority;

				// checks if the rule applies to the specific groupBatchID
				if (checkIfKeepTogether(batchGroupID))
				{
					int weight = batchGroup.Weight;
					int numberOfViolations = checkBatchKeepTogether(chromosome, batch);
					numberOfViolationsList.Add(numberOfViolations);
					penaltyValues.Add(QuadraticPenaltyCalculation(numberOfViolations, weight));

					// Any other batchGroup rules is not relevent if the batchGroup is low or high priority
					if (batchPriority.Equals(Priority.LOW_PRIO) || batchPriority.Equals(Priority.HIGH_PRIO))
					{
						break;
					}
				}
			}
		}
		
		double penaltySum = penaltyValues.Sum(x => x);
		
		return -penaltySum;
	}
	
	// Checks if a specific batch Group has the rule "Keep Together" applyed to it
	private bool checkIfKeepTogether(String groupID)
	{
		foreach(BatchGroup batchGroup in BatchGroups)
		{
			if (batchGroup.BatchGroupId.Equals(groupID) && 
				batchGroup.Rule.Contains( "Keep together"))
			{
				return true;
			}
		}
		return false; 
	}
	
	// Calculates the number of violations of the "Keep Together" rule on a batch basis.
	private int checkBatchKeepTogether(ScheduleChromosome chromosome, Batch batch)
	{

		List<Job> jobs = batch.Jobs;
		Queue<Job> currentJobOrder = new Queue<Job>();
		Queue<int> firstLastIndex = new Queue<int>();

		int cooldown = batch.Cooldown;

		int bestDistance = 0;
		bestDistance = batch.BestDistance;		
		
		int startIndex = 0;
		int stepsToEndIndex = 0;

		int numberOfViolations = 0;

		if (batch.Jobs.Count > 1)
		{
			// Collects the jobs from the specified batch, in the order they are found in the chromosome  
			for (int i = 0; i < chromosome.Length; i++)
			{
				foreach (Job job in jobs)
				{
					if ((chromosome.GetGene(i).Value as Job).Equals(job))
					{
						currentJobOrder.Enqueue(job);
					}
				}
			}
						
			Job firstJob = currentJobOrder.Dequeue();
			Job lastJob = currentJobOrder.Last();

			// Finds the index of the first job and how many indexes away the last instance of a job from current batch is 
			firstLastIndex = firstLastPosition(firstJob, lastJob, chromosome);
			
			startIndex = firstLastIndex.Dequeue();
			stepsToEndIndex = firstLastIndex.Dequeue();
		}
		
		// Handles instances when a batch has less then 2 jobs  
		if (stepsToEndIndex != 0)
		{
			numberOfViolations = stepsToEndIndex - bestDistance;
		}
		else
		{
			numberOfViolations = stepsToEndIndex;
		}

		if (numberOfViolations < 0) { numberOfViolations *= -1; }

		return numberOfViolations;
	}

	// Finds the index of the first job and how many indexes away the second job is from the first job 
	private Queue<int> firstLastPosition(Job job1, Job job2, ScheduleChromosome chromosome)
	{
		Queue<int> firstLastPosition = new Queue<int>();
		int indexOfJob1 = -1;
		int stepsToJob2 = 0; 
		
		// search for first instance of a job in current batch 
		for (int i = 0; i < chromosome.Length; i++)
		{
			if ((chromosome.GetGene(i).Value as Job).Equals(job1))
			{
				indexOfJob1 = i;
				break;
			}
		}

		// search for instance of second job (if it exist) 
		for (int j = indexOfJob1 + 1; j < chromosome.Length - 1; j++)
		{
			stepsToJob2++;

			if ((chromosome.GetGene(j).Value as Job).Equals(job2))
			{
				break;
			}
		}

		firstLastPosition.Enqueue(indexOfJob1);
		firstLastPosition.Enqueue(stepsToJob2);

		return firstLastPosition;
	}

	// checks if job1 >= job2 
	private int CustomerDeliverySequenceLowerThan(Job job1, Job job2)
	{
		if (job1.CustomerDeliverySequence >= job2.CustomerDeliverySequence)
		{
			return 1;
		}
		return 0;
	}

	// Checks if the highest priority jobs are placed first
	// violation calculation (numberOfViolations, weight)^2
	private double checkHighestPriorityPlacement(ScheduleChromosome chromosome)
	{
		
		int weight = 0;
		
		List<int> penaltyPointsSum = new List<int>();
		Priority priorityValue = Priority.HIGH_PRIO;

		// Finds the batchgroups with High Priority and records them and there rule weight
		foreach (BatchGroup batchGroup in BatchGroups)
		{
			int numberFromOptimum = 0;
			int numberOfHighPriority = 0;
			int indexOfLastHighPriority = 0;
			weight = batchGroup.Weight; 
			
			// only enters if High Priority
			if (batchGroup.Priority.Equals(priorityValue))
			{
				// registers the index of the last instance of job in a High Priority batchGroup
				for (int i = 0; i < chromosome.Length; i++)
				{
					Job job1 = chromosome.GetGene(i).Value as Job;
					List<String> job1BatchGroupIDs = job1.BatchGroupId;

					foreach (string groupID in job1BatchGroupIDs)
					{
						if (groupID.Equals(batchGroup.BatchGroupId))
						{
							indexOfLastHighPriority = i + 1;
							numberOfHighPriority++;
						}
					}
				}

				numberFromOptimum = indexOfLastHighPriority - numberOfHighPriority;
			}
			penaltyPointsSum.Add(numberFromOptimum);
		}

		int numberOfViolations = penaltyPointsSum.Sum(x => x);
		return -QuadraticPenaltyCalculation(numberOfViolations, weight);
	}


	// Checks if the lowest priority jobs are placed last
	// violation calculation (numberOfViolations, weight)^2
	private double checkLowestPriorityPlacement(ScheduleChromosome chromosome)
	{
		List<int> penaltyPointsSum = new List<int>();
		int weight = 0;
		Priority priorityValue = Priority.LOW_PRIO;

		// Finds the batchgroups with High Priority and records them and there rule weight
		foreach (BatchGroup batchGroup in BatchGroups)
		{
			int numberFromOptimum = 0;
			int numberOfLowPriority = 0;
			int indexOfFirstLowPriority = -1;
			weight = batchGroup.Weight;
			double batchGroupPenelty = 0.0;

			// only enters if High Priority
			if (batchGroup.Priority.Equals(priorityValue))
			{
				// registers the index of the last instance of job in a High Priority batchGroup
				for (int i = 0; i < chromosome.Length; i++)
				{
					Job job1 = chromosome.GetGene(i).Value as Job;
					List<String> job1BatchGroupIDs = job1.BatchGroupId;

					foreach (string groupID in job1BatchGroupIDs)
					{
						if (groupID.Equals(batchGroup.BatchGroupId))
						{
							if (indexOfFirstLowPriority == -1)
							{
								indexOfFirstLowPriority = i;
							}
							numberOfLowPriority++;
						}
					}
				}
				if (indexOfFirstLowPriority != -1)
				{
					int bestFirstPlacement = (chromosome.Jobs.Length - 1) - numberOfLowPriority;
					for (int i = indexOfFirstLowPriority; i < bestFirstPlacement + 1; i++)
					{
						numberFromOptimum++;
					}
				}
			}
			penaltyPointsSum.Add(numberFromOptimum);
		}

		int numberOfViolations = penaltyPointsSum.Sum(x => x);
		
		return -QuadraticPenaltyCalculation(numberOfViolations, weight);
	}
}
