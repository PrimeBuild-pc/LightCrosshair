using System.Runtime.InteropServices;
using System.Text.Json;
internal static class GammaReadback
{
 [DllImport("user32.dll")] static extern nint GetDC(nint window);
 [DllImport("user32.dll")] static extern int ReleaseDC(nint window,nint dc);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern nint CreateDC(string driver,string? device,string? output,nint init);
 [DllImport("gdi32.dll")] static extern bool DeleteDC(nint dc);
 [DllImport("gdi32.dll",SetLastError=true)] static extern bool GetDeviceGammaRamp(nint dc,[Out] ushort[] ramp);
 // Samples Red[128] of each named display as fast as GDI allows, to catch a ramp briefly returning to its original value.
 public static void Watch(double seconds, string output, string[] displays)
 {
  var lines=new List<string>{"Ms,"+string.Join(",",displays)};
  var dcs=displays.Select(d=>CreateDC("DISPLAY",d,null,0)).ToArray();
  var ramp=new ushort[768]; var watch=System.Diagnostics.Stopwatch.StartNew();
  try
  {
   while(watch.Elapsed.TotalSeconds<seconds)
   {
    var row=new List<string>{watch.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)};
    foreach(var dc in dcs) row.Add(GetDeviceGammaRamp(dc,ramp)?ramp[128].ToString():"-1");
    lines.Add(string.Join(",",row)); Thread.Sleep(1);
   }
  }
  finally { foreach(var dc in dcs) DeleteDC(dc); }
  File.WriteAllLines(output,lines);
 }
 public static void Read(string output)
 {
  var rows=new List<object>();
  foreach(var apartment in new[]{ApartmentState.MTA,ApartmentState.STA})
  {
   var thread=new Thread(()=> {
    foreach(var name in new[]{"desktop","generic","\\\\.\\DISPLAY1","\\\\.\\DISPLAY2"})
    {
     nint dc=name=="desktop"?GetDC(0):CreateDC("DISPLAY",name=="generic"?null:name,null,0);
     var ramp=new ushort[768]; bool ok=GetDeviceGammaRamp(dc,ramp); int error=Marshal.GetLastWin32Error();
     rows.Add(new{Apartment=apartment.ToString(),Display=name,Success=ok,Error=error,Red128=ok?ramp[128]:-1,Hash=ok?Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MemoryMarshal.AsBytes(ramp.AsSpan()))):null});
     if(name=="desktop")ReleaseDC(0,dc);else DeleteDC(dc);
    }
   }); thread.SetApartmentState(apartment);thread.Start();thread.Join();
  }
  File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine(File.ReadAllText(output));
 }
}
