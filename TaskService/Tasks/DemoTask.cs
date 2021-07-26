using DotNetCore.CAP;
using Repository.Database;
using System;
using System.Timers;

namespace TaskService.Tasks
{
    class DemoTask
    {

        private readonly dbContext db;
        private readonly ICapPublisher cap;

        public DemoTask(dbContext context, ICapPublisher capPublisher)
        {
            db = context;
            cap = capPublisher;
        }

        public void Run()
        {
            var tim = new Timer(1000 * 3);

            tim.Elapsed += TimElapsed;
            tim.Start();
        }



        private void TimElapsed(object sender, ElapsedEventArgs e)
        {
            //周期性执行的方法
            Console.WriteLine("HelloWord");

            cap.Publish("ShowMessage", DateTime.Now.ToString());
        }

    }
}
