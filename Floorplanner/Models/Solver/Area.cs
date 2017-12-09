using Floorplanner.Models.Components;
using Floorplanner.ProblemParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Models.Solver
{
    public class Area
    {
        /// <summary>
        /// Board whre this area is allocated.
        /// </summary>
        public FPGA FPGA { get; private set; }

        /// <summary>
        /// Region allocated into this area.
        /// </summary>
        public Region Region { get; private set; }

        /// <summary>
        /// Top left corner column number.
        /// </summary>
        public int X { get; set; } = 0;

        /// <summary>
        /// Top left corner row number.
        /// </summary>
        public int Y { get; set; } = 0;

        /// <summary>
        /// Area width counting from the block after X.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Area height counting from the block after Y.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Center point of this area
        /// </summary>
        public Point Center
        {
            get
            {
                return new Point(X + Width / 2, Y + Height / 2);
            }
        }

        /// <summary>
        /// Area surface in blocks.
        /// </summary>
        public int Value
        {
            get
            {
                return (Width + 1) * (Height + 1);
            }
        }

        /// <summary>
        /// Resources covered by this area on the fpga.
        /// </summary>
        public IReadOnlyDictionary<BlockType, int> Resources
        {
            get
            {
                var res = DesignParser.EmptyResources();

                for(int y = Y; y <= Y + Height; y++)
                    for (int x = X; x <= X + Width; x++)
                        res[FPGA.Design[y, x]]++;

                return res;
            }
        }

        /// <summary>
        /// True if this area has enough resources of each type to contain its associated region.
        /// </summary>
        public bool IsSufficient
        {
            get
            {
                return Region.Resources.Select(pair => Resources[pair.Key] >= pair.Value).Aggregate((a, b) => a && b);
            }
        }

        /// <summary>
        /// True if the region can be placed in this area.
        /// Static regions can be placed everywhere, while reconfigurable regions need to be between certain columns
        /// </summary>
        public bool IsValid
        {
            get
            {
                return Region.Type == RegionType.Static
                    || (FPGA.LRecCol[X] && FPGA.RRecCol[X + Width]);
            }
        }
        
        /// <summary>
        /// Initialize a new area on the specified FPGA to allocate given region.
        /// The new area is initialized covering the whole FPGA
        /// </summary>
        /// <param name="container">FPGA where this area will be allocated</param>
        /// <param name="associated">Region which sholud take place inside this area</param>
        public Area(FPGA container, Region associated)
        {
            FPGA = container;
            Region = associated;

            Width = FPGA.Design.GetLength(1) - 1;
            Height = FPGA.Design.GetLength(0) - 1;
        }

        /// <summary>
        /// Check if two areas are overlapping.
        /// </summary>
        /// <param name="other">Area to check overlapping with.</param>
        /// <returns>True if the two areas are overlapping or are in incorrect position because of reconfigurable regions constraints, false else.</returns>
        public bool IsOverlapping(Area other)
        {
            if (FPGA != other.FPGA || this == other)
                return false;

            IEnumerable<int> thisTiles = coveredTiles();
            IEnumerable<int> otherTiles = other.coveredTiles();

            bool onSameTiles = thisTiles.Intersect(otherTiles).Any();

            if (!onSameTiles)
                return false;

            bool notXOverlapping = (X + Width) < other.X || (other.X + other.Width) < X;

            if (other.Region.Type == RegionType.Reconfigurable && Region.Type == RegionType.Reconfigurable)
                return notXOverlapping;

            bool notYOverlapping = (Y + Height) < other.Y || (other.Y + other.Height) < Y;

            return notXOverlapping || notYOverlapping;
        }

        /// <summary>
        /// Tiles covered by this area.
        /// </summary>
        /// <returns>Covered tiles row numbers. (Indexing from 1)</returns>
        public IEnumerable<int> coveredTiles()
        {
            int startTile = X / FPGA.TileHeight + 1;
            int endTile = (X + Width) / FPGA.TileHeight + 1;

            return Enumerable.Range(startTile, endTile - startTile + 1).ToArray();
        } 
    }
}
