using Floorplanner.Models.Solver;

namespace Floorplanner.Solver.RouteOptimizers
{
    public interface IRouteOptimizer
    {
        /// <summary>
        /// Update given array of centers with new values computed considering confirmed area
        /// effective center points.
        /// Center points in the array can be accessed by area id to bind them correctly.
        /// </summary>
        /// <param name="centers">Initialized centers array.</param>
        void GetOptimizedCenters(ref Point[] centers);
    }
}
