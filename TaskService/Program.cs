using System;

namespace TaskService
{
    class Program
    {
        static void Main(string[] args)
        {
            int minute = 2;

            minute = minute * 60 * 1000;


            for (int i = 0; 0 == 0; i++)
            {
                Console.WriteLine(DateTime.Now.ToString() + " Task Service Start !");

                Tasks.SyncData.Main.Run();

                Console.WriteLine(DateTime.Now.ToString() + " Task Service End !");

                System.Threading.Thread.Sleep(10000);
            }
        }
    }
}
