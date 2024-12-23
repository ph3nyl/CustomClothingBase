using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using System.Collections.Generic;

//ClothingTableEx
//  ClothingBaseEffectEx (Dic)
//      CloObjectEffect (List)
//          Index
//          ModelId
//          CloTextureEffectEx (List)
//              OldTexture
//              NewTexture
//  CloSubPalEffectEx (Dic)
//      Icon
//      CloSubPaletteEx (List)
//          CloSubPaletteRangeEx (List)
//              Offset
//              NumColors
public class ClothingTableEx : ClothingTable
{
    public new Dictionary<uint, ClothingBaseEffectEx> ClothingBaseEffects { get; set; } = new();
    public new Dictionary<uint, CloSubPalEffectEx> ClothingSubPalEffects { get; set; } = new();
    public new uint Id { get; set; }

    public ClothingTable Convert()
    {
        ClothingTable value = new();
        value.Id = Id;

        foreach (var cbe in ClothingBaseEffects)
            value.ClothingBaseEffects.Add(cbe.Key, cbe.Value.Convert());

        foreach (var cbe in ClothingSubPalEffects)
            value.ClothingSubPalEffects.Add(cbe.Key, cbe.Value.Convert());

        return value;
    }
}

public class CloSubPalEffectEx : CloSubPalEffect
{
    public uint Icon { get; set; }
    public new List<CloSubPaletteEx> CloSubPalettes { get; set; } = new();

    public CloSubPalEffect Convert()
    {
        CloSubPalEffect value = new();
        value.Icon = Icon;
        value.CloSubPalettes.AddRange(CloSubPalettes.ConvertAll(x => x.Convert()));
        return value;
    }
}

public class CloSubPaletteEx : CloSubPalette
{
    public new List<CloSubPaletteRange> Ranges { get; set; } = new();
    public new uint PaletteSet { get; set; }

    public CloSubPalette Convert()
    {
        CloSubPalette value = new();
        value.PaletteSet = PaletteSet;
        value.Ranges.AddRange(Ranges);
        return value;
    }
}

public class ClothingBaseEffectEx : ClothingBaseEffect
{
    public new List<CloObjectEffectEx> CloObjectEffects { get; set; } = new();

    public ClothingBaseEffect Convert()
    {
        ClothingBaseEffect value = new();
        value.CloObjectEffects.AddRange(CloObjectEffects.ConvertAll(x => x.Convert()));
        return value;
    }
}

public class CloObjectEffectEx : CloObjectEffect
{
    public new uint Index { get; set; }
    public new uint ModelId { get; set; }
    public new List<CloTextureEffectEx> CloTextureEffects { get; set; } = new();

    public CloObjectEffect Convert()
    {
        CloObjectEffect value = new();
        value.Index = Index;
        value.ModelId = ModelId;
        value.CloTextureEffects.AddRange(CloTextureEffects.ConvertAll(x => x.Convert()));
        return value;
    }
}

public class CloTextureEffectEx : CloTextureEffect
{
    public new uint OldTexture { get; set; }
    public new uint NewTexture { get; set; }

    public CloTextureEffect Convert()
    {
        return new()
        {
            NewTexture = this.NewTexture,
            OldTexture = this.OldTexture,
        };
    }
}