using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace UnityTcp.Editor.Helpers
{
    /// <summary>
    /// Helper class for handling binary framed TCP communication
    /// </summary>
    public static class BinaryFrameHelper
    {
        private const ulong MaxFrameBytes = 64UL * 1024 * 1024; // 64 MiB hard cap for framed payloads
        private const int FrameIOTimeoutMs = 30000; // Per-read timeout to avoid stalled clients

        /// <summary>
        /// Timeout-aware exact read helper with cancellation; avoids indefinite stalls and background task leaks
        /// </summary>
        public static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, int timeoutMs, CancellationToken cancel = default)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (offset < count)
            {
                int remaining = count - offset;
                int remainingTimeout = timeoutMs <= 0
                    ? Timeout.Infinite
                    : timeoutMs - (int)stopwatch.ElapsedMilliseconds;

                // If a finite timeout is configured and already elapsed, fail immediately
                if (remainingTimeout != Timeout.Infinite && remainingTimeout <= 0)
                {
                    throw new System.IO.IOException("Read timed out");
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                if (remainingTimeout != Timeout.Infinite)
                {
                    cts.CancelAfter(remainingTimeout);
                }

                try
                {
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
                    int read = await stream.ReadAsync(buffer.AsMemory(offset, remaining), cts.Token).ConfigureAwait(false);
#else
                    int read = await stream.ReadAsync(buffer, offset, remaining, cts.Token).ConfigureAwait(false);
#endif
                    if (read == 0)
                    {
                        throw new System.IO.IOException("Connection closed before reading expected bytes");
                    }
                    offset += read;
                }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                {
                    throw new System.IO.IOException("Read timed out");
                }
            }

            return buffer;
        }

        /// <summary>
        /// Write a framed payload to the stream with default timeout
        /// </summary>
        public static async Task WriteFrameAsync(NetworkStream stream, byte[] payload)
        {
            using var cts = new CancellationTokenSource(FrameIOTimeoutMs);
            await WriteFrameAsync(stream, payload, cts.Token);
        }

        /// <summary>
        /// Write a framed payload to the stream with cancellation support
        /// </summary>
        public static async Task WriteFrameAsync(NetworkStream stream, byte[] payload, CancellationToken cancel)
        {
            if (payload == null)
            {
                throw new System.ArgumentNullException(nameof(payload));
            }
            if ((ulong)payload.LongLength > MaxFrameBytes)
            {
                throw new System.IO.IOException($"Frame too large: {payload.LongLength}");
            }
            byte[] header = new byte[8];
            WriteUInt64BigEndian(header, (ulong)payload.LongLength);
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
            await stream.WriteAsync(header.AsMemory(0, header.Length), cancel).ConfigureAwait(false);
            await stream.WriteAsync(payload.AsMemory(0, payload.Length), cancel).ConfigureAwait(false);
#else
            await stream.WriteAsync(header, 0, header.Length, cancel).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, cancel).ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// Read a framed UTF-8 string from the stream
        /// </summary>
        public static async Task<string> ReadFrameAsUtf8Async(NetworkStream stream, int timeoutMs, CancellationToken cancel)
        {
            byte[] header = await ReadExactAsync(stream, 8, timeoutMs, cancel).ConfigureAwait(false);
            ulong payloadLen = ReadUInt64BigEndian(header);
             if (payloadLen > MaxFrameBytes)
            {
                throw new System.IO.IOException($"Invalid framed length: {payloadLen}");
            }
            if (payloadLen == 0UL)
                throw new System.IO.IOException("Zero-length frames are not allowed");
            if (payloadLen > int.MaxValue)
            {
                throw new System.IO.IOException("Frame too large for buffer");
            }
            int count = (int)payloadLen;
            byte[] payload = await ReadExactAsync(stream, count, timeoutMs, cancel).ConfigureAwait(false);
            return System.Text.Encoding.UTF8.GetString(payload);
        }

        /// <summary>
        /// Read a UInt64 from a byte array in big-endian format
        /// </summary>
        public static ulong ReadUInt64BigEndian(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 8) return 0UL;
            return ((ulong)buffer[0] << 56)
                 | ((ulong)buffer[1] << 48)
                 | ((ulong)buffer[2] << 40)
                 | ((ulong)buffer[3] << 32)
                 | ((ulong)buffer[4] << 24)
                 | ((ulong)buffer[5] << 16)
                 | ((ulong)buffer[6] << 8)
                 | buffer[7];
        }

        /// <summary>
        /// Write a UInt64 to a byte array in big-endian format
        /// </summary>
        public static void WriteUInt64BigEndian(byte[] dest, ulong value)
        {
            if (dest == null || dest.Length < 8)
            {
                throw new System.ArgumentException("Destination buffer too small for UInt64");
            }
            dest[0] = (byte)(value >> 56);
            dest[1] = (byte)(value >> 48);
            dest[2] = (byte)(value >> 40);
            dest[3] = (byte)(value >> 32);
            dest[4] = (byte)(value >> 24);
            dest[5] = (byte)(value >> 16);
            dest[6] = (byte)(value >> 8);
            dest[7] = (byte)(value);
        }
    }
}
