using System;
using System.Collections.Generic;
using System.Text;

namespace Methods.String
{
    public class OrderNo
    {
        /// <summary>
        /// 生成一个订单号
        /// </summary>
        /// <returns></returns>
        public static string GetOrderNo()
        {
            string orderno = "";

            Random ran = new Random();
            int RandKey = ran.Next(10000, 99999);


            orderno = "N" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + RandKey;

            return orderno;
        }
    }
}
