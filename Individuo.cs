using System;

namespace ConcurrentLab25
{
    [Serializable]
    public class Individuo
    {
        public double[] genes;

        public Individuo(double[] genes)
        {
            this.genes = genes;
        }

        public double Fitness(Func<double[], double> func)
        {
            return 1 / (1 + func(genes));
        }
    }
}