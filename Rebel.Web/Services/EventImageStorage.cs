using Microsoft.AspNetCore.Http;

namespace Rebel.Web.Services
{
    public sealed class EventImageStorage : IEventImageStorage
    {
        private const long MaximumImageSize = 5 * 1024 * 1024;

        private static readonly Dictionary<string, string[]> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = [".jpg", ".jpeg"],
                ["image/png"] = [".png"],
                ["image/webp"] = [".webp"]
            };

        private readonly IWebHostEnvironment _environment;

        public EventImageStorage(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveAsync(
            IFormFile image,
            CancellationToken cancellationToken = default)
        {
            if (image.Length == 0)
            {
                throw new InvalidDataException("Choose a non-empty image file.");
            }

            if (image.Length > MaximumImageSize)
            {
                throw new InvalidDataException("The event poster must be 5 MB or smaller.");
            }

            if (!AllowedExtensions.TryGetValue(
                    image.ContentType,
                    out var allowedExtensions))
            {
                throw new InvalidDataException("Use a JPG, PNG or WebP image.");
            }

            var suppliedExtension = Path.GetExtension(image.FileName);

            if (!allowedExtensions.Contains(
                    suppliedExtension,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The image extension does not match its file type.");
            }

            await using (var validationStream = image.OpenReadStream())
            {
                var header = new byte[12];
                var bytesRead = await validationStream.ReadAsync(
                    header,
                    cancellationToken);

                if (!HasValidSignature(image.ContentType, header, bytesRead))
                {
                    throw new InvalidDataException("The selected file is not a valid image.");
                }
            }

            var storageDirectory = Path.Combine(
                _environment.WebRootPath,
                "images",
                "events");

            Directory.CreateDirectory(storageDirectory);

            var savedExtension = allowedExtensions[0];
            var fileName = $"event-{Guid.NewGuid():N}{savedExtension}";
            var physicalPath = Path.Combine(storageDirectory, fileName);

            try
            {
                await using var output = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                await image.CopyToAsync(output, cancellationToken);
            }
            catch
            {
                File.Delete(physicalPath);
                throw;
            }

            return $"/images/events/{fileName}";
        }

        public Task DeleteAsync(
            string? imageUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return Task.CompletedTask;
            }

            var normalizedUrl = imageUrl.Replace('\\', '/');
            if (!normalizedUrl.StartsWith(
                    "/images/events/event-",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            var fileName = Path.GetFileName(normalizedUrl);
            var extension = Path.GetExtension(fileName);
            var isGeneratedEventImage =
                fileName.StartsWith("event-", StringComparison.OrdinalIgnoreCase) &&
                AllowedExtensions.Values
                    .SelectMany(extensions => extensions)
                    .Contains(extension, StringComparer.OrdinalIgnoreCase);

            if (!isGeneratedEventImage)
            {
                return Task.CompletedTask;
            }

            var storageDirectory = Path.GetFullPath(Path.Combine(
                _environment.WebRootPath,
                "images",
                "events"));
            var physicalPath = Path.GetFullPath(Path.Combine(
                storageDirectory,
                fileName));
            var storagePrefix = storageDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!physicalPath.StartsWith(
                    storagePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            return Task.CompletedTask;
        }

        private static bool HasValidSignature(
            string contentType,
            byte[] header,
            int bytesRead)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" =>
                    bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF,
                "image/png" =>
                    bytesRead >= 8 &&
                    header.AsSpan(0, 8).SequenceEqual(
                        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                "image/webp" =>
                    bytesRead >= 12 &&
                    header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                    header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => false
            };
        }
    }
}
