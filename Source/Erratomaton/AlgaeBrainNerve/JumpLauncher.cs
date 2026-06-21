using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EM 
{
    /// <summary>
    /// CompProperties for CompJumpLauncher. 配置参数全部在此，通过 XML 设置。
    /// </summary>
    public class CompProperties_JumpLauncher : CompProperties
    {
        /// <summary>最大充能次数</summary>
        public int maxCharges = 3;

        /// <summary>每恢复一格充能所需的 tick 数 (1秒=60tick)</summary>
        public int cooldownTicks = 1800;

        /// <summary>最大跳跃距离 (格)</summary>
        public float jumpRange = 25f;

        /// <summary>最小跳跃距离 (格), 0=不限</summary>
        public float minRange = 0f;

        /// <summary>起跳前暖机时间 (tick)</summary>
        public int warmupTicks = 60;

        /// <summary>目标选择时的半径指示圈大小</summary>
        public float targeterRadius = 25f;

        /// <summary>起跳点尘埃 mote/fleck 类型名</summary>
        public string jumpEffectMote;

        /// <summary>跳跃音效</summary>
        public SoundDef jumpSound;

        public CompProperties_JumpLauncher()
        {
            compClass = typeof(CompJumpLauncher);
        }
    }



    /// <summary>
    /// 跳跃 Comp：挂载到 Pawn 上，提供类似 LongjumpMechLauncher 的跳跃能力。
    /// 不走 Abilities 系统，通过 Gizmo 按钮 + 目标选择触发。
    /// 拥有使用次数 (charges) 和自动恢复冷却 (cooldown)。
    /// </summary>
    public class CompJumpLauncher : ThingComp
    {
        public CompProperties_JumpLauncher Props => (CompProperties_JumpLauncher)props;

        // ---- 运行时状态 ----
        public int currentCharges;
        public int cooldownRemaining;

        // 是否正在暖机
        public bool isWarmingUp;
        public int warmupRemaining;
        public LocalTargetInfo jumpTarget = LocalTargetInfo.Invalid;

        public bool CanJump => currentCharges > 0 && !isWarmingUp && Charged;
        public bool Charged => currentCharges > 0;

        private Pawn Pawn => (Pawn)parent;

        // ========== 初始化 ==========
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            currentCharges = Props.maxCharges;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
                currentCharges = Props.maxCharges;
        }

        // ========== Tick ==========
        public override void CompTick()
        {
            base.CompTick();

            // 冷却恢复
            if (cooldownRemaining > 0)
            {
                cooldownRemaining--;
                if (cooldownRemaining <= 0 && currentCharges < Props.maxCharges)
                {
                    currentCharges++;
                    if (currentCharges < Props.maxCharges)
                        cooldownRemaining = Props.cooldownTicks;
                }
            }

            // 暖机计时
            if (isWarmingUp)
            {
                if (warmupRemaining > 0)
                {
                    warmupRemaining--;

                    // 如果 Pawn 被打断（倒地/眩晕等），取消跳跃
                    if (Pawn.Downed || Pawn.stances?.stunner?.Stunned == true)
                    {
                        CancelJump();
                        return;
                    }

                    // 维持瞄准姿态：强制面向目标
                    if (jumpTarget.IsValid && Pawn.stances != null)
                    {
                        Pawn.stances.SetStance(new Stance_Warmup(
                            Props.warmupTicks, jumpTarget.Cell, null));
                        Pawn.Rotation = Pawn.Rotation;
                    }
                }
                else
                {
                    ExecuteJump();
                }
            }
        }

        // ========== 跳跃流程 ==========

        /// <summary>玩家选择目标后调用，开始暖机</summary>
        public void StartJump(LocalTargetInfo target)
        {
            if (!CanJump)
                return;

            jumpTarget = target;
            isWarmingUp = true;
            warmupRemaining = Props.warmupTicks;

            // 打断当前工作，进入暖机姿态
            if (Pawn.jobs?.curJob != null)
                Pawn.jobs.StopAll();

            Pawn.stances?.SetStance(new Stance_Warmup(
                Props.warmupTicks, target.Cell, null));
        }

        /// <summary>暖机完成，执行跳跃</summary>
        private void ExecuteJump()
        {
            isWarmingUp = false;

            if (Pawn?.Map == null || !jumpTarget.IsValid)
            {
                CancelJump();
                return;
            }

            IntVec3 origin = Pawn.Position;
            IntVec3 dest = jumpTarget.Cell;

            // 修正目的地：如果不可站立，找最近的可站立格子
            if (!dest.Walkable(Pawn.Map))
                dest = CellFinder.RandomClosewalkCellNear(dest, Pawn.Map, 3);

            // 起跳效果
            if (!Props.jumpEffectMote.NullOrEmpty())
                FleckMaker.ThrowDustPuff(origin.ToVector3Shifted(), Pawn.Map, 2f);

            if (Props.jumpSound != null)
                Props.jumpSound.PlayOneShot(new TargetInfo(origin, Pawn.Map));

            // 传送 Pawn
            Pawn.Position = dest;
            Pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);

            // 落地效果
            if (!Props.jumpEffectMote.NullOrEmpty())
                FleckMaker.ThrowDustPuff(dest.ToVector3Shifted(), Pawn.Map, 2f);

            if (Props.jumpSound != null)
                Props.jumpSound.PlayOneShot(new TargetInfo(dest, Pawn.Map));

            // 消耗充能
            currentCharges--;
            if (cooldownRemaining <= 0)
                cooldownRemaining = Props.cooldownTicks;

            jumpTarget = LocalTargetInfo.Invalid;
        }

        /// <summary>取消跳跃（暖机被打断）</summary>
        private void CancelJump()
        {
            isWarmingUp = false;
            warmupRemaining = 0;
            jumpTarget = LocalTargetInfo.Invalid;
        }

        // ========== 存档 ==========
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentCharges, "jumpCharges", Props.maxCharges);
            Scribe_Values.Look(ref cooldownRemaining, "jumpCooldown", 0);
            Scribe_Values.Look(ref isWarmingUp, "jumpWarmingUp", false);
            Scribe_Values.Look(ref warmupRemaining, "jumpWarmupRemaining", 0);
            Scribe_TargetInfo.Look(ref jumpTarget, "jumpTarget");
        }

        // ========== Gizmo ==========
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 只在征召且属于玩家阵营时显示
            if (!Pawn.Drafted || Pawn.Faction != Faction.OfPlayer)
                yield break;

            Command_JumpLauncher cmd = new Command_JumpLauncher(this, Pawn);
            if (!CanJump)
                cmd.Disable("充能不足或正在冷却");
            yield return cmd;
        }

        // ========== 调试/状态 ==========
        public override string CompInspectStringExtra()
        {
            return $"跳跃充能: {currentCharges}/{Props.maxCharges}"
                + (cooldownRemaining > 0
                    ? $"\n冷却剩余: {cooldownRemaining.TicksToSeconds():F1}秒"
                    : "");
        }
    }



    /// <summary>
    /// 跳跃指令 Gizmo：点击后进入目标选择模式，选中有效地面后触发跳跃。
    /// </summary>
    public class Command_JumpLauncher : Command
    {
        public CompJumpLauncher comp;
        public Pawn pawn;

        public Command_JumpLauncher(CompJumpLauncher comp, Pawn pawn)
        {
            this.comp = comp;
            this.pawn = pawn;

            defaultLabel = "跳跃";
            defaultDesc = $"向目标位置跳跃。最大距离 {comp.Props.jumpRange:F0} 格。\n"
                + $"充能: {comp.currentCharges}/{comp.Props.maxCharges}";
            icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", true)
                ?? BaseContent.BadTex;
            hotKey = KeyBindingDefOf.Misc3;

            // 充能耗尽时显示灰色
            if (!comp.CanJump)
                Disable("充能不足");
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();

            Find.Targeter.BeginTargeting(
                GetTargetParams(),
                delegate (LocalTargetInfo target)
                {
                    comp.StartJump(target);
                }
            );
        }

        private TargetingParameters GetTargetParams()
        {
            TargetingParameters tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
            };

            // 目标验证：必须是地面，且在范围内，可站立
            tp.validator = delegate (TargetInfo target)
            {
                if (target.IsValid && target.Cell.IsValid
                    && target.Cell.InBounds(pawn.Map)
                    && target.Cell.Walkable(pawn.Map))
                {
                    float dist = pawn.Position.DistanceTo(target.Cell);
                    return dist >= comp.Props.minRange && dist <= comp.Props.jumpRange;
                }
                return false;
            };

            return tp;
        }
    }
}
