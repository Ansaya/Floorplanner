using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Floorplanner.Models.Solution
{
    public class Area
    {
        public Region Region { get; private set; }

        public int X { get; set; }

        public int Y { get; set; }

        /// <summary>
        /// Area width counting from the block after X
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Area height counting from the block after Y
        /// </summary>
        public int Height { get; set; }

        public Area(Region associated)
        {
            Region = associated;
        }

        public bool IsOverlappingOn(FPGA fpga, Area other)
        {
            int[] thisTiles = coveredTiles(fpga);
            int[] otherTiles = coveredTiles(fpga);

            bool onSameTiles = thisTiles.Intersect(otherTiles).Count() > 0;

            if (!onSameTiles)
                return false;

            bool notXOverlapping = (X + Width) < other.X || (other.X + other.Width) < X;

            if (other.Region.Type == RegionType.Reconfigurable && Region.Type == RegionType.Reconfigurable)
                return notXOverlapping;

            bool notYOverlapping = (Y + Height) < other.Y || (other.Y + other.Height) < Y;

            return notXOverlapping || notYOverlapping;
        }

        public bool ExpandOn(FPGA fpga)
        {

        }

        public int[] coveredTiles(FPGA fpga)
        {
            int startTile = X / fpga.TileHeight;
            int endTile = (X + Width) / fpga.TileHeight;

            return Enumerable.Range(startTile, endTile - startTile + 1).ToArray();
        } 
    }
}
