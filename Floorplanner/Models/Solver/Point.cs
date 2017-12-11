using System;

namespace Floorplanner.Models.Solver
{
#pragma warning disable CS0660 // Il tipo definisce l'operatore == o l'operatore != ma non esegue l'override di Object.Equals(object o)
#pragma warning disable CS0661 // Il tipo definisce l'operatore == o l'operatore != ma non esegue l'override di Object.GetHashCode()
    public class Point
#pragma warning restore CS0661 // Il tipo definisce l'operatore == o l'operatore != ma non esegue l'override di Object.GetHashCode()
#pragma warning restore CS0660 // Il tipo definisce l'operatore == o l'operatore != ma non esegue l'override di Object.Equals(object o)
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

        public static bool operator ==(Point point1, Point point2)
        {
            return point1.X == point2.X && point2.Y == point1.Y;
        }

        public static bool operator !=(Point point1, Point point2)
        {
            return !(point1 == point2);
        }
    }
}
