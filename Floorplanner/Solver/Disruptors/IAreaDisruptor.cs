using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System.Collections.Generic;

namespace Floorplanner.Solver.Disruptors
{
    public interface IAreaDisruptor
    {
        void DisruptStateFor(Region region, IList<Area> notConfirmed, Floorplan floorPlan);
    }
}
