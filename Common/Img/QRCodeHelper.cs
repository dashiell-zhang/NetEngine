using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace Common.Img
{
    public static class QRCodeHelper
    {


        /// <summary>
        /// string生成二维码
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Bitmap GetQrCode(string text)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();

            QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);

            QRCode qrCode = new QRCode(qrCodeData);

            Bitmap qrCodeImage = qrCode.GetGraphic(15);

            Encoder myEncoder = Encoder.Quality;

            var myEncoderParameters = new EncoderParameters(5);

            var myEncoderParameter = new EncoderParameter(myEncoder, 25L);

            myEncoderParameters.Param[0] = myEncoderParameter;

            return qrCodeImage;
        }
    }
}
