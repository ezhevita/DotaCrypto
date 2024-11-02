using System;
using System.Security.Cryptography;

namespace DotaCrypto;

public sealed class DotaCryptoTransform : ICryptoTransform
{
	private readonly Decryptor _decryptor;

	internal DotaCryptoTransform(Decryptor decryptor) => _decryptor = decryptor;

	public void Dispose()
	{
	}

	public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
	{
		_decryptor.PerformDecryption(
			inputBuffer.AsSpan().Slice(inputOffset, InputBlockSize), outputBuffer.AsSpan().Slice(outputOffset, OutputBlockSize));

		return OutputBlockSize;
	}

	public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
	{
		var result = new byte[OutputBlockSize];
		_decryptor.PerformDecryption(inputBuffer.AsSpan().Slice(inputOffset, InputBlockSize), result);

		return result;
	}

	public bool CanReuseTransform => true;
	public bool CanTransformMultipleBlocks => false;
	public int InputBlockSize => 8;
	public int OutputBlockSize => 8;
}
