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

                Point idealCenter = idealCenters[area.ID];
                NearestPointEnumerator nearestPoint = new NearestPointEnumerator(idealCenter, fPlan.FreePoints);
                                
                while (!area.IsConfirmed)
                {

                    // Find a suitable place to expand current area
                    NearestPlace(area, fPlan, nearestPoint);

                    // Expand area filling all available space
                    Expand(area, fPlan);

                    // Validate the area and check if there are sufficent resources
                    // If validation isn't possible or left resources after validation
                    // aren't enough remove explored points from list and continue
                    if (!ShrinkToValidReconfigurable(area) || !area.IsSufficient)
                    {
                        nearestPoint.Skip(area.Points);
                        nearestPoint.MoveNext();
                        continue;
                    }

                    // Compute bramTiles/FPGAtiles and dspTiles/FPGAtiles ratios to define a majour
                    // shrinking direction
                    double fpgaTiles = (double)fpgaHeight / area.FPGA.TileHeight;
                    bool heightWidth = (double)area.Region.Resources[BlockType.BRAM] / area.FPGA.TileHeight > fpgaTiles * 0.35
                        || (double)area.Region.Resources[BlockType.DSP] / area.FPGA.TileHeight > fpgaTiles * 0.35;

                    ShrinkOn(heightWidth, area, idealCenter);
                    ShrinkOn(!heightWidth, area, idealCenter);
                    area.IsConfirmed = true;
                }
            }

            Console.WriteLine("Region areas optimization completed successfully.");

            return fPlan;
        }

        public void ShrinkOn(bool heightWidth, Area a, Point idealCenter)
        {
            if(heightWidth)
            {
                bool done = false;
                int oldTopLeftX = (int)a.TopLeft.X;
                int oldWidth = a.Width;

                do
                {
                    Direction shrinkDir = idealCenter.X > a.TopLeft.X ? Direction.Left : Direction.Right;

                    while (!done && a.TryShape(Models.Solver.Action.Shrink, shrinkDir) && !a.IsValid) ;

                    if (!a.IsValid || !a.IsSufficient)
                    {
                        a.TopLeft.X = oldTopLeftX;
                        a.Width = oldWidth;
                        done = true;
                    }

                    while (done && a.TryShape(Models.Solver.Action.Shrink, shrinkDir.Opposite()) && !a.IsValid) ;

                    if (a.IsValid && a.IsSufficient)
                    {
                        oldTopLeftX = (int)a.TopLeft.X;
                        oldWidth = a.Width;
                    }
                    else
                    {
                        a.TopLeft.X = oldTopLeftX;
                        a.Width = oldWidth;
                        if (done == true) return;
                    }
                } while (a.IsSufficient && a.Width > 0);
            }
            else
            {
                bool upDown = a.FPGA.TileHeight - (a.TopLeft.Y % a.FPGA.TileHeight)
                            < (a.TopLeft.Y + a.Height) % a.FPGA.TileHeight;

                Direction shrinkDir = upDown ? Direction.Down : Direction.Up;

                while (a.TryShape(Models.Solver.Action.Shrink, shrinkDir) && a.IsSufficient && a.Height > 0) ;

                if (!a.IsSufficient)
                    a.TryShape(Models.Solver.Action.Expand, shrinkDir);

                shrinkDir = shrinkDir.Opposite();

                while (a.TryShape(Models.Solver.Action.Shrink, shrinkDir) && a.IsSufficient && a.Height > 0) ;

                if (!a.IsSufficient)
                    a.TryShape(Models.Solver.Action.Expand, shrinkDir);
            }
        }

        /// <summary>
        /// Shrink specified area until left and right sides reach a valid position for a reconfigurabla region.
        /// If the given area is a static one no action is performed.
        /// If the area isn't validated it is restored to initial dimesions and position
        /// </summary>
        /// <param name="a">Area to shrink until valid.</param>
        /// <returns>True if a valid area has been obtained, false else.</returns>
        public bool ShrinkToValidReconfigurable(Area a)
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
        /// Expand given area on specified floorplan until reaching maximim occupied surface without overlapping with other areas.
        /// No checks on area validity.
        /// </summary>
        /// <param name="a">Area to be expanded.</param>
        /// <param name="fPlan">Floorplan to refer to.</param>
        public void Expand(Area a, Floorplan fPlan)
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
        /// <exception cref="Exception">If a place for the given area was not found.</exception>
        public void NearestPlace(Area a, Floorplan fPlan, IEnumerator<Point> pointSequence)
        {
            Exception error = new Exception("Can't place an area in current floorplan. Sorry for the inconvenience.");

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
