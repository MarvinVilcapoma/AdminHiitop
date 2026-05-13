namespace AdminHiitop.Api.Application.DTOs.Common;

public sealed class FileDownloadResponse
{
    public byte[] Content { get; init; } = [];
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "download.bin";
}
