using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;
using System.Threading.Tasks;
using System.Timers;
using TaskService.Libraries;

namespace TaskService.Tasks
{
    class DemoTask : TaskCore
    {

        protected override Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                var timer = new Timer(1000 * 1);
                timer.Elapsed += TimerElapsed;
                timer.Elapsed += TimerElapsed;
                timer.Start();
            }, stoppingToken);
        }



        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            using (var scope = Program.ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();


                //周期性执行的方法
                Console.WriteLine("HelloWord" + snowflakeHelper.GetId());
            }
        }

    }
}
