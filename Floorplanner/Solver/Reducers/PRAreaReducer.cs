using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Solver.Reducers
{
    public class PRAreaReducer : IAreaReducer
    {
        private readonly IAreaReducer _backupReducer;

        private Func<Area, Costs, int> _costFunction = (Area a, Costs c) => 
            /*(a.TileRows.Count() - 1) * 1000000 +*/ c.AreaWeight != 0 ? a.GetCost(c) 
                : a.GetCost(new Costs(c.MaxScore, c.WireWeight, c.WireWeight, 1, 1, 1));

        /// <summary>
        /// Predefined cost function take into account how many tile rows are
        /// covered by the area and area surface cost
        /// </summary>
        public Func<Area, Costs, int> CostFunction
        {
            get => _costFunction;

            set
            {
                _costFunction = value;
                _backupReducer.CostFunction = value;
            }
        }

        public PRAreaReducer(IAreaReducer bacukpReducer)
        {
            _backupReducer = bacukpReducer;
        }

        public void Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            if(area.Type == RegionType.Static)
            {
                _backupReducer.Reduce(area, idealCenter, floorPlan);
                return;
            }

            int tileHeight = floorPlan.Design.FPGA.TileHeight;

            // Get center tile row number
            int centerTRow = (int)idealCenter.Y / tileHeight;

            
            // If the area isn't only on one tile row shrink it there
            // or as close as possible taking steps of complete tiles
            if (area.Height + 1 > tileHeight)
                TileGrainHeightShrink(area, centerTRow, tileHeight);

            Direction shrinkDir = area.Center.X > idealCenter.X
                ? Direction.Right : Direction.Left;

            ShrinkWidthOn(shrinkDir, area);

            ShrinkWidthOn(shrinkDir.Opposite(), area);
        }

        /// <summary>
        /// Shrink given area using tile height granularity as height minimum unit
        /// toward a specified tile row
        /// </summary>
        /// <param name="area">Area to reduce. (Must be taller than a tile height)</param>
        /// <param name="centerTRow">Idel center tile row number.</param>
        /// <param name="tileHeight">FPGA tile height value.</param>
        private void TileGrainHeightShrink(Area area, int centerTRow, int tileHeight)
        {
            if (area.Height + 1 <= tileHeight)
                throw new Exception("Only areas taller than a tile height are valid here.");

            // Get current max and min tile rows numbers covered by the area
            IEnumerable<int> expandedTRows = area.TileRows;
            int minTRow = expandedTRows.Min();
            int maxTRow = expandedTRows.Max();

            // Calculate shrink direction based on center tile row position
            Direction shrinkDir = minTRow >= centerTRow
                    ? Direction.Down : Direction.Up;

            do
            {
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

            // If the loop stopped because the area isn't sufficient
            // any more expand it back to occupy a full tile height
            // in opposite shrinking direction
            if (!area.IsSufficient)
            {
                shrinkDir = shrinkDir.Opposite();

                do
                {
                    area.TryShape(Models.Solver.Action.Expand, shrinkDir);
                } while ((area.Height + 1) % tileHeight == 0);
            }
        }

        /// <summary>
        /// Shrink area from given direction as much as possible.
        /// After the call the area will still be valid and sufficient.
        /// </summary>
        /// <param name="shrinkDir">Direction to shrink from. (Must be left or right)</param>
        /// <param name="area">Area to shrink.</param>
        private void ShrinkWidthOn(Direction shrinkDir, Area area)
        {
            if ((int)shrinkDir % 2 == 0)
                throw new Exception("Only right or left direction are valid here.");

            // Try reducing on chosen direction until possible or resources are insufficient
            while (area.IsSufficient)
                if (!area.TryShape(Models.Solver.Action.Shrink, shrinkDir))
                    break;

            shrinkDir = shrinkDir.Opposite();

            // Bring area back to valid and sufficient dimension after shrinking
            while (!area.IsSufficient || !area.IsValid)
                if (!area.TryShape(Models.Solver.Action.Expand, shrinkDir))
                    break;
        }
    }
}
