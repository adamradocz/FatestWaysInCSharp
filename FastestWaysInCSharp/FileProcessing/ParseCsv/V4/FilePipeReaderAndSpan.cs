using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace FastestWaysInCSharp.FileProcessing.ParseCsv.V4;

public static class FilePipeReaderAndSpan
{
    private const byte _delimiterAsByte = (byte)';';
    private const byte _newLineAsByte = (byte)'\n';
    private const byte _forwardSlashAsByte = (byte)'/';

    private static readonly byte[] _header = Encoding.UTF8.GetBytes("Id;Guid;Gender;GivenName;Surname;City;StreetAddress;EmailAddress;Birthday;Domain");

    private static async IAsyncEnumerable<FakeName> ParseAsync(string filePath)
    {
        var filePipeReader = new FilePipeReader(filePath);

        while (true)
        {
            var result = await filePipeReader.ReadAsync();
            var buffer = result.Buffer;

            ParseLines(ref buffer);

            filePipeReader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        filePipeReader.Complete();
    }

    private static IEnumerable<FakeName> ParseLines(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new BufferReader(buffer);

        while (!reader.End)
        {
            var span = reader.UnreadSegment;
            int index = span.IndexOf(_newLineAsByte);
            int length;

            if (index != -1)
            {
                length = index;
                var fakeName = GetFakeName(span.Slice(0, index));
                if (fakeName != null)
                {
                    yield return fakeName;
                }
            }
            else
            {
                // We didn't find the new line in the current segment, see if it's another segment
                var current = reader.Position;
                var linePos = buffer.Slice(current).PositionOf(_newLineAsByte);

                if (linePos == null)
                {
                    // Nope
                    break;
                }

                // We found one, so get the line and parse it
                var line = buffer.Slice(current, linePos.Value);
                var fakeName = ParseLine(line);
                if (fakeName != null)
                {
                    yield return fakeName;
                }

                length = (int)line.Length;
            }

            // Advance past the line + the \n
            reader.Advance(length + 1);
        }

        // Update the buffer
        buffer = buffer.Slice(reader.Position);
    }

    private static FakeName? ParseLine(in ReadOnlySequence<byte> line)
    {
        FakeName? fakeName;

        // Lines are always small so we incur a small copy if we happen to cross a buffer boundary
        if (line.IsSingleSegment)
        {
            fakeName = GetFakeName(line.First.Span);
        }
        else if (line.Length < 256)
        {
            // Small lines we copy to the stack
            Span<byte> stackLine = stackalloc byte[(int)line.Length];
            line.CopyTo(stackLine);
            fakeName = GetFakeName(stackLine);
        }
        else
        {
            // Should be extremely rare
            int length = (int)line.Length;
            byte[]? buffer = ArrayPool<byte>.Shared.Rent(length);
            line.CopyTo(buffer);
            fakeName = GetFakeName(buffer.AsSpan(0, length));
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return fakeName;
    }

    private static FakeName? GetFakeName(ReadOnlySpan<byte> line)
    {
        // Skip the header
        if (line.IndexOf(_header) >= 0)
        {
            return default;
        }

        var fakeName = new FakeName();

        // Id
        int delimiterAt = line.IndexOf(_delimiterAsByte);
        _ = Utf8Parser.TryParse(line.Slice(0, delimiterAt), out int id, out _);
        fakeName.Id = id;
        line = line.Slice(delimiterAt + 1);

        // Guid
        delimiterAt = line.IndexOf(_delimiterAsByte);
        _ = Utf8Parser.TryParse(line.Slice(0, delimiterAt), out Guid guid, out _);
        fakeName.Guid = guid;
        line = line.Slice(delimiterAt + 1);

        // Gender
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.Gender = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // GivenName
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.GivenName = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // Surname
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.Surname = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // City
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.City = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // StreetAddress
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.StreetAddress = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // EmailAddress
        delimiterAt = line.IndexOf(_delimiterAsByte);
        fakeName.EmailAddress = Encoding.UTF8.GetString(line.Slice(0, delimiterAt));
        line = line.Slice(delimiterAt + 1);

        // Birthday
        // Month
        int slashAt = line.IndexOf(_forwardSlashAsByte);
        _ = Utf8Parser.TryParse(line.Slice(0, slashAt), out int month, out _);
        line = line.Slice(slashAt + 1);

        // Day
        slashAt = line.IndexOf(_forwardSlashAsByte);
        _ = Utf8Parser.TryParse(line.Slice(0, slashAt), out int day, out _);
        line = line.Slice(slashAt + 1);

        // Year
        delimiterAt = line.IndexOf(_delimiterAsByte);
        _ = Utf8Parser.TryParse(line.Slice(0, delimiterAt), out int year, out _);
        fakeName.Birthday = new DateOnly(year, month, day);
        line = line.Slice(delimiterAt + 1);

        // Domain
        fakeName.Domain = new string(Encoding.UTF8.GetString(line));

        return fakeName;
    }
}
