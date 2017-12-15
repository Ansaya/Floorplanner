using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;

namespace Floorplanner.Solver.Reducers
{
    public interface IAreaReducer
    {
        Func<Area, Costs, int> CostFunction { get; set; }

        void Reduce(Area area, Point idealCenter, Floorplan floorPlan);
    }
}
