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
            IList<Point> availablePoints = floorPlan.FreePoints.ToList();

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

                // If current area is eligible define some base points to reduce throw
                IEnumerable<Point> horizontalAnchors = 
                    GetHorizontalAnchors(freeArea.TopLeft.X, freeArea.TopLeft.X + freeArea.Width, freeArea.TopLeft.Y);

                // Reduce the area throw each of calculated points
                foreach (Point ha in horizontalAnchors)
                {
                    areaReducers.Add(Task.Factory.StartNew(TopBottomReduce, new ReducerInfo()
                    {
                        Area = new Area(freeArea),
                        TopCenter = ha,
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
        /// Reduce given area throw specified top center point then checks the same area placed 
        /// at the bottom of starting expanded area for a better cost and return less costly result.
        /// </summary>
        /// <param name="reducerInfo">Reducer info containing expanded area, related floorplan and top center point.
        /// After computation Area will be the reduced area and Cost the relative area cost computed with
        /// reducer CostFunction.</param>
        /// <returns>Best reduced area and cost.</returns>
        private ReducerInfo TopBottomReduce(object reducerInfo)
        {
            ReducerInfo ri = (ReducerInfo)reducerInfo;

            // Store area bottom for later
            int bottomY = (int)ri.Area.TopLeft.Y + ri.Area.Height;

            _areaReducer.Reduce(ri.Area, ri.TopCenter, ri.Floorplan);

            Area topA = ri.Area;

            // Move reduced area to the bottom of starting expanded area
            Area bottomA = new Area(ri.Area);
            bottomA.MoveTo(new Point(topA.TopLeft.X, bottomY - topA.Height)); // If exception thrown here there is a formal error in the code (not tested yet)

            // Check cost of top and bottom area and chose the best
            int topACost = _areaReducer.CostFunction(topA, ri.Floorplan);
            int bottomACost = _areaReducer.CostFunction(bottomA, ri.Floorplan);

            // If reduced area moved to the bottom cost less take it
            if (bottomACost < topACost)
            {
                ri.Area = bottomA;
                ri.Cost = bottomACost;
            }
            else
                ri.Cost = topACost;

            return ri;
        }

        /// <summary>
        /// Split given width in segments as long as inDistance value and returns the list of points
        /// </summary>
        /// <param name="left">Leftmost x value.</param>
        /// <param name="right">Rightmost x value.</param>
        /// <param name="y">Y constant value to use.</param>
        /// <param name="inDistance">Distance to put in between subsequent points.(Last point could be nearer to previous than this)</param>
        /// <returns></returns>
        private IEnumerable<Point> GetHorizontalAnchors(double left, double right, double y, int inDistance = 5)
        {
            double width = right - left;

            yield return new Point(left, y);

            if (width > inDistance * 2)
                for(int i = (int)(width / inDistance); i > 0; i--)
                {
                    left += inDistance;
                    yield return new Point(left, y);
                }

            yield return new Point(right, y);
        }

        private class ReducerInfo
        {
            public Area Area { get; set; }

            public int Cost { get; set; }

            public Point TopCenter { get; set; }

            public Floorplan Floorplan { get; set; }
        }
    }
}
