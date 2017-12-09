using System;

namespace Floorplanner.Models.Solver
{
    public class Point
    {
        public double X { get; private set; }

        public double Y { get; private set; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
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
    }
}
