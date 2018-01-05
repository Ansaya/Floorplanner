using Floorplanner.Models.Solver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Solver.Placers
{
    public sealed class NearestPointEnumerator : IEnumerator<Point>
    {

        private IList<Point> _sortedPoints;

        /// <summary>
        /// Initialize an enumerator which will return given points from nearest to farthest from the origin.
        /// </summary>
        /// <param name="origin">Point from which distances are calculated.</param>
        /// <param name="ends">Points to be ordered.</param>
        public NearestPointEnumerator(Point origin, IEnumerable<Point> ends)
        {
            _sortedPoints = ends.OrderBy(p => p.ManhattanFrom(origin)).ToList();
            Current = null;
        }

        /// <summary>
        /// Skip specified points from current eumeration.
        /// </summary>
        /// <param name="area">Area of points to be skipped during current enumeration.</param>
        public void Skip(Area area)
        {
            foreach (Point p in _sortedPoints.ToArray())
                if (area.Contains(p)) _sortedPoints.Remove(p);
        }

        public void Skip(IEnumerable<Point> toSkip)
        {
            foreach (Point p in _sortedPoints.ToArray())
                if (toSkip.Contains(p)) _sortedPoints.Remove(p);
        }

        public Point Current { get; private set; }

        object IEnumerator.Current { get => Current; }

        public void Dispose()
        {
            _sortedPoints.Clear();
        }

        public bool MoveNext()
        {
            if (_sortedPoints.Count == 0) return false;

            Current = _sortedPoints[0];
            _sortedPoints.RemoveAt(0);

            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
