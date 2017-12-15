using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Floorplanner.Models;
using Floorplanner.Models.Solver;

namespace Floorplanner.Solver.Reducers
{
    public class PRAreaReducer : IAreaReducer
    {
        public Func<Area, Costs, int> CostFunction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            // TODO: reduce area tile per tile


            throw new NotImplementedException();
        }
    }
}
