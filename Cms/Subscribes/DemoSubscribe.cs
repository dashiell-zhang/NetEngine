using DotNetCore.CAP;
using System;

namespace Cms.Subscribes
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
