using System.Diagnostics;

namespace CustomClothingBase;

public class JsonFileWatcher
{
    private FileSystemWatcher _contentWatcher;

    public JsonFileWatcher(string directoryToWatch)
    {
        _contentWatcher = new FileSystemWatcher
        {
            Path = directoryToWatch,
            Filter = "*.json", // Only watch JSON files
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        _contentWatcher.Changed += OnFileChanged;
        _contentWatcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        PatchClass.ClearClothingCache();
        //var name = Path.GetFileNameWithoutExtension(e.FullPath);
        //if (!uint.TryParse(name.Substring(2), out var id))
        //    return;

        //if (id > 0x10000000 && id <= 0x10FFFFFF && DatManager.PortalDat.FileCache.TryRemove(id, out var value))
        //    ModManager.Log($"Cleared {id}");
        //else
        //    ModManager.Log($"Nothing to clear {id}");
    }

    public void Dispose() => _contentWatcher.Dispose();
}
