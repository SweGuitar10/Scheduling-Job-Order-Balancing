using GeneticSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace thesis_project;

internal class Population : IPopulation
{
    public DateTime CreationDate => DateTime.Now;
    public IList<Generation> Generations { get; set; }
    public Generation CurrentGeneration { get; set; }
	public int GenerationsNumber { get; set; }
	public int MinSize { get; set; }
    public int MaxSize { get; set; }
    public IChromosome BestChromosome { get; set; }
    public IGenerationStrategy GenerationStrategy { get; set; }
	protected IChromosome AdamChromosome { get; set; }
	
	public event EventHandler BestChromosomeChanged;   // Occurs when best chromosome change

	// Constructor
	public Population(int minSize, int maxSize, IChromosome adamChromosome)
    {
		if (minSize < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(minSize), "The minimum size for a population is 2 chromosomes.");
		}

		if (maxSize < minSize)
		{
			throw new ArgumentOutOfRangeException(nameof(maxSize), "The maximum size for a population should be equal or greater than minimum size.");
		}

		ExceptionHelper.ThrowIfNull(nameof(adamChromosome), adamChromosome);

		MinSize = minSize;
		MaxSize = maxSize;
		AdamChromosome = adamChromosome;
		Generations = new List<Generation>();
		GenerationStrategy = new PerformanceGenerationStrategy(10);  // this version only keeps track of the previus generation created
																	 // if all generations are to be available use TrackingStrategy (it is slower though)
	}
	
	// Creates the initial population with the specified amount of chromosomes
    public void CreateInitialGeneration()
    {
		Generations = new List<Generation>();
		GenerationsNumber = 0;

		var chromosomes = new List<IChromosome>();

		for (int i = 0; i < MinSize; i++)
		{
			var c = AdamChromosome.CreateNew();

			if (c == null)
			{
				throw new InvalidOperationException("The Adam chromosome's 'CreateNew' method generated a null chromosome. This is a invalid behavior, please, check your chromosome code.");
			}

			c.ValidateGenes();

			chromosomes.Add(c);
		}

		CreateNewGeneration(chromosomes);
	}

	// Adds a newly created chromosome to the list of generations
    public void CreateNewGeneration(IList<IChromosome> chromosomes)
    {
		ExceptionHelper.ThrowIfNull("chromosomes", chromosomes);
		chromosomes.ValidateGenes();

		CurrentGeneration = new Generation(++GenerationsNumber, chromosomes);
		Generations.Add(CurrentGeneration);
		GenerationStrategy.RegisterNewGeneration(this);
	}


	// Seems to check which chromosome has the best fittnes though i still dont understand how.. 
    public void EndCurrentGeneration()  
    {
		CurrentGeneration.End(MaxSize);

		if (BestChromosome == null || BestChromosome.CompareTo(CurrentGeneration.BestChromosome) != 0)
		{
			BestChromosome = CurrentGeneration.BestChromosome;

			OnBestChromosomeChanged(EventArgs.Empty);
		}
	}

	// this chromosome seems to be related to the one above 
	protected virtual void OnBestChromosomeChanged(EventArgs args)
	{
		BestChromosomeChanged?.Invoke(this, args);
	}
}
