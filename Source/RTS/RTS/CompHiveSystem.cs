using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using static UnityEngine.UI.CanvasScaler;

namespace RTS
{
    public class CompPropertiesHiveSystem : CompProperties 
    {
        public CompPropertiesHiveSystem() 
        {
            this.compClass = typeof(CompHiveSystem);
        }

        public int costLimit = 100;
        public float massLimit = 100;
        public float massPerCount = 1;
        public float healPointPerMass = 10f;
        public float healMassPerUnit = 1;
        public int healInterval = 25;
        public float massToRestorePart = 10;
        public float initMass = 10;
        public float recyclePercent = 1f;
        public ThingDef resource;
        public List<UnitData> units = new List<UnitData>();
    }
    public class CompHiveSystem : ThingComp, IThingHolder,IRenameable
    {
        public CompPropertiesHiveSystem Props => (CompPropertiesHiveSystem)this.props;
        public override string TransformLabel(string label)
        { 
            return this.hiveName;
        }
        public float CostLimit => this.Props.costLimit;
        public float CurMassLimit => this.targetMass * this.Props.massLimit;
        public float Mass
        {
            get => this.mass;
            set
            { 
                this.mass = Math.Min(this.Props.massLimit,value);
                if (this.mass <= 0) 
                {
                    this.mass = 0;
                }
            }
        }
        public CompHiveSystem()
        {
            this.inner = new ThingOwner<Pawn>(this);
        }
        public Lord Lord 
        {
            get 
            {
                var lord = ((Building)this.parent).GetLord();
                if (lord == null && this.parent.Map != null) 
                {
                    lord = LordMaker.MakeNewLord(this.parent.Faction,new LordJob_HiveSystem()
                        ,this.parent.Map);
                    lord.AddBuilding(this.parent as Building);
                }
                return lord;
            }
        }

        public string RenamableLabel 
        {
            get => this.hiveName; set =>this.hiveName = value; }

        public string BaseLabel => this.parent.def.label;

        public string InspectLabel => this.hiveName;

        public override void PostPostMake()
        {
            base.PostPostMake();
            this.hiveName = this.parent.def.label;
            this.targetMass = this.Props.massLimit;
            this.mass = this.Props.initMass;
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.CalcuateCost(); 
        }
        public override void CompTick()
        {
            this.inner.DoTick();
            if (this.spawnings.Any() && ! this.pause
                && this.cost < this.CostLimit) 
            {
                var data = this.spawnings.First();
                data.spawnTime++;
                if (data.spawnTime >= data.data.spawnTime)
                {
                    EndBuild(data);
                }
            }
            if (this.autoRepair && this.parent.IsHashIntervalTick(this.Props.healInterval))
            {
                HealUnits();
            }
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (Find.WindowStack.Windows.ToList().Find(w => w is Window_HiveSystem window
            && window.comp == this) is Window_HiveSystem hiveWindow) 
            {
                Find.WindowStack.TryRemove(hiveWindow);
            }
        }
        private void HealUnits()
        {
            this.healCost = 0f;
            foreach (var pawn in this.inner)
            {
                if (this.Mass <= 0)
                {
                    break;
                }
                float healMass = Math.Min(this.Props.healMassPerUnit, this.Mass);
                float healPoint = healMass * this.Props.healPointPerMass;
                var injuries = new List<Hediff_Injury>();
                pawn.health.hediffSet.GetHediffs<Hediff_Injury>(ref injuries);
                foreach (var injury in injuries)
                { 
                    if (Prefs.DevMode) Log.Message(injury.Label);
                    float heal = Math.Min(injury.Severity, healPoint);
                    injury.Heal(heal);
                    healPoint -= heal;
                    this.Mass -= heal / this.Props.healPointPerMass;
                    this.healCost += heal / this.Props.healPointPerMass;
                }
                if (healPoint > 0)
                {
                    List<Hediff_MissingPart> misseds = new List<Hediff_MissingPart>();
                    pawn.health.hediffSet.GetHediffs<Hediff_MissingPart>(ref misseds,
                        (Hediff_MissingPart h) => h.Part.parent != null &&
                        pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h
                        .Part.parent)
                        == null
                        &&
                        pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent)
                        == null);
                    foreach (var miss in misseds)
                    {
                        if (Prefs.DevMode) Log.Message(miss.Label); 
                        if (healPoint < this.Props.massToRestorePart) 
                        {
                            break;
                        }
                        BodyPartRecord part = miss.Part;
                        pawn.health.RemoveHediff(miss);
                        Hediff hediff5 = pawn.health.AddHediff(HediffDefOf.Misc, part, null, null);
                        float partHealth = pawn.health.hediffSet.GetPartHealth(part);
                        hediff5.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
                        pawn.health.hediffSet.Notify_Regenerated(partHealth - hediff5.Severity);
                        healPoint -= this.Props.massToRestorePart;
                        this.healCost += this.Props.massToRestorePart / this.Props.healPointPerMass;
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action() 
            {
            defaultLabel = "RTS_AllReturn".Translate(),
            defaultDesc = "RTS_AllReturnDesc".Translate(),
            //icon = ContentFinder<Texture2D>.Get(),
            action = () => 
            {
                foreach (var unit in this.units.ListFullCopy())
                {
                    if (unit == null || unit.Dead || !unit.Spawned) return; 
                    unit.jobs.StartJob(JobMaker.MakeJob(RTS_DefOf.RTS_Return,this.parent));
                }
            }
            };
            yield return new Command_Action()
            {
                defaultLabel = "RTS_Eject".Translate(),
                defaultDesc = "RTS_EjectDesc".Translate(),
                //icon = ContentFinder<Texture2D>.Get(),
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_Slider("RTS_EjectText".Translate()
                        , 0, (int)(this.Mass / this.Props.massPerCount), (c)
                        =>
                        {
                            if (c <= 0) return;
                            int i = Mathf.FloorToInt(this.mass);
                            this.mass -= c * this.Props.massPerCount;
                            while (i > 0)
                            {
                                Thing thing = ThingMaker.MakeThing(ThingDefOf.Steel, null);
                                thing.stackCount = Mathf.Min(i, ThingDefOf.Steel.stackLimit);
                                i -= thing.stackCount;
                                GenPlace.TryPlaceThing(thing, this.parent.Position, this.parent.Map, ThingPlaceMode.Near, null, null, null, 1);
                                thing.SetForbidden(true, true);
                            }
                        }));
                }
            };
            if (!(this.parent is Building_PassengerShuttle))
            {
                yield return new Command_Action()
                {
                    defaultLabel = "RTS_RenameHive".Translate(),
                    defaultDesc = "RTS_RenameHiveDesc".Translate(),
                    //icon = ContentFinder<Texture2D>.Get(),
                    action = () =>
                    {
                        Find.WindowStack.Add(new Dialog_SetHiveName(this));
                    }
                };
            }
            yield return new Gizmo_SetMassLevel(this);
            if (Prefs.DevMode) 
            {
                yield return new Command_Action()
                {
                    defaultLabel = "Dev:FillMax",
                    action = () =>
                    {
                        this.mass = this.Props.massLimit;
                    }
                };
            }
            yield break;
        }
        public void EndBuild(UnitSpawnData data)
        {
            Pawn pawn = (Pawn)PawnGenerator.GeneratePawn(data.data.kind, parent.Faction);
            if (this.autoLaunch)
            {
                this.Launch(pawn,false);
            }
            else
            {
                this.inner.TryAdd(pawn);
            }
            data.count--;
            data.spawnTime = 0;
            if (data.count <= 0)
            {
                this.spawnings.Remove(data);
            }
            this.CalcuateCost();
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return this.inner;
        }
        public void CalcuateCost() 
        {
            this.units.RemoveAll(u => u == null || u.Dead || u.Map != this.parent.Map);
            float cost = 0;
            foreach (var unit in units)
            {
                if (unit.TryGetComp<UnitComp>() is UnitComp comp)
                {
                    cost += comp.Props.cost;
                }
                else 
                {
                    cost++;
                }
            }
            foreach (var pawn in inner)
            {
                if (pawn.TryGetComp<UnitComp>() is UnitComp comp)
                {
                    cost += comp.Props.cost;
                }
                else
                {
                    cost++;
                }
            }
            this.cost = cost; 
        }
        public void Recycle(Pawn pawn)
        {
            this.inner.Remove(pawn);
            if (pawn.TryGetComp<UnitComp>() is UnitComp comp)
            {
                this.Mass += comp.Props.mass * this.Props.recyclePercent;
            }
            else 
            {
                this.Mass += 10f * this.Props.recyclePercent;
            }
            this.CalcuateCost();
        }
        public void LaunchAll() 
        {
            while (this.inner.Any) 
            {
                this.Launch(this.inner[0]);
            }
            this.CalcuateCost() ;
        }
        public void Launch(Pawn pawn,bool isInner = true)
        {
            if (!this.parent.Spawned) return; 
            if (this.inner.Contains(pawn))
            {
                this.inner.TryDrop(pawn, this.parent.Position,
            this.parent.Map, ThingPlaceMode.Near, out Pawn p);
                this.units.Add(pawn);
                this.CalcuateCost();
            }
            else if (!isInner) 
            {
                GenSpawn.Spawn(pawn,this.parent.Position,this.parent.Map);
                this.units.Add(pawn);
                this.CalcuateCost();
            }
            if (pawn.TryGetComp<UnitComp>() is UnitComp comp) 
            {
                comp.root = this.parent;
            }
        }
        public void StartBuild(UnitData unit)
        {
            if (this.mass >= unit.Mass && this.cost + unit.UnitCost <= this.CostLimit)
            {
                if (this.spawnings.Any() && this.spawnings.Last() is UnitSpawnData
                    data && data.data == unit)
                {
                    data.count++;
                }
                else
                {
                    this.spawnings.Add(new UnitSpawnData(unit));
                } 
                this.mass -= unit.Mass;
            }
            else 
            {
                Messages.Message("RTS_UnableToBuild".Translate(),MessageTypeDefOf.RejectInput);
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.hiveName, "hiveName");
            Scribe_Values.Look(ref this.mass, "mass");
            Scribe_Values.Look(ref this.cost, "cost");
            Scribe_Values.Look(ref this.allowAutoRefuel, "allowAutoRefuel");
            Scribe_Values.Look(ref this.targetMass, "targetMass");
            Scribe_Values.Look(ref this.pause, "pause");
            Scribe_Values.Look(ref this.autoLaunch, "autoLaunch");
            Scribe_Values.Look(ref this.autoRepair, "autoRepair");
            Scribe_Deep.Look(ref this.inner,"inner",new object[] {this}); 
            Scribe_Collections.Look(ref this.units, "units",LookMode.Reference);
            Scribe_Collections.Look(ref this.spawnings, "spawning", LookMode.Deep);
        }

        public string hiveName;

        public float targetMass;
        private float mass = 0;
        public float cost = 0;
        public bool pause = false;
        public bool autoLaunch = false;
        public bool autoRepair = true;
        public float healCost = 0;
        public ThingOwner<Pawn> inner;
        public List<Pawn> units = new List<Pawn>();
        public List<UnitSpawnData> spawnings = new List<UnitSpawnData>();
        public bool allowAutoRefuel = true;
    }

    public class UnitSpawnData : IExposable
    {
        public UnitSpawnData() { }
        public UnitSpawnData(UnitData data)
        {
            this.data = data;
        }
        public void ExposeData()
        {
            Scribe_Deep.Look(ref this.data,"data");
            Scribe_Values.Look(ref this.spawnTime, "spawnTime");
            Scribe_Values.Look(ref this.count, "count");
        }

        public UnitData data;
        public int spawnTime;
        public int count = 1;
    }
}
