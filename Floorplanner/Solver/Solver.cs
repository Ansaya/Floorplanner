using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Floorplanner.Solver
{
    public class Solver
    {
        public Design Design { get; private set; }

        public Solver(Design toPlan)
        {
            Design = toPlan;
        }

        public Floorplan Solve()
        {
            DistanceOptimizer dOpt = new DistanceOptimizer(Design);

            Point[] startingPoints = dOpt.GetOptimizedCenters();

            Floorplan bestPlanEver = new Floorplan(Design);

            // TODO: squeeze each area to get its center as near as possible to optimal computed center point
            //       ensuring feasibility and design boundaries are respected

            return bestPlanEver;
        }
    }
}
