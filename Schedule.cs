namespace thesis_project;

internal class Schedule
{
	public List<TimeSlot> TimeSlots { get; private set; }
	private int maxSlots;

	public Schedule(int maxSlots)
	{
		this.maxSlots = maxSlots;
		TimeSlots = new List<TimeSlot>(maxSlots);

		for (int i = 0; i < maxSlots; i++)
		{
			TimeSlots.Add(new TimeSlot(i));
		}

	}

	public Schedule(IEnumerable<Job> jobs)
	{
		maxSlots = jobs.Count();
		TimeSlots = new List<TimeSlot>();

		for (int i = 0; i < maxSlots; i++)
		{
			TimeSlots.Add(new TimeSlot(i, jobs.ElementAt(i)));
		}
	}

	public void AddJob(Job job, int slotIndex)
	{
		TimeSlots[slotIndex].Job = job;
	}

	public List<Job> GetAllJobs()
	{
		List<Job> jobs = new List<Job>();
		foreach(TimeSlot slot in TimeSlots)
		{
			jobs.Add(slot.Job);
		}
		return jobs;
	}
}
