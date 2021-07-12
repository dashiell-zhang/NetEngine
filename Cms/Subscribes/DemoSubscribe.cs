using DotNetCore.CAP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
