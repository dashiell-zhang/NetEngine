namespace TaskService.Models.AppSetting
{


    /// <summary>
    /// RabbitMQ 配置信息
    /// </summary>
    public class RabbitMQSetting
    {


        /// <summary>
        /// 主机名称
        /// </summary>
        public string HostName { get; set; }



        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }



        /// <summary>
        /// 密码
        /// </summary>
        public string PassWord { get; set; }



        /// <summary>
        /// 虚拟主机
        /// </summary>
        public string VirtualHost { get; set; }



        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; }



        /// <summary>
        /// Ssl配置信息
        /// </summary>
        public SslSettings Ssl { get; set; }



        /// <summary>
        /// Ssl配置
        /// </summary>
        public class SslSettings
        {

            /// <summary>
            /// 是否启用
            /// </summary>
            public bool Enabled { get; set; }


            /// <summary>
            /// SSL Cn名称
            /// </summary>
            public string ServerName { get; set; }

        }


    }

}
