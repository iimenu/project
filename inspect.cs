using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main() {
        try {
            Assembly pun = Assembly.LoadFrom(@"C:\Users\ASUSN\OneDrive\Desktop\iiReborn\menu\menu\References\Managed\PhotonRealtime.dll");
            Type cbType = pun.GetType("Photon.Realtime.IOnEventCallback");
            
            Assembly asm = Assembly.LoadFrom(@"C:\Users\ASUSN\OneDrive\Desktop\iiReborn\menu\menu\References\Managed\Assembly-CSharp.dll");
            foreach (Type t in asm.GetTypes()) {
                if (cbType != null && cbType.IsAssignableFrom(t)) {
                    Console.WriteLine("Implements IOnEventCallback: " + t.FullName);
                }
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
