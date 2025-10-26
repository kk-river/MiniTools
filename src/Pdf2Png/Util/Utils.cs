using System.Runtime.CompilerServices;

namespace Pdf2Png.Util;

internal static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Round2UInt(double num) => (uint)Math.Round(num, MidpointRounding.AwayFromZero);
}
