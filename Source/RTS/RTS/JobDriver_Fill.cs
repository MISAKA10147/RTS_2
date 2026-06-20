using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RTS
{
    public class JobDriver_Fill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.TargetThingA, this.job, 1,this.TargetThingA.stackCount, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.Goto(TargetIndex.B, PathEndMode.InteractionCell);
            Toil toil = Toils_General.WaitWith(TargetIndex.B, 
                this.TargetThingA.stackCount * 20, true,true);
            toil.initAction = () => { this.pawn.rotationTracker.FaceTarget(TargetB); };
            yield return toil;
            yield return new Toil()
            {
                initAction = () =>
                {
                    if (this.TargetThingB.TryGetComp<CompHiveSystem>()
                    is CompHiveSystem comp) 
                    {
                        float differentCount = (comp.CurMassLimit - comp.Mass) /
                        comp.Props.massPerCount;
                        if (differentCount < this.TargetThingA.stackCount)
                        {
                            this.TargetThingA.stackCount -= (int)differentCount;
                            comp.Mass = comp.CurMassLimit;
                        }
                        else 
                        {
                            comp.Mass += this.TargetThingA.stackCount * comp.Props.massPerCount;
                            this.TargetThingA.Destroy();
                        }
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Delay
            };
            yield break;
        }
    }
}
