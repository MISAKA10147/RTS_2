using HarmonyLib;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RTS
{
    [StaticConstructorOnStartup]
    public static class PatchMain
    {
        static PatchMain()
        {
            Harmony harmony = new Harmony("RTS_Patch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
