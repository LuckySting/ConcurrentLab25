using System;
using System.Linq;
using MPI;

namespace ConcurrentLab25
{
    public class Population
    {
        public Func<double[], double> func;
        public Individuo[] _population;
        private int _seed;
        public Population(Func<double[], double> func)
        {
            this.func = func;
        }

        private void Select(ref Individuo a, ref Individuo b, double p = 1)
        {
            Random rnd = new Random();
            var pos = rnd.NextDouble();
            if (a.Fitness(func) > b.Fitness(func) && pos <= p)
            {
                Array.Copy(a.genes, b.genes, a.genes.Length);
            }
            else
            {
                Array.Copy(b.genes, a.genes, b.genes.Length);
            }
        }

        private void Cross(ref Individuo a, ref Individuo b)
        {
            Random rnd = new Random();
            var start = rnd.Next(0, a.genes.Length);
            var stop = rnd.Next(start, a.genes.Length);
            for (int i = start; i < stop; i++)
            {
                double c = a.genes[i];
                a.genes[i] = b.genes[i];
                b.genes[i] = c;
            }
        }

        private void Mutate(ref Individuo a, double minAdd, double maxAdd, double p = 0.2)
        {
            Random rnd = new Random();
            for (int i = 0; i < a.genes.Length; i++)
            {
                var pos = rnd.NextDouble();
                if (pos <= p)
                {
                    a.genes[i] += rnd.NextDouble() * (maxAdd - minAdd) + minAdd;
                }
            }
        }

        public void Initialize(int amount, int genesCount, double minArg, double maxArg, int seed)
        {
            _population = new Individuo[amount];
            Random rnd = new Random(seed);
            _seed = seed;
            for (int i = 0; i < amount; i++)
            {
                double[] genes = new double[genesCount];
                for (int j = 0; j < genesCount; j++)
                {
                    genes[j] = rnd.NextDouble() * (maxArg - minArg) + minArg;
                }

                _population[i] = new Individuo(genes);
            }
        }

        public void Selection(double p = 1)
        {
            Random rnd = new Random(_seed);
            _population = _population.OrderBy(x => rnd.Next()).ToArray();
            for (int i = 1; i < _population.Length; i += 2)
            {
                Select(ref _population[i - 1], ref _population[i], p);
            }
        }

        public void Crossover()
        {
            Random rnd = new Random(_seed);
            _population = _population.OrderBy(x => rnd.Next()).ToArray();
            for (int i = 1; i < _population.Length; i += 2)
            {
                Cross(ref _population[i - 1], ref _population[i]);
            }
        }

        public void Mutation(double minAdd, double maxAdd, double p = 0.2)
        {
            Random rnd = new Random(_seed);
            _population = _population.OrderBy(x => rnd.Next()).ToArray();
            for (int i = 0; i < _population.Length; i++)
            {
                Mutate(ref _population[i], minAdd, maxAdd, p);
            }
        }

        public double Average()
        {
            return _population.Select(x => func(x.genes)).Average();
        }

        public int EvaluateUnless(double targetValue, double mutationMin, double mutationMax, double selectionP = 1)
        {
            double current = Average();
            int counter = 0;
            while (current > targetValue)
            {
                Selection(selectionP);
                Crossover();
                Mutation(mutationMin, mutationMax);
                current = Average();
                counter++;
            }

            return counter;
        }
        
        public int EvaluateUnlessWithMigration(double targetValue, double mutationMin, double mutationMax, double selectionP = 1)
        {
            var comm = Intercommunicator.world;
            double current = Average();
            int counter = 0;
            bool done = false;
            bool totalDone = false;
            bool[] dones= new bool[comm.Size];
            while (current > targetValue)
            {
                comm.Allgather(done, ref dones);
                totalDone = dones.Any(d => d);
                if (totalDone)
                {
                    break;
                }
                Selection(selectionP);
                Crossover();
                Mutation(mutationMin, mutationMax);
                current = Average();
                counter++;
                if (counter % 5 == 0)
                {
                    Migrate(_population.Length / 4);
                }
            }
            done = true;
            if (!totalDone)
            {
                comm.Allgather(done, ref dones);
            }
            return counter;
        }
        
        public void Evaluate(int times, double mutationMin, double mutationMax, double selectionP = 1)
        {
            for (int i = 0; i < times; i++)
            {
                Selection(selectionP);
                Crossover();
                Mutation(mutationMin, mutationMax);
            }
        }

        public Individuo[] GetSubPopulation(int[] indexes)
        {
            return indexes.Select(i => _population[i]).ToArray();
        }

        public int[] GetSubPopulationIndexes(int count, bool best = true)
        {
            if (best)
            {
                return _population.Select((x, i) => new {ind = x, idx = i}).OrderByDescending(x => x.ind.Fitness(func))
                    .Take(count).Select(x => x.idx).ToArray();
            }
            Random rnd = new Random(_seed);
            return Enumerable.Range(0, _population.Length).OrderBy(x => rnd.Next()).Take(count).ToArray();
        }

        public void Migrate(int subPopulationSize, bool onlyBest = true)
        {
            Intracommunicator comm = Intracommunicator.world;
            if (comm.Size < 2)
            {
                return;
            }
            var indexes = GetSubPopulationIndexes(subPopulationSize, onlyBest);
            var subPopulation = GetSubPopulation(indexes);
            int to = (comm.Rank + 1) % comm.Size;
            int from = (comm.Rank + comm.Size - 1) % comm.Size;
            comm.Send(subPopulation, to, 0);
            Individuo[] guests = new Individuo[subPopulationSize];
            comm.Receive(from, 0, out guests);
            int i = 0;
            foreach (var index in indexes)
            {
                _population[index] = guests[i];
                i++;
            }
        }
    }
}