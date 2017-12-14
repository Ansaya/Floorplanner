using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Solver.Disruptors
{
    public class CommonResourcesDisruptor : IAreaDisruptor
    {
        private readonly SolverTuning _st;

        private readonly Random _var = new Random();

        public CommonResourcesDisruptor(SolverTuning st)
        {
            _st = st ??
                throw new ArgumentNullException("Solver options must be provided.");
        }

        public void DisruptStateFor(Area area, ref IList<Area> unconfirmed, Floorplan floorPlan)
        {
            IList<Area> confirmed = floorPlan.Areas.Where(a => a.IsConfirmed).ToList();

            // If there aren't so much areas to disrupt, perturb 70% of available ones
            if(confirmed.Count < _st.CaosFactor * 1.3)
            {
                int max = (int)(confirmed.Count * 0.7);

                Console.WriteLine($"\tDisrupting {max} areas from current solution.");

                confirmed.Shuffle();

                for (int i = 0; i < max; i++)
                    confirmed[i].Disrupt(ref unconfirmed);

                return;
            }

            IList<Area> smaller = new List<Area>();
            IList<Area> bigger = new List<Area>();

            for (int j = 0; j < confirmed.Count; j++)
            {
                Area current = confirmed[j];

                if (current.Resources.Merge(area.Region.Resources, FPHelper.sub).Any(rv => rv.Value < 0))
                    smaller.Add(current);
                else
                    bigger.Add(current);
            }

            smaller.Shuffle();
            bigger.Shuffle();

            int caosedAreas = _st.CaosFactor + _var.Next(-1 * _st.CaosVariance, _st.CaosVariance);
            if (caosedAreas <= 0)
                caosedAreas = 1;

            Console.WriteLine($"\tDisrupting {caosedAreas} areas from current solution.");

            int smallCaos = smaller.Count > bigger.Count ? (int)Math.Ceiling(caosedAreas * 0.65)
                : (int)Math.Ceiling(caosedAreas * 0.8);

            int bigCaos = caosedAreas - smallCaos;

            AggregateDisrupt(smallCaos, smaller, ref unconfirmed);

            SingleDisrupt(bigCaos, bigger, ref unconfirmed);
        }

        private void AggregateDisrupt(int caosedAreas, IList<Area> disruptable, ref IList<Area> unconfirmed)
        {
            for (int i = 0; i < disruptable.Count || caosedAreas <= 0; i++)
            {
                Area ai = disruptable[i];

                for (int j = i + 1; j < disruptable.Count || caosedAreas <= 0; j++)
                {
                    Area aj = disruptable[j];

                    if (ai.IsAdjacent(aj) && ai.Resources.Merge(aj.Resources, FPHelper.add)
                        .All(rv => rv.Value >= _st.ResourceDisruptThreshold))
                    {
                        ai.Disrupt(ref unconfirmed);
                        aj.Disrupt(ref unconfirmed);
                        caosedAreas -= 2;
                    }
                }
            }

            if(caosedAreas > 0)
                SingleDisrupt(caosedAreas, disruptable, ref unconfirmed);
        }
        
        private void SingleDisrupt(int caosedAreas, IList<Area> disruptable, ref IList<Area> unconfirmed)
        {
            for (int i = 0; i < disruptable.Count && caosedAreas > 0; i++)
            {
                disruptable[i].Disrupt(ref unconfirmed);
                caosedAreas--;
            }
        }
    }
}
