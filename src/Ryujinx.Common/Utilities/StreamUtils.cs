﻿using Microsoft.IO;
using Ryujinx.Common.Memory;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Common.Utilities
{
    public static class StreamUtils
    {
        public static byte[] StreamToBytes(Stream input)
        {
            using RecyclableMemoryStream output = StreamToRecyclableMemoryStream(input);

            return output.ToArray();
        }

        public static IMemoryOwner<byte> StreamToOwnedMemory(Stream input)
        {
            if (input is MemoryStream inputMemoryStream)
            {
                return MemoryStreamToOwnedMemory(inputMemoryStream);
            }
            else if (input.CanSeek)
            {
                long bytesExpected = input.Length;

                IMemoryOwner<byte> ownedMemory = ByteMemoryPool.Shared.Rent(bytesExpected);

                var destSpan = ownedMemory.Memory.Span;

                int totalBytesRead = 0;

                while (totalBytesRead < bytesExpected)
                {
                    int bytesRead = input.Read(destSpan.Slice(totalBytesRead));

                    if (bytesRead == 0)
                    {
                        ownedMemory.Dispose();

                        throw new IOException($"Tried reading {bytesExpected} but the stream closed after reading {totalBytesRead}.");
                    }

                    totalBytesRead += bytesRead;
                }

                return ownedMemory;
            }
            else
            {
                // If input is (non-seekable) then copy twice: first into a RecyclableMemoryStream, then to a rented IMemoryOwner<byte>.
                using RecyclableMemoryStream output = StreamToRecyclableMemoryStream(input);

                return MemoryStreamToOwnedMemory(output);
            }
        }

        public static async Task<byte[]> StreamToBytesAsync(Stream input, CancellationToken cancellationToken = default)
        {
            using (MemoryStream stream = MemoryStreamManager.Shared.GetStream())
            {
                await input.CopyToAsync(stream, cancellationToken);

                return stream.ToArray();
            }
        }

        private static IMemoryOwner<byte> MemoryStreamToOwnedMemory(MemoryStream input)
        {
            input.Position = 0;

            IMemoryOwner<byte> ownedMemory = ByteMemoryPool.Shared.Rent(input.Length);

            input.Read(ownedMemory.Memory.Span);

            return ownedMemory;
        }

        private static RecyclableMemoryStream StreamToRecyclableMemoryStream(Stream input)
        {
            RecyclableMemoryStream stream = MemoryStreamManager.Shared.GetStream();

            input.CopyTo(stream);

            return stream;
        }
    }
}