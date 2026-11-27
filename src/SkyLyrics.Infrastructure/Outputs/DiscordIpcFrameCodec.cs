using System.Buffers.Binary;

namespace SkyLyrics.Infrastructure.Outputs;

public sealed record DiscordIpcFrame(int Opcode, byte[] Payload);

public static class DiscordIpcFrameCodec
{
    public const int MaximumPayloadBytes = 1024 * 1024;

    public static byte[] Encode(int opcode, ReadOnlySpan<byte> payload)
    {
        if (opcode < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opcode));
        }

        if (payload.Length > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Discord IPC frame exceeds the 1 MiB payload limit.");
        }

        var frame = new byte[8 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), payload.Length);
        payload.CopyTo(frame.AsSpan(8));
        return frame;
    }

    public static async ValueTask WriteAsync(
        Stream stream,
        int opcode,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (!payload.IsEmpty)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<DiscordIpcFrame> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[8];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (opcode < 0 || length < 0 || length > MaximumPayloadBytes)
        {
            throw new InvalidDataException("Discord IPC frame header is invalid.");
        }

        var payload = new byte[length];
        if (length > 0)
        {
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        return new DiscordIpcFrame(opcode, payload);
    }
}
