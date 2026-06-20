using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse.AI.Group;

namespace RTS
{
    public class LordJob_HiveSystem : LordJob
    {
        public override StateGraph CreateGraph()
        {
            StateGraph result = new StateGraph();
            result.AddToil(new LordToil_HiveSystem());
            return result;
        }
    }


    public class LordToil_HiveSystem : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (var pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DutyDefOf.Defend,pawn.Position);
            }
        }
    }
}
