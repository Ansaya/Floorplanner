using System;
using System.IO;

namespace Floorplanner.Models.Solver
{
    public class SolverTuning
    {
        public int CaosFactor { get; set; } = 5;

        public int CaosVariance { get; set; } = 2;

        public int MaxOptIteration { get; set; } = 5;

        public double DisruptPerIteration { get; set; } = 0.5;

        public int MaxDisruption { get; set; } = 30;

        public int ResourceDisruptThreshold { get; set; } = 2;

        public int MaxConcurrent { get; set; } = Environment.ProcessorCount;

        public int MinDimension { get; set; } = 1;

        public void PrintValuesTo(TextWriter tw)
        {
            foreach(var p in GetType().GetProperties())
                tw.WriteLine($"\t{p.Name}: {p.GetValue(this).ToString()}");
        }
    }
}
