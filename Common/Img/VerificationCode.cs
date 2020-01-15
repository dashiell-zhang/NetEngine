using System;
using System.DrawingCore;

namespace Common.Img
{
    public class VerificationCode
    {
        //生成验证码
        public sealed class RandImage
        {
            private const string RandCharString = "0123456789";
            private int width;
            private int height;

            public string randString { get; set; }

            //指定二维码的宽度和高度
            public RandImage()
                    : this(140, 40)
            {
            }

            public RandImage(int width, int height)
            {
                this.width = width;
                this.height = height;
            }
            public Image GetImage()
            {
                Bitmap image = new Bitmap(width, height);
                Graphics g = Graphics.FromImage(image);
                g.Clear(Color.White);
                randString = "";
                Random random = new Random();
                do
                {
                    //使用DateTime.Now.Millisecond作为生成随机数的参数，增加随机性
                    randString += RandCharString.Substring(random.Next(DateTime.Now.Millisecond) % RandCharString.Length, 1);
                }

                while (randString.Length < 6);//此处的4表示二维码的包含几个数字
                float emSize = (float)width / randString.Length;
                Font font = new Font("Arial", emSize, (FontStyle.Bold | FontStyle.Italic));
                Pen pen = new Pen(Color.Silver);
                #region 画图片的背景噪音线
                int x1, y1, x2, y2;

                for (int i = 0; i < 100; i++)
                {
                    x1 = random.Next(image.Width);
                    y1 = random.Next(image.Height);
                    x2 = random.Next(image.Width);
                    y2 = random.Next(image.Height);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                #endregion

                #region 画图片的前景噪音点
                for (int i = 0; i < 800; i++)
                {
                    x1 = random.Next(image.Width);
                    y1 = random.Next(image.Height);
                    image.SetPixel(x1, y1, Color.FromArgb(random.Next(Int32.MaxValue)));
                }
                #endregion

                g.DrawString(randString, font, Brushes.Red, 2, 2);
                g.Dispose();
                return image;
            }
        }
    }
}
