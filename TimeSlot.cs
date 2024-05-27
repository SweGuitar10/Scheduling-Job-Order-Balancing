namespace thesis_project;

public class TimeSlot
{
	public int Slot { get; set; } // index in time table
	public Job Job
	{
		get; set;
	}
	private bool isOccupied;

	public TimeSlot(int index)
	{
		Slot = index;
		isOccupied = false;
	}
	public TimeSlot(int index, Job job)
	{
		Slot = index;
		Job = job;
		isOccupied = true;
	}
}
