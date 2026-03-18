using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// Fast read-only pixel accessor shared by all algorithm implementations.
    /// Uses LockBits + Marshal.Copy for 10-50× speedup over GetPixel.
    /// </summary>
    internal sealed class AlgoPixelBuffer : IDisposable
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Pixels;
        public readonly int Stride;

        public AlgoPixelBuffer(Bitmap bmp)
        {
            Width  = bmp.Width;
            Height = bmp.Height;
            var data = bmp.LockBits(
                new Rectangle(0, 0, Width, Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            Stride = data.Stride;
            Pixels = new byte[Stride * Height];
            Marshal.Copy(data.Scan0, Pixels, 0, Pixels.Length);
            bmp.UnlockBits(data);
        }

        /// <summary>BT.601 luminance: 0.299R + 0.587G + 0.114B</summary>
        public double Luminance(int x, int y)
        {
            int o = y * Stride + x * 3;
            return 0.299 * Pixels[o + 2] + 0.587 * Pixels[o + 1] + 0.114 * Pixels[o];
        }

        public (byte R, byte G, byte B) GetRgb(int x, int y)
        {
            int o = y * Stride + x * 3;
            return (Pixels[o + 2], Pixels[o + 1], Pixels[o]);
        }

        public void Dispose() { }
    }
}
