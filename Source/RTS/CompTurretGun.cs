using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using UnityEngine;

namespace RTS
{
    public class CompProperties_TurretGun : CompProperties
    {
        public CompProperties_TurretGun()
        {
            this.compClass = typeof(CompTurretGun);
        }

        public ThingDef turretDef;
        public float angleOffset;
        public bool autoAttack = true;
        public List<PawnRenderNodeProperties> renderNodeProperties;

        public int littleCooldown;
        public int extraShootCount;
        public int warmupTime;
    }
    [StaticConstructorOnStartup]
    public class CompTurretGun : ThingComp, IAttackTargetSearcher
    {
        public Thing Thing
        {
            get
            {
                return this.parent;
            }
        }
        public CompProperties_TurretGun Props
        {
            get
            {
                return (CompProperties_TurretGun)this.props;
            }
        }
        public Verb CurrentEffectiveVerb
        {
            get
            {
                return this.AttackVerb;
            }
        }

        public LocalTargetInfo LastAttackedTarget
        {
            get
            {
                return this.lastAttackedTarget;
            }
        }
        public int LastAttackTargetTick
        {
            get
            {
                return this.lastAttackTargetTick;
            }
        }
        public CompEquippable GunCompEq
        {
            get
            {
                return this.gun.TryGetComp<CompEquippable>();
            }
        }
        public Verb AttackVerb
        {
            get
            {
                return this.GunCompEq.PrimaryVerb;
            }
        }
        private bool WarmingUp
        {
            get
            {
                return this.burstWarmupTicksLeft > 0;
            }
        }
        private bool CanShoot
        {
            get
            {
                Pawn pawn = this.parent as Pawn;
                if (pawn != null)
                {
                    if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
                    {
                        return false;
                    }
                    if (pawn.stances.stunner.Stunned)
                    {
                        return false;
                    }
                    if (this.TurretDestroyed)
                    {
                        return false;
                    }
                    if (!this.fireAtWill)
                    {
                        return false;
                    }
                }
                CompCanBeDormant compCanBeDormant = this.parent.TryGetComp<CompCanBeDormant>();
                return compCanBeDormant == null || compCanBeDormant.Awake;
            }
        }

        public bool TurretDestroyed
        {
            get
            {
                Pawn pawn = this.parent as Pawn;
                return pawn != null && this.AttackVerb.verbProps.linkedBodyPartsGroup != null && this.AttackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable && PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, this.AttackVerb.verbProps.linkedBodyPartsGroup) <= 0f;
            }
        }
        public bool AutoAttack
        {
            get
            {
                return this.Props.autoAttack;
            }
        }
        public override void PostPostMake()
        {
            base.PostPostMake();
            this.MakeGun();
        }
        private void MakeGun()
        {
            this.gun = ThingMaker.MakeThing(this.Props.turretDef, null);
            this.UpdateGunVerbs();
        }
        private void UpdateGunVerbs()
        {
            List<Verb> allVerbs = this.gun.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this.parent;
                verb.castCompleteCallback = delegate ()
                { 
                    this.burstCooldownTicksLeft = this.AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
                };
            }
        }
        public override void CompTick()
        {
            if (!this.CanShoot)
            {
                return;
            }
            if (this.currentTarget.IsValid)
            {
                this.curRotation = (this.currentTarget.Cell.ToVector3Shifted() - this.parent.DrawPos).AngleFlat() + this.Props.angleOffset;
            }
            else 
            {
                this.curRotation = this.parent.Rotation.AsAngle - 90f;
            }
            this.AttackVerb.VerbTick();
            if (this.AttackVerb.state != VerbState.Bursting)
            {
                if (this.currentTarget.IsValid && this.isShooting)
                {
                    if (this.currentTarget.Pawn is Pawn p && (!p.Spawned || p.Dead || p.Downed))
                    {
                        this.ResetCurrentTarget();
                        return;
                    }
                    this.littleCooldown--;
                    if (this.littleCooldown <= 0)
                    {
                        this.AttackVerb.TryStartCastOn(this.currentTarget, false, true, false, true);
                        this.lastAttackTargetTick = Find.TickManager.TicksGame;
                        this.lastAttackedTarget = this.currentTarget;
                        if (this.shootCount < this.Props.extraShootCount)
                        {
                            this.shootCount++;
                            this.littleCooldown = this.Props.littleCooldown;
                            if (Prefs.DevMode)
                            {
                                Log.Message("小冷却" + this.littleCooldown);
                            }
                        }
                        if (this.shootCount >= this.Props.extraShootCount)
                        {
                            this.isShooting = false;
                            this.shootCount = 0;
                        }
                    }
                    return;
                }
                if (this.WarmingUp)
                {
                    this.burstWarmupTicksLeft--;
                    if (this.burstWarmupTicksLeft == 0)
                    {
                        this.AttackVerb.TryStartCastOn(this.currentTarget, false, true, false, true);
                        this.lastAttackTargetTick = Find.TickManager.TicksGame;
                        this.lastAttackedTarget = this.currentTarget;
                        this.isShooting = this.Props.extraShootCount > 0;
                        if (this.isShooting) 
                        {
                            this.littleCooldown = this.Props.littleCooldown;
                            if (Prefs.DevMode)
                            {
                                Log.Message("小冷却" + this.littleCooldown);
                            }
                        }
                        return;
                    }
                }
                else
                {
                    if (this.burstCooldownTicksLeft > 0)
                    {
                        this.burstCooldownTicksLeft--;
                    }
                    if (this.burstCooldownTicksLeft <= 0 && this.parent.IsHashIntervalTick(10))
                    {
                        if (this.forcedTarget != null)
                        {
                            if (this.forcedTarget.ThingDestroyed)
                            {
                                this.forcedTarget = null;
                                this.ResetCurrentTarget();
                                return;
                            }
                            if (this.AttackVerb.ValidateTarget(this.forcedTarget))
                            {
                                this.currentTarget = this.forcedTarget;
                                this.burstWarmupTicksLeft = ((int)this.Props.warmupTime);
                                if (Prefs.DevMode)
                                {
                                    Log.Message("大冷却" + this.burstWarmupTicksLeft);
                                }
                                return;
                            }
                            else
                            {
                                this.forcedTarget = null;
                                this.ResetCurrentTarget();
                            }
                        }
                        this.currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, null, 0f, 9999f);
                        if (this.currentTarget.IsValid)
                        {
                            this.burstWarmupTicksLeft = ((int)this.Props.warmupTime);
                            if (Prefs.DevMode)
                            {
                                Log.Message("大冷却" + this.burstWarmupTicksLeft);
                            }
                            return;
                        }
                        this.ResetCurrentTarget();
                    }
                }
            }
        }
        private void ResetCurrentTarget()
        {
            this.currentTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
            this.isShooting = false;
            this.shootCount = 0;
            this.littleCooldown = 0;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            Pawn pawn = this.parent as Pawn;
            if (pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandToggleTurret".Translate(),
                    defaultDesc = "CommandToggleTurretDesc".Translate(),
                    isActive = (() => this.fireAtWill),
                    icon = CompTurretGun.ToggleTurretIcon.Texture,
                    toggleAction = delegate ()
                    {
                        this.fireAtWill = !this.fireAtWill;
                        this.forcedTarget = null;
                        this.ResetCurrentTarget();
                    }
                };
                Command_SpecialAction c = new Command_SpecialAction
                {
                    defaultLabel = "ForcedAttack".Translate(),
                    defaultDesc = "ForcedAttackDesc".Translate(),
                    icon = Icon_Attacking,
                    postAction = (t) =>
                    {
                        if (!this.AttackVerb.CanHitTarget(t)) 
                        {
                            return;
                        }
                        this.currentTarget = t;
                        this.forcedTarget = t;
                        this.burstWarmupTicksLeft = ((int)this.Props.warmupTime);
                        if (Prefs.DevMode)
                        {
                            Log.Message("大冷却" + this.burstWarmupTicksLeft);
                        }
                    }
                };
                c.GiveAction(this.AttackVerb);
                yield return c;
                if (this.currentTarget != null || this.forcedTarget != null) 
                {
                    yield return new Command_MultipleAction
                    {
                        defaultLabel = "CommandToggleTurret".Translate(),
                        defaultDesc = "CommandToggleTurretDesc".Translate(),
                        icon = TexCommand.CannotShoot,
                        action = delegate ()
                        {
                            this.forcedTarget = null;
                            this.ResetCurrentTarget();
                        }
                    };
                }
            }
            yield break;
        }
        public override List<PawnRenderNode> CompRenderNodes()
        {
            if (!this.Props.renderNodeProperties.NullOrEmpty<PawnRenderNodeProperties>())
            {
                Pawn pawn = this.parent as Pawn;
                if (pawn != null)
                {
                    List<PawnRenderNode> list = new List<PawnRenderNode>();
                    foreach (PawnRenderNodeProperties pawnRenderNodeProperties in this.Props.renderNodeProperties)
                    {
                        PawnRenderNode_TurretGun pawnRenderNode_TurretGun = (PawnRenderNode_TurretGun)Activator.CreateInstance(pawnRenderNodeProperties.nodeClass, new object[]
                        {
                            pawn,
                            pawnRenderNodeProperties,
                            pawn.Drawer.renderer.renderTree
                        });
                        pawnRenderNode_TurretGun.turretComp = this;
                        list.Add(pawnRenderNode_TurretGun);
                    }
                    return list;
                }
            }
            return base.CompRenderNodes();
        }
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (this.Props.turretDef != null)
            {
                yield return new StatDrawEntry(StatCategoryDefOf.PawnCombat, "Turret".Translate(), this.Props.turretDef.LabelCap, "Stat_Thing_TurretDesc".Translate(), 5600, null, Gen.YieldSingle<Dialog_InfoCard.Hyperlink>(new Dialog_InfoCard.Hyperlink(this.Props.turretDef, -1)), false, false);
            }
            yield break;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.currentTarget, "currentTarget");
            Scribe_TargetInfo.Look(ref this.forcedTarget, "forcedTarget");
            Scribe_Deep.Look<Thing>(ref this.gun, "gun", Array.Empty<object>());
            Scribe_Values.Look<bool>(ref this.fireAtWill, "fireAtWill", true, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.gun == null)
                {
                    Log.Error("CompTurrentGun had null gun after loading. Recreating.");
                    this.MakeGun();
                    return;
                }
                this.UpdateGunVerbs();
            }
        }

        public bool isShooting;
        //处于连射中
        public int shootCount = 0;
        //连射次数
        public int littleCooldown;
        //连射冷却
        private const int StartShootIntervalTicks = 10;
        private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Gizmos/ToggleTurret");
        public Thing gun;
        protected int burstCooldownTicksLeft;
        protected int burstWarmupTicksLeft;
        protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;
        protected LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;
        private bool fireAtWill = true;
        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        private int lastAttackTargetTick;
        public float curRotation;

        private static readonly Texture2D Icon_Attacking = ContentFinder<Texture2D>.Get("UI/Icons/ColonistBar/Attacking", true);
    }
}
