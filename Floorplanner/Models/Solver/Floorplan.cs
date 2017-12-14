using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Floorplanner.Models.Solver
{
    public class Floorplan : IComparer<Area>
    {
        public Design Design { get; private set; }

        public IList<Area> Areas { get; private set; }

        /// <summary>
        /// Return all FPGA points not covered by confirmed areas
        /// or not forbidden by the FPGA design
        /// </summary>
        public IEnumerable<Point> FreePoints
        {
            get
            {
                IList<Point> fpgaPoints = new List<Point>(Design.FPGA.ValidPoints);
                IEnumerable<Area> confirmedAreas = Areas.Where(a => a.IsConfirmed);

                if (confirmedAreas.Any())
                    foreach (var p in confirmedAreas.SelectMany(a => a.Points))
                        fpgaPoints.Remove(p);

                return fpgaPoints;
            }
        }

        public Floorplan(Design planFor)
        {
            Design = planFor;
            Areas = Design.Regions.Select(r => new Area(Design.FPGA, r)).ToList();
        }

        public Floorplan(Floorplan toCopy)
        {
            Design = toCopy.Design;
            Areas = toCopy.Areas.Select(old => new Area(old)).ToList();
        }

        public int GetScore()
        {
            Func<double, double, double> sum = (a, b) => a + b;

            int totalArea = Areas.Sum(a => a.GetCost(Design.Costs));

            double totalWireDistance = Areas
                .Sum(a => a.Region.IOConns
                    .Sum(io => io.Point.ManhattanFrom(a.Center) * io.Wires));
            
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

        public void PrintDesignToConsole()
        {
            FPGA fpga = Design.FPGA;
            RegionType[,] currentDesign = new RegionType[fpga.Design.GetLength(0), fpga.Design.GetLength(1)];

            foreach (Area a in Areas.Where(a => a.IsConfirmed))
                foreach (Point p in a.Points)
                    currentDesign[(int)p.Y, (int)p.X] = a.Type;

            IDictionary<RegionType, ConsoleColor> color = new Dictionary<RegionType, ConsoleColor>()
            {
                { RegionType.Static, ConsoleColor.Green },
                { RegionType.Reconfigurable, ConsoleColor.Magenta },
                { RegionType.None, ConsoleColor.Gray }
            };
            
            for (int y = 0; y < currentDesign.GetLength(0); y++) {

                for (int x = 0; x < currentDesign.GetLength(1); x++)
                {
                    BlockType bt = fpga.Design[y, x];

                    Console.ForegroundColor = bt == BlockType.Forbidden ? ConsoleColor.DarkRed
                        : color[currentDesign[y, x]];

                    Console.Write($" {(char)bt}");
                }

                Console.WriteLine();
            }

            Console.ResetColor();
        }

        public int Compare(Area x, Area y) => Design.Compare(x.Region, y.Region);

        /// <summary>
        /// Check if given area is not overlapping with other areas in this floorplan and doesn't contain forbidden blocks.
        /// Area.IsOverlapping function is used in placeability computatio.
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
