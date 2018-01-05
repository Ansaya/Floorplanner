using System;
using System.Runtime.CompilerServices;

namespace Floorplanner.Models.Solver
{
    public class Point
    {
        public double X { get; set; }

        public double Y { get; set; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Point(Point toCopy)
        {
            X = toCopy.X;
            Y = toCopy.Y;
        }

        /// <summary>
        /// Euclidean distance between this and another point
        /// </summary>
        /// <param name="other">Other point to calculate distance from</param>
        /// <returns>Distance between the two points</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DistanceFrom(Point other)
        {
            double xDiff = X - other.X;
            double yDiff = Y - other.Y;

            return xDiff * xDiff + yDiff * yDiff;
        }

        /// <summary>
        /// Manhattan distance between this and another point
        /// </summary>
        /// <param name="other">Other point to calculate distance from</param>
        /// <returns>Distance between the two points</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ManhattanFrom(Point other)
        {
            double xDiff = X - other.X;
            if (xDiff < 0) xDiff = -xDiff;
            double yDiff = Y - other.Y;
            if (yDiff < 0) yDiff = -yDiff;

            return xDiff + yDiff;
        }

        public void Move(Direction direction, double step)
        {
            switch(direction)
            {
                case Direction.Up:
                    Y = Y - step;
                    break;
                case Direction.Right:
                    X = X + step;
                    break;
                case Direction.Down:
                    Y = Y + step;
                    break;
                case Direction.Left:
                    X = X - step;
                    break;
                default:
                    break;
            }
        }

        public override bool Equals(Object obj)
        {
            // Check for null values and compare run-time types.
            if (obj == null || GetType() != obj.GetType())
                return false;

            Point p = (Point)obj;
            return (X == p.X) && (Y == p.Y);
        }

        public override int GetHashCode()
        {
            return (int)X ^ (int)Y;
        }
    }
}
