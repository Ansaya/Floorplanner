using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floorplanner.Models.Solver
{
    public class Floorplan : IComparer<Area>
    {
        public Design Design { get; private set; }

        public IList<Area> Areas { get; private set; }

        public Floorplan(Design planFor)
        {
            Design = planFor;
            Areas = new SortedSet<Area>(Design.Regions.Select(r => new Area(Design.FPGA, r)), this).ToList();
        }

        /// <summary>
        /// Print solution to given text writer
        /// </summary>
        /// <param name="tw">Text writer to print solution to</param>
        public void PrintOn(TextWriter tw)
        {
            tw.WriteLine(Design.ID);

            foreach (var a in Areas)
                tw.WriteLine($"{a.X} {a.Y} {a.Width + 1} {a.Height + 1}");
        }

        public int Compare(Area x, Area y) => Design.Compare(x.Region, y.Region);
    }
}
