using System.IO.Compression;
using System.Text;

namespace SensorStream;

/// <summary>Minimal dependency-free PNG encoder (8-bit RGB/RGBA, no filtering).</summary>
internal static class Png
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Write(string path, int width, int height, int channels, byte[] pixels)
    {
        using var fs = File.Create(path);
        fs.Write([137, 80, 78, 71, 13, 10, 26, 10]); // signature

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;                                  // bit depth
        ihdr[9] = (byte)(channels == 4 ? 6 : 2);      // color type: 6=RGBA, 2=RGB
        WriteChunk(fs, "IHDR", ihdr);

        // Raw image data: each scanline prefixed with filter-type byte 0 (none).
        var stride = width * channels;
        using var raw = new MemoryStream((stride + 1) * height);
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(pixels, y * stride, stride);
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            raw.WriteTo(z);
        WriteChunk(fs, "IDAT", compressed.ToArray());

        WriteChunk(fs, "IEND", []);
    }

    private static void WriteChunk(Stream fs, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        fs.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        fs.Write(typeBytes);
        fs.Write(data);

        var crc = Crc32(data, Crc32(typeBytes)) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        fs.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint crc = 0xFFFFFFFF)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
