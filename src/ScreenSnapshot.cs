using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Framelet {
    internal static class ScreenSnapshot {
        // Physical pixels, with no video encoder or even-dimension padding.
        internal static Bitmap Capture(Rectangle area, bool includeCursor) {
            if(!Native.ValidRegion(area))throw new ArgumentException("截图范围不在当前屏幕内，请重新选择。");
            var image=new Bitmap(area.Width,area.Height,PixelFormat.Format24bppRgb);
            try {
                using(var graphics=Graphics.FromImage(image)) {
                    IntPtr screen=Native.GetDC(IntPtr.Zero),destination=graphics.GetHdc();
                    try {
                        if(screen==IntPtr.Zero||!Native.BitBlt(destination,0,0,area.Width,area.Height,screen,area.X,area.Y,0x40CC0020))throw new IOException("读取屏幕画面失败，请确认屏幕未锁定。");
                        if(includeCursor)DrawCursor(destination,area.Location);
                    } finally { graphics.ReleaseHdc(destination);if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen); }
                }
                return image;
            } catch { image.Dispose();throw; }
        }
        private static void DrawCursor(IntPtr destination,Point origin) {
            var cursor=new Native.CURSORINFO{Size=Marshal.SizeOf(typeof(Native.CURSORINFO))};
            if(!Native.GetCursorInfo(ref cursor)||(cursor.Flags&1)==0)return;
            Native.ICONINFO icon;
            if(!Native.GetIconInfo(cursor.Cursor,out icon))return;
            try { Native.DrawIconEx(destination,cursor.Position.X-origin.X-(int)icon.HotspotX,cursor.Position.Y-origin.Y-(int)icon.HotspotY,cursor.Cursor,0,0,0,IntPtr.Zero,3); }
            finally { if(icon.Mask!=IntPtr.Zero)Native.DeleteObject(icon.Mask);if(icon.Color!=IntPtr.Zero)Native.DeleteObject(icon.Color); }
        }
        internal static void SavePng(Image image,string destination) {
            string stage=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { image.Save(stage,ImageFormat.Png);if(File.Exists(destination))File.Replace(stage,destination,null);else File.Move(stage,destination); }
            finally { try{File.Delete(stage);}catch{} }
        }
    }
}
