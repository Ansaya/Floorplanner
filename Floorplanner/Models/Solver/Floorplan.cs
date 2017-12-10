using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner.Models.Solver
{
    public class Floorplan : IComparer<Area>
    {
        public Design Design { get; private set; }

        public IList<Area> Areas { get; private set; }

        public IEnumerable<Point> FreePoints
        {
            get
            {
                IList<Point> fpga = new List<Point>(Design.FPGA.Points);
                IEnumerable<Area> confirmedAreas = Areas.Where(a => a.IsConfirmed);

                if (!confirmedAreas.Any()) return fpga;

                foreach (var a in confirmedAreas)
                    foreach (var p in a.Points)
                        fpga.Remove(p);

                return fpga;
            }
        }

        public Floorplan(Design planFor)
        {
            Design = planFor;
            Areas = new SortedSet<Area>(Design.Regions.Select(r => new Area(Design.FPGA, r)), this).ToList();
        }

        public int GetScore()
        {
            Func<double, double, double> sum = (a, b) => a + b;

            int totalArea = Areas.Select(a => a.Score(Design.Costs)).Aggregate((a, b) => a + b);

            double totalWireDistance = Areas
                .Select(a => a.Region.IOConns
                    .Select(io => io.Point.ManhattanFrom(a.Center) * io.Wires)
                    .Aggregate(sum))
                .Aggregate(sum);

            IList<Area> designOrderedAreas = new List<Area>();
            for(int r = 0; r < Design.Regions.Length; r++)
                designOrderedAreas.Add(Areas.Single(a => a.Region == Design.Regions[r]));

            for(int r = 0; r < designOrderedAreas.Count; r++)
            {
                Point current = designOrderedAreas[r].Center;

                for(int i = 0; i < Design.RegionWires.GetLength(1); i++)
                {
                    int wires = Design.RegionWires[r, i];

                    if (wires != 0)
                        totalWireDistance += designOrderedAreas[i].Center.ManhattanFrom(current) * wires;
                }
            }

            return Design.Costs.MaxScore - totalArea * Design.Costs.Area - (int)totalWireDistance * Design.Costs.WireLength;
        }

        /// <summary>
        /// Print solution to given text writer
        /// </summary>
        /// <param name="tw">Text writer to print solution to</param>
        public void PrintOn(TextWriter tw)
        {
            tw.WriteLine(Design.ID);

            foreach (var a in Areas)
                tw.WriteLine($"{(int)a.TopLeft.X + 1} {(int)a.TopLeft.Y + 1} {a.Width + 1} {a.Height + 1}");
        }

        public int Compare(Area x, Area y) => Design.Compare(x.Region, y.Region);

        /// <summary>
        /// Check if given area is not overlapping with other areas in this floorplan and doesn't contain forbidden blocks
        /// </summary>
        /// <param name="a">Area to check.</param>
        /// <returns>True if no overlap is found, false if given area overlaps with another.</returns>
        public bool CanPlace(Area a)
        {
            IEnumerable<Area> confirmedAreas = Areas.Where(ar => ar.IsConfirmed);

            return a.Resources[BlockType.Forbidden] == 0 
                && (!confirmedAreas.Any()
                || confirmedAreas.Select(a.IsOverlapping).Aggregate((o1, o2) => o1 || o2));
        }
    }
}
