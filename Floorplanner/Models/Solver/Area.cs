﻿using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Floorplanner.Models.Solver
{
    public class Area
    {
        public int ID {
            get => Region.ID;
        }

        public bool IsConfirmed { get; set; } = false;

        /// <summary>
        /// Board whre this area is allocated.
        /// </summary>
        public FPGA FPGA { get; private set; }

        /// <summary>
        /// Region allocated into this area.
        /// </summary>
        public Region Region { get; private set; }

        public RegionType Type {
            get => Region.Type;
        }

        public Point TopLeft { get; private set; } = new Point(0, 0);

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Point(TopLeft.X + (double)Width / 2, TopLeft.Y + (double)Height / 2);
        }

        /// <summary>
        /// Tile rows covered by this area.
        /// </summary>
        /// <returns>Covered tiles' row number.</returns>
        public IEnumerable<int> TileRows
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int startTile = (int)TopLeft.Y / FPGA.TileHeight;
                int endTile = ((int)TopLeft.Y + Height) / FPGA.TileHeight;

                int[] tRows = new int[endTile - startTile + 1];

                for (int i = 0; i < tRows.Length; i++)
                    tRows[i] = startTile++;

                return tRows;
            }
        }

        /// <summary>
        /// Area surface in blocks.
        /// </summary>
        public int Value {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>  (Width + 1) * (Height + 1);
        }

        /// <summary>
        /// Resources covered by this area on the fpga.
        /// </summary>
        public IDictionary<BlockType, int> Resources { get => FPGA.ResourcesFor(this); }

        /// <summary>
        /// Area/Region resource ratio for this area and associated region.
        /// Values are computed on each call, so they are always up to date.
        /// </summary>
        public IReadOnlyDictionary<BlockType, double> ResourceRatio
        {
            get => Resources.ToDictionary(
                pair => pair.Key,
                pair => (double)pair.Value / Region.Resources[pair.Key]);
        }

        /// <summary>
        /// True if this area has enough resources of each type to contain its associated region.
        /// </summary>
        public bool IsSufficient
        {
            get => !Resources.Any(kv => kv.Value < Region.Resources[kv.Key]);
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
        
        /// <summary>
        /// Return all points contained in this area
        /// </summary>
        public IEnumerable<Point> Points
        {
            get
            {
                for (int y = (int)TopLeft.Y; y <= TopLeft.Y + Height; y++)
                    for (int x = (int)TopLeft.X; x <= TopLeft.X + Width; x++)
                        yield return new Point(x, y);
            }
        }

        /// <summary>
        /// Return border points for this area
        /// </summary>
        public IEnumerable<Point> BorderPoints
        {
            get
            {
                for (int y = (int)TopLeft.Y; y <= TopLeft.Y + Height; y++)
                {
                    yield return new Point(TopLeft.X, y);
                    yield return new Point(TopLeft.X + Width, y);
                }                    

                for (int x = (int)TopLeft.X; x <= TopLeft.X + Width; x++)
                {
                    yield return new Point(x, TopLeft.Y);
                    yield return new Point(x, TopLeft.Y + Height);
                }
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

        public Area(FPGA container, Region associated, Point topLeft)
        {
            FPGA = container;
            Region = associated;
            TopLeft = new Point(topLeft);
        }

        public Area(Area copy)
        {
            FPGA = copy.FPGA;
            Region = copy.Region;
            TopLeft = new Point(copy.TopLeft);
            Width = copy.Width;
            Height = copy.Height;
            IsConfirmed = copy.IsConfirmed;
        }

        public int GetCost(Costs costs) => Resources.GetCost(costs);

        /// <summary>
        /// Move the area to the given point.
        /// If the new position exceeds FPGA bounds an exception is thrown.
        /// </summary>
        /// <param name="topLeft">Top left point to move the area to.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the given point place the area or part of it out of FPGA bounds.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveTo(Point topLeft)
        {
            if (!TryMoveTo(topLeft))
                throw new ArgumentOutOfRangeException($"Point ({topLeft.X}, {topLeft.Y}) is not valid " +
                    $"as top left corner for this area (W: {Width}, H: {Height})");
        }

        /// <summary>
        /// Move the area to the given point if possible.
        /// Only FPGA bounds are checked, no validity or sufficency check is performed.
        /// </summary>
        /// <param name="topLeft">Top left point to move the area to.</param>
        /// <returns>True if moved the area successfuccly, false else.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMoveTo(Point topLeft)
        {
            Point oldTopLeft = TopLeft;
            TopLeft = new Point(topLeft);

            if (FPGA.Contains(this)) return true;

            TopLeft = oldTopLeft;

            return false;
        }

        /// <summary>
        /// Expand or shrink this area in a given direction if possible
        /// </summary>
        /// <param name="action">Action to perform.</param>
        /// <param name="direction">Edge to be moved.</param>
        /// <param name="steps">Moving steps from current edge position.</param>
        /// <returns>True if the action was succesfull, false if FPGA bounds have been reached.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryShape(Action action, Direction direction, int steps = 1)
        {
            Point topLeft = new Point(TopLeft);
            topLeft.Move(direction, steps);

            if(action == Action.Expand && TryMoveTo(topLeft))
            {
                switch(direction)
                {
                    case Direction.Up:
                        Height += steps;
                        break;
                    case Direction.Right:
                        TopLeft.X -= steps;
                        Width += steps;
                        break;
                    case Direction.Down:
                        TopLeft.Y -= steps;
                        Height += steps;
                        break;
                    case Direction.Left:
                        Width += steps;
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
                        if (Height < steps) return false;
                        TopLeft.Y += steps;
                        Height -= steps;
                        break;
                    case Direction.Right:
                        if (Width < steps) return false;
                        Width -= steps;
                        break;
                    case Direction.Down:
                        if (Height < steps) return false;
                        Height -= steps;
                        break;
                    case Direction.Left:
                        if (Width < steps) return false;
                        TopLeft.X += steps;
                        Width -= steps;
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
        /// No validity check is performed before comparison.
        /// </summary>
        /// <param name="other">Area to check overlapping with.</param>
        /// <returns>True if the two areas are overlapping or are in incorrect position because of reconfigurable regions constraints, false else.</returns>
        public bool IsOverlapping(Area other)
        {
            if (this == other) return false;

            IEnumerable<int> thisTiles = TileRows;
            IEnumerable<int> otherTiles = other.TileRows;

            bool sameTileRow = thisTiles.Any(otherTiles.Contains);

            if (!sameTileRow) return false;

            bool xOverlapping = (TopLeft.X <= other.TopLeft.X && other.TopLeft.X <= (TopLeft.X + Width))
                || (other.TopLeft.X <= TopLeft.X && TopLeft.X <= (other.TopLeft.X + other.Width));

            bool yOverlapping = (TopLeft.Y <= other.TopLeft.Y && other.TopLeft.Y <= (TopLeft.Y + Height))
                || (other.TopLeft.Y <= TopLeft.Y && TopLeft.Y <= (other.TopLeft.Y + other.Height));

            if (other.Region.Type == RegionType.Static || Region.Type == RegionType.Static)
                return yOverlapping && xOverlapping;

            return xOverlapping;
        }
        
        /// <summary>
        /// Check if given area has some borders in common with this area.
        /// </summary>
        /// <param name="other">Area to check adjacency with.</param>
        /// <returns>True if the two areas are adjacent, false else.</returns>
        public bool IsAdjacent(Area other)
        {
            double xDiff = Math.Abs(Center.X - other.Center.X);
            double yDiff = Math.Abs(Center.Y - other.Center.Y);

            bool leftrightAdj = (Width + other.Width) / 2d == xDiff;
            bool updownAdj = (Height + other.Height) / 2d == yDiff;

            return leftrightAdj && yDiff == 0
                || updownAdj && xDiff == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Point p)
        {
            return TopLeft.X <= p.X && p.X <= TopLeft.X + Width
                && TopLeft.Y <= p.Y && p.Y <= TopLeft.Y + Height;
        }

        public override bool Equals(object obj)
        {
            var area = obj as Area;
            return area != null &&
                   ID == area.ID;
        }

        public override int GetHashCode()
        {
            return 1213502048 + ID.GetHashCode();
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
