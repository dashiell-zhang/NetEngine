using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Models;
using Methods.Crypto;

namespace WebApi.Methods
{
    public class Public
    {
        public static string GetKey(Run run)
        {
            string data = run.run + run.uid + run.data;

            return Md5.GetMd5(data);
        }
    }
}
