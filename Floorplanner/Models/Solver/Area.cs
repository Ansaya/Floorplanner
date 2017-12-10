using Floorplanner.Models.Components;
using Floorplanner.ProblemParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Models.Solver
{
    public class Area
    {
        public bool IsConfirmed { get; set; } = false;

        /// <summary>
        /// Board whre this area is allocated.
        /// </summary>
        public FPGA FPGA { get; private set; }

        /// <summary>
        /// Region allocated into this area.
        /// </summary>
        public Region Region { get; private set; }

        public RegionType Type { get => Region.Type; }

        public Point TopLeft { get; set; } = new Point(0, 0);

        /// <summary>
        /// Area width counting from the block after X.
        /// </summary>
        public int Width { get; set; } = 0;

        /// <summary>
        /// Area height counting from the block after Y.
        /// </summary>
        public int Height { get; set; } = 0;

        /// <summary>
        /// Center point of this area
        /// </summary>
        public Point Center
        {
            get
            {
                return new Point(TopLeft.X + (double)Width / 2, TopLeft.Y + (double)Height / 2);
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

                for(int y = (int)TopLeft.Y; y <= TopLeft.Y + Height; y++)
                    for (int x = (int)TopLeft.X; x <= TopLeft.X + Width; x++)
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
        /// True if this are can be placed in current position on the FPGA (no forbidden blocks are covered).
        /// This check doesn't take care of other areas.
        /// Static regions can be placed everywhere, while reconfigurable regions need to be between certain columns.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return Resources[BlockType.Forbidden] == 0 &&
                    ( Region.Type == RegionType.Static
                    || (FPGA.LRecCol[(int)TopLeft.X] && FPGA.RRecCol[(int)TopLeft.X + Width]));
            }
        }
        
        public IEnumerable<Point> Points
        {
            get
            {
                IList<Point> covered = new List<Point>();

                for (int y = (int)TopLeft.Y; y <= TopLeft.Y + Height; y++)
                    for (int x = (int)TopLeft.X; x <= TopLeft.X + Width; x++)
                        covered.Add(new Point(x, y));

                return covered;
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
        }

        public int Score(Costs costs)
        {
            return Resources.Select(pair => pair.Value * costs.ResourceWeight[pair.Key]).Aggregate((c1, c2) => c1 + c2);
        }

        /// <summary>
        /// Move the area in the given direction if possible (not going out of FPGA bounds).
        /// </summary>
        /// <param name="direction">Movement direction.</param>
        /// <returns>True if the move was performed, false if FPGA bound have been reached.</returns>
        public bool TryMove(Direction direction)
        {
            switch(direction)
            {
                case Direction.Up:
                    if(TopLeft.Y > 0)
                    {
                        TopLeft.Y--;
                        return true;
                    }
                    break;

                case Direction.Right:
                    if(TopLeft.X + Width < FPGA.Design.GetLength(1) - 1)
                    {
                        TopLeft.X++;
                        return true;
                    }
                    break;

                case Direction.Down:
                    if(TopLeft.Y + Height < FPGA.Design.GetLength(0) - 1)
                    {
                        TopLeft.Y++;
                        return true;
                    }
                    break;

                case Direction.Left:
                    if(TopLeft.X > 0)
                    {
                        TopLeft.X--;
                        return true;
                    }
                    break;

                default:
                    break;
            }

            return false;
        }

        public bool TryMoveTo(Point topLeft)
        {
            Point oldTopLeft = TopLeft;
            TopLeft = topLeft;

            if (FPGA.Contains(this))
                return true;

            TopLeft = oldTopLeft;

            return false;
        }

        /// <summary>
        /// Expand or shrink this area in a given direction if possible
        /// </summary>
        /// <param name="action">Action to perform.</param>
        /// <param name="direction">Edge to be moved</param>
        /// <returns>True if the action was succesfull, false if FPGA bounds have been reached.</returns>
        public bool TryShape(Action action, Direction direction)
        {
            if(action == Action.Expand && TryMove(direction))
            {

                switch(direction)
                {
                    case Direction.Up:
                        Height++;
                        break;
                    case Direction.Right:
                        TopLeft.X--;
                        Width++;
                        break;
                    case Direction.Down:
                        TopLeft.Y--;
                        Height++;
                        break;
                    case Direction.Left:
                        Width++;
                        break;
                    default:
                        break;
                }
                return true;
            }
            
            if(action == Action.Shrink)
            {
                switch (direction)
                {
                    case Direction.Up:
                        if (Height == 0) return false;
                        TopLeft.Y++;
                        Height--;
                        break;
                    case Direction.Right:
                        if (Width == 0) return false;
                        Width--;
                        break;
                    case Direction.Down:
                        if (Height == 0) return false;
                        Height--;
                        break;
                    case Direction.Left:
                        if (Width == 0) return false;
                        TopLeft.X++;
                        Width--;
                        break;
                    default:
                        break;
                }

                return true;
            }

            return false;
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

            bool notXOverlapping = (TopLeft.X + Width) < other.TopLeft.X || (other.TopLeft.X + other.Width) < TopLeft.X;

            if (other.Region.Type == RegionType.Reconfigurable && Region.Type == RegionType.Reconfigurable)
                return notXOverlapping;

            bool notYOverlapping = (TopLeft.Y + Height) < other.TopLeft.Y || (other.TopLeft.Y + other.Height) < TopLeft.Y;

            return notXOverlapping || notYOverlapping;
        }

        /// <summary>
        /// Tiles covered by this area.
        /// </summary>
        /// <returns>Covered tiles row numbers. (Indexing from 1)</returns>
        public IEnumerable<int> coveredTiles()
        {
            int startTile =(int) TopLeft.X / FPGA.TileHeight + 1;
            int endTile = ((int)TopLeft.X + Width) / FPGA.TileHeight + 1;

            return Enumerable.Range(startTile, endTile - startTile + 1).ToArray();
        } 
    }

    public enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    public enum Action
    {
        Expand,
        Shrink
    }
}
