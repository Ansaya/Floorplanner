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

        public bool IsConfirmed { get => Areas.All(a => a.IsConfirmed); }

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
        /// Get a score accounting for all points of given area adjacent to other confirmed areas
        /// or FPGA borders or forbidden areas.
        /// </summary>
        /// <param name="unconfirmed">Area to calculate the adjacent score for.</param>
        /// <param name="multiplier">Multiplier to use with the calculated number of adjacent points.(Default = 1)</param>
        /// <returns>Calculated score (number of adjacent points if multiplier is default).</returns>
        public int GetAdjacentScore(Area unconfirmed, int multiplier = 1)
        {
            if (unconfirmed.IsConfirmed)
                throw new Exception("Given area must be unconfirmed.");

            // Get all border points from confirmed areas and FPGA board
            IEnumerable<Point> borders = Areas
                .Where(a => a.IsConfirmed)
                .SelectMany(a => a.BorderPoints)
                .Concat(Design.FPGA.ForbiddenPoints)
                .Concat(Design.FPGA.ExternalBorderPoints);

            // Check if there is any adjacent point from given area
            int score = unconfirmed.BorderPoints
                .Count(p => borders.Any(b => b.ManhattanFrom(p) == 1));

            return score * multiplier;
        }

        /// <summary>
        /// Calculate total floorplan score when all areas have been placed and confirmed.
        /// </summary>
        /// <exception cref="Exception">If called when some areas haven't been confirmed yet.</exception>
        /// <returns>Overall floorplan score.</returns>
        public int GetScore()
        {
            if (!IsConfirmed)
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

        /// <summary>
        /// Calculate maximum wirelength of a connection in a confirmed floorplan
        /// </summary>
        /// <exception cref="Exception">If called when some areas haven't been confirmed yet.</exception>
        /// <returns>Maximum connection length.</returns>
        public int GetMaxWirelength()
        {
            if (!IsConfirmed)
                throw new Exception("Cannot calculate floorplan score if some areas haven't been placed.");

            return (int)GetMaxWireLengthFor(Areas);
        }

        /// <summary>
        /// Calculate maximum wirelength for currently confirmed areas plus given unconfirmed area.
        /// </summary>
        /// <param name="unconfirmed">Unconfirmed area to be added to cost computation.</param>
        /// <returns>Partial maximum wirelength value.</returns>
        public int GetPartialMaxWirelengthWith(Area unconfirmed)
        {
            IList<Area> confirmed = new List<Area>(Areas.Where(a => a.IsConfirmed));
            confirmed.Add(unconfirmed);

            return (int)GetMaxWireLengthFor(confirmed);
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

        private double GetMediumWireLengthFor(IEnumerable<Area> areas)
        {
            double length = 0;
            int wires = 0;

            foreach(Area a in areas)
            {
                Point aCenter = a.Center;

                foreach(Area b in areas)
                {
                    if(Design.RegionWires[a.ID, b.ID] > 0)
                    {
                        length +=b.Center.ManhattanFrom(aCenter);
                        wires++;
                    }                    
                }

                foreach(IOConn io in a.Region.IOConns)
                {
                    length += io.Point.ManhattanFrom(aCenter);
                    wires++;
                }
            }

            return length / wires;
        }

        private double GetMaxWireLengthFor(IEnumerable<Area> areas)
        {
            double maxLength = 0;

            foreach (Area a in areas)
            {
                Point aCenter = a.Center;

                foreach (Area b in areas)
                {
                    if(Design.RegionWires[a.ID, b.ID] > 0)
                    {
                        double length = b.Center.ManhattanFrom(aCenter);

                        if (length > maxLength) maxLength = length;
                    }
                }

                foreach (IOConn io in a.Region.IOConns)
                {
                    double length = io.Point.ManhattanFrom(aCenter);

                    if (length > maxLength) maxLength = length;
                }
            }

            return maxLength;
        }

        /// <summary>
        /// Print solution to given text writer
        /// </summary>
        /// <param name="tw">Text writer to print solution to</param>
        public void PrintSolutionOn(TextWriter tw)
        {
            tw.WriteLine(Design.ID);

            foreach (Area a in Areas)
                tw.WriteLine($"{(int)a.TopLeft.X + 1} {(int)a.TopLeft.Y + 1} {a.Width + 1} {a.Height + 1}");                
        }

        /// <summary>
        /// Print current design to console specifying unconfirmed areas if any
        /// </summary>
        public void PrintDesignToConsole()
        {
            FPGA fpga = Design.FPGA;
            RegionType[,] currentDesign = new RegionType[fpga.Design.GetLength(0), fpga.Design.GetLength(1)];

            IEnumerable<Area> missing = Areas.Where(a => !a.IsConfirmed);

            if (missing.Any())
            {
                Console.WriteLine("\nMissing regions are:");
                foreach (Area a in missing)
                {
                    Console.Write("\t");
                    a.Region.PrintInfoTo(Console.Out);
                }
            }

            IEnumerable<Area> confirmed = Areas.Except(missing);

            Console.WriteLine($"\nPlaced areas statistics:\n" +
                $"\tMedium region height: {confirmed.Average(a => a.Height + 1):N4}\n" +
                $"\tMinimum region height: {confirmed.Min(a => a.Height + 1):N4}\n" +
                $"\tMedium region width: {confirmed.Average(a => a.Width + 1):N4}\n" +
                $"\tMinimum region width: {confirmed.Min(a => a.Width + 1):N4}\n" +
                $"\tMedium connections length: {GetMediumWireLengthFor(confirmed):N4}\n" +
                $"\tMaximum connection length: {GetMaxWireLengthFor(confirmed):N4}\n");

            foreach (Area a in confirmed)
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
            return a.Resources[BlockType.Forbidden] == 0 
                && !Areas.Any(ar => ar.IsConfirmed && a.IsOverlapping(ar));
        }
    }
}
