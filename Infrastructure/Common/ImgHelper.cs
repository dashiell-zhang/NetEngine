﻿using SkiaSharp;
using SkiaSharp.QrCode;

namespace Common
{
    public class ImgHelper
    {

        /// <summary>
        /// 生成二维码
        /// </summary>
        /// <param name="text">二维码内容</param>
        /// <returns></returns>
        public static byte[] GetQrCode(string text)
        {
            using QRCodeGenerator generator = new();
            using var qr = generator.CreateQrCode(text, ECCLevel.L);
            SKImageInfo info = new(500, 500);

            using var surface = SKSurface.Create(info);
            using var canvas = surface.Canvas;
            canvas.Render(qr, info.Width, info.Height, SKColors.White, SKColors.Black);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }


        /// <summary>
        /// 从图片截取部分区域
        /// </summary>
        /// <param name="fromImagePath">源图路径</param>
        /// <param name="offsetX">距上</param>
        /// <param name="offsetY">距左</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        public static byte[] Screenshot(string fromImagePath, int offsetX, int offsetY, int width, int height)
        {
            using var original = SKBitmap.Decode(fromImagePath);
            using SKBitmap bitmap = new(width, height);
            using SKCanvas canvas = new(bitmap);
            SKRect sourceRect = new(offsetX, offsetY, offsetX + width, offsetY + height);
            SKRect destRect = new(0, 0, width, height);

            canvas.DrawBitmap(original, sourceRect, destRect);

            using var img = SKImage.FromBitmap(bitmap);
            using SKData p = img.Encode(SKEncodedImageFormat.Png, 100);
            return p.ToArray();
        }


        /// <summary>
        /// 获取图像数字验证码
        /// </summary>
        /// <param name="text">验证码内容，如4为数字</param>
        /// <returns></returns>
        public static byte[] GetVerifyCode(string text)
        {

            int width = 128;
            int height = 45;

            Random random = new();

            //创建bitmap位图
            using SKBitmap image = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            //创建画笔
            using SKCanvas canvas = new(image);
            //填充背景颜色为白色
            canvas.DrawColor(SKColors.White);

            //画图片的背景噪音线
            for (int i = 0; i < (width * height * 0.015); i++)
            {
                using SKPaint drawStyle = new();
                drawStyle.Color = new(Convert.ToUInt32(random.Next(Int32.MaxValue)));

                canvas.DrawLine(random.Next(0, width), random.Next(0, height), random.Next(0, width), random.Next(0, height), drawStyle);
            }

            //将文字写到画布上
            using (SKPaint drawStyle = new())
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

            using var img = SKImage.FromBitmap(image);
            using SKData p = img.Encode(SKEncodedImageFormat.Png, 100);
            return p.ToArray();
        }

    }

}
