using Microsoft.Extensions.Configuration;
using MyTCG.Application;
using SkiaSharp;

namespace MyTCG.Infrastructure;
public sealed class LocalMediaStorage(IConfiguration config) : IMediaStorage
{
    private string Root => Path.GetFullPath(config["Media:Root"] ?? "media");

    public async Task<Guid> Save(Guid tcg, Stream stream, string contentType, CancellationToken ct)
    {
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new TcgException(400, "image_format", "Formats acceptés : JPEG, PNG, WebP.");
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int n;
        while ((n = await stream.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + n > 5 * 1024 * 1024)
                throw new TcgException(400, "image_size", "Image limitée à 5 Mo.");
            await buffer.WriteAsync(bytes.AsMemory(0, n), ct);
        }

        buffer.Position = 0;
        using var codec = SKCodec.Create(buffer);
        if (codec is null)
            throw new TcgException(400, "image_format", "Image invalide.");
        var expected = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => ""
        };
        if (expected != contentType)
            throw new TcgException(400, "image_format", "Le contenu ne correspond pas au format déclaré.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 4096 || info.Height > 4096 || info.Width * (long)info.Height > 16_000_000 || codec.FrameCount > 1)
            throw new TcgException(400, "image_size", "Image fixe de 4096 pixels maximum requise.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new TcgException(400, "image_format", "Image endommagée.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var directory = Path.Combine(Root, tcg.ToString());
        Directory.CreateDirectory(directory);
        var imageId = Guid.NewGuid();
        var name = imageId.ToString("N") + ".png";
        await using var output = File.Create(Path.Combine(directory, name));
        encoded.SaveTo(output);
        await output.FlushAsync(ct);
        return imageId;
    }

    public Task<(Stream Stream, string ContentType)> Read(Guid tcg, string name, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{32}\\.png$"))
            throw new TcgException(404, "not_found", "Image introuvable.");
        var path = Path.Combine(Root, tcg.ToString(), name);
        if (!File.Exists(path))
            throw new TcgException(404, "not_found", "Image introuvable.");
        return Task.FromResult<(Stream, string)>((File.OpenRead(path), "image/png"));
    }
}

