using DotNetCore.CAP;
using System;

namespace TaskAdmin.Subscribes
{


    public class DemoSubscribe : ICapSubscribe
    {

        [CapSubscribe("ShowMessage")]
        public void ShowMessage(string message)
        {

            Console.WriteLine(message);
        }
    }
}
