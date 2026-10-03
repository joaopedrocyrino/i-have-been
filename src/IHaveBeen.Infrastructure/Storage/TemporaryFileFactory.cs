using IHaveBeen.Application.Abstractions;

namespace IHaveBeen.Infrastructure.Storage;

internal sealed class TemporaryFileFactory : ITemporaryFileFactory
{
    public Stream Create()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
            BufferSize = 65536, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(Path.Combine(Path.GetTempPath(), $"ihb-{Guid.NewGuid():N}"), options);
    }
}
