using DotNetCore.CAP;
using System;

namespace TaskService.Subscribes
{

    public class DemoSubscribe : ICapSubscribe
    {


        [CapSubscribe("ShowMessage")]
        public void ShowMessage(string msg)
        {
            Console.WriteLine(msg);
        }



    }
}
