using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Floorplanner.Solver.Reducers
{
    public class PRAreaReducer : IAreaReducer
    {
        private readonly IAreaReducer _backupReducer;

        private readonly int _minDim;

        private Func<Area, Floorplan, int> _costFunction = (Area a, Floorplan f) =>
            /*(a.TileRows.Count() - 1) * 100 +*/ a.GetCost(f.Design.Costs.ToNonZero());

        /// <summary>
        /// Predefined cost function take into account how many tile rows are
        /// covered by the area and area surface cost
        /// </summary>
        public Func<Area, Floorplan, int> CostFunction
        {
            get => _costFunction;

            set
            {
                _costFunction = value;
                _backupReducer.CostFunction = value;
            }
        }

        public bool BackupEnabled { get => _backupReducer != null; }

        public PRAreaReducer(IAreaReducer bacukpReducer = null, int minDimension = 1)
        {
            _backupReducer = bacukpReducer;
            _minDim = minDimension;
        }

        public IAreaReducer Clone()
        {
            IAreaReducer clone = new PRAreaReducer(_backupReducer);
            clone.CostFunction = CostFunction;

            return clone;
        }

        public void Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            if(area.Type == RegionType.Static && BackupEnabled)
            {
                _backupReducer.Reduce(area, idealCenter, floorPlan);
                return;
            }

            int tileHeight = floorPlan.Design.FPGA.TileHeight;

            // Get center tile row number
            int centerTRow = (int)idealCenter.Y / tileHeight;
            
            IEnumerable<Area> areas = TileGrainHeightShrink(area, centerTRow, tileHeight);

            foreach(Area a in areas)
            {
                Direction shrinkDir = a.Center.X > idealCenter.X
                ? Direction.Right : Direction.Left;

                ShrinkWidthOn(shrinkDir, a);

                ShrinkWidthOn(shrinkDir.Opposite(), a);

                ShrinkHeight(a, tileHeight);
            }
            
            Area bestArea = areas
                .Aggregate((a1, a2) => CostFunction(a1, floorPlan) < CostFunction(a2, floorPlan) ? a1 : a2);

            area.Width = bestArea.Width;
            area.Height = bestArea.Height;
            area.MoveTo(bestArea.TopLeft);
        }

        /// <summary>
        /// Shrink given area using tile height granularity as height minimum unit
        /// toward a specified tile row
        /// </summary>
        /// <param name="area">Area to reduce. (Must be taller than a tile height)</param>
        /// <param name="centerTRow">Idel center tile row number.</param>
        /// <param name="tileHeight">FPGA tile height value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<Area> TileGrainHeightShrink(Area area, int centerTRow, int tileHeight)
        {
            if (area.Height + 1 <= tileHeight)
                return new Area[] { area };

            // Get current max and min tile rows numbers covered by the area
            IEnumerable<int> expandedTRows = area.TileRows;
            int minTRow = expandedTRows.Min();
            int maxTRow = expandedTRows.Max();
            int oldMinTRow = minTRow;
            int oldMaxTRow = maxTRow;

            IList<Area> areas = new List<Area>();
            areas.Add(new Area(area));
            Direction shrinkDir;

            do
            {
                if (oldMinTRow != minTRow)
                {
                    oldMinTRow = minTRow;
                    areas.Add(new Area(area));
                }

                if (oldMaxTRow != maxTRow)
                {
                    oldMaxTRow = maxTRow;
                    areas.Add(new Area(area));
                }

                // Calculate shrink direction based on center tile row position
                shrinkDir = minTRow >= centerTRow
                        ? Direction.Down : Direction.Up;

                // If can't shrink here there is something wrong
                // because the loop has to exit when minimum height is that of
                // a tile and if we are here it should have been more
                if (!area.TryShape(Models.Solver.Action.Shrink, shrinkDir))
                    throw new Exception("Unexpected behaviour of PRAreaReducer.\n" +
                        "Here the area should be at least a tile in height.");

                // Update current tile rows occupied
                expandedTRows = area.TileRows;
                minTRow = expandedTRows.Min();
                maxTRow = expandedTRows.Max();

            } while (area.IsSufficient && minTRow < maxTRow);

            if (area.IsSufficient) areas.Add(area);

            return areas;
        }

        /// <summary>
        /// Shrink area from given direction as much as possible.
        /// After the call the area will still be valid and sufficient.
        /// </summary>
        /// <param name="shrinkDir">Direction to shrink from. (Must be left or right)</param>
        /// <param name="area">Area to shrink.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ShrinkWidthOn(Direction shrinkDir, Area area)
        {
            if ((int)shrinkDir % 2 == 0)
                throw new Exception("Only right or left direction are valid here.");

            // Try reducing on chosen direction until possible or resources are insufficient
            while (area.IsSufficient && area.Width > _minDim)
                if (!area.TryShape(Models.Solver.Action.Shrink, shrinkDir))
                    break;

            // Bring area back to valid and sufficient dimension after shrinking
            while (!area.IsSufficient || !area.IsValid)
                if (!area.TryShape(Models.Solver.Action.Expand, shrinkDir))
                    break;
        }

        /// <summary>
        /// Shrink area height as much as possible positioning it to occupy less
        /// tile rows as possible
        /// </summary>
        /// <param name="area">Area to shrink.</param>
        /// <param name="tileHeight">Tile height.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ShrinkHeight(Area area, int tileHeight)
        {
            int startY = (int)area.TopLeft.Y;
            int endY = startY + area.Height;

            if (area.Height == _minDim) return;

            do
            {
                if (!area.TryShape(Models.Solver.Action.Shrink, Direction.Down))
                    break;
            } while (area.IsSufficient && area.Height > _minDim);


            if (!area.IsSufficient)
                area.TryShape(Models.Solver.Action.Expand, Direction.Down);

            if (area.Height == endY - startY) return;

            int topOverflow = tileHeight - startY % tileHeight;
            int bottomOverflow = endY % tileHeight;
            int shrinkHeight = endY - startY - area.Height;

            if (topOverflow <= shrinkHeight)
                area.MoveTo(new Point(area.TopLeft.X, startY + topOverflow));
        }
    }
}
