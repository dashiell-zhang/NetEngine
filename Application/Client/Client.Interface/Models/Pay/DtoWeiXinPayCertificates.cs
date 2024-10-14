namespace Client.Interface.Models.Pay
{
#pragma warning disable IDE1006 // 命名样式
    public class DtoWeiXinPayCertificates
    {
        public List<Datum> data { get; set; }

        public class Datum
        {
            public DateTimeOffset effective_time { get; set; }
            public Encrypt_Certificate encrypt_certificate { get; set; }
            public DateTimeOffset expire_time { get; set; }
            public string serial_no { get; set; }

            public class Encrypt_Certificate
            {
                public string algorithm { get; set; }
                public string associated_data { get; set; }
                public string ciphertext { get; set; }
                public string nonce { get; set; }
            }



            /// <summary>
            /// 解密后的证书内容
            /// </summary>
            public string? certificate { get; set; }
        }


    }



}
