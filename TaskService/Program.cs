using Microsoft.Extensions.Configuration;
using System;

namespace TaskService
{
    class Program
    {
        public static System.Timers.Timer tim = new System.Timers.Timer(1000 * 10);


        static void Main(string[] args)
        {
            Common.EnvironmentHelper.InitTestServer();


            //为各数据库注入连接字符串
            Repository.Database.dbContext.ConnectionString = Common.IO.Config.Get().GetConnectionString("dbConnection");


            tim.Elapsed += Tim_Elapsed;
            tim.Start();

            Console.WriteLine("启动成功，输入 exit 回车后停止！");
            bool end = true;
            do
            {
                var read = Console.ReadLine();

                if (read == "exit")
                {
                    end = false;
                }
            } while (end);

        }

        private static void Tim_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Run();
        }


        private static void Run()
        {

        }
    }
}
