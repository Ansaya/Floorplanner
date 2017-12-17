using Floorplanner.Models.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner.Models.Solver
{
    public class Floorplan
    {
        public Design Design { get; private set; }

        public IList<Area> Areas { get; private set; }

        /// <summary>
        /// Return all FPGA points not covered by confirmed areas
        /// or not forbidden by the FPGA design
        /// </summary>
        public IEnumerable<Point> FreePoints
        {
            get => Design.FPGA.ValidPoints.Except(Areas.Where(a => a.IsConfirmed).SelectMany(a => a.Points));
        }

        public Floorplan(Design planFor)
        {
            Design = planFor;
            Areas = Design.Regions.Select(r => new Area(Design.FPGA, r)).ToList();
        }

        public Floorplan(Floorplan toCopy)
        {
            Design = toCopy.Design;
            Areas = new List<Area>(toCopy.Areas.Select(old => new Area(old)));
        }

        /// <summary>
        /// Calculate total floorplan score when all areas have been placed and confirmed.
        /// </summary>
        /// <exception cref="Exception">If called when some areas haven't been confirmed yet.</exception>
        /// <returns>Overall floorplan score.</returns>
        public int GetScore()
        {
            if (Areas.Any(a => !a.IsConfirmed))
                throw new Exception("Cannot calculate floorplan score if some areas haven't been placed.");

            return Design.Costs.MaxScore - GetCostFor(Areas);
        }

        /// <summary>
        /// Calculate floorplan cost for currently confirmed areas plus given unconfirmed area.
        /// </summary>
        /// <param name="unconfirmed">Unconfirmed area to be added to cost computation.</param>
        /// <returns>Partial cost value.</returns>
        public int GetPartialCostWith(Area unconfirmed)
        {
            IList<Area> confirmed = new List<Area>(Areas.Where(a => a.IsConfirmed));
            confirmed.Add(unconfirmed);

            return GetCostFor(confirmed);
        }

        private int GetCostFor(IEnumerable<Area> areas)
        {
            // Get total areas cost
            int totalArea = areas.Sum(a => a.GetCost(Design.Costs));

            // Get I/O wire length
            double totalWireDistance = Areas
                .Sum(a => a.Region.IOConns
                    .Sum(io => io.Point.ManhattanFrom(a.Center) * io.Wires));

            // Get areas interconnections wirelength
            foreach (Area a in areas)
            {
                Point aCenter = a.Center;

                foreach (Area b in areas)
                {
                    if (a.ID == b.ID) continue;

                    int wires = Design.RegionWires[a.ID, b.ID];

                    if (wires != 0)
                        totalWireDistance += b.Center.ManhattanFrom(aCenter) * wires;
                }
            }
            
            return totalArea * Design.Costs.AreaWeight + (int)totalWireDistance * Design.Costs.WireWeight;
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
                    currentDesign[(int)p.Y, (int)p.X] = IsBorder(p.X, p.Y, a) ? (RegionType)((int)a.Type + 1) : a.Type;

            IDictionary<RegionType, ConsoleColor> color = new Dictionary<RegionType, ConsoleColor>()
            {
                { RegionType.Static, ConsoleColor.DarkGreen },
                { RegionType.StaticBorder, ConsoleColor.Green },
                { RegionType.Reconfigurable, ConsoleColor.DarkMagenta },
                { RegionType.ReconfigurableBorder, ConsoleColor.Magenta },
                { RegionType.None, ConsoleColor.Gray }
            };
            
            for (int y = 0; y < currentDesign.GetLength(0); y++) {

                for (int x = 0; x < currentDesign.GetLength(1); x++)
                {
                    BlockType bt = fpga.Design[y, x];

                    Console.ForegroundColor = bt == BlockType.Forbidden ? ConsoleColor.DarkRed
                        : color[currentDesign[y, x]];

                    Console.Write($"{(char)bt}");
                }

                Console.WriteLine();
            }

            Console.ResetColor();
        }

        private bool IsBorder(double x, double y, Area a)
        {
            return x == a.TopLeft.X || x == a.TopLeft.X + a.Width
                || y == a.TopLeft.Y || y == a.TopLeft.Y + a.Height;
        }

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
