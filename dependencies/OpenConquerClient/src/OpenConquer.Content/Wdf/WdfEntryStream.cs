namespace OpenConquer.Content.Wdf;

/// <summary>
/// A read-only stream over one entry of a WDF archive. Reads stop at the end of the entry.
/// </summary>
internal sealed class WdfEntryStream : Stream
{
    private readonly FileStream _archiveStream;
    private readonly long _entryOffset;
    private readonly long _length;

    private long _position;
    private bool _disposed;

    public WdfEntryStream(FileStream archiveStream, uint entryOffset, uint length)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);

        _archiveStream = archiveStream;
        _entryOffset = entryOffset;
        _length = length;

        _archiveStream.Seek(_entryOffset, SeekOrigin.Begin);
    }

    public override bool CanRead => !_disposed && _archiveStream.CanRead;
    public override bool CanSeek => !_disposed && _archiveStream.CanSeek;
    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _position;
        }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int requestedLength = (int)Math.Min(buffer.Length, _length - _position);

        if (requestedLength <= 0)
        {
            return 0;
        }

        int bytesRead = _archiveStream.Read(buffer[..requestedLength]);

        if (bytesRead == 0)
        {
            throw new EndOfStreamException("The WDF archive ended before the selected entry was fully read.");
        }

        _position += bytesRead;

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(_position + offset),
            SeekOrigin.End => checked(_length + offset),

            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown seek origin."),
        };

        if (position < 0 || position > _length)
        {
            throw new IOException("Cannot seek outside a WDF entry.");
        }

        _archiveStream.Seek(checked(_entryOffset + position), SeekOrigin.Begin);

        _position = position;

        return position;
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        throw new NotSupportedException("WDF entry streams are read-only.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        throw new NotSupportedException("WDF entry streams are read-only.");
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);

            return;
        }

        try
        {
            if (disposing)
            {
                _archiveStream.Dispose();
            }
        }
        finally
        {
            _disposed = true;

            base.Dispose(disposing);
        }
    }
}
