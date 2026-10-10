using System;
using System.Runtime.InteropServices;

namespace DogSC.Replica {
 // Resolve the selected HMONITOR through DXGI; WinForms display order can differ across adapters.
 static class DesktopDuplication {
  [DllImport("dxgi.dll")]static extern int CreateDXGIFactory1(ref Guid id,out IntPtr factory);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Enumerate(IntPtr self,uint index,out IntPtr item);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Describe(IntPtr self,out OutputDescription description);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int DescribeAdapter(IntPtr self,out AdapterDescription description);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct AdapterDescription{[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Name;public uint Vendor,Device,Subsystem,Revision;public UIntPtr VideoMemory,SystemMemory,SharedMemory;public long Luid;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct OutputDescription{[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;public int Left,Top,Right,Bottom;public int AttachedToDesktop,Rotation;public IntPtr Monitor;}
  static T Method<T>(IntPtr obj,int slot){return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),typeof(T));}
  public static uint Vendor(int index){IntPtr factory=IntPtr.Zero,adapter=IntPtr.Zero;try{Guid id=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");if(CreateDXGIFactory1(ref id,out factory)<0||Method<Enumerate>(factory,12)(factory,(uint)index,out adapter)<0)return 0;AdapterDescription d;return Method<DescribeAdapter>(adapter,8)(adapter,out d)>=0?d.Vendor:0;}catch{return 0;}finally{if(adapter!=IntPtr.Zero)Marshal.Release(adapter);if(factory!=IntPtr.Zero)Marshal.Release(factory);}}
  public static bool HasVendor(uint vendor){IntPtr factory=IntPtr.Zero;try{Guid id=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");if(CreateDXGIFactory1(ref id,out factory)<0)return false;var enumerate=Method<Enumerate>(factory,12);for(uint i=0;i<32;i++){IntPtr adapter;if(enumerate(factory,i,out adapter)<0)break;try{AdapterDescription d;if(Method<DescribeAdapter>(adapter,8)(adapter,out d)>=0&&d.Vendor==vendor)return true;}finally{Marshal.Release(adapter);}}}catch{}finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);}return false;}
  public static int[] Find(IntPtr monitor){IntPtr factory=IntPtr.Zero;try{Guid id=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");if(CreateDXGIFactory1(ref id,out factory)<0)return null;var adapters=Method<Enumerate>(factory,12);for(uint a=0;a<64;a++){IntPtr adapter;if(adapters(factory,a,out adapter)<0)break;try{var outputs=Method<Enumerate>(adapter,7);for(uint o=0;o<64;o++){IntPtr output;if(outputs(adapter,o,out output)<0)break;try{OutputDescription description;if(Method<Describe>(output,7)(output,out description)>=0&&description.Monitor==monitor)return new[]{(int)a,(int)o};}finally{Marshal.Release(output);}}}finally{Marshal.Release(adapter);}}}catch{return null;}finally{if(factory!=IntPtr.Zero)Marshal.Release(factory);}return null;}
 }
}
