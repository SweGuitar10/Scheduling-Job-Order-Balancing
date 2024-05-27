namespace thesis_project;

public enum Priority
{
	HIGH_PRIO,
	NORMAL,
	LOW_PRIO
}

public class BatchGroup
{
	public string BatchGroupId { get; private set; }
	public string CustomerId { get; private set; }

	public int BatchQuantity { get; private set; } 

	public Priority Priority { get; private set; }

	public int Cooldown { get; set; } // prefered distance to next instance of job

	public int Weight { get; private set; }

	public string BatchOrder { get; private set; }

	public string Rule { get; private set; }

	List<Job> jobs;
	
	public BatchGroup(string batchGrId, string custId, int batchQuant,
		int weight, string batchOrder, string rule,
		Priority prio = Priority.NORMAL, int bestDistance = 0, int maxDistance = 0, int cooldown = 1)
	{
		BatchGroupId = batchGrId;
		CustomerId = custId;
		BatchQuantity = batchQuant;
		Priority = prio;
		jobs = new List<Job>();
		Cooldown = cooldown;
		Weight = weight;
		BatchOrder = batchOrder;
		Rule = rule;

	}

	public void OrderBatchContent(bool isDescending = false)
	{
		if (!isDescending)
			jobs = jobs.OrderBy(j => j.CustomerDeliverySequence).ToList();
		else
			jobs = jobs.OrderByDescending(j => j.CustomerDeliverySequence).ToList();
	}

	public void AddJobb(Job job)
	{
		jobs.Add(job);
	}

	public string JobsToString()
	{
		return string.Join("\n", jobs);
	}

	public double GetWeightFraction(double d)
	{
		return d * Weight;
	}

	public override string ToString()
	{
		return BatchGroupId;
	}
	public List<Job> GetJobs()
	{
		return jobs;
	}
}
