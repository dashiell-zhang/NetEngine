using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace Methods.Img
{
    /// <summary>
    /// 截图类
    /// </summary>
    public class Screenshot
    {


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
        public static bool Run(string fromImagePath, int offsetX, int offsetY, string toImagePath, int width, int height)
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

    }
}
