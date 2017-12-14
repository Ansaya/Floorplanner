using Floorplanner.Models.Solver;
using System.Collections.Generic;

namespace Floorplanner.Solver.Disruptors
{
    public interface IAreaDisruptor
    {
        void DisruptStateFor(Area area, ref IList<Area> notConfirmed, Floorplan floorPlan);
    }
}
