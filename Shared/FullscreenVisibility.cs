using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QuotaTray {
// Only suppress overlays on the monitor occupied by the foreground fullscreen app.
sealed class FullscreenVisibility:IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;public Rectangle Bounds{get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}}}
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect bounds);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int size);
 [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int GetFrameBounds(IntPtr window,int attribute,out NativeRect bounds,int size);
 readonly Form owner;
 readonly Action<bool> changed;
 readonly Timer timer=new Timer{Interval=200};
 public bool Hidden{get;private set;}
 public FullscreenVisibility(Form form,Action<bool> onChange){owner=form;changed=onChange;timer.Tick+=(s,e)=>Refresh();}
 public void Start(){Refresh();timer.Start();}
 public bool Refresh(){bool next=ShouldHide(owner.Bounds);if(next!=Hidden){Hidden=next;changed(next);}return Hidden;}
 public static bool CoversMonitor(Rectangle window,Rectangle monitor){const int tolerance=2;return window.Width>0&&window.Height>0&&window.Left<=monitor.Left+tolerance&&window.Top<=monitor.Top+tolerance&&window.Right>=monitor.Right-tolerance&&window.Bottom>=monitor.Bottom-tolerance;}
 public static bool IsDesktopClass(string name){return name=="Progman"||name=="WorkerW"||name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd";}
 public static bool ShouldHide(Rectangle ringBounds){
  IntPtr window=GetForegroundWindow();if(window==IntPtr.Zero)return false;
  IntPtr root=GetAncestor(window,2);if(root!=IntPtr.Zero)window=root;
  if(!IsWindowVisible(window)||IsIconic(window))return false;
  var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);if(IsDesktopClass(name.ToString()))return false;
  NativeRect bounds;if(!GetWindowRect(window,out bounds))return false;
  try{NativeRect frame;if(GetFrameBounds(window,9,out frame,Marshal.SizeOf(typeof(NativeRect)))==0&&frame.Bounds.Width>0&&frame.Bounds.Height>0)bounds=frame;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  return CoversMonitor(bounds.Bounds,Screen.FromRectangle(ringBounds).Bounds);
 }
 public void Dispose(){timer.Stop();timer.Dispose();}
}
}
