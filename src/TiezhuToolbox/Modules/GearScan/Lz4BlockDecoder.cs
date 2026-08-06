using System.Buffers.Binary;

namespace TiezhuToolbox.Modules.GearScan;

public static class Lz4BlockDecoder
{
    private const int MaximumDecodedLength = 64 * 1024 * 1024;

    /// <summary>解码游戏 Lua 层使用的“原始长度 + 压缩长度 + LZ4 Block”结构。</summary>
    public static byte[] DecodeGamePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidDataException("LZ4 载荷不足 8 字节。");
        var originalLength = BinaryPrimitives.ReadInt32LittleEndian(payload);
        var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        if (originalLength < 0 || originalLength > MaximumDecodedLength
            || compressedLength < 0 || compressedLength != payload.Length - 8)
            throw new InvalidDataException($"LZ4 长度头无效：原始={originalLength}，压缩={compressedLength}，载荷={payload.Length}。");

        var source = payload[8..];
        var output = new byte[originalLength];
        var sourceOffset = 0;
        var outputOffset = 0;
        while (sourceOffset < source.Length)
        {
            var token = source[sourceOffset++];
            var literalLength = ReadLength(source, ref sourceOffset, token >> 4);
            EnsureAvailable(source.Length, sourceOffset, literalLength, "LZ4 字面量");
            EnsureAvailable(output.Length, outputOffset, literalLength, "LZ4 输出");
            source.Slice(sourceOffset, literalLength).CopyTo(output.AsSpan(outputOffset));
            sourceOffset += literalLength;
            outputOffset += literalLength;
            if (sourceOffset == source.Length)
                break;

            EnsureAvailable(source.Length, sourceOffset, 2, "LZ4 回溯距离");
            var matchOffset = BinaryPrimitives.ReadUInt16LittleEndian(source[sourceOffset..]);
            sourceOffset += 2;
            if (matchOffset == 0 || matchOffset > outputOffset)
                throw new InvalidDataException($"LZ4 回溯距离无效：{matchOffset}。");
            var matchLength = ReadLength(source, ref sourceOffset, token & 0x0F) + 4;
            EnsureAvailable(output.Length, outputOffset, matchLength, "LZ4 匹配输出");
            for (var index = 0; index < matchLength; index++)
                output[outputOffset + index] = output[outputOffset - matchOffset + index];
            outputOffset += matchLength;
        }

        if (outputOffset != output.Length)
            throw new InvalidDataException($"LZ4 解码长度不一致：期望 {output.Length}，实际 {outputOffset}。");
        return output;
    }

    private static int ReadLength(ReadOnlySpan<byte> source, ref int offset, int initial)
    {
        var length = initial;
        if (initial != 15)
            return length;
        byte next;
        do
        {
            EnsureAvailable(source.Length, offset, 1, "LZ4 扩展长度");
            next = source[offset++];
            length = checked(length + next);
        } while (next == byte.MaxValue);
        return length;
    }

    private static void EnsureAvailable(int total, int offset, int count, string field)
    {
        if (count < 0 || offset < 0 || offset > total - count)
            throw new InvalidDataException($"{field}越界。");
    }
}
