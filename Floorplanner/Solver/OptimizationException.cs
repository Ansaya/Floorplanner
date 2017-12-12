using System;

namespace Floorplanner.Solver
{
    public class OptimizationException : Exception
    {
        public OptimizationException()
        {
        }

        public OptimizationException(string message) : base(message)
        {
        }
    }
}
