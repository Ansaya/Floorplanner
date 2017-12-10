using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

            // Get optimal region centers to minimize wire weight
            Point[] idealCenters = dOpt.GetOptimizedCenters();

            Floorplan fPlan = new Floorplan(Design);

            int fpgaHeight = Design.FPGA.Design.GetLength(0);
            int fpgaWidth = Design.FPGA.Design.GetLength(1);

            // Try place and expand each area nearest possible to computed center points
            for(int i = 0; i < idealCenters.Length; i++)
            {
                Area area = fPlan.Areas[i];
                List<Point> toSearch = new List<Point>(fPlan.FreePoints);
                IEnumerator<Point> spiralPoint = new SpiralPoint(idealCenters[i], toSearch);

                // Find a suitable place to expand current area
                while (SpiralPlace(area, fPlan, spiralPoint))
                {
                    // Expand area filling all available space
                    Expand(area, fPlan);

                    // Validate the area and check if there are sufficent resources
                    // If validation isn't possible or left resources after validation
                    // aren't enough remove explored points from list and continue
                    if (!ShrinkToValidReconfigurable(area) || !area.IsSufficient)
                    {
                        foreach (var p in area.Points) toSearch.Remove(p);
                        spiralPoint.MoveNext();
                        continue;
                    }

                    // Compute bramTiles/FPGAtiles and dspTiles/FPGAtiles ratios to define a majour
                    // shrinking direction
                    double fpgaTiles = (double)fpgaHeight / area.FPGA.TileHeight;
                    bool heightWidth = (double)area.Region.Resources[BlockType.BRAM] / area.FPGA.TileHeight > fpgaTiles * 0.35
                        || (double)area.Region.Resources[BlockType.DSP] / area.FPGA.TileHeight > fpgaTiles * 0.35;

                    ShrinkOn(heightWidth, area, idealCenters[i]);
                    ShrinkOn(!heightWidth, area, idealCenters[i]);
                    area.IsConfirmed = true;
                    break;
                }

                // If area couldn't be placed throw exception
                if (!area.IsConfirmed)
                    throw new Exception("Can't place an area in current floorplan. Sorry for the inconvenience.");
            }

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
                } while (a.IsSufficient);
            }
            else
            {
                bool upDown = a.FPGA.TileHeight - (a.TopLeft.Y % a.FPGA.TileHeight)
                            < (a.TopLeft.Y + a.Height) % a.FPGA.TileHeight;

                Direction shrinkDir = upDown ? Direction.Down : Direction.Up;

                while (a.TryShape(Models.Solver.Action.Shrink, shrinkDir) && a.IsSufficient) ;

                if (!a.IsSufficient)
                    a.TryShape(Models.Solver.Action.Expand, shrinkDir);

                shrinkDir = shrinkDir.Opposite();

                while (a.TryShape(Models.Solver.Action.Shrink, shrinkDir) && a.IsSufficient) ;

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
        /// Find nearst possible point from given sequence where the specified area can be placed on the floorplan
        /// </summary>
        /// <param name="a">Area to be placed.</param>
        /// <param name="fPlan">Reference floorplan.</param>
        /// <param name="spiralPoint">Point sequence to be searched.</param>
        /// <returns>True if a place was found, false if enumerator exipred before a position was found</returns>
        public bool SpiralPlace(Area a, Floorplan fPlan, IEnumerator<Point> spiralPoint)
        {
            a.TopLeft = spiralPoint.Current;
            a.Width = 0;
            a.Height = 0;

            // While current point is inside the FPGA and i can't place the area search
            // for a suitable point
            while (!fPlan.CanPlace(a) && spiralPoint.MoveNext()) ;

            if (fPlan.CanPlace(a))
                a.TopLeft = new Point(a.TopLeft);
            else
                return false;

            return true;
        }

        private class SpiralPoint : IEnumerator<Point>
        {
            private IList<Point> _inside;

            private Point _start;

            private IEnumerator<Direction> _spiral = new SpiralDirection();

            public Point Current { get; private set; }

            object IEnumerator.Current => this;

            public SpiralPoint(Point start, IList<Point> inside)
            {
                _inside = inside;
                _inside.Remove(start);
                _start = new Point(start);
                Current = new Point(start);
            }

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                _inside.Remove(Current);

                if (!_inside.Any())
                    return false;

                while(!_inside.Contains(Current))
                {
                    _spiral.MoveNext();
                    Current.Move(_spiral.Current, 1);
                }

                return true;
            }

            public void Reset()
            {
                Current = _start;
                _spiral = new SpiralDirection();
            }
        }

        public class SpiralDirection : IEnumerator<Direction>
        {
            private int _level = 0;

            private int _pos = 0;

            private int _index = 0;

            private Direction[] list = (Direction[])Enum.GetValues(typeof(Direction));

            public Direction Current => list[_index];

            object IEnumerator.Current => this;

            public void Dispose()
            {
                
            }

            public bool MoveNext()
            {
                // Step until level 
                if (_pos < _level)
                    _pos++;
                else
                {
                    // Reset position
                    _pos = 0;

                    // Turn to next direction
                    if (_index < list.Length - 1)
                        _index++;
                    else
                        _index = 0;

                    // Change level every two turns
                    if (_index % 2 == 0)
                        _level++;
                }

                // There is always a following step in an outgoing spiral
                return true;
            }

            public void Reset()
            {
                _level = 1;
                _pos = 0;
                _index = 0;
            }
        }
    }
}
