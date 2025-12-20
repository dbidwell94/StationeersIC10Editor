namespace StationeersIC10Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using ImGuiNET;

using UnityEngine;

using static Settings;

// style struct to hold color and background info
// there will be more fields later (squiggle underlines for instance)
public struct Style
{
    public uint Color = 0xFFFFFFFF;
    public uint Background = 0;

    public Style(uint color = 0xFFFFFFFF, uint background = 0)
    {
        Color = color;
        Background = background;
    }

    public Style(string htmlColor, string htmlBackground = null)
    {
        Color = ICodeFormatter.ColorFromHTML(htmlColor);
        Background = htmlBackground != null ? ICodeFormatter.ColorFromHTML(htmlBackground) : 0;
    }

    public static implicit operator Style(uint color)
    {
        return new Style(color, 0);
    }

}

