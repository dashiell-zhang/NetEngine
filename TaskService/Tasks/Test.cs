using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repository.Database;

namespace TaskService.Tasks
{
    class Test
    {

        readonly dbContext _theContext;

        public Test(dbContext theContext) => _theContext = theContext;

        public void Run()
        {
            var x = _theContext.TUser.ToList();
        }

    }
}
