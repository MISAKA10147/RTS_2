using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RTS
{
    public class WorkGiver_FillMass : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerBuildings.allBuildingsColonist.FindAll(b => 
            pawn.CanReach(b,PathEndMode.Touch,Danger.Deadly) && b.HasComp<CompHiveSystem>());
        }
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return base.HasJobOnThing(pawn, t, forced) &&
                t.TryGetComp<CompHiveSystem>() is CompHiveSystem comp
                && comp.Mass < comp.Props.massLimit && comp.allowAutoRefuel;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.TryGetComp<CompHiveSystem>() is CompHiveSystem comp
                && comp.Mass < comp.CurMassLimit
                && GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForDef(ThingDefOf.Steel), PathEndMode.Touch,
                TraverseParms.For(pawn),9999,t0 =>
                !t0.IsForbidden(pawn) && pawn.CanReserve(t0,1,t0.stackCount)) is Thing steel) 
            {
                Job job = JobMaker.MakeJob(RTS_DefOf.RTS_Fill,steel,t);
                job.count = steel.stackCount;
                return job;
            }
            return null;
        }
    }
}
