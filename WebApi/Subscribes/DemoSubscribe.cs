using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Repository.Database;
using System;

namespace WebApi.Subscribes
{


    public class DemoSubscribe : ICapSubscribe
    {

        [CapSubscribe("ShowMessage")]
        public void ShowMessage(string msg)
        {

            using (var scope = Program.ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            }

            Console.WriteLine(msg);
        }
    }
}
