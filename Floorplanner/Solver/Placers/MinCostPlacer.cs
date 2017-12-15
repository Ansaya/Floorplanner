using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.Solver.Reducers;
using System;
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

                Expand(freeArea, floorPlan);

                // Remove already covered points
                foreach (Point p in availablePoints.ToArray())
                    if (freeArea.Contains(p)) availablePoints.Remove(p);

                // Validate the area and check if there are sufficent resources
                // If validation isn't possible or left resources after validation
                // aren't enough continue searching for a valid area
                if (!ShrinkToValidReconfigurable(freeArea) || !freeArea.IsSufficient)
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

                        ri.Cost = _areaReducer.GetCost(ri.Area, ri.Floorplan.Design.Costs);

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

        /// <summary>
        /// Shrink specified area until left and right sides reach a valid position for a reconfigurabla region.
        /// If the given area is a static one no action is performed.
        /// If the area isn't validated it is restored to initial dimesions and position
        /// </summary>
        /// <param name="a">Area to shrink until valid.</param>
        /// <returns>True if a valid area has been obtained, false else.</returns>
        private bool ShrinkToValidReconfigurable(Area a)
        {
            if (a.Type == RegionType.Reconfigurable)
            {
                FPGA fpga = a.FPGA;
                double oldTopLeftX = a.TopLeft.X;
                int oldWidth = a.Width;

                while (!fpga.LRecCol[(int)a.TopLeft.X]
                    && a.TryShape(Models.Solver.Action.Shrink, Direction.Left)) ;

                while (!fpga.RRecCol[(int)a.TopLeft.X + a.Width]
                    && a.TryShape(Models.Solver.Action.Shrink, Direction.Right)) ;

                // If couldn't validate the area restore old width and position
                if (!a.IsValid)
                {
                    a.TopLeft.X = oldTopLeftX;
                    a.Width = oldWidth;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Expand given area on specified floorplan until reaching maximum occupied surface without 
        /// overlapping with other areas or forbidden blocks. No checks on area validity is performed.
        /// </summary>
        /// <param name="a">Area to be expanded.</param>
        /// <param name="fPlan">Floorplan to refer to.</param>
        private void Expand(Area a, Floorplan fPlan)
        {
            Direction[] directions = (Direction[])Enum.GetValues(typeof(Direction));

            // Expand in each valid direction
            foreach (var d in directions)
            {
                // Expand till FPGA edge or other area overlap
                while (a.TryShape(Models.Solver.Action.Expand, d) && fPlan.CanPlace(a)) ;

                // Go back to last valid edge position if exited loop for
                // invalid area position
                if (!fPlan.CanPlace(a))
                    a.TryShape(Models.Solver.Action.Shrink, d);
            }
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
