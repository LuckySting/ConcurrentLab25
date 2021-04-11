using System;
using System.Diagnostics;
using System.Linq;
using MPI;

namespace ConcurrentLab25
{
    class Program
    {
        static double Sphere(double[] args)
        {
            return args.Select(x => (x - 1) * (x - 1)).Sum();
        }

        static double test()
        {
            Intracommunicator comm = Intracommunicator.world;
            Stopwatch sw = new Stopwatch();
            if (comm.Rank == 0)
            {
                sw.Start();
            }

            var population = new Population(Sphere);
            population.Initialize(3000, 10, -100, 100, 11+comm.Rank);
            population.EvaluateUnlessWithMigration(0.5, -0.01, 0.01);
            if (comm.Rank == 0)
            {
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }

            return 0.0;
        }

        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Console.WriteLine(test());
            }
        }
    }
}