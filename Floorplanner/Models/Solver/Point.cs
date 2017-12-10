using System;
using System.Collections.Generic;

namespace Floorplanner.Models.Solver
{
    public class Point : IEquatable<Point>
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
        public double DistanceFrom(Point other)
        {
            return Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
        }

        /// <summary>
        /// Manhattan distance between this and another point
        /// </summary>
        /// <param name="other">Other point to calculate distance from</param>
        /// <returns>Distance between the two points</returns>
        public double ManhattanFrom(Point other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
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

        public override bool Equals(object obj)
        {
            return obj is Point && Equals(obj);
        }

        public bool Equals(Point other)
        {
            return other != null &&
                   X == other.X &&
                   Y == other.Y;
        }

        public override int GetHashCode()
        {
            var hashCode = 1861411795;
            hashCode = hashCode * -1521134295 + X.GetHashCode();
            hashCode = hashCode * -1521134295 + Y.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Point point1, Point point2)
        {
            return EqualityComparer<Point>.Default.Equals(point1, point2);
        }

        public static bool operator !=(Point point1, Point point2)
        {
            return !(point1 == point2);
        }
    }
}
