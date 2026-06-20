using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.UI.CanvasScaler;

namespace RTS
{
    public class UnitCompProperties : CompProperties
    {
        public UnitCompProperties()
        {
            this.compClass = typeof(UnitComp);
        }

        public float mass;
        public float cost;
    }
    public class UnitComp : ThingComp
    {
        public UnitCompProperties Props => (UnitCompProperties)this.props;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (this.root != null && this.root.Map != this.parent.Map) 
            {
                if (this.root.TryGetComp<CompHiveSystem>()
    is CompHiveSystem compHive && compHive.units.Contains(this.parent))
                {
                    compHive.units.Remove((Pawn)this.parent);
                    compHive.CalcuateCost();
                }
            }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (this.parent.Faction == null || !this.parent.Faction.IsPlayer) yield break;
            yield return new Command_Action()
            {
                defaultLabel = "RTS_Return".Translate(),
                defaultDesc = "RTS_ReturnDesc".Translate(),
                //icon = ContentFinder<Texture2D>.Get(),
                action = () =>
                {
                    if (this.root != null && this.root.Map == this.parent.Map
                    && ((Pawn)this.parent).CanReach(this.root,Verse.AI.PathEndMode.InteractionCell,Danger.Deadly)) 
                    {
                        ((Pawn)this.parent).jobs.StartJob(JobMaker.MakeJob(RTS_DefOf.RTS_Return, 
                            this.root));
                    }
                }
            };
            if (this.parent.Map != null)
            {
                Command_SpecialAction bind = new Command_SpecialAction()
                {
                    defaultLabel = "RTS_BindHive".Translate(),
                    defaultDesc = "RTS_BindHiveDesc".Translate(),
                    postActionHive = (comp) =>
                    {
                        if (comp.cost + this.Props.cost > comp.CostLimit)
                        {
                            Messages.Message("UnableToBindCost".Translate(),
                                MessageTypeDefOf.RejectInput);
                            return;
                        }
                        if (this.root != null && this.root.TryGetComp<CompHiveSystem>()
                        is CompHiveSystem compHive && compHive.units.Contains(this.parent))
                        {
                            compHive.units.Remove((Pawn)this.parent);
                            compHive.CalcuateCost();
                        }
                        this.root = (Building)comp.parent;
                        if (!comp.units.Contains(this.parent))
                        {
                            comp.units.Add((Pawn)this.parent);
                            comp.CalcuateCost();
                        }
                    }
                };
                bind.GiveActionHive(this.parent.Map);
                yield return bind;
            }
            yield break;
        }
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (this.root != null && this.root.TryGetComp<CompHiveSystem>()
                is CompHiveSystem comp) 
            {
                if (comp.units.Contains(this.parent)) 
                {
                    comp.units.Remove((Pawn)this.parent);
                }
                comp.CalcuateCost();
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref this.root,"root");
        }
        public Thing root;
    }
}
