namespace thesis_project;

internal class Batch
{
	public List<BatchGroup> BatchGroups { get; set; } // Placed in order of importance
	public List<Job> Jobs { get; set; }
	public int Cooldown { get; private set; }
	public int BestDistance { get; private set; }
	public int Id { get; private set; }
	private static int lastId = 1;

	public Batch()
	{
		BatchGroups = new List<BatchGroup>();
		Jobs = new List<Job>();
		Id = lastId++;
	}

	public Batch(IEnumerable<BatchGroup> batchGroups, IEnumerable<Job> jobs, int cooldown)
	{
		BatchGroups = batchGroups.ToList();
		Jobs = jobs.ToList();
		Cooldown = cooldown;
		if(Jobs.Count() == 2)
		{
			// instances when there are only two jobs is an edge case handled here
			BestDistance = Jobs.Count() * Cooldown;
		}
		else
		{
			BestDistance = (Jobs.Count() * Cooldown) - Cooldown;
		}
		Id = lastId++;

	}
}
