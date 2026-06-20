using HarmonyLib;
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
    /// 在游戏启动时自动应用所有 Harmony 补丁。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            Harmony harmony = new Harmony("Erratomaton.Turret16Dir");
            harmony.PatchAll();
            Log.Message("Erratomaton: Turret16Dir Harmony patches applied.");
        }
    }

    /// <summary>
    /// 十六向炮塔贴图：专为 WeaponTurret 设计，将连续旋转角度映射到 16 张预渲染方向贴图。
    /// 与 Graphic_Single（单张贴图旋转）不同，此类使用不同角度的独立贴图来避免旋转失真。
    ///
    /// 贴图命名约定：
    ///   {path}_0  ~  {path}_15   （按 0°~337.5°，每 22.5° 一步）
    ///
    ///   角度映射：index = RoundToInt(extraRotation / 22.5°) % 16
    ///     0: 0°(南),  1: 22.5°,  2: 45°,   3: 67.5°,
    ///     4: 90°(东), 5: 112.5°, 6: 135°,  7: 157.5°,
    ///     8: 180°(北),9: 202.5°, 10: 225°, 11: 247.5°,
    ///    12: 270°(西),13: 292.5°,14: 315°, 15: 337.5°
    ///
    /// 用法：
    ///   在 ThingDef 的 graphicData 中设置：
    ///   &lt;graphicClass&gt;Graphic_Turret_16Dir&lt;/graphicClass&gt;
    /// </summary>
    public class Graphic_Turret_16Dir : Graphic
    {
        private Material[] mats = new Material[16];

        private float drawRotatedExtraAngleOffset;

        // ===== 材质属性（基础方向，用于 MatAt(Rot4) 回退） =====
        public override Material MatSingle => mats[0];  // 南(默认)
        public override Material MatWest => mats[12]; // 270°
        public override Material MatSouth => mats[0];  // 0°
        public override Material MatEast => mats[4];  // 90°
        public override Material MatNorth => mats[8];  // 180°

        public override bool WestFlipped => false;
        public override bool EastFlipped => false;

        /// <summary>
        /// 始终使用独立方向贴图，不进行贴图旋转渲染。
        /// </summary>
        public override bool ShouldDrawRotated => false;

        public override float DrawRotatedExtraAngleOffset => this.drawRotatedExtraAngleOffset;

        // ===== 角度→索引转换 =====
        /// <summary>
        /// 将 extraRotation 角度映射到 16 向索引。
        /// </summary>
        public static int AngleToIndex(float angle)
        {
            float normalized = ((angle % 360f) + 360f) % 360f;
            return Mathf.RoundToInt(normalized / 22.5f) % 16;
        }

        /// <summary>
        /// 根据索引返回该方向的理论角度。
        /// </summary>
        public static float IndexToAngle(int index)
        {
            return index * 22.5f;
        }

        /// <summary>
        /// 根据角度获取对应的材质。用于外部渲染系统（如 TurretTop.DrawTurret）直接获取材质。
        /// </summary>
        public Material GetMaterialForAngle(float angle)
        {
            int index = AngleToIndex(angle);
            if (index >= 0 && index < mats.Length)
                return mats[index];
            return mats[0];
        }

        /// <summary>
        /// 检查指定 ThingDef 的 graphicData 是否使用了此类。
        /// </summary>
        public static bool IsGraphicTurret(ThingDef def)
        {
            if (def?.building?.turretGunDef?.graphicData?.Graphic is Graphic_Turret_16Dir)
                return true;
            return false;
        }

        /// <summary>
        /// 从 ThingDef 获取 Graphic_Turret_16Dir 实例。
        /// </summary>
        public static Graphic_Turret_16Dir GetFromDef(ThingDef def)
        {
            return def?.building?.turretGunDef?.graphicData?.Graphic as Graphic_Turret_16Dir;
        }

        // ===== MatAt: 基于 Rot4 的材质获取 =====
        // Rot4.South(2) = 建筑默认朝向 = 贴图索引 0（0°）
        // Rot4.East(1)  = 90° = 索引 4
        // Rot4.North(0) = 180° = 索引 8
        // Rot4.West(3)  = 270° = 索引 12
        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            switch (rot.AsInt)
            {
                case 0: return mats[8];  // North = 180°
                case 1: return mats[4];  // East = 90°
                case 2: return mats[0];  // South = 0°
                case 3: return mats[12]; // West = 270°
                default: return mats[0];
            }
        }

        // ===== DrawWorker: 核心渲染 — 使用 extraRotation 选择对应贴图 =====
        // 贴图已经预渲染为对应角度，因此只使用 building 的 Rot4 旋转 mesh，
        // 不再对 quat 叠加 extraRotation。
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            int index = AngleToIndex(extraRotation);
            Mesh mesh = this.MeshAt(rot);
            Quaternion quat = this.QuatFromRot(rot);

            if (this.data != null && this.data.addTopAltitudeBias)
                quat *= Quaternion.Euler(Vector3.left * 2f);

            loc += this.DrawOffset(rot);
            Material mat = this.mats[index];
            this.DrawMeshInt(mesh, loc, quat, mat);

            if (this.ShadowGraphic != null)
                this.ShadowGraphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        // ===== Print: SectionLayer 渲染 =====
        // 贴图预渲染角度已包含旋转，因此只使用 building Rot4 角度。
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            int index = AngleToIndex(extraRotation);
            Vector2 vector = this.drawSize;
            float num = this.QuatFromRot(thing.Rotation).eulerAngles.y;
            Vector3 center = thing.TrueCenter() + this.DrawOffset(thing.Rotation);
            Material mat = this.mats[index];

            Vector2[] uvs;
            Color32 color;
            Graphic.TryGetTextureAtlasReplacementInfo(mat,
                thing.def.category.ToAtlasGroup(), false, true,
                out mat, out uvs, out color);

            Printer_Plane.PrintPlane(layer, center, vector, mat, num, false,
                uvs, new Color32[] { color, color, color, color }, 0.01f, 0f);

            if (this.ShadowGraphic != null)
                this.ShadowGraphic.Print(layer, thing, 0f);
        }

        // ===== Init: 加载 16 张贴图并创建材质 =====
        public override void TryInsertIntoAtlas(TextureAtlasGroup groupKey)
        {
            for (int i = 0; i < 16; i++)
            {
                Material material = this.mats[i];
                Texture2D mask = null;
                if (material.HasProperty(ShaderPropertyIDs.MaskTex))
                    mask = (Texture2D)material.GetTexture(ShaderPropertyIDs.MaskTex);
                GlobalTextureAtlasManager.TryInsertStatic(groupKey,
                    (Texture2D)material.mainTexture, mask);
            }
        }

        public override void Init(GraphicRequest req)
        {
            this.data = req.graphicData;
            this.path = req.path;
            this.maskPath = req.maskPath;
            this.color = req.color;
            this.colorTwo = req.colorTwo;
            this.drawSize = req.drawSize;

            // 加载 16 向贴图
            Texture2D[] textures = new Texture2D[16];
            for (int i = 0; i < 16; i++)
            {
                textures[i] = ContentFinder<Texture2D>.Get(req.path + "_" + i, false);
            }

            // 回退：若 _0 缺失，尝试无后缀的基础路径
            if (textures[0] == null)
            {
                textures[0] = ContentFinder<Texture2D>.Get(req.path, false);
            }

            if (textures[0] == null)
            {
                Log.Error("Failed to find any textures at " + req.path
                    + " while constructing " + this.ToStringSafe<Graphic_Turret_16Dir>());
                for (int i = 0; i < 16; i++) this.mats[i] = BaseContent.BadMat;
                return;
            }

            // 其他方向缺失时回退到 _0（不旋转贴图的回退策略）
            for (int i = 1; i < 16; i++)
            {
                if (textures[i] == null)
                    textures[i] = textures[0];
            }

            // Mask 贴图
            Texture2D[] maskTextures = null;
            if (req.shader.SupportsMaskTex())
            {
                maskTextures = new Texture2D[16];
                string maskBase = this.maskPath.NullOrEmpty() ? this.path : this.maskPath;
                string maskExtra = this.maskPath.NullOrEmpty() ? "m" : string.Empty;

                for (int i = 0; i < 16; i++)
                {
                    maskTextures[i] = ContentFinder<Texture2D>.Get(
                        maskBase + "_" + i + maskExtra, false);
                }

                if (maskTextures[0] == null)
                    maskTextures[0] = ContentFinder<Texture2D>.Get(maskBase + maskExtra, false);

                for (int i = 1; i < 16; i++)
                {
                    if (maskTextures[i] == null)
                        maskTextures[i] = maskTextures[0];
                }
            }

            // 创建 16 个材质
            for (int i = 0; i < 16; i++)
            {
                MaterialRequest matReq = default(MaterialRequest);
                matReq.mainTex = textures[i];
                matReq.shader = req.shader;
                matReq.color = this.color;
                matReq.colorTwo = this.colorTwo;
                matReq.maskTex = (maskTextures != null) ? maskTextures[i] : null;
                matReq.shaderParameters = req.shaderParameters;
                matReq.renderQueue = req.renderQueue;
                this.mats[i] = MaterialPool.MatFrom(matReq);
            }
        }

        // ===== 工具方法 =====
        public override Graphic GetColoredVersion(Shader newShader, Color newColor,
            Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_Turret_16Dir>(this.path, newShader,
                this.drawSize, newColor, newColorTwo, this.data, this.maskPath);
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "Turret16Dir(initPath=", this.path,
                ", color=", this.color.ToString(),
                ", colorTwo=", this.colorTwo.ToString(), ")"
            });
        }

        public override int GetHashCode()
        {
            return Gen.HashCombineStruct<Color>(
                Gen.HashCombineStruct<Color>(
                    Gen.HashCombine<string>(0, this.path), this.color), this.colorTwo);
        }
    }
    /// <summary>
    /// Harmony 补丁：拦截 TurretTop.DrawTurret，使 Graphic_Turret_16Dir 生效。
    ///
    /// 问题背景：
    ///   原版 TurretTop.DrawTurret 直接调用 Graphics.DrawMesh 并使用单一材质旋转渲染，
    ///   完全绕过了 Graphic 系统，导致 graphicClass 配置无效。
    ///
    /// 补丁逻辑：
    ///   检测炮塔是否使用了 Graphic_Turret_16Dir，若是则使用对应角度的预渲染贴图，
    ///   mesh 仅应用 ArtworkRotation 修正（不再叠加 CurRotation）。
    /// </summary>
    [HarmonyPatch(typeof(TurretTop), "DrawTurret")]
    public static class Harmony_Turret16Dir
    {
        [HarmonyPrefix]
        public static bool DrawTurret_Prefix(TurretTop __instance,
            Vector3 drawLoc, Vector3 recoilDrawOffset, float recoilAngleOffset)
        {
            // 获取 parentTurret 私有字段
            Building_Turret turret = Traverse.Create(__instance)
                .Field("parentTurret")
                .GetValue<Building_Turret>();
            if (turret == null) return true;

            // 检查是否使用了 Graphic_Turret_16Dir
            Graphic_Turret_16Dir graphic = Graphic_Turret_16Dir.GetFromDef(turret.def);
            if (graphic == null) return true;

            // === 自定义 16 向渲染 ===
            Vector3 topOffset = new Vector3(
                turret.def.building.turretTopOffset.x, 0f,
                turret.def.building.turretTopOffset.y);
            float drawSize = turret.def.building.turretTopDrawSize;

            topOffset = topOffset.RotatedBy(recoilAngleOffset);
            topOffset += recoilDrawOffset;

            // 获取当前炮塔旋转角度
            Verb currentVerb = turret.CurrentEffectiveVerb;
            float curRotation = currentVerb?.AimAngleOverride ?? __instance.CurRotation;

            Vector3 pos = drawLoc + Altitudes.AltIncVect + topOffset;

            // mesh 只应用 ArtworkRotation 基准修正，不再叠加 CurRotation
            //（贴图已预渲染为对应角度，无需额外旋转）
            Quaternion q = ((float)TurretTop.ArtworkRotation).ToQuat();
            Vector3 s = new Vector3(drawSize, 1f, drawSize);

            Material mat = graphic.GetMaterialForAngle(curRotation);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, q, s), mat, 0);

            return false; // 跳过原方法
        }
    }

}
