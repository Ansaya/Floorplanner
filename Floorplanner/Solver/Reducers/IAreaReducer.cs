using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;

namespace Floorplanner.Solver.Reducers
{
    public interface IAreaReducer
    {
        /// <summary>
        /// Function used to calculate area cost
        /// </summary>
        Func<Area, Costs, int> CostFunction { get; set; }

        /// <summary>
        /// Reduces a given area as much as possible calculating cost with CostFunction.
        /// Area with minimal cost is chosen.
        /// </summary>
        /// <param name="area">Area to reduce; it must be a valid and sufficient area for the associated region.</param>
        /// <param name="idealCenter">Ideal precalculated area center point.</param>
        /// <param name="floorPlan">Given area's associated floorplan object.</param>
        void Reduce(Area area, Point idealCenter, Floorplan floorPlan);
    }
}
