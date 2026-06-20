using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RTS
{
    public class JobDriver_ReturnHive : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.Goto(TargetIndex.A,PathEndMode.Touch);
            yield return new Toil() 
            {
            initAction = () =>
            {
                if (this.TargetThingA.TryGetComp<CompHiveSystem>() is CompHiveSystem comp)
                {
                    this.pawn.DeSpawn();
                    comp.inner.TryAddOrTransfer(this.pawn);
                    if(comp.units.Contains(this.pawn)) comp.units.Remove(this.pawn);
                }
            }
            };
            yield break;
        }
    }
}
