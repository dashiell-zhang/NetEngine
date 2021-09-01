using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repository.Database;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace TaskService.Tasks
{
    class DemoTask : BackgroundService
    {

        private readonly ICapPublisher cap;


        public DemoTask(ICapPublisher capPublisher)
        {
            cap = capPublisher;
        }




        protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var timer = new Timer(1000 * 3);

                timer.Elapsed += TimerElapsed;
                timer.Start();
            });
        }




        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {

            using (var scope = Program.ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<dbContext>();

            }


            //周期性执行的方法
            Console.WriteLine("HelloWord");

            cap.Publish("ShowMessage", DateTime.Now.ToString());
        }

    }
}
