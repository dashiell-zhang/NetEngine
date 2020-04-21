using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common
{
    public static class SystemHelper
    {


        /// <summary>
        /// 获取本机全部IP
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllIpAddress()
        {
            var allIp = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(t => t.ToString()).ToList();

            return allIp;
        }



        /// <summary>
        /// 获取本机 IPV4 地址
        /// </summary>
        /// <returns></returns>
        public static string GetIpv4Address()
        {
            var ipv4 = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();

            return ipv4;
        }



        /// <summary>
        /// 获取本机 IPV6 地址
        /// </summary>
        /// <returns></returns>
        public static string GetIpv6Address()
        {
            var ipv6 = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)?.ToString();

            return ipv6;
        }
    }
}
