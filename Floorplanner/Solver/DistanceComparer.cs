using Floorplanner.Models.Solver;
using System.Collections.Generic;

namespace Floorplanner.Solver
{
    public class DistanceComparer : IComparer<Point>
    {

        private Point _origin;

        private bool _nearestFarthest;

        public DistanceComparer(Point origin, DistanceComparePolicy policy)
        {
            _origin = new Point(origin);
            _nearestFarthest = policy == DistanceComparePolicy.FarthestFirst;
        }

        public int Compare(Point x, Point y)
        {
            double fromX = _origin.ManhattanFrom(x);
            double fromY = _origin.ManhattanFrom(y);

            if (_nearestFarthest)
                return fromX >= fromY ? -1 : 1;
            else
                return fromX <= fromY ? -1 : 1;
        }
    }

    public enum DistanceComparePolicy
    {
        NearestFirst,
        FarthestFirst
    }
}
