using System;
using System.Drawing;

namespace LightCrosshair
{
    internal interface ICrosshairRenderBackend : IDisposable
    {
        bool AntiAlias { get; set; }
        float DpiScale { get; set; }
        Bitmap RenderIfNeeded(CrosshairProfile cfg);
        void Invalidate();
    }
}
