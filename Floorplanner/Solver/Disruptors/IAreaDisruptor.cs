using Floorplanner.Models.Solver;
using System.Collections.Generic;

namespace Floorplanner.Solver.Disruptors
{
    public interface IAreaDisruptor
    {
        void DisruptStateFor(Area area, IList<Area> notConfirmed, Floorplan floorPlan);
    }
}
