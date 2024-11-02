using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace DotaCrypto.Benchmarks;

[MemoryDiagnoser]
public class DecryptBenchmarks
{
	private byte[] _data = null!;
	private Decryptor _decryptor;
	private byte[] _output = null!;

	[Benchmark(Baseline = true)]
	public void Decrypt()
	{
		_decryptor.Decrypt(_data, _output);
	}

	[Benchmark]
	public void DecryptFromCryptoStream()
	{
		using var fs = File.OpenRead("Resources/private_metadata.bin");
		using var cs = new CryptoStream(fs, _decryptor.CreateCryptoTransform(), CryptoStreamMode.Read);
		cs.CopyTo(Stream.Null);
	}

	[Benchmark]
	public void DecryptParallelized()
	{
		_decryptor.DecryptParallelized(_data, _output);
	}

	[Benchmark]
	public void DecryptStream()
	{
		using var fs = File.OpenRead("Resources/private_metadata.bin");
		_decryptor.DecryptStream(fs, Stream.Null);
	}

	[Benchmark]
	public async Task DecryptStreamAsync()
	{
		await using var fs = File.OpenRead("Resources/private_metadata.bin");
		await _decryptor.DecryptStreamAsync(fs, Stream.Null);
	}

	[Benchmark]
	public void DecryptStreamParallelized()
	{
		using var fs = File.OpenRead("Resources/private_metadata.bin");
		_decryptor.DecryptStreamParallelized(fs, Stream.Null);
	}

	[Benchmark]
	public async Task DecryptStreamParallelizedAsync()
	{
		await using var fs = File.OpenRead("Resources/private_metadata.bin");
		await _decryptor.DecryptStreamParallelizedAsync(fs, Stream.Null);
	}

	[GlobalSetup]
	public void Setup()
	{
		_data = File.ReadAllBytes("Resources/private_metadata.bin");
		_output = new byte[_data.Length];
		_decryptor = new Decryptor(1277083357);
	}
}
