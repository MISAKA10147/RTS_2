using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RTS
{
    public class ITab_HiveSystem : ITab
    {
        public ITab_HiveSystem() 
        {
            this.labelKey = "ITab_HiveSystem";
        }
        public CompHiveSystem Comp => this.SelThing.TryGetComp<CompHiveSystem>();
        public override void OnOpen()
        {
            base.OnOpen();
            if (Find.WindowStack.Windows.ToList().Find(w => w is Window_HiveSystem)
                is Window_HiveSystem window)
            {
                Find.WindowStack.TryRemove(window);
                Find.WindowStack.Add(new Window_HiveSystem(this.Comp));
            }
            else 
            {
                Find.WindowStack.Add(new Window_HiveSystem(this.Comp));
            }
            this.CloseTab();
        }

        protected override void FillTab()
        {
            this.CloseTab();
        }


    }
}
