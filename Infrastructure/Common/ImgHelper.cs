using SkiaSharp;
using SkiaSharp.QrCode;

namespace Common;

/// <summary>
/// 提供二维码、图片裁剪和验证码图片生成能力
/// </summary>
public class ImgHelper
{

    /// <summary>
    /// 生成二维码
    /// </summary>
    /// <param name="text">二维码内容</param>
    /// <returns></returns>
    public static byte[] GetQrCode(string text)
    {
        var qr = QRCodeGenerator.CreateQrCode(text, ECCLevel.L);
        SKImageInfo info = new(500, 500);

        using var surface = SKSurface.Create(info);
        using var canvas = surface.Canvas;
        canvas.Render(qr, info.Width, info.Height, SKColors.White, SKColors.Black);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }


#if !BROWSER
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
        if (!File.Exists(fromImagePath))
        {
            throw new FileNotFoundException("图片文件不存在", fromImagePath);
        }

        using var original = SKBitmap.Decode(fromImagePath);

        if (original == null)
        {
            throw new ArgumentException("图片文件无法解码", nameof(fromImagePath));
        }

        if (offsetX < 0 || offsetY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetX), "裁剪偏移量不能小于0");
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "裁剪宽高必须大于0");
        }

        if ((long)offsetX + width > original.Width || (long)offsetY + height > original.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "裁剪区域超出源图片范围");
        }

        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        SKRect sourceRect = new(offsetX, offsetY, offsetX + width, offsetY + height);
        SKRect destRect = new(0, 0, width, height);

        canvas.DrawBitmap(original, sourceRect, destRect, new SKSamplingOptions(), null);

        using var img = SKImage.FromBitmap(bitmap);
        using SKData p = img.Encode(SKEncodedImageFormat.Png, 100);
        return p.ToArray();
    }
#endif


    /// <summary>
    /// 获取图像数字验证码
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    public static byte[] GetVerifyCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("验证码文本不能为空", nameof(text));
        }

        if (text.Length > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "验证码文本不能超过8个字符");
        }

        int width = Math.Max(128, 30 + text.Length * 32);
        int height = 45;

        Random random = new();

        //创建bitmap位图
        using SKBitmap bitmap = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        //创建画笔
        using SKCanvas canvas = new(bitmap);

        //填充背景颜色为白色
        canvas.DrawColor(SKColors.White);

        using SKPaint paint = new();

        //画图片的背景噪音线
        for (int i = 0; i < (width * height * 0.015); i++)
        {
            paint.Color = new(Convert.ToUInt32(random.Next(Int32.MaxValue)));

            canvas.DrawLine(random.Next(0, width), random.Next(0, height), random.Next(0, width), random.Next(0, height), paint);
        }

        using SKFont font = new();
        font.Size = height;

        // 设定初始水平偏移
        float horizontalOffset = 15;

        float x = horizontalOffset;
        float y = height * 0.86f;  // 基线位置

        List<SKColor> vibrantColors = [
            SKColors.Red,          // 红色
            SKColors.Lime,         // 酸橙色
            SKColors.Fuchsia,      // 紫红色
            SKColors.Yellow,       // 黄色
            SKColors.OrangeRed,    // 橙红色
            SKColors.DeepPink,     // 深粉红色
            SKColors.DodgerBlue    // 道奇蓝
        ];

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            int index = random.Next(vibrantColors.Count);

            paint.Color = vibrantColors[index];  // 为每个字符生成随机颜色

            string currentChar = ch.ToString();

            // 计算当前字符宽度，调整下一个字符的位置
            float charWidth = font.MeasureText(currentChar, paint);
            canvas.DrawText(currentChar, x, y, SKTextAlign.Left, font, paint);
            x += charWidth;  // 更新下一个字符的X坐标位置
        }

        //画图片的前景噪音点
        for (int i = 0; i < (width * height * 0.6); i++)
        {
            bitmap.SetPixel(random.Next(0, width), random.Next(0, height), new SKColor(Convert.ToUInt32(random.Next(Int32.MaxValue))));
        }

        using var img = SKImage.FromBitmap(bitmap);
        using SKData p = img.Encode(SKEncodedImageFormat.Png, 100);
        return p.ToArray();
    }

}
