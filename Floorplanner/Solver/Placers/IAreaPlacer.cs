using Floorplanner.Models.Solver;

namespace Floorplanner.Solver.Placers
{
    public interface IAreaPlacer
    {
        void PlaceArea(Area area, Floorplan floorPlan, Point idealCenter);
    }
}
