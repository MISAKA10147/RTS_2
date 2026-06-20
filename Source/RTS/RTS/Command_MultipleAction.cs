using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RTS
{
    public class Command_MultipleAction : Command_Action
    { 
        public override void MergeWith(Gizmo other)
        {
            base.MergeWith(other);
            if (other is Command_SpecialAction command) 
            {
                this.action += command.action;
            }
        } 
    }
}
