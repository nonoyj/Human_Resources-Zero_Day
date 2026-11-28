// RenderDump -- OPT-IN ONLY. Never compiled by CompileCheck.csproj or by Unity.
//
// MenuView.BuildRoom() blits seven kit sprites into a 480x270 buffer. If Unity cannot read those textures
// (Read/Write disabled, or an importer that hands back a null texture) every Blit silently returns false
// and the composed room is transparent -- the menu then shows only its own text over the desk scene, which
// is exactly what the player photographed in v119.3. The screenshots cannot tell "the sprites are missing"
// from "the sprites are there but wrong", so this harness renders the room offscreen and prints the answer.
//
// Build:  dotnet build tools/UnityStubs/RenderDump.csproj -c Release -o /tmp/rd
// Run:    /tmp/rd/RenderDump  -> writes /tmp/render/menu_room.png
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using HRZeroDay.Game.Interaction;
using HRZeroDay.Core.Rules;

public static class RenderDump
{
    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "unity/Assets/Resources";
        string outPath = args.Length > 1 ? args[1] : "/tmp/render/menu_room.png";
        var mpLoader = Resources.Loader;
        Resources.Loader = path => LoadPng(Path.Combine(root, path + ".png"));
        try
        {
            var view = new MenuView();
            var fld = typeof(MenuView).GetField("_glyphs", BindingFlags.Instance | BindingFlags.NonPublic);
            fld.SetValue(view, HRZeroDay.Game.MonitorScreenView.Load(
                "fonts/monitor_font_5x7", HRZeroDay.Game.MonitorScreenView.PaletteCached));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            // "nolamp": build the room with the warm practical switched OFF, so the dump can be DIFFED
            // against the normal one. That difference IS the lamp's light, and it is the only way to check
            // that the light lands on the desk rather than on the wall without opening Unity.
            if (args.Length > 2 && args[2] == "nolamp")
                typeof(MenuView).GetField("_lampOn", flags).SetValue(view, false);
            var m = typeof(MenuView).GetMethod("BuildRoom", flags);
            if (m == null) { Console.WriteLine("no BuildRoom"); return 2; }
            m.Invoke(view, null);
            var room = typeof(MenuView).GetField("_room", flags).GetValue(view) as Color32[];
            if (room == null) { Console.WriteLine("no room buffer"); return 3; }

            // the boot reveal is time-based and the stub's clock is frozen at 0; skip it, or every row is
            // still "not typed yet" and the dump shows a menu with no options in it
            typeof(MenuView).GetField("_bootReveal", flags).SetValue(view, 1f);
            if (args.Length > 2 && args[2] == "keyboard") { view.TypeOnDesk(); view.TypeOnDesk(); typeof(MenuView).GetField("_typeReveal", flags).SetValue(view, 1f); }
            if (args.Length > 2 && args[2] == "lamphover") { view.SetPointer(MenuLayout.LampX + 28, MenuLayout.LampY + 70, 5); }
            if (args.Length > 2 && args[2] == "fade") { view.TypeOnDesk(); view.TypeOnDesk(); typeof(MenuView).GetField("_typeReveal", flags).SetValue(view, 0.45f); typeof(MenuView).GetField("_typeLeft", flags).SetValue(view, 0f); }

            // Paint() needs a real Texture2D + sprite renderer, which the stub does not build. So run the
            // same passes Paint() runs, in the same order, on a copy -- this is the frame the player sees.
            var frame = (Color32[])room.Clone();
            foreach (var name in new[] { "StackScreens", "Dust", "Props", "Text" })
            {
                var step = typeof(MenuView).GetMethod(name, flags);
                if (step == null) { Console.WriteLine("no " + name); continue; }
                step.Invoke(view, step.GetParameters().Length == 0 ? null : new object[] { frame });
            }
            Write(frame, 480, 270, outPath);
            Console.WriteLine("wrote " + outPath);
            return 0;
        }
        finally { Resources.Loader = mpLoader; }
    }

    private static Texture2D LoadPng(string file)
    {
        if (!File.Exists(file)) { Console.WriteLine("  missing " + file); return null; }
        var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(file);
        var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false) { isReadable = true };
        var px = new Color32[img.Width * img.Height];
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                var p = img[x, y];
                px[(img.Height - 1 - y) * img.Width + x] = new Color32(p.R, p.G, p.B, p.A);   // bottom-up
            }
        tex.SetPixels32(px);
        return tex;
    }

    private static void Write(Color32[] px, int w, int h, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = px[y * w + x];
                img[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32(c.r, c.g, c.b, c.a);
            }
        using (var fs = File.Create(path)) img.Save(fs, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
    }
}
