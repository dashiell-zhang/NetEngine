using SkiaSharp;
using SkiaSharp.QrCode;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Common
{

    public class ImgHelper
    {


        public static byte[] GetQrCode(string text)
        {
            using (var generator = new QRCodeGenerator())
            {
                // Generate QrCode
                var qr = generator.CreateQrCode(text, ECCLevel.L);

                // Render to canvas
                var info = new SKImageInfo(512, 512);
                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.Render(qr, info.Width, info.Height);

                    // Output to Stream -> File
                    using (var image = surface.Snapshot())
                    {
                        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            return data.ToArray();
                        }
                    }
                }
            }
        }



        /// <summary>
        /// 从指定的图片截取指定坐标的部分下来
        /// </summary>
        /// <param name="fromImagePath">源图路径</param>
        /// <param name="offsetX">距上</param>
        /// <param name="offsetY">距左</param>
        /// <param name="toImagePath">保存路径</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        public static bool Screenshot(string fromImagePath, int offsetX, int offsetY, string toImagePath, int width, int height)
        {
            try
            {
                //原图片文件
                Image fromImage = Image.FromFile(fromImagePath);
                //创建新图位图
                Bitmap bitmap = new Bitmap(width, height);
                //创建作图区域
                Graphics graphic = Graphics.FromImage(bitmap);
                //截取原图相应区域写入作图区
                graphic.DrawImage(fromImage, 0, 0, new Rectangle(offsetX, offsetY, width, height), GraphicsUnit.Pixel);
                //从作图区生成新图
                Image saveImage = Image.FromHbitmap(bitmap.GetHbitmap());
                //保存图片
                saveImage.Save(toImagePath, ImageFormat.Png);
                //释放资源   
                saveImage.Dispose();
                graphic.Dispose();
                bitmap.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }




        /// <summary>
        /// 获取图像数字验证码
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static byte[] GetVerifyCode(string text)
        {

            int width = 128;
            int height =45;

            Random random = new();

            //创建bitmap位图
            using (var image = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
            {
                //创建画笔
                using (var canvas = new SKCanvas(image))
                {
                    //填充背景颜色为白色
                    canvas.DrawColor(SKColors.White);

                    //画图片的背景噪音线
                    for (int i = 0; i < (width * height * 0.015); i++)
                    {
                        using (SKPaint drawStyle = new SKPaint())
                        {
                            drawStyle.Color = new SKColor(Convert.ToUInt32(random.Next(Int32.MaxValue)));

                            canvas.DrawLine(random.Next(0, width), random.Next(0, height), random.Next(0, width), random.Next(0, height), drawStyle);
                        }
                    }

                    //将文字写到画布上
                    using (SKPaint drawStyle = new SKPaint())
                    {
                        drawStyle.Color = SKColors.Red;
                        drawStyle.TextSize = height;
                        drawStyle.StrokeWidth = 1;

                        float emHeight = height - (float)height * (float)0.14;
                        float emWidth = ((float)width / text.Length) - ((float)width * (float)0.13);

                        canvas.DrawText(text, emWidth, emHeight, drawStyle);
                    }

                    //画图片的前景噪音点
                    for (int i = 0; i < (width * height * 0.6); i++)
                    {
                        image.SetPixel(random.Next(0, width), random.Next(0, height), new SKColor(Convert.ToUInt32(random.Next(Int32.MaxValue))));
                    }

                    using (var img = SKImage.FromBitmap(image))
                    {
                        using (SKData p = img.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            return p.ToArray();
                        }
                    }
                }
            }
        }
    }

}
