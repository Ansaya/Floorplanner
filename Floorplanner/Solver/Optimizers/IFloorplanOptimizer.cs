using Floorplanner.Models.Solver;
using System.Threading;

namespace Floorplanner.Solver.Optimizers
{
    public interface IFloorplanOptimizer
    {
        Floorplan OptimizePlacement(Floorplan startingPlan, CancellationToken ct);
    }
}
