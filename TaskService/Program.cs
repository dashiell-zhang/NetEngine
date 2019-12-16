using System;

namespace TaskService
{
    class Program
    {
        public static System.Timers.Timer tim = new System.Timers.Timer(1000 * 60 * 60 * 6);


        static void Main(string[] args)
        {
            tim.Elapsed += Tim_Elapsed;
            tim.Start();
        }

        private static void Tim_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Run();
        }


        private static void Run()
        {


        }
    }
}
