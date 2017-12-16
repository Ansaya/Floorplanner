using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Floorplanner.Models;
using Floorplanner.Models.Solver;

namespace Floorplanner.Solver.Reducers
{
    public class PRAreaReducer : IAreaReducer
    {
        public Func<Area, Costs, int> CostFunction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Reduce(Area area, Point idealCenter, Floorplan floorPlan)
        {
            int tileHeight = floorPlan.Design.FPGA.TileHeight;

            // Store starting area position and dimensions
            Point oldTopLeft = new Point(area.TopLeft);
            int oldWidth = area.Width, oldHeight = area.Height;

            // Get center tile row number
            int centerTRow = (int)idealCenter.Y / tileHeight;

            // If the area isn't only on centerTRow shrink it there
            // or as close as possible
            if(!area.TileRows.All(r => r == centerTRow))
            {
                // Shrink and move area to center tile row
                area.Height = tileHeight - 1;
                area.MoveTo(new Point(oldTopLeft.X, tileHeight * centerTRow));

                if (!area.IsSufficient)
                {
                    // TODO: expand on a second/third/etc. tile row until area is again sufficient
                    //       then shrink left and right only until minimum area
                }

                // Store better found area
                oldTopLeft = new Point(area.TopLeft);
                oldWidth = area.Width;
                oldHeight = area.Height;
            }

            Direction shrinkDir = area.Center.X > idealCenter.X
                ? Direction.Right : Direction.Left;

            ShrinkOn(shrinkDir, area);

            ShrinkOn(shrinkDir.Opposite(), area);
        }

        /// <summary>
        /// Shrink area from given direction as much as possible.
        /// After the call the area will still be valid and sufficient.
        /// </summary>
        /// <param name="shrinkDir">Direction to shrink from.</param>
        /// <param name="area">Area to shrink.</param>
        private void ShrinkOn(Direction shrinkDir, Area area)
        {
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
