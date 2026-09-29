using System;
using System.Collections.Generic;
using System.IO;

// 直接解析 PE 资源表，从 exe 提取全部 RT_ICON 图标，生成标准多尺寸 ICO 文件（C# 5 兼容）
class ExtractIcon
{
    class IconImage
    {
        public int Size;
        public byte[] Data;
        public bool IsPng;
        public ushort Planes;
        public ushort Bpp;
        public IconImage(int size, byte[] data, bool isPng, ushort planes, ushort bpp)
        { Size = size; Data = data; IsPng = isPng; Planes = planes; Bpp = bpp; }
    }

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: ExtractIcon <source.exe> <output.ico>");
            return;
        }
        string src = Path.GetFullPath(args[0]);
        string dst = Path.GetFullPath(args[1]);

        if (!File.Exists(src))
        {
            Console.WriteLine("source not found: " + src);
            return;
        }

        List<IconImage> icons = ParsePeIcons(src);
        if (icons.Count == 0)
        {
            Console.WriteLine("no RT_ICON resources found in " + src);
            return;
        }

        // 按尺寸降序去重
        var seen = new HashSet<int>();
        var final = new List<IconImage>();
        foreach (var ic in icons)
        {
            if (seen.Add(ic.Size)) final.Add(ic);
        }
        final.Sort(delegate(IconImage a, IconImage b) { return b.Size.CompareTo(a.Size); });

        foreach (var ic in final)
            Console.WriteLine("size " + ic.Size + " (" + (ic.IsPng ? "png" : "bmp") + ", " + ic.Data.Length + " bytes)");

        SaveIco(dst, final);
        Console.WriteLine("saved: " + dst + " (" + final.Count + " sizes)");
    }

    // ---------- PE 资源解析 ----------

    static List<IconImage> ParsePeIcons(string path)
    {
        byte[] f = File.ReadAllBytes(path);
        var list = new List<IconImage>();

        int e_lfanew = BitConverter.ToInt32(f, 0x3C);
        if (e_lfanew + 24 > f.Length) return list;
        if (BitConverter.ToUInt32(f, e_lfanew) != 0x00004550) return list; // "PE\0\0"

        int coff = e_lfanew + 4;
        ushort nSections = BitConverter.ToUInt16(f, coff + 2);
        int opt = coff + 20;
        ushort magic = BitConverter.ToUInt16(f, opt);
        bool pe32plus = magic == 0x20B;

        // DataDirectory[2] = 资源表
        int ddBase = opt + (pe32plus ? 112 : 96);
        uint resRva = BitConverter.ToUInt32(f, ddBase + 2 * 8);
        if (resRva == 0) return list;

        int secOff = opt + (pe32plus ? 240 : 224);
        var secs = new List<uint[]>();
        for (int i = 0; i < nSections; i++)
        {
            int s = secOff + i * 40;
            secs.Add(new uint[] {
                BitConverter.ToUInt32(f, s + 8),   // VirtualSize
                BitConverter.ToUInt32(f, s + 12),  // VirtualAddress
                BitConverter.ToUInt32(f, s + 16),  // SizeOfRawData
                BitConverter.ToUInt32(f, s + 20)   // PointerToRawData
            });
        }

        uint rootOff = RvaToOff(secs, resRva);
        if (rootOff + 16 > f.Length) return list;

        uint type3 = 0, type14 = 0; // RT_ICON=3, RT_GROUP_ICON=14
        uint rootEntries = EntryCount(f, rootOff);
        for (uint i = 0; i < rootEntries; i++)
        {
            uint e = rootOff + 16 + i * 8;
            uint id = BitConverter.ToUInt32(f, (int)e);
            uint off = BitConverter.ToUInt32(f, (int)e + 4);
            if (id == 3) type3 = Resolve(rootOff, off);
            if (id == 14) type14 = Resolve(rootOff, off);
        }
        if (type3 == 0 || type14 == 0) return list;

        // 遍历所有图标组
        uint gCount = EntryCount(f, type14);
        for (uint gi = 0; gi < gCount; gi++)
        {
            uint e = type14 + 16 + gi * 8;
            uint subDir = Resolve(rootOff, BitConverter.ToUInt32(f, (int)e + 4));
            uint lCount = EntryCount(f, subDir);
            if (lCount == 0) continue;
            // 取第一个语言叶子
            uint leafOff = Resolve(rootOff, BitConverter.ToUInt32(f, (int)(subDir + 16) + 4));
            uint dataRva = BitConverter.ToUInt32(f, (int)leafOff);
            uint fileOff = RvaToOff(secs, dataRva);
            if (fileOff + 6 > f.Length) continue;

            int count = BitConverter.ToUInt16(f, (int)fileOff + 4);
            for (int k = 0; k < count; k++)
            {
                int ent = (int)fileOff + 6 + k * 14;
                byte w = f[ent];
                byte h = f[ent + 1];
                ushort planes = BitConverter.ToUInt16(f, ent + 4);
                ushort bpp = BitConverter.ToUInt16(f, ent + 6);
                uint bytesInRes = BitConverter.ToUInt32(f, ent + 8);
                ushort nid = BitConverter.ToUInt16(f, ent + 12);
                if (bytesInRes == 0) continue;

                // 在 RT_ICON 目录中找到 nid 对应的图标数据
                uint iconCount = EntryCount(f, type3);
                for (uint ii = 0; ii < iconCount; ii++)
                {
                    uint ie = type3 + 16 + ii * 8;
                    if (BitConverter.ToUInt32(f, (int)ie) != nid) continue;
                    uint iSub = Resolve(rootOff, BitConverter.ToUInt32(f, (int)ie + 4));
                    uint ilCount = EntryCount(f, iSub);
                    if (ilCount == 0) break;
                    uint iLeaf = Resolve(rootOff, BitConverter.ToUInt32(f, (int)(iSub + 16) + 4));
                    uint idataRva = BitConverter.ToUInt32(f, (int)iLeaf);
                    uint idataSize = BitConverter.ToUInt32(f, (int)iLeaf + 4);
                    uint iOff = RvaToOff(secs, idataRva);
                    if (iOff + idataSize > f.Length) break;
                    byte[] raw = new byte[idataSize];
                    Array.Copy(f, (int)iOff, raw, 0, (int)idataSize);
                    bool isPng = raw.Length > 8 && raw[0] == 0x89 && raw[1] == 0x50
                        && raw[2] == 0x4E && raw[3] == 0x47;
                    list.Add(new IconImage(w == 0 ? 256 : (int)w, raw, isPng, planes, bpp));
                    break;
                }
            }
        }
        return list;
    }

    // RVA -> 文件偏移
    static uint RvaToOff(List<uint[]> secs, uint rva)
    {
        foreach (var sc in secs)
        {
            if (rva >= sc[1] && rva < sc[1] + Math.Max(sc[0], sc[2]))
                return rva - sc[1] + sc[3];
        }
        return rva;
    }

    // 资源目录条目偏移（目录或数据）相对资源段起始
    static uint Resolve(uint rootOff, uint off)
    {
        return rootOff + (off & 0x7FFFFFFF);
    }

    // 目录中的条目数（低 16 位）
    static uint EntryCount(byte[] f, uint dirOff)
    {
        return BitConverter.ToUInt32(f, (int)(dirOff + 14)) & 0xFFFF;
    }

    // ---------- ICO 组装 ----------

    static void SaveIco(string path, List<IconImage> images)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);                 // reserved
            w.Write((ushort)1);                 // type = icon
            w.Write((ushort)images.Count);
            int offset = 6 + 16 * images.Count;
            foreach (var img in images)
            {
                byte wh = img.Size >= 256 ? (byte)0 : (byte)img.Size;
                w.Write(wh);                    // width
                w.Write(wh);                    // height
                w.Write((byte)0);               // palette
                w.Write((byte)0);               // reserved
                w.Write(img.Planes == 0 ? (ushort)1 : img.Planes);
                w.Write(img.Bpp == 0 ? (ushort)32 : img.Bpp);
                w.Write((uint)img.Data.Length);
                w.Write((uint)offset);
                offset += img.Data.Length;
            }
            foreach (var img in images)
            {
                w.Write(img.Data);
            }
        }
    }
}
