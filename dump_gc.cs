using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        try {
            Assembly asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Gorilla Tag_Data\Managed\Assembly-CSharp.dll");
            Type gcType = asm.GetType("GorillaComputer");
            if (gcType == null) { Console.WriteLine("GorillaComputer not found."); return; }
            foreach (var prop in gcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)) {
                if (prop.Name.ToLower().Contains("voice") || prop.Name.ToLower().Contains("ptt")) Console.WriteLine("Field: " + prop.Name + " (" + prop.FieldType.Name + ")");
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
