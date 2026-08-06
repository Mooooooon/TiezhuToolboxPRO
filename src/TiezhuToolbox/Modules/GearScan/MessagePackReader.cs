using System.Buffers.Binary;
using System.Text;

namespace TiezhuToolbox.Modules.GearScan;

public sealed class MessagePackReader(ReadOnlyMemory<byte> source)
{
    private const int MaximumDepth = 64;
    private const int MaximumNodes = 2_000_000;
    private readonly ReadOnlyMemory<byte> _source = source;
    private int _offset;
    private int _nodeCount;

    public object? ReadDocument()
    {
        var value = ReadValue(0);
        if (_offset != _source.Length)
            throw new InvalidDataException($"MessagePack 文档尾部残留 {_source.Length - _offset} 字节。");
        return value;
    }

    private object? ReadValue(int depth)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException("MessagePack 嵌套层级过深。");
        if (++_nodeCount > MaximumNodes)
            throw new InvalidDataException("MessagePack 节点数量过多。");
        var code = ReadByte();
        if (code <= 0x7F)
            return (long)code;
        if (code >= 0xE0)
            return (long)(sbyte)code;
        if ((code & 0xF0) == 0x80)
            return ReadMap(code & 0x0F, depth + 1);
        if ((code & 0xF0) == 0x90)
            return ReadArray(code & 0x0F, depth + 1);
        if ((code & 0xE0) == 0xA0)
            return ReadString(code & 0x1F);

        return code switch
        {
            0xC0 => null,
            0xC2 => false,
            0xC3 => true,
            0xC4 => ReadBinary(ReadByte()),
            0xC5 => ReadBinary(ReadUInt16()),
            0xC6 => ReadBinary(checked((int)ReadUInt32())),
            0xC7 => ReadExtension(ReadByte()),
            0xC8 => ReadExtension(ReadUInt16()),
            0xC9 => ReadExtension(checked((int)ReadUInt32())),
            0xCA => BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32())),
            0xCB => BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64())),
            0xCC => (long)ReadByte(),
            0xCD => (long)ReadUInt16(),
            0xCE => (long)ReadUInt32(),
            0xCF => ReadUInt64Value(),
            0xD0 => (long)(sbyte)ReadByte(),
            0xD1 => (long)unchecked((short)ReadUInt16()),
            0xD2 => (long)unchecked((int)ReadUInt32()),
            0xD3 => unchecked((long)ReadUInt64()),
            0xD4 => ReadExtension(1),
            0xD5 => ReadExtension(2),
            0xD6 => ReadExtension(4),
            0xD7 => ReadExtension(8),
            0xD8 => ReadExtension(16),
            0xD9 => ReadString(ReadByte()),
            0xDA => ReadString(ReadUInt16()),
            0xDB => ReadString(checked((int)ReadUInt32())),
            0xDC => ReadArray(ReadUInt16(), depth + 1),
            0xDD => ReadArray(checked((int)ReadUInt32()), depth + 1),
            0xDE => ReadMap(ReadUInt16(), depth + 1),
            0xDF => ReadMap(checked((int)ReadUInt32()), depth + 1),
            _ => throw new InvalidDataException($"不支持的 MessagePack 类型：0x{code:X2}。"),
        };
    }

    private Dictionary<string, object?> ReadMap(int count, int depth)
    {
        ValidateCollectionLength(count, bytesPerEntry: 2);
        var result = new Dictionary<string, object?>(count, StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var key = ReadValue(depth);
            var textKey = key switch
            {
                string text => text,
                long number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ulong number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new InvalidDataException($"MessagePack 映射键类型无效：{key?.GetType().Name ?? "null"}。"),
            };
            result[textKey] = ReadValue(depth);
        }
        return result;
    }

    private List<object?> ReadArray(int count, int depth)
    {
        ValidateCollectionLength(count, bytesPerEntry: 1);
        var result = new List<object?>(count);
        for (var index = 0; index < count; index++)
            result.Add(ReadValue(depth));
        return result;
    }

    private string ReadString(int length) => Encoding.UTF8.GetString(ReadBytes(length));

    private byte[] ReadBinary(int length) => ReadBytes(length).ToArray();

    private MessagePackExtension ReadExtension(int length)
    {
        var type = unchecked((sbyte)ReadByte());
        return new MessagePackExtension(type, ReadBytes(length).ToArray());
    }

    private object ReadUInt64Value()
    {
        var value = ReadUInt64();
        return value <= long.MaxValue ? (long)value : value;
    }

    private byte ReadByte()
    {
        EnsureAvailable(1);
        return _source.Span[_offset++];
    }

    private ushort ReadUInt16()
    {
        var bytes = ReadBytes(2);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private uint ReadUInt32()
    {
        var bytes = ReadBytes(4);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private ulong ReadUInt64()
    {
        var bytes = ReadBytes(8);
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    private ReadOnlySpan<byte> ReadBytes(int count)
    {
        EnsureAvailable(count);
        var result = _source.Span.Slice(_offset, count);
        _offset += count;
        return result;
    }

    private void EnsureAvailable(int count)
    {
        if (count < 0 || _offset > _source.Length - count)
            throw new InvalidDataException("MessagePack 数据意外结束。");
    }

    private void ValidateCollectionLength(int count, int bytesPerEntry)
    {
        if (count < 0 || count > MaximumNodes || count > (_source.Length - _offset) / bytesPerEntry)
            throw new InvalidDataException("MessagePack 集合长度无效。");
    }
}

public sealed record MessagePackExtension(sbyte Type, byte[] Data);
