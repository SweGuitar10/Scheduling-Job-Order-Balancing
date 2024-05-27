using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CsvHelper.Configuration;

namespace thesis_project; 

	
public class TestValues
{
	public double Score { get; set; }
	public long Milliseconds { get; set; }
	public TestValues(double score, long milliseconds) 
	{ 
		Score= score;
		Milliseconds= milliseconds;
	}
}
