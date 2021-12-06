using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;

namespace TaskService.Subscribes
{

    public class DemoSubscribe : ICapSubscribe
    {


        [CapSubscribe("ShowMessage")]
        public static void ShowMessage(string msg)
        {

            using (var scope = Program.ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<dbContext>();

            }

            Console.WriteLine(msg);
        }



    }
}
