namespace thesis_project;

internal interface IScheduler
{
	public List<Job> Jobs { get; set; }
	public List<BatchGroup> BatchGroups { get; set; }
	public List<TimeSlot> TimeSlots { get; set; }
	public List<Batch> Batches{ get; set; }
	public Schedule ScheduleJobs(List<Job> jobs, List<BatchGroup> batcheGroups, List<TimeSlot> timeSlots);

}
