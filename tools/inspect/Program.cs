using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
    var asmPath = @"c:\Users\Eric\Documents\GitHub\wizard-survivors\bin\Debug\net8.0\WizardSurvivors.dll"; // this might be a problem
        Console.WriteLine($"Looking for assembly: {asmPath}");
        if (!System.IO.File.Exists(asmPath)) { Console.WriteLine("Assembly not found"); return; }
        var asm = Assembly.LoadFile(asmPath);
        var types = asm.GetTypes();
        foreach (var t in types.Where(t => t.Name.Contains("TitleScreen")))
        {
            Console.WriteLine($"Found type: {t.FullName}");
        }
    }
}
