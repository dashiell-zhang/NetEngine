using DotNetCore.CAP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
