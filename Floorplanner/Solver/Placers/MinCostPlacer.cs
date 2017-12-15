using Floorplanner.Models.Solver;
using Floorplanner.Solver.Reducers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Floorplanner.Solver.Placers
{
    public class MinCostPlacer : IAreaPlacer
    {
        private readonly IAreaReducer _areaReducer;

        public MinCostPlacer(IAreaReducer areaReducer)
        {
            _areaReducer = areaReducer;
        }

        public void PlaceArea(Area area, Floorplan floorPlan, Point idealCenter)
        {
            IList<Point> availablePoints = new List<Point>(floorPlan.FreePoints);

            IList<Task<ReducerInfo>> areaReducers = new List<Task<ReducerInfo>>();

            // Get all valid areas among available points
            while(availablePoints.Count() > 0)
            {
                Point current = availablePoints.First();

                Area freeArea = new Area(area.FPGA, area.Region, current);

                PlacerHelper.Expand(freeArea, floorPlan);

                // Remove already covered points
                foreach (Point p in availablePoints.ToArray())
                    if (freeArea.Contains(p)) availablePoints.Remove(p);

                // Validate the area and check if there are sufficent resources
                // If validation isn't possible or left resources after validation
                // aren't enough continue searching for a valid area
                if (!PlacerHelper.TryValidatePR(freeArea) || !freeArea.IsSufficient)
                    continue;

                // If current area is eligible launch some reducing tasks
                Point[] areaMainP = new Point[]
                {
                    new Point(freeArea.TopLeft),
                    new Point(freeArea.TopLeft.X + freeArea.Width, freeArea.TopLeft.Y),
                    new Point(freeArea.TopLeft.X, freeArea.TopLeft.Y + freeArea.Height),
                    new Point(freeArea.TopLeft.X + freeArea.Width, freeArea.TopLeft.Y + freeArea.Height),
                    new Point(freeArea.Center)
                };

                foreach(Point p in areaMainP)
                {
                    areaReducers.Add(Task.Factory.StartNew(info =>
                    {
                        ReducerInfo ri = (ReducerInfo)info;

                        _areaReducer.Reduce(ri.Area, ri.Center, ri.Floorplan);

                        ri.Cost = _areaReducer.CostFunction(ri.Area, ri.Floorplan.Design.Costs);

                        return ri;
                    }, new ReducerInfo()
                    {
                        Area = new Area(freeArea),
                        Center = p,
                        Floorplan = floorPlan
                    }));
                }
            }

            // If no reducer task has been scheduled, there is no suitable area for current region
            if(areaReducers.Count == 0)
                throw new OptimizationException("Can't place an area in current floorplan. Sorry for the inconvenience.");

            // Wait for all area reducer tasks to complete
            Task.WaitAll(areaReducers.ToArray());

            // Compare all results and get less costly one
            Area bestFound = areaReducers
                .Select(r => r.Result)
                .Aggregate((r1, r2) => r1.Cost < r2.Cost ? r1 : r2)
                .Area;

            // Update effective area from method call
            area.Height = bestFound.Height;
            area.Width = bestFound.Width;
            area.MoveTo(bestFound.TopLeft);
        }

        private class ReducerInfo
        {
            public Area Area { get; set; }

            public int Cost { get; set; }

            public Point Center { get; set; }

            public Floorplan Floorplan { get; set; }
        }
    }
}
