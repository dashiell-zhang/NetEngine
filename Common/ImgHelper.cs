using SkiaSharp;
using SkiaSharp.QrCode;
using System;
using System.Collections.Generic;
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
        public static byte[] GetVerificationCode(int width, int height)
        {

            Random random = new Random();

            string randString = random.Next(1000, 9999).ToString();

            List<SKColor> colors = new();

            colors.Add(SKColors.AliceBlue);
            colors.Add(SKColors.PaleGreen);
            colors.Add(SKColors.PaleGoldenrod);
            colors.Add(SKColors.Orchid);
            colors.Add(SKColors.OrangeRed);
            colors.Add(SKColors.Orange);
            colors.Add(SKColors.OliveDrab);
            colors.Add(SKColors.Olive);
            colors.Add(SKColors.OldLace);
            colors.Add(SKColors.Navy);
            colors.Add(SKColors.NavajoWhite);
            colors.Add(SKColors.Moccasin);
            colors.Add(SKColors.MistyRose);
            colors.Add(SKColors.MintCream);
            colors.Add(SKColors.MidnightBlue);
            colors.Add(SKColors.MediumVioletRed);
            colors.Add(SKColors.MediumTurquoise);
            colors.Add(SKColors.MediumSpringGreen);
            colors.Add(SKColors.LightSlateGray);
            colors.Add(SKColors.LightSteelBlue);
            colors.Add(SKColors.LightYellow);
            colors.Add(SKColors.Lime);
            colors.Add(SKColors.LimeGreen);
            colors.Add(SKColors.Linen);
            colors.Add(SKColors.PaleTurquoise);
            colors.Add(SKColors.Magenta);
            colors.Add(SKColors.MediumAquamarine);
            colors.Add(SKColors.MediumBlue);
            colors.Add(SKColors.MediumOrchid);
            colors.Add(SKColors.MediumPurple);
            colors.Add(SKColors.MediumSeaGreen);
            colors.Add(SKColors.MediumSlateBlue);
            colors.Add(SKColors.Maroon);
            colors.Add(SKColors.PaleVioletRed);
            colors.Add(SKColors.PapayaWhip);
            colors.Add(SKColors.PeachPuff);
            colors.Add(SKColors.Snow);
            colors.Add(SKColors.SpringGreen);
            colors.Add(SKColors.SteelBlue);
            colors.Add(SKColors.Tan);
            colors.Add(SKColors.Teal);
            colors.Add(SKColors.Thistle);
            colors.Add(SKColors.SlateGray);
            colors.Add(SKColors.Tomato);
            colors.Add(SKColors.Violet);
            colors.Add(SKColors.Wheat);
            colors.Add(SKColors.White);
            colors.Add(SKColors.WhiteSmoke);
            colors.Add(SKColors.Yellow);
            colors.Add(SKColors.YellowGreen);
            colors.Add(SKColors.Turquoise);
            colors.Add(SKColors.LightSkyBlue);
            colors.Add(SKColors.SlateBlue);
            colors.Add(SKColors.Silver);
            colors.Add(SKColors.Peru);
            colors.Add(SKColors.Pink);
            colors.Add(SKColors.Plum);
            colors.Add(SKColors.PowderBlue);
            colors.Add(SKColors.Purple);
            colors.Add(SKColors.Red);
            colors.Add(SKColors.SkyBlue);
            colors.Add(SKColors.RosyBrown);
            colors.Add(SKColors.SaddleBrown);
            colors.Add(SKColors.Salmon);
            colors.Add(SKColors.SandyBrown);
            colors.Add(SKColors.SeaGreen);
            colors.Add(SKColors.SeaShell);
            colors.Add(SKColors.Sienna);
            colors.Add(SKColors.RoyalBlue);
            colors.Add(SKColors.LightSeaGreen);
            colors.Add(SKColors.LightSalmon);
            colors.Add(SKColors.LightPink);
            colors.Add(SKColors.Crimson);
            colors.Add(SKColors.Cyan);
            colors.Add(SKColors.DarkBlue);
            colors.Add(SKColors.DarkCyan);
            colors.Add(SKColors.DarkGoldenrod);
            colors.Add(SKColors.DarkGray);
            colors.Add(SKColors.Cornsilk);
            colors.Add(SKColors.DarkGreen);
            colors.Add(SKColors.DarkMagenta);
            colors.Add(SKColors.DarkOliveGreen);
            colors.Add(SKColors.DarkOrange);
            colors.Add(SKColors.DarkOrchid);
            colors.Add(SKColors.DarkRed);
            colors.Add(SKColors.DarkSalmon);
            colors.Add(SKColors.DarkKhaki);
            colors.Add(SKColors.DarkSeaGreen);
            colors.Add(SKColors.CornflowerBlue);
            colors.Add(SKColors.Chocolate);


            //创建bitmap位图
            using (SKBitmap image2d = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
            {
                //创建画笔
                using (SKCanvas canvas = new SKCanvas(image2d))
                {
                    //填充背景颜色为白色
                    canvas.DrawColor(SKColors.White);

                    SKTypeface font = SKTypeface.FromFamilyName(null, SKFontStyleWeight.SemiBold, SKFontStyleWidth.ExtraCondensed, SKFontStyleSlant.Upright);
                    SKPaint paint = new SKPaint();
                    paint.IsAntialias = true;
                    paint.Color = SKColors.Black;
                    paint.Typeface = font;
                    paint.TextSize = height;


                    //将文字写到画布上
                    using (SKPaint drawStyle = paint)
                    {
                        canvas.DrawText(randString, 1, height - 1, drawStyle);
                    }
                    //画随机干扰线
                    using (SKPaint drawStyle = new SKPaint())
                    {

                        int lineNum = 40; //干扰线数量
                        int lineStrookeWidth = 1; //干扰线宽度

                        for (int i = 0; i < lineNum; i++)
                        {
                            drawStyle.Color = colors[random.Next(colors.Count)];
                            drawStyle.StrokeWidth = lineStrookeWidth;
                            canvas.DrawLine(random.Next(0, width), random.Next(0, height), random.Next(0, width), random.Next(0, height), drawStyle);
                        }
                    }
                    //返回图片byte
                    using (SKImage img = SKImage.FromBitmap(image2d))
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
