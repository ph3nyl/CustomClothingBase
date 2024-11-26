namespace CustomClothingBase;

public class Settings
{
    public bool WatchContent { get; set; } = true;
    public bool ClearCacheOnShutdown { get; set; } = true;

    public HashSet<string> HexKeys { get; set; } = new()
    {
        "PaletteSet",
        "ModelId",
        "Id",
    };
}