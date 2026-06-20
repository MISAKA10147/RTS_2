using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RTS
{
    public class Dialog_SetHiveName : Dialog_Rename<CompHiveSystem>
    {
        public Dialog_SetHiveName(CompHiveSystem renaming) : base(renaming)
        {
        }
    }
}
