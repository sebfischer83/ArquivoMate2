using ArquivoMate2.Application.Interfaces;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    public class ThumbnailService : IThumbnailService
    {
        public byte[] GenerateThumbnail(Stream inputFile)
        {
            try
            {
                inputFile.Position = 0;
                using (var magickImage = new MagickImage(inputFile))
                {
                    // Resize to 400px width, maintain aspect ratio
                    magickImage.Resize(new MagickGeometry(400, 0)
                    {
                        IgnoreAspectRatio = false
                    });

                    // Convert to WebP format
                    magickImage.Format = MagickFormat.WebP;

                    // Return as byte array
                    return magickImage.ToByteArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating thumbnail: {ex.Message}", ex);
            }
        }
    }
}
