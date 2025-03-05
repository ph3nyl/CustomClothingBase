﻿using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Commands.Handlers;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;
using System.Diagnostics;
using System.Text;
using static ACE.Server.WorldObjects.Player;

namespace CustomClothingBase;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static JsonSerializerOptions _jsonSettings;

    static JsonFileWatcher _contentWatcher;
    static string ModDir => ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
    static string StubDir => Path.Combine(ModDir, "stub");
    static string ContentDir => Path.Combine(ModDir, "json");
    public static string GetFilename(uint fileId) => Path.Combine(ContentDir, $"{fileId:X8}.json");
    public static bool JsonFileExists(uint fileId) => File.Exists(GetFilename(fileId));

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer.Settings;

        _jsonSettings = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            Converters = { },
            TypeInfoResolver = new HexTypeResolver(Settings.HexKeys),
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        //Dictionaries messier to handle
        List<JsonConverter> converters = new();
        if (Settings.HexKeys.Contains(nameof(ClothingBaseEffect)))
        {
            converters.Add(new HexKeyDictionaryConverter<uint, ClothingBaseEffect>());
            converters.Add(new HexKeyDictionaryConverter<uint, ClothingBaseEffectEx>());
        }

        foreach (var converter in converters)
            _jsonSettings.Converters.Add(converter);


        if (Settings.WatchContent)
        {
            Directory.CreateDirectory(ContentDir);
            _contentWatcher = new(ContentDir);
            ModManager.Log($"Watching ClothingBase changes in:\n{ContentDir}");
        }

        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        _contentWatcher?.Dispose();

        if (Settings.ClearCacheOnShutdown)
            ClearClothingCache();

        base.Stop();
    }

    #region Patches
    /// <summary>
    /// ClothingTable.Unpack. We're going to go ahead and add our custom information into this.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="__instance"></param>
    /// 
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClothingTable), nameof(ClothingTable.Unpack), new Type[] { typeof(BinaryReader) })]
    public static void PostUnpack(BinaryReader reader, ref ClothingTable __instance)
    {
        ClothingTable? cb = GetJsonClothing(__instance.Id);
        if (cb != null)
        {
            __instance = MergeClothingTable(__instance, cb);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DatDatabase), nameof(DatDatabase.GetReaderForFile), new Type[] { typeof(uint) })]
    public static bool PreGetReaderForFile(uint fileId, ref DatDatabase __instance, ref DatReader __result)
    {
        // File already exists in the dat file -- proceed to let it run normally
        if (__instance.AllFiles.ContainsKey(fileId))
            return true;
        else if (__instance.Header.DataSet == DatDatabaseType.Portal && fileId > 0x10000000 && fileId <= 0x10FFFFFF)
        {
            // We're trying to load a ClothingTable entry that does not exist in the Portal.dat. Does it exist as JSON?
            if (JsonFileExists(fileId) && createStubClothingBase(fileId))
            {
                string directory = ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
                string stubFilename = Path.Combine(directory, "stub", $"{fileId:X8}.bin");

                // Load our stub into the DatReader
                __result = new DatReader(stubFilename, 0, 12, 16);

                // No need to run the original function -- we've cheated it with the result!
                return false;
            }
        }
        //Return true to execute original
        return true;
    }

    /// <summary>
    /// Inserts cb2 contents into cb. If a ClothingBaseEffect or ClothingSubPalEffects exists, it will overwrite it.
    /// </summary>
    /// <param name="cb"></param>
    /// <param name="cb2"></param>
    /// <returns></returns>
    private static ClothingTable MergeClothingTable(ClothingTable cb, ClothingTable cb2)
    {
        foreach (var cbe in cb2.ClothingBaseEffects)
        {
            if (cb.ClothingBaseEffects.ContainsKey(cbe.Key))
                cb.ClothingBaseEffects[cbe.Key] = cbe.Value;
            else
                cb.ClothingBaseEffects.Add(cbe.Key, cbe.Value);
        }
        foreach (var csp in cb2.ClothingSubPalEffects)
        {
            if (cb.ClothingSubPalEffects.ContainsKey(csp.Key))
                cb.ClothingSubPalEffects[csp.Key] = csp.Value;
            else
                cb.ClothingSubPalEffects.Add(csp.Key, csp.Value);
        }
        return cb;
    }

    /// <summary>
    /// Clears the DatManager.PortalDat.FileCache of all ClothingTable entries, allowing us to pull any new or updated custom data
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameters"></param>
    [CommandHandler("clear-clothing-cache", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Clears the ClothingTable file cache.")]
    public static void HandleClearConsoleCache(Session session, params string[] parameters)
    {
        ClearClothingCache();
    }

    /// <summary>
    /// Exports a ClothingBase entry to a JSON file
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameters"></param>
    [CommandHandler("clothingbase-export", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Exports a ClothingBase entry to a JSON file in the CustomClothingBase mod folder.")]
    public static void HandleExportClothing(Session session, params string[] parameters)
    {
        string syntax = "clothingbase-export <id>";
        if (parameters == null || parameters?.Length < 1)
        {
            ModManager.Log(syntax);
            return;
        }

        uint clothingBaseId;
        if (parameters[0].StartsWith("0x"))
        {
            string hex = parameters[0].Substring(2);
            if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out clothingBaseId))
            {
                ModManager.Log(syntax);
                return;
            }
        }
        else
        if (!uint.TryParse(parameters[0], out clothingBaseId))
        {
            ModManager.Log(syntax);
            return;
        }

        ExportClothingBase(clothingBaseId);
    }

    [CommandHandler("export-clothing", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Exports ClothingBase entry to a JSON file in the CustomClothingBase mod folder.")]
    public static void HandleExportWeenieClothing(Session session, params string[] parameters)
    {
        if (session?.Player is not Player player)
            return;

        if (!player.TryGetCurrentSelection(out var wo))
        {
            player.SendMessage($"No object is selected.");
            return;
        }

        if (wo.ClothingBase is null)
        {
            player.SendMessage($"No ClothingBase found for {wo.Name}");
            return;
        }

        ExportClothingBase(wo.ClothingBase.Value);

        player.SendMessage($"Exported ClothingBase {wo.ClothingBase:X8} for {wo.Name} to:\n{GetFilename(wo.ClothingBase.Value)}");
    }

    [CommandHandler("display-clothing", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Exports ClothingBase entry to a JSON file in the CustomClothingBase mod folder.")]
    public static void HandleDisplayClothing(Session session, params string[] parameters)
    {
        if (session?.Player is not Player player)
            return;

        if (!player.TryGetCurrentSelection(out var wo))
        {
            player.SendMessage($"No object is selected.");
            return;
        }

        if (wo.ClothingBase is null || DatManager.PortalDat.ReadFromDat<ClothingTable>(wo.ClothingBase.Value) is not ClothingTable ct)
        {
            player.SendMessage($"No ClothingBase found for {wo.Name}");
            return;
        }

        StringBuilder sb = new();

        sb.Append($"\n{ct.Id:X8}");
        sb.Append($"\nClothingBaseEffect");
        foreach (var cbe in ct.ClothingBaseEffects)
        {
            sb.Append($"\n  {cbe.Key:X8}");
            foreach (var coe in cbe.Value.CloObjectEffects)
            {
                sb.Append($"\n    Index {coe.Index}");
                sb.Append($"\n    ModelId {coe.ModelId:X8}");
                sb.Append($"\n    CloTextureEffects ({coe.CloTextureEffects.Count})");
                foreach (var cte in coe.CloTextureEffects)
                {
                    sb.Append($"\n      NewTexture {cte.NewTexture:X8}");
                    sb.Append($"\n      OldTexture {cte.OldTexture:X8}");
                }
            }
        }
        sb.Append("\nCloSubPalEffect");
        foreach (var cbe in ct.ClothingSubPalEffects)
        {
            sb.Append($"\n  {cbe.Key:X8}");
            foreach (var csp in cbe.Value.CloSubPalettes)
            {
                sb.Append($"\n    PaletteSet {csp.PaletteSet:X8}");
                sb.Append($"\n    Ranges ({csp.Ranges.Count})");
                foreach (var cspr in csp.Ranges)
                {
                    sb.Append($"\n    NumColors {cspr.NumColors}");
                    sb.Append($"\n    Offset {cspr.Offset}");
                }
            }
        }
        ModManager.Log($"{sb}");
        player.SendMessage($"{sb}");
    }

    [CommandHandler("convert-clothing", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Attempts to convert all custom clothing content to the serializer in the settings.")]
    public static void ConvertClothing(Session session, params string[] parameters)
    {
        var conversionDir = Path.Combine(ContentDir, "converted");
        Directory.CreateDirectory(conversionDir);

        JsonSerializerOptions defaultOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        StringBuilder sb = new("Conversion to current JSON format:");
        foreach (var file in Directory.GetFiles(ContentDir, "*.json"))
        {
            FileInfo fi = new(file);
            if (!fi.RetryRead(out var json))
                continue;

            try
            {
                var content = JsonSerializer.Deserialize<ClothingTableEx>(json, defaultOptions);
                var converted = JsonSerializer.Serialize<ClothingTableEx>(content, _jsonSettings);
                var outpath = Path.Combine(conversionDir, fi.Name);
                
                File.WriteAllText(outpath, converted);
                sb.Append($"\nConverted {fi.Name}");
            }
            catch (Exception ex)
            {
                sb.Append($"\nFailed to convert {fi.Name}:\n{ex.GetFullMessage()}");
            }
        }
        ModManager.Log(sb.ToString());
    }
    #endregion

    private static bool createStubClothingBase(uint fileId)
    {
        // Create stub directory if it doesn't already exist       
        Directory.CreateDirectory(StubDir);

        string stubFilename = Path.Combine(StubDir, $"{fileId:X8}.bin");

        // If this already exists, no need to recreate
        if (File.Exists(stubFilename))
            return true;

        try
        {
            using (FileStream fs = new FileStream(stubFilename, FileMode.Create))
            {
                using (System.IO.BinaryWriter writer = new(fs))
                {
                    writer.Write((int)0); // NextAddress, since this is a dat "block", this goes first
                    writer.Write(fileId);
                    writer.Write((int)0); // num ClothingBaseEffects
                    writer.Write((int)0); // num ClothingSubPalEffects
                }
            }
        }
        catch (Exception ex)
        {
            ModManager.Log($"Error creating CustomClothingTable stub for {fileId}", ModManager.LogLevel.Error);
            return false;
        }

        return true;
    }

    private static ClothingTable? GetJsonClothing(uint fileId)
    {
        var fileName = GetFilename(fileId);
        if (JsonFileExists(fileId))
        {
            try
            {
                //Add retries
                FileInfo file = new(fileName);
                if (!file.RetryRead(out var jsonString, 10))
                    return null;

                var ctx = JsonSerializer.Deserialize<ClothingTableEx>(jsonString, _jsonSettings);
                var clothingTable = ctx.Convert();
                return clothingTable;
            }
            catch (Exception E)
            {
                ModManager.Log(E.GetFullMessage(), ModManager.LogLevel.Error);
                return null;
            }
        }
        return null;
    }

    private static void ExportClothingBase(uint clothingBaseId)
    {
        if (clothingBaseId < 0x10000000 || clothingBaseId > 0x10FFFFFF)
        {
            ModManager.Log($"{clothingBaseId:X8} is not a valid ClothingBase between 0x10000000 and 0x10FFFFFF");
            return;
        }

        if (!DatManager.PortalDat.AllFiles.ContainsKey(clothingBaseId))
        {
            ModManager.Log($"ClothingBase {clothingBaseId:X8} not found.");
            return;
        }

        string exportFilename = GetFilename(clothingBaseId);
        var cbToExport = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId);

        // make sure the mod/json folder exists -- if not, create it
        string path = Path.GetDirectoryName(exportFilename);
        Directory.CreateDirectory(path);

        try
        {
            var json = JsonSerializer.Serialize(cbToExport, _jsonSettings);
            File.WriteAllText(exportFilename, json);
            ModManager.Log($"Saved to {exportFilename}");
        }
        catch (Exception E)
        {
            ModManager.Log(E.GetFullMessage(), ModManager.LogLevel.Error);
        }
    }

    public static void ClearClothingCache()
    {
        uint count = 0;
        foreach (var e in DatManager.PortalDat.FileCache)
        {
            if (e.Key > 0x10000000 && e.Key <= 0x10FFFFFF)
            {
                DatManager.PortalDat.FileCache.TryRemove(e);
                count++;
            }
        }

        if (PlayerManager.GetAllOnline().FirstOrDefault() is not Player player)
            return;

        player.EnqueueBroadcast(new GameMessageObjDescEvent(player));
        foreach(var equipment in player.EquippedObjects.Values)
            player.EnqueueBroadcast(new GameMessageObjDescEvent(equipment));

        ModManager.Log($"Removed {count} ClothingTable entires from FileCache");
    }
}

public static class SelectionExtensions
{
    public static bool TryGetCurrentSelection(this Player player, out WorldObject wo, SearchLocations locations = SearchLocations.Everywhere)
    {
        wo = null;

        if (player is null)
            return false;

        //Try to find selected object ID
        var objectId = ObjectGuid.Invalid;
        if (player.HealthQueryTarget.HasValue)
            objectId = new ObjectGuid(player.HealthQueryTarget.Value);
        else if (player.ManaQueryTarget.HasValue)
            objectId = new ObjectGuid(player.ManaQueryTarget.Value);
        else if (player.CurrentAppraisalTarget.HasValue)
            objectId = new ObjectGuid(player.CurrentAppraisalTarget.Value);

        if (objectId == ObjectGuid.Invalid)
            return false;

#if REALM
        wo = player.FindObject(objectId, locations);
#else
        wo = player.FindObject(objectId.Full, locations);
#endif

        return wo is not null;
    }
}