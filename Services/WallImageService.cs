namespace WallPanelPlanner.Services;

public sealed class WallImageService : IWallImageService
{
    public async Task<string?> PickAndImportImageAsync(CancellationToken cancellationToken = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Seleziona immagine parete",
            FileTypes = FilePickerFileType.Images
        });

        if (result is null)
        {
            return null;
        }

        var extension = System.IO.Path.GetExtension(result.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".img" : extension;
        var targetFolder = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "wall-images");
        Directory.CreateDirectory(targetFolder);

        var targetPath = System.IO.Path.Combine(targetFolder, $"{Guid.NewGuid():N}{safeExtension}");

        await using var sourceStream = await result.OpenReadAsync();
        await using var destinationStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        return targetPath;
    }
}
