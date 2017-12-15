using Floorplanner.Models.Solver;
using Floorplanner.Solver.Reducers;
using System;
using System.Linq;

namespace Floorplanner.Solver.Placers
{
    public class NearestCenterPlacer : IAreaPlacer
    {
        private readonly IAreaReducer _areaReducer;

        public NearestCenterPlacer(IAreaReducer areaReducer)
        {
            _areaReducer = areaReducer ??
                throw new ArgumentNullException("Area reducer must be set to an instance of an object.");
        }

        public void PlaceArea(Area area, Floorplan floorPlan, Point idealCenter)
        {
            NearestPointEnumerator nearestPoint = new NearestPointEnumerator(idealCenter, floorPlan.FreePoints);

            while (true)
            {

                // Find a suitable place to expand current area
                if(!nearestPoint.MoveNext())
                    throw new OptimizationException("Can't place an area in current floorplan. Sorry for the inconvenience.");

                area.Width = 0;
                area.Height = 0;
                area.MoveTo(nearestPoint.Current);

                // Expand area filling all available space
                PlacerHelper.Expand(area, floorPlan);

                Point[] heuristicCenters = new Point[]
                {
                    new Point(area.TopLeft),
                    new Point(area.TopLeft.X + area.Width, area.TopLeft.Y),
                    new Point(area.TopLeft.X, area.TopLeft.Y + area.Height),
                    new Point(area.TopLeft.X + area.Width, area.TopLeft.Y + area.Height),
                    new Point(area.Center)
                };

                // Remove all newly explored 
                nearestPoint.Skip(area);

                // Validate the area and check if there are sufficent resources
                // If validation isn't possible or left resources after validation
                // aren't enough continue searching for a valid area
                if (!PlacerHelper.TryValidatePR(area) || !area.IsSufficient)
                    continue;

                Area bestArea = heuristicCenters.AsParallel()
                    .Select((point, index) =>
                    {
                        Area a = new Area(area);

                        _areaReducer.Reduce(a, point, floorPlan);

                        return a;
                    })
                    .AsSequential()
                    .Aggregate((a1, a2) => 
                        _areaReducer.GetCost(a1, floorPlan.Design.Costs) < _areaReducer.GetCost(a2, floorPlan.Design.Costs) 
                        ? a1 : a2);

                area.Width = bestArea.Width;
                area.Height = bestArea.Height;
                area.MoveTo(bestArea.TopLeft);

                break;
            }
        }
    }
}
