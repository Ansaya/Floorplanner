using Floorplanner.Models;
using Floorplanner.Models.Solver;

namespace Floorplanner.Solver.Reducers
{
    public interface IAreaReducer
    {
        void Reduce(Area area, Point idealCenter, Floorplan floorPlan);

        int GetCost(Area a, Costs c);
    }
}
