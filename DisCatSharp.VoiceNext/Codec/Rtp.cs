// This file is part of the DisCatSharp project, based off DSharpPlus.
//
// Copyright (c) 2021-2022 AITSYS
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Buffers.Binary;

namespace DisCatSharp.VoiceNext.Codec
{
	/// <summary>
	/// The rtp.
	/// </summary>
	internal sealed class Rtp : IDisposable
	{
		/// <summary>
		/// The header size.
		/// </summary>
		public const int HEADER_SIZE = 12;

		/// <summary>
		/// The rtp no extension.
		/// </summary>
		private const byte RTP_NO_EXTENSION = 0x80;
		/// <summary>
		/// The rtp extension.
		/// </summary>
		private const byte RTP_EXTENSION = 0x90;
		/// <summary>
		/// The rtp version.
		/// </summary>
		private const byte RTP_VERSION = 0x78;

		/// <summary>
		/// Initializes a new instance of the <see cref="Rtp"/> class.
		/// </summary>
		public Rtp()
		{ }

		/// <summary>
		/// Encodes the header.
		/// </summary>
		/// <param name="sequence">The sequence.</param>
		/// <param name="timestamp">The timestamp.</param>
		/// <param name="ssrc">The ssrc.</param>
		/// <param name="target">The target.</param>
		public void EncodeHeader(ushort sequence, uint timestamp, uint ssrc, Span<byte> target)
		{
			if (target.Length < HEADER_SIZE)
				throw new ArgumentException("Header buffer is too short.", nameof(target));

			target[0] = RTP_NO_EXTENSION;
			target[1] = RTP_VERSION;

			// Write data big endian
			BinaryPrimitives.WriteUInt16BigEndian(target[2..], sequence);  // header + magic
			BinaryPrimitives.WriteUInt32BigEndian(target[4..], timestamp); // header + magic + sizeof(sequence)
			BinaryPrimitives.WriteUInt32BigEndian(target[8..], ssrc);      // header + magic + sizeof(sequence) + sizeof(timestamp)
		}

		/// <summary>
		/// Are the rtp header.
		/// </summary>
		/// <param name="source">The source.</param>
		/// <returns>A bool.</returns>
		public bool IsRtpHeader(ReadOnlySpan<byte> source) => source.Length >= HEADER_SIZE && (source[0] == RTP_NO_EXTENSION || source[0] == RTP_EXTENSION) && source[1] == RTP_VERSION;

		/// <summary>
		/// Decodes the header.
		/// </summary>
		/// <param name="source">The source.</param>
		/// <param name="sequence">The sequence.</param>
		/// <param name="timestamp">The timestamp.</param>
		/// <param name="ssrc">The ssrc.</param>
		/// <param name="hasExtension">If true, has extension.</param>
		public void DecodeHeader(ReadOnlySpan<byte> source, out ushort sequence, out uint timestamp, out uint ssrc, out bool hasExtension)
		{
			if (source.Length < HEADER_SIZE)
				throw new ArgumentException("Header buffer is too short.", nameof(source));

			if ((source[0] != RTP_NO_EXTENSION && source[0] != RTP_EXTENSION) || source[1] != RTP_VERSION)
				throw new ArgumentException("Invalid RTP header.", nameof(source));

			hasExtension = source[0] == RTP_EXTENSION;

			// Read data big endian
			sequence = BinaryPrimitives.ReadUInt16BigEndian(source[2..]);
			timestamp = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
			ssrc = BinaryPrimitives.ReadUInt32BigEndian(source[8..]);
		}

		/// <summary>
		/// Calculates the packet size.
		/// </summary>
		/// <param name="encryptedLength">The encrypted length.</param>
		/// <param name="encryptionMode">The encryption mode.</param>
		/// <returns>An int.</returns>
		public int CalculatePacketSize(int encryptedLength, EncryptionMode encryptionMode) =>
			encryptionMode switch
			{
				EncryptionMode.XSalsa20Poly1305 => HEADER_SIZE + encryptedLength,
				EncryptionMode.XSalsa20Poly1305Suffix => HEADER_SIZE + encryptedLength + Interop.SodiumNonceSize,
				EncryptionMode.XSalsa20Poly1305Lite => HEADER_SIZE + encryptedLength + 4,
				_ => throw new ArgumentException("Unsupported encryption mode.", nameof(encryptionMode)),
			};

		/// <summary>
		/// Gets the data from packet.
		/// </summary>
		/// <param name="packet">The packet.</param>
		/// <param name="data">The data.</param>
		/// <param name="encryptionMode">The encryption mode.</param>
		public void GetDataFromPacket(ReadOnlySpan<byte> packet, out ReadOnlySpan<byte> data, EncryptionMode encryptionMode)
		{
			switch (encryptionMode)
			{
				case EncryptionMode.XSalsa20Poly1305:
					data = packet[HEADER_SIZE..];
					return;

				case EncryptionMode.XSalsa20Poly1305Suffix:
					data = packet.Slice(HEADER_SIZE, packet.Length - HEADER_SIZE - Interop.SodiumNonceSize);
					return;

				case EncryptionMode.XSalsa20Poly1305Lite:
					data = packet.Slice(HEADER_SIZE, packet.Length - HEADER_SIZE - 4);
					break;

				default:
					throw new ArgumentException("Unsupported encryption mode.", nameof(encryptionMode));
			}
		}

		/// <summary>
		/// Disposes the Rtp.
		/// </summary>
		public void Dispose()
		{

		}
	}
}
