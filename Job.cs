namespace thesis_project;

public class Job
{
	public string ProductionOrderID { get; private set; } // unique identifier
	public string CustomerID { get; private set; }
	public int CustomerDeliverySequence { get; private set; }
	public List<string> BatchGroupId { get; private set; } // type 

	
	public Job(string prodId, string custId, int custDelSeq, List<string> batchGrId)
	{
		ProductionOrderID = prodId;
		CustomerID = custId;
		CustomerDeliverySequence = custDelSeq;
		BatchGroupId = batchGrId;
	}

	public override string ToString()
	{
		return $"Production Order ID: {ProductionOrderID}, Customer ID: {CustomerID}" +
			$"CustomerDeliverySequence: {CustomerDeliverySequence}, BatchGroupID: {string.Join(",", BatchGroupId)}";
	}
}