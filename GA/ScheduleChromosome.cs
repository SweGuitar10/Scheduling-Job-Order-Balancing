using ClosedXML;
using GeneticSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace thesis_project;
internal class ScheduleChromosome : IChromosome
{
	public Job[] Jobs { get; set; }
	public List<BatchGroup> BatchGroups { get; set; }

	public List <Batch> Batches { get; set; }

	private Gene[] timeJobbGene;
	public double? Fitness { get; set; }
	public int Length => Jobs.Length;

	
	public ScheduleChromosome(List<Job> jobs, List<Batch> batches, List<BatchGroup> batchGroups )
	{
		BatchGroups = batchGroups;
		Batches = batches;
		List<Job> randomOrderJobs = jobs.OrderBy(x => Random.Shared.Next()).ToList();
		Jobs = randomOrderJobs.ToArray();
		ValidateLength(Length);
		timeJobbGene = new Gene[Length]; // fills the array with empty genes
				
		CreateGenes();
	}

	// handles instances when an already sorted list is to be scored 
	public ScheduleChromosome(List<Job> jobs, List<Batch> batches, List<BatchGroup> batchGroups, bool isSorted )
	{
		BatchGroups = batchGroups;
		Batches = batches;
		this.Jobs = jobs.ToArray();
		ValidateLength(Length);
		timeJobbGene = new Gene[Length]; // fills the array with empty genes

		CreateGenes();
	}

	// creates a clone of an entire chromosome
	public IChromosome Clone()
	{
		var clone = CreateNew();
		clone.ReplaceGenes(0, GetGenes());
		clone.Fitness = Fitness;

		return clone;
	}

	// checks to see if the if the specafied chromosome is equal to the current chromosome. 
	public override bool Equals(object obj)
	{
		var other = obj as IChromosome;

		if (other == null)
		{
			return false;
		}

		return CompareTo(other) == 0;
	}

	// Compares the fittnes of two chromosomes 
	public int CompareTo(IChromosome? other)
	{
		// there is no other chromosome 
		if (other == null)
		{
			return -1;
		}

		var otherFitness = other.Fitness;

		if (Fitness == otherFitness)
		{
			return 0;
		}

		return Fitness > otherFitness ? 1 : -1; 
	}

	// creates a new instance of this class 
	public IChromosome CreateNew()   
	{
		return new ScheduleChromosome(Jobs.ToList(), this.Batches, this.BatchGroups); 
	}

	// creates a new instance of this class 
	public ScheduleChromosome CreateNewScheduelChromosome()
	{
		return new ScheduleChromosome(Jobs.ToList(), Batches, this.BatchGroups);
	}

	// creates a gene at the specefied index
	protected void CreateGene(int index)
	{
		ReplaceGene(index, GenerateGene(index));
	}

	// creates all genes in the order of the job array
	protected void CreateGenes()
	{
		for (int i = 0; i < Length; i++)
		{
			ReplaceGene(i, GenerateGene(i));
		}
	}

	// creates a jobb gene 
	public Gene GenerateGene(int geneIndex)
	{
		return new Gene(Jobs[geneIndex]);
	}
	
	// Returns gene at specified index
	public Gene GetGene(int index)
	{
		return timeJobbGene[index];
	}

	// Returns all the genes (the array containing the genes)
	public Gene[] GetGenes()
	{
		return timeJobbGene;
	}

	// Replaces a gene on the specified index with suplied gene
	public void ReplaceGene(int index, Gene gene)
	{
		if (index < 0 || index >= Length)
		{
			throw new ArgumentOutOfRangeException(nameof(index), "There is no Gene on index {0} to be replaced.".With(index));
		}

		timeJobbGene[index] = gene;
		Fitness = null;
	}

	// Replaces multiple genes with suplied genes, starting from specified index
	public void ReplaceGenes(int startIndex, Gene[] genes)
	{
		ExceptionHelper.ThrowIfNull("genes", genes);

		if (genes.Length > 0)
		{
			if (startIndex < 0 || startIndex >= Length)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex), "There is no Gene on index {0} to be replaced.".With(startIndex));
			}

			Array.Copy(genes, 0, timeJobbGene, startIndex, Math.Min(genes.Length, Length - startIndex));

			Fitness = null;
		}
	}

	// chromosome will not be resized in this solution 
	public void Resize(int newLength)
	{
		throw new NotImplementedException();
	}

	// Validates the length of the Chromosome 
	private static void ValidateLength(int length)
	{
		if (length < 2)
		{
			throw new ArgumentException("The minimum length for a chromosome is 2 genes.", nameof(length));
		}
	}
}
