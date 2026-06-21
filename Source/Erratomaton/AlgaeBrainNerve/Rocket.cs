using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;



namespace EM 
{
    /// <summary>
    /// 继承自 Projectile_Explosive 的弹射物，飞行时同时产生烟雾 Fleck 粒子 + 线形拖尾。
    ///
    /// 烟雾粒子通过插值补全保证高速弹连成线，线形拖尾通过 GenDraw.DrawLineBetween 绘制渐变宽度线条。
    ///
    /// XML 可配置参数（烟雾粒子）：
    ///   smokeIntervalTicks  : 每多少 tick 执行一次烟雾生成逻辑 (默认 1)
    ///   smokeSpacing        : 拖尾粒子之间的间距 (格) (默认 0.3)
    ///   smokeRandomRadius   : 粒子生成位置的随机偏移半径 (默认 0.1)
    ///   smokeDissipateTicks : 每个烟雾粒子持续 tick 数，0=使用 FleckDef 默认值 (默认 20)
    ///   smokeFleckDef       : 烟雾 FleckDef 名称 (默认 "EM_Fleck_ProjectileSmoke")
    ///
    /// XML 可配置参数（线形拖尾）：
    ///   trailMaxLength      : 拖尾历史记录最大数量 (默认 120)
    ///   trailLifespanTicks  : 子弹消失后拖尾持续 tick 数 (默认 180, 即 3 秒)
    ///   trailWidthStart     : 拖尾线条起始宽度 (靠近弹头，默认 0.3)
    ///   trailWidthEnd       : 拖尾线条结束宽度 (远离弹头，默认 0.05)
    ///   trailColorR/G/B/A   : 拖尾颜色 RGBA (默认 0.9, 0.97, 0.9, 0.95)
    /// </summary>
    [StaticConstructorOnStartup]
    public class Projectile_ExplosiveSmokeTrail : Projectile_Explosive
    {
        // ---- 烟雾粒子 XML 配置 ----
        public int smokeIntervalTicks = 1;
        public float smokeSpacing = 0.3f;
        public float smokeRandomRadius = 0.1f;
        public int smokeDissipateTicks = 10;
        public string smokeFleckDef = "EM_Fleck_ProjectileSmoke";

        // ---- 线形拖尾 XML 配置 ----
        public int trailMaxLength = 20;
        public int trailLifespanTicks = 20;
        public float trailWidthStart = 0.3f;
        public float trailWidthEnd = 0.05f;
        public float trailColorR = 0.95f;
        public float trailColorG = 0.97f;
        public float trailColorB = 0.95f;
        public float trailColorA = 0.75f;

        // ---- 线形拖尾 静态材质 ----
        private static Material _trailMat;
        private static Color _trailColor;
        private static Material TrailMat
        {
            get
            {
                if (_trailMat == null)
                    _trailMat = MaterialPool.MatFrom(GenDraw.LineTexPath,
                        ShaderDatabase.MoteGlowDistorted, _trailColor);
                return _trailMat;
            }
        }

        // ---- 运行时状态 ----
        private int tickCounter = 0;
        private Vector3 lastSmokePos;
        private bool hasLastPos;
        private FleckDef smokeFleck;
        private List<Vector3> trailHistory = new List<Vector3>();
        private bool trailMatDirty = true;
        private bool trailRegistered = false;

        private FleckDef SmokeFleck
        {
            get
            {
                if (smokeFleck == null)
                    smokeFleck = DefDatabase<FleckDef>.GetNamed(smokeFleckDef);
                return smokeFleck;
            }
        }

        private void EnsureTrailMat()
        {
            Color newColor = new Color(trailColorR, trailColorG, trailColorB, trailColorA);
            if (trailMatDirty || _trailColor != newColor)
            {
                _trailColor = newColor;
                _trailMat = null; // 下次访问时用新颜色重建
                trailMatDirty = false;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false,
            Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags,
                preventFriendlyFire, equipment, targetCoverDef);
            lastSmokePos = origin;
            hasLastPos = true;
            EnsureTrailMat();
        }

        protected override void Tick()
        {
            base.Tick();

            if (this.Destroyed)
                return;

            if (this.Map == null)
                return;

            // ---- 烟雾粒子 ----
            if (SmokeFleck != null)
            {
                tickCounter++;
                if (tickCounter >= smokeIntervalTicks)
                {
                    tickCounter = 0;
                    CreateSmokeTrail();
                }
            }
        }

        /// <summary>
        /// 击中或到期时，将当前拖尾注册到 MapComponent 以便子弹消失后继续淡出。
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            RegisterFadingTrail();
            base.Impact(hitThing, blockedByShield);
        }

        private void RegisterFadingTrail()
        {
            if (trailRegistered || trailHistory.Count < 2 || this.Map == null)
                return;

            trailRegistered = true;

            // 确保 MapComponent 存在
            MapComponent_ProjectileTrail comp =
                this.Map.GetComponent<MapComponent_ProjectileTrail>();
            if (comp == null)
            {
                comp = new MapComponent_ProjectileTrail(this.Map);
                this.Map.components.Add(comp);
            }

            FadingTrail fade = new FadingTrail
            {
                positions = new List<Vector3>(trailHistory),
                birthTick = Find.TickManager.TicksGame,
                lifespanTicks = trailLifespanTicks,
                widthStart = trailWidthStart,
                widthEnd = trailWidthEnd,
                color = new Color(trailColorR, trailColorG, trailColorB, trailColorA)
            };

            comp.RegisterFadingTrail(fade);
        }

        protected override void DrawAt(Vector3 position, bool flip = false)
        {
            base.DrawAt(position, flip);
            UpdateAndDrawTrail();
        }

        // ========== 线形拖尾 ==========

        private void UpdateAndDrawTrail()
        {
            if (!Find.TickManager.Paused)
            {
                Vector3 drawPos = this.DrawPos;
                drawPos.y -= 0.1f;
                trailHistory.Insert(0, drawPos);

                if (trailHistory.Count > trailMaxLength)
                    trailHistory.RemoveAt(trailHistory.Count - 1);
            }

            if (trailHistory.Count < 2)
                return;

            EnsureTrailMat();

            for (int i = 0; i < trailHistory.Count - 1; i++)
            {
                Vector3 a = trailHistory[i];
                Vector3 b = trailHistory[i + 1];
                float t = (float)i / trailHistory.Count;
                float lineWidth = Mathf.Lerp(trailWidthStart, trailWidthEnd, t);
                GenDraw.DrawLineBetween(a, b, TrailMat, lineWidth);
            }
        }

        // ========== 烟雾 Fleck 粒子 ==========

        private void CreateSmokeTrail()
        {
            Vector3 curPos = this.DrawPos;

            if (!hasLastPos)
            {
                lastSmokePos = curPos;
                hasLastPos = true;
                SpawnSingleSmoke(curPos);
                return;
            }

            float dist = Vector3.Distance(lastSmokePos, curPos);
            if (dist <= 0.001f)
            {
                SpawnSingleSmoke(curPos);
                return;
            }

            Vector3 dir = (curPos - lastSmokePos).normalized;
            int count = Mathf.Max(1, Mathf.CeilToInt(dist / smokeSpacing));
            float step = dist / count;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = lastSmokePos + dir * (i + 1) * step;
                SpawnSingleSmoke(pos);
            }

            lastSmokePos = curPos;
        }

        private void SpawnSingleSmoke(Vector3 pos)
        {
            Vector3 randomOffset = new Vector3(
                Rand.Range(-smokeRandomRadius, smokeRandomRadius),
                0f,
                Rand.Range(-smokeRandomRadius, smokeRandomRadius));
            Vector3 spawnPos = pos + randomOffset;
            float randomRotation = Rand.Range(0f, 360f);

            FleckCreationData data = FleckMaker.GetDataStatic(
                spawnPos, this.Map, SmokeFleck);
            data.rotation = randomRotation;

            if (smokeDissipateTicks > 0)
                data.airTimeLeft = smokeDissipateTicks.TicksToSeconds();

            this.Map.flecks.CreateFleck(data);
        }
    }
    /// <summary>
    /// MapComponent: 管理弹射物销毁后的残存线形拖尾。
    /// 弹射物击中/消失时将拖尾路径注册到此，持续绘制直到生命结束。
    /// </summary>
    public class MapComponent_ProjectileTrail : MapComponent
    {
        private List<FadingTrail> activeTrails = new List<FadingTrail>();

        public MapComponent_ProjectileTrail(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            if (activeTrails.Count == 0)
                return;

            int currentTick = Find.TickManager.TicksGame;

            for (int idx = activeTrails.Count - 1; idx >= 0; idx--)
            {
                FadingTrail trail = activeTrails[idx];

                if (currentTick >= trail.deathTick)
                {
                    trail.Dispose();
                    activeTrails.RemoveAt(idx);
                    continue;
                }

                float lifeProgress = (float)(currentTick - trail.birthTick) / trail.lifespanTicks;
                float alphaFactor = 1f - lifeProgress;

                // 更新材质透明度
                Color fadedColor = trail.color;
                fadedColor.a = trail.color.a * alphaFactor;
                trail.material.color = fadedColor;

                for (int i = 0; i < trail.positions.Count - 1; i++)
                {
                    Vector3 a = trail.positions[i];
                    Vector3 b = trail.positions[i + 1];
                    float t = (float)i / trail.positions.Count;
                    float baseWidth = Mathf.Lerp(trail.widthStart, trail.widthEnd, t);
                    float lineWidth = baseWidth * alphaFactor;

                    if (lineWidth < 0.001f)
                        continue;

                    GenDraw.DrawLineBetween(a, b, trail.material, lineWidth);
                }
            }
        }

        public void RegisterFadingTrail(FadingTrail trail)
        {
            trail.InitMaterial();
            activeTrails.Add(trail);
        }
    }

    /// <summary>
    /// 残存拖尾数据，由 Projectile 在销毁时注册到 MapComponent_ProjectileTrail。
    /// </summary>
    public class FadingTrail
    {
        public List<Vector3> positions;
        public int birthTick;
        public int lifespanTicks;
        public float widthStart;
        public float widthEnd;
        public Color color;
        public Material material;
        public int deathTick => birthTick + lifespanTicks;

        public void InitMaterial()
        {
            if (material != null)
                return;

            material = MaterialPool.MatFrom(GenDraw.LineTexPath,
                ShaderDatabase.MoteGlowDistorted, color);
            // 确保获得独立材质实例，以便修改 color 不影响其他使用者
            material = new Material(material);
        }

        public void Dispose()
        {
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }
        }
    }


}
