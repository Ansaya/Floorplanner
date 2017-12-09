using Floorplanner.Models;
using Floorplanner.Models.Components;
using Floorplanner.Models.Solver;
using Floorplanner.ProblemParser;
using System.Collections.Generic;
using System.Linq;
using WAWrapper;

namespace Floorplanner.Solver
{
    public class DistanceOptimizer
    {
        public Region[] Regions { get => _design.Regions; }

        public int[,] InterConn { get => _design.RegionWires; }

        public FPGA FPGA { get => _design.FPGA; }

        private readonly Design _design;

        private readonly WAEngine _waService = new WAEngine()
        {
            APIKey = DesignParser.WolframAPIKey
        };

        public DistanceOptimizer(Design design)
        {
            _design = design;
        }

        public Point[] GetOptimizedCenters()
        {
            List<string> xDistanceEqAndAss = GetXDistanceEquationAndAssumptions();
            List<string> yDistanceEqAndAss = GetYDistanceEquationAndAssumptions();
            
            WAQueryResult xResult = _waService.RunQuery(xDistanceEqAndAss.Aggregate((a, b) => $"{a},{b}"));
            WAQueryResult yResult = _waService.RunQuery(yDistanceEqAndAss.Aggregate((a, b) => $"{a},{b}"));
            
            int[] xCoord = GetCoords(xResult);
            int[] yCoord = GetCoords(yResult);

            Point[] centers = new Point[xCoord.Length];

            for (int i = 0; i < xCoord.Length; i++)
                centers[i] = new Point(xCoord[i], yCoord[i]);

            return centers;
        }

        private int[] GetCoords(WAQueryResult result)
        {
            string coordString = result.Pods.Single(pod => pod.ID == "GlobalMinima").SubPods[0].PlainText;
            coordString = coordString.Substring(coordString.LastIndexOf('(') + 1).TrimEnd(')');

            return coordString.Split(',').Select(v => int.Parse(v)).ToArray();
        }

        private List<string> GetXDistanceEquationAndAssumptions()
        {
            int startVar = 'a';

            string equation = "minimize ";
            List<string> constraints = new List<string>();

            int r = 0;
            for (; r < Regions.Length; r++)
            {
                Region currentReg = Regions[r];
                char var = (char)(startVar + r);

                constraints.Add($"0<={var}<{FPGA.Design.GetLength(1)}");

                // Sum distances for io connections to current region
                foreach (var io in currentReg.IOConns)
                    equation += $"{io.Wires}|{io.Column}-{var}|+";

                // Sum distances for region interconnections
                for (int i = 0; i < InterConn.GetLength(0); i++)
                {
                    char connRegVar = (char)(startVar + i);
                    int wires = InterConn[r, i];

                    if (wires > 0)
                        equation += $"{wires}|{var}-{connRegVar}|+";
                }
            }

            constraints.Insert(0, equation.TrimEnd('+'));

            return constraints;
        }

        private List<string> GetYDistanceEquationAndAssumptions()
        {
            int startVar = 'a';

            string equation = "minimize ";
            List<string> constraints = new List<string>();

            int r = 0;
            for (; r < Regions.Length; r++)
            {
                Region currentReg = Regions[r];
                char var = (char)(startVar + r);

                constraints.Add($"0<={var}<{FPGA.Design.GetLength(0)}");

                // Sum distances for io connections to current region
                foreach (var io in currentReg.IOConns)
                    equation += $"{io.Wires}|{io.Row}-{var}|+";

                // Sum distances for region interconnections
                for (int i = 0; i < InterConn.GetLength(0); i++)
                {
                    char connRegVar = (char)(startVar + i);
                    int wires = InterConn[r, i];

                    if (wires > 0)
                        equation += $"{wires}|{var}-{connRegVar}|+";
                }
            }

            constraints.Insert(0, equation.TrimEnd('+'));

            return constraints;
        }
    }
}
