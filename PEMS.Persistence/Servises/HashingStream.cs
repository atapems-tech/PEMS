using System.Security.Cryptography;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS — Enterprise File Storage Management» بند ۲۱: SHA-256 حین Streaming محاسبه
/// می‌شود - هرگز فایل دوبار خوانده نمی‌شود و هرگز کل آن در حافظه بافر نمی‌شود. این Stream فقط
/// یک Pass-through با محاسبه‌ی Hash است؛ IStorageProviderClient.SaveAsync معمولاً با
/// content.CopyToAsync(...) این Stream را می‌خواند، پس Hash به‌طور طبیعی حین همان یک بار
/// نوشتن محاسبه می‌شود.
/// </summary>
public sealed class HashingStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _finalized;
    private string? _hexHash;

    public long BytesRead { get; private set; }

    public HashingStream(Stream inner)
    {
        _inner = inner;
    }

    public string GetHexHash()
    {
        if (!_finalized)
        {
            _hexHash = Convert.ToHexStringLower(_hash.GetHashAndReset());
            _finalized = true;
        }
        return _hexHash!;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        if (read > 0)
        {
            _hash.AppendData(buffer[..read]);
            BytesRead += read;
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        if (read > 0)
        {
            _hash.AppendData(buffer.Span[..read]);
            BytesRead += read;
        }
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
