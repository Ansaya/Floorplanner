using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Solver
{
    public class FloorplanOptimizer
    {
        public Design Design { get; private set; }

        public FloorplanOptimizer(Design toPlan)
        {
            Design = toPlan;
        }

        public Floorplan Solve()
        {
            Floorplan fPlan = new Floorplan(Design);

            DistanceOptimizer dOpt = new DistanceOptimizer(Design.Regions, Design.RegionWires, Design.FPGA);
            Console.WriteLine("Optimizing region center points...");

            // Get optimal region centers to minimize wire weight
            Point[] idealCenters = dOpt.GetOptimizedCenters();

            int fpgaHeight = Design.FPGA.Design.GetLength(0);
            int fpgaWidth = Design.FPGA.Design.GetLength(1);

            Console.WriteLine("Generating regions hierarchy...");

            IList<Area> orderedAreas = new SortedSet<Area>(fPlan.Areas, fPlan).ToList();

            Console.WriteLine("Starting regions area optimization...");

            // Try place and expand each area nearest possible to computed center points
            foreach(Area area in orderedAreas)
            {
                Console.WriteLine($"Optimizing region {area.ID}...");

                // TODO: possible improvement - run center points optimization after confirming each area to calculate 
                //                              new solution according to current placing choices

                Point idealCenter = idealCenters[area.ID];
                NearestPointEnumerator nearestPoint = new NearestPointEnumerator(idealCenter, fPlan.FreePoints);
                                
                while (!area.IsConfirmed)
                {

                    // Find a suitable place to expand current area
                    NearestPlace(area, fPlan, nearestPoint);

                    // Expand area filling all available space
                    Expand(area, fPlan);
                    
                    // Remove all newly explored 
                    nearestPoint.Skip(area.Points);

                    // Validate the area and check if there are sufficent resources
                    // If validation isn't possible or left resources after validation
                    // aren't enough continue searching for a valid area
                    if (!ShrinkToValidReconfigurable(area) || !area.IsSufficient)
                        continue;

                    // TODO: possible improvement - run Reduce in parallel among all available areas, according 
                    //                              to NearestPlace function, then take the lesser demanding result

                    // Reduce maximized area trying to lower cost as much as possible
                    Reduce(area, idealCenter, fPlan);

                    area.IsConfirmed = true;
                }
            }

            Console.WriteLine("Region areas optimization completed successfully.");

            return fPlan;
        }

        /// <summary>
        /// Reduce given area cost as much as possible also trying to near area center to ideal one.
        /// </summary>
        /// <param name="area">Area to reduce.</param>
        /// <param name="idealCenter">Ideal center point for given area.</param>
        /// <param name="floorPlan">Floorplan whom given area belongs to.</param>
        /// <returns>Reduced area cost.</returns>
        private int Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            // Initialize explored shrinking direction vector
            IDictionary<Direction, bool> exploredShrinkDir = new Dictionary<Direction, bool>();
            foreach (Direction d in Enum.GetValues(typeof(Direction)))
                exploredShrinkDir.Add(d, false);

            do
            {
                // Store current area position and dimensions
                Point oldTopLeft = new Point(area.TopLeft);
                int oldHeight = area.Height;
                int oldWidth = area.Width;

                // Chose a shrinking axis looking at area/region BRAM's and DSP's ratios
                // and check if chose axis hasn't been completely explored yet
                bool widthHeightShrink = (area.ResourceRatio[BlockType.BRAM] < 1.5
                    || area.ResourceRatio[BlockType.DSP] < 1.5)
                    && (!exploredShrinkDir[Direction.Up] || !exploredShrinkDir[Direction.Down]);
                
                Direction shrinkDir;
                
                // Chose a shrinking direction on chosen axis if possible
                if (!widthHeightShrink 
                    && (!exploredShrinkDir[Direction.Left] || !exploredShrinkDir[Direction.Right]))
                    shrinkDir = idealCenter.X > area.Center.X ? Direction.Left : Direction.Right;
                else
                    shrinkDir = idealCenter.Y > area.Center.Y ? Direction.Down : Direction.Up;
               
                // If wanted direction has already been explored chose opposite one
                if (exploredShrinkDir[shrinkDir]) shrinkDir = shrinkDir.Opposite();

                // NOTE: at this point a valid direction is there for shure, else the loop
                //       would have exited

                do
                {
                    // Try reducing on chosen direction
                    // If reduction isn't possible or leads to an area with insufficient resources
                    if (!area.TryShape(Models.Solver.Action.Shrink, shrinkDir) || !area.IsSufficient)
                    {
                        // Set current direction as explored
                        exploredShrinkDir[shrinkDir] = true;

                        // Restore area placement and dimensions before wrong shrinking
                        area.MoveTo(oldTopLeft);
                        area.Height = oldHeight;
                        area.Width = oldWidth;

                        // And break
                        break;
                    }

                } while (!area.IsValid);

                // I'm not sure code up to here is correct, so better throw an exception
                // if something wrong after shrink loop
                if (!area.IsValid || !area.IsSufficient)
                    throw new Exception("Reduce function behaviour unexpected.");

                // TODO: check distance from ideal center to improve area if it is too long in width
                // TODO: check distance from ideal center to improve area if it is too high in height

            } while (exploredShrinkDir.Values.Any(explored => !explored));

            return area.Score(floorPlan.Design.Costs);
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
                if(!a.IsValid)
                {
                    a.TopLeft.X = oldTopLeftX;
                    a.Width = oldWidth;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Expand given area on specified floorplan until reaching maximim occupied surface without 
        /// overlapping with other areas. No checks on area validity is performed.
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
                if(!fPlan.CanPlace(a))
                    a.TryShape(Models.Solver.Action.Shrink, d);
            }
        }

        /// <summary>
        /// Find first point in the sequence where the specified area can be placed on the floorplan.
        /// </summary>
        /// <param name="a">Area to be placed.</param>
        /// <param name="fPlan">Reference floorplan.</param>
        /// <param name="pointSequence">Point sequence to be searched.</param>
        /// <exception cref="OptimizationException">If a place for the given area was not found.</exception>
        private void NearestPlace(Area a, Floorplan fPlan, IEnumerator<Point> pointSequence)
        {
            OptimizationException error = 
                new OptimizationException("Can't place an area in current floorplan. Sorry for the inconvenience.");

            if (!pointSequence.MoveNext())
                throw error;

            a.Width = 0;
            a.Height = 0;
            a.MoveTo(pointSequence.Current);

            // While current point is inside the FPGA and i can't place the area search
            // for a suitable point
            while (!fPlan.CanPlace(a) && pointSequence.MoveNext())
                a.MoveTo(pointSequence.Current);

            if (!fPlan.CanPlace(a))
                throw error;
        }
    }
}
