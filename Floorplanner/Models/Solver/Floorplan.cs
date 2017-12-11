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

        /// <summary>
        /// Return all FPGA points not covered by confirmed areas
        /// </summary>
        public IEnumerable<Point> FreePoints
        {
            get
            {
                IList<Point> fpgaPoints = new List<Point>(Design.FPGA.Points);
                IEnumerable<Area> confirmedAreas = Areas.Where(a => a.IsConfirmed);

                if (confirmedAreas.Any())
                    foreach (var p in confirmedAreas.Select(a => a.Points).Aggregate(Enumerable.Concat))
                        fpgaPoints.Remove(p);

                return fpgaPoints;
            }
        }

        public Floorplan(Design planFor)
        {
            Design = planFor;
            Areas = Design.Regions.Select(r => new Area(Design.FPGA, r)).ToList();
        }

        public int GetScore()
        {
            Func<double, double, double> sum = (a, b) => a + b;

            int totalArea = Areas.Select(a => a.Score(Design.Costs)).Aggregate((a, b) => a + b);

            double totalWireDistance = Areas
                .Select(a => a.Region.IOConns.Any() ? a.Region.IOConns
                    .Select(io => io.Point.ManhattanFrom(a.Center) * io.Wires)
                    .Aggregate(sum) : 0)
                .Aggregate(sum);
            
            foreach(Area a in Areas)
            {
                Point current = a.Center;

                for(int i = 0; i < Design.RegionWires.GetLength(1); i++)
                {
                    int wires = Design.RegionWires[a.ID, i];

                    if (wires != 0)
                        totalWireDistance += Areas[i].Center.ManhattanFrom(current) * wires;
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

            foreach (Area a in Areas)
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
                && !(confirmedAreas.Any()
                && confirmedAreas.Select(a.IsOverlapping).Aggregate((o1, o2) => o1 || o2));
        }
    }
}
