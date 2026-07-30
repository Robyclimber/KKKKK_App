namespace RouteLab.Ui;

public static class ImageFileSizeReader
{
    public static Size? TryGetPixelSize(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(imagePath);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 24)
            {
                return null;
            }

            var signature = reader.ReadBytes(8);
            stream.Position = 0;

            if (IsPng(signature))
            {
                return ReadPng(reader);
            }

            if (signature[0] == 0xFF && signature[1] == 0xD8)
            {
                return ReadJpeg(reader);
            }
        }
        catch
        {
        }

        return null;
    }

    private static Size? ReadPng(BinaryReader reader)
    {
        reader.BaseStream.Position = 16;
        var width = ReadInt32BigEndian(reader);
        var height = ReadInt32BigEndian(reader);
        return width > 0 && height > 0 ? new Size(width, height) : null;
    }

    private static Size? ReadJpeg(BinaryReader reader)
    {
        reader.BaseStream.Position = 2;
        while (reader.BaseStream.Position < reader.BaseStream.Length - 1)
        {
            if (reader.ReadByte() != 0xFF)
            {
                continue;
            }

            var marker = reader.ReadByte();
            while (marker == 0xFF)
            {
                marker = reader.ReadByte();
            }

            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            var segmentLength = ReadUInt16BigEndian(reader);
            if (segmentLength < 2)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                _ = reader.ReadByte();
                var height = ReadUInt16BigEndian(reader);
                var width = ReadUInt16BigEndian(reader);
                return width > 0 && height > 0 ? new Size(width, height) : null;
            }

            reader.BaseStream.Seek(segmentLength - 2, SeekOrigin.Current);
        }

        return null;
    }

    private static bool IsPng(byte[] signature)
    {
        return signature.Length >= 8
            && signature[0] == 0x89
            && signature[1] == 0x50
            && signature[2] == 0x4E
            && signature[3] == 0x47
            && signature[4] == 0x0D
            && signature[5] == 0x0A
            && signature[6] == 0x1A
            && signature[7] == 0x0A;
    }

    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(sizeof(int));
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(sizeof(ushort));
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt16(bytes, 0);
    }
}
