using Floorplanner.Models;
using Floorplanner.Models.Solver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Floorplanner.Solver.Disruptors
{
    public class CommonResourcesDisruptor : IAreaDisruptor
    {
        private readonly SolverTuning _st;

        private readonly Random _var;

        private readonly int _seed;

        public CommonResourcesDisruptor(SolverTuning st, int seed)
        {
            _st = st ??
                throw new ArgumentNullException("Solver options must be provided.");
            _seed = seed;
            _var = new Random(seed);
        }

        public void DisruptStateFor(Region region, IList<Area> unconfirmed, Floorplan floorPlan)
        {
            IList<Area> confirmed = floorPlan.Areas.Where(a => a.IsConfirmed).ToList();

            // If there aren't so much areas to disrupt, perturb 70% of available ones
            if(confirmed.Count < _st.CaosFactor * 1.3)
            {
                int max = (int)(confirmed.Count * 0.7);

                confirmed.Shuffle(_seed);

                for (int i = 0; i < max; i++)
                    confirmed[i].Disrupt(unconfirmed);

                return;
            }

            IList<Area> smaller = new List<Area>();
            IList<Area> bigger = new List<Area>();

            for (int j = 0; j < confirmed.Count; j++)
            {
                Area current = confirmed[j];

                if (current.Resources.Any(rv => rv.Value < region.Resources[rv.Key]))
                    smaller.Add(current);
                else
                    bigger.Add(current);
            }

            smaller.Shuffle(_seed);
            bigger.Shuffle(_seed);

            int caosedAreas = _st.CaosFactor + _var.Next(-1 * _st.CaosVariance, _st.CaosVariance);
            if (caosedAreas <= 0)
                caosedAreas = 1;

            int smallCaos = smaller.Count > bigger.Count ? (int)Math.Ceiling(caosedAreas * 0.65)
                : (int)Math.Ceiling(caosedAreas * 0.8);

            int bigCaos = caosedAreas - smallCaos;

            AggregateDisrupt(smallCaos, smaller, unconfirmed);

            SingleDisrupt(bigCaos, bigger, unconfirmed);
        }

        private void AggregateDisrupt(int caosedAreas, IList<Area> disruptable, IList<Area> unconfirmed)
        {
            for (int i = 0; i < disruptable.Count && caosedAreas > 0; i++)
            {
                Area ai = disruptable[i];

                for (int j = i + 1; j < disruptable.Count && caosedAreas > 0; j++)
                {
                    Area aj = disruptable[j];

                    if (ai.IsAdjacent(aj) && !ai.Resources
                        .Any(rv => rv.Value + aj.Resources[rv.Key] < _st.ResourceDisruptThreshold))
                    {
                        ai.Disrupt(unconfirmed);
                        aj.Disrupt(unconfirmed);
                        caosedAreas -= 2;
                    }
                }
            }

            if(caosedAreas > 0)
                SingleDisrupt(caosedAreas, disruptable, unconfirmed);
        }
        
        private void SingleDisrupt(int caosedAreas, IList<Area> disruptable, IList<Area> unconfirmed)
        {
            for (int i = 0; i < disruptable.Count && caosedAreas > 0; i++)
            {
                disruptable[i].Disrupt(unconfirmed);
                caosedAreas--;
            }
        }
    }
}
