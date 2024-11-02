using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DotaCrypto;

[SuppressMessage(
	"Performance", "CA1815:Override equals and operator equals on value types",
	Justification = "This struct is useless for comparing.")]
public readonly struct Decryptor
{
	private readonly Keystream _keystream;

	public Decryptor(uint key) => _keystream = GenerateKeystream(key);

	private const byte BlockSize = 8;

	public void DecryptParallelized(ReadOnlyMemory<byte> input, Memory<byte> output, int maxDegreeOfParallelism = -1)
	{
		if (input.Length != output.Length)
		{
			throw new ArgumentException("The input and output arrays' lengths are different.");
		}

		if (input.Length % BlockSize != 0)
		{
			throw new ArgumentException("Input array length is not divisible by 8 (block size).");
		}

		if (input.Length == 0)
		{
			return;
		}

		var instance = this;

		Parallel.For(
			0, input.Length / BlockSize, new ParallelOptions {MaxDegreeOfParallelism = maxDegreeOfParallelism},
			i => instance.PerformDecryption(
				input.Slice(i * BlockSize, BlockSize).Span, output.Slice(i * BlockSize, BlockSize).Span));
	}

	public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
	{
		if (input.Length != output.Length)
		{
			throw new ArgumentException("The input and output arrays' lengths are different.");
		}

		if (input.Length % BlockSize != 0)
		{
			throw new ArgumentException("Input array length is not divisible by 8 (block size).");
		}

		if (input.Length == 0)
		{
			return;
		}

		for (var i = 0; i < input.Length; i += BlockSize)
		{
			PerformDecryption(input.Slice(i, BlockSize), output.Slice(i, BlockSize));
		}
	}

	public void DecryptStream(Stream input, Stream output, int bufferSize = BlockSize * 128)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(output);

		if (!input.CanRead)
		{
			throw new ArgumentException("Input stream is not readable.");
		}

		if (!output.CanWrite)
		{
			throw new ArgumentException("Output stream is not writable.");
		}

		if (bufferSize % BlockSize != 0)
		{
			throw new ArgumentException("Buffer size is not divisible by 8 (block size).");
		}

		var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

		try
		{
			var length = input.Read(buffer);
			do
			{
				for (var i = 0; i < length; i += BlockSize)
				{
					PerformDecryption(buffer.AsSpan(i, BlockSize), buffer.AsSpan(i, BlockSize));
				}

				output.Write(buffer.AsSpan(0, length));
				length = input.Read(buffer);
			} while (length > 0);
		} finally
		{
			Array.Clear(buffer);
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public async Task DecryptStreamAsync(Stream input, Stream output, int bufferSize = BlockSize * 128)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(output);

		if (!input.CanRead)
		{
			throw new ArgumentException("Input stream is not readable.");
		}

		if (!output.CanWrite)
		{
			throw new ArgumentException("Output stream is not writable.");
		}

		if (bufferSize % BlockSize != 0)
		{
			throw new ArgumentException("Buffer size is not divisible by 8 (block size).");
		}

		var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

		try
		{
			var length = await input.ReadAsync(buffer).ConfigureAwait(false);
			do
			{
				for (var i = 0; i < length; i += BlockSize)
				{
					PerformDecryption(buffer.AsSpan(i, BlockSize), buffer.AsSpan(i, BlockSize));
				}

				await output.WriteAsync(buffer.AsMemory(0, length)).ConfigureAwait(false);
				length = await input.ReadAsync(buffer).ConfigureAwait(false);
			} while (length > 0);
		} finally
		{
			Array.Clear(buffer);
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public void DecryptStreamParallelized(Stream input, Stream output, int bufferSize = BlockSize * 4096,
		int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(output);

		if (!input.CanRead)
		{
			throw new ArgumentException("Input stream is not readable.");
		}

		if (!output.CanWrite)
		{
			throw new ArgumentException("Output stream is not writable.");
		}

		if (bufferSize % BlockSize != 0)
		{
			throw new ArgumentException("Buffer size is not divisible by 8 (block size).");
		}

		var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = maxDegreeOfParallelism};
		var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

		try
		{
			var instance = this;
			var length = input.Read(buffer);
			do
			{
				Parallel.For(
					0, length / BlockSize, parallelOptions,
					i => instance.PerformDecryption(
						buffer.AsSpan(i * BlockSize, BlockSize), buffer.AsSpan(i * BlockSize, BlockSize)));

				output.Write(buffer.AsSpan(0, length));
				length = input.Read(buffer);
			} while (length > 0);
		} finally
		{
			Array.Clear(buffer);
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public async Task DecryptStreamParallelizedAsync(Stream input, Stream output, int bufferSize = BlockSize * 4096,
		int maxDegreeOfParallelism = -1)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(output);

		if (!input.CanRead)
		{
			throw new ArgumentException("Input stream is not readable.");
		}

		if (!output.CanWrite)
		{
			throw new ArgumentException("Output stream is not writable.");
		}

		if (bufferSize % BlockSize != 0)
		{
			throw new ArgumentException("Buffer size is not divisible by 8 (block size).");
		}

		var parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = maxDegreeOfParallelism};
		var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

		try
		{
			var instance = this;
			var length = await input.ReadAsync(buffer).ConfigureAwait(false);
			do
			{
				Parallel.For(
					0, length / BlockSize, parallelOptions,
					i => instance.PerformDecryption(
						buffer.AsSpan(i * BlockSize, BlockSize), buffer.AsSpan(i * BlockSize, BlockSize)));

				await output.WriteAsync(buffer.AsMemory(0, length)).ConfigureAwait(false);

				length = await input.ReadAsync(buffer).ConfigureAwait(false);
			} while (length > 0);
		} finally
		{
			Array.Clear(buffer);
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public ICryptoTransform CreateCryptoTransform() => new DotaCryptoTransform(this);

	internal void PerformDecryption(ReadOnlySpan<byte> input, Span<byte> output)
	{
		const byte KeystreamRoundLength = 6;
		const byte DecryptionRounds = 8;

		var block1 = BinaryPrimitives.ReadUInt32BigEndian(input[..sizeof(int)]);
		var block2 = BinaryPrimitives.ReadUInt32BigEndian(input[sizeof(int)..]);

		for (var round = 0; round < DecryptionRounds; round++)
		{
			var keystreamIndex = (DecryptionRounds - 1 - round) * KeystreamRoundLength;
			var roundKeystream = _keystream[keystreamIndex..(keystreamIndex + KeystreamRoundLength)];

			var block1Key = GenerateXorKey(block2, roundKeystream.Slice(KeystreamRoundLength / 2, KeystreamRoundLength / 2));
			block1 ^= block1Key;

			var block2Key = GenerateXorKey(block1, roundKeystream[..(KeystreamRoundLength / 2)]);
			block2 ^= block2Key;
		}

		MemoryMarshal.AsBytes([BinaryPrimitives.ReverseEndianness(block2)]).CopyTo(output);
		MemoryMarshal.AsBytes([BinaryPrimitives.ReverseEndianness(block1)]).CopyTo(output[sizeof(int)..]);
	}

	private static uint GenerateXorKey(uint block, ReadOnlySpan<uint> keystream)
	{
		const uint Lsb10Bits = 0b1111111111;

		var scramble1 = ((block >> 16) & Lsb10Bits) | ((block & 0b11) << 18) | ((block >> 24) << 10);
		var scramble2 = (block & Lsb10Bits) | ((block & (Lsb10Bits << 8)) << 2);
		var scrambledKey = keystream[2] & (scramble1 ^ scramble2);

		var temp1 = scrambledKey ^ scramble2 ^ keystream[1];
		var temp2 = scrambledKey ^ scramble1 ^ keystream[0];

		var decryptionTable = Utilities.DecryptionTable;
		ref var decTableRef = ref MemoryMarshal.GetReference(decryptionTable);

		var key = Unsafe.Add(ref decTableRef, (temp1 & Lsb10Bits) | 0xC00) |
			Unsafe.Add(ref decTableRef, (temp2 & Lsb10Bits) | 0x400) |
			Unsafe.Add(ref decTableRef, (temp1 >> 10) | 0x800) |
			Unsafe.Add(ref decTableRef, temp2 >> 10);

		return key;
	}

	private static Keystream GenerateKeystream(uint key)
	{
		const byte NumbersPerRound = 3;
		var key1 = key ^ 0x0633a998;
		var key2 = key ^ 0x4e6c32b9;

		Span<byte> buffer = stackalloc byte[8];
		BinaryPrimitives.WriteUInt32BigEndian(buffer, key1);
		BinaryPrimitives.WriteUInt32BigEndian(buffer[sizeof(int)..], key2);
		var ks = MemoryMarshal.Cast<byte, ushort>(buffer);

		var keystream = new Keystream();
		for (var round = 0; round < 16; round++)
		{
			ks = round switch
			{
				1 or 2 or 3 or 7 or 8 => Shuffle(ks, 1),
				4 or 5 or 10 or 12 or 14 => Shuffle(ks, 3),
				6 or 9 or 11 or 13 or 15 => Shuffle(ks, 2),
				_ => ks
			};

			var offset = round * NumbersPerRound;
			for (var i = 0; i < 15; i++)
			{
				var index = offset + i % NumbersPerRound;
				keystream[index] <<= 4;
				for (var j = 0; j < ks.Length; j++)
				{
					keystream[index] |= (uint)((ks[j] & 1) << (ks.Length - 1 - j));
					ks[j] = InvertLsbAndAppendAsMsb(ks[j]);
				}
			}
		}

		return keystream;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Span<ushort> Shuffle(Span<ushort> input, byte startIndex)
	{
		if (AdvSimd.IsSupported)
		{
			var vector = Vector64.Create((ReadOnlySpan<ushort>)input);
#pragma warning disable CA1857 // The argument should be a constant for optimal performance - constant will be inlined
			AdvSimd.ExtractVector64(vector, vector, startIndex).CopyTo(input);
#pragma warning restore CA1857 // The argument should be a constant for optimal performance
		} else
		{
			// .NET does not translate any Vector64 operations to hardware intrinsics and falls back to software implementation
			Span<ushort> copy = stackalloc ushort[input.Length];
			input.CopyTo(copy);

			switch (startIndex % 4)
			{
				case 1:
					input[3] = copy[0];
					input[2] = copy[3];
					input[1] = copy[2];
					input[0] = copy[1];

					break;
				case 2:
					input[3] = copy[1];
					input[2] = copy[0];
					input[1] = copy[3];
					input[0] = copy[2];

					break;
				case 3:
					input[3] = copy[2];
					input[2] = copy[1];
					input[1] = copy[0];
					input[0] = copy[3];

					break;
			}
		}

		return input;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ushort InvertLsbAndAppendAsMsb(ushort value) => (ushort)((value >> 1) | (~(value & 1) << 15));

	[InlineArray(48)]
	private struct Keystream
	{
		public uint Ks0;
	}
}
