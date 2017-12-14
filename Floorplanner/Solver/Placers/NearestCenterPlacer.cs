using Floorplanner.Models;
using Floorplanner.Models.Components;
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

            while (!area.IsConfirmed)
            {

                // Find a suitable place to expand current area
                if(!nearestPoint.MoveNext())
                    throw new OptimizationException("Can't place an area in current floorplan. Sorry for the inconvenience.");

                area.Width = 0;
                area.Height = 0;
                area.MoveTo(nearestPoint.Current);

                // Expand area filling all available space
                Expand(area, floorPlan);

                Point[] heuristicCenters = new Point[]
                {
                    new Point(area.TopLeft),
                    new Point(area.TopLeft.X + area.Width, area.TopLeft.Y),
                    new Point(area.TopLeft.X, area.TopLeft.Y + area.Height),
                    new Point(area.TopLeft.X + area.Width, area.TopLeft.Y + area.Height),
                    new Point(area.Center)
                };

                // Remove all newly explored 
                nearestPoint.Skip(area.Points);

                // Validate the area and check if there are sufficent resources
                // If validation isn't possible or left resources after validation
                // aren't enough continue searching for a valid area
                if (!ShrinkToValidReconfigurable(area) || !area.IsSufficient)
                    continue;

                Area bestArea = heuristicCenters.AsParallel()
                    .Select((point, index) =>
                    {
                        Area a = new Area(area);

                        _areaReducer.Reduce(ref a, point, floorPlan);

                        return a;
                    })
                    .AsSequential()
                    .Aggregate((a1, a2) => 
                        _areaReducer.GetCost(a1, floorPlan.Design.Costs) < _areaReducer.GetCost(a2, floorPlan.Design.Costs) 
                        ? a1 : a2);

                area.Width = bestArea.Width;
                area.Height = bestArea.Height;
                area.MoveTo(bestArea.TopLeft);

                area.IsConfirmed = true;
            }
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
    }
}
