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
    /// 八方向贴图：在 Graphic_Multi 四方向基础上，额外支持四个对角线方向。
    /// 当 Thing 为 Pawn 且正在斜向移动时，自动使用对应的对角线贴图。
    ///
    /// 贴图命名约定：
    ///   {path}_north, {path}_northeast, {path}_east, {path}_southeast,
    ///   {path}_south, {path}_southwest, {path}_west, {path}_northwest
    ///
    /// 回退策略：
    ///   对角线贴图缺失 → 回退到最近的主方向贴图（NE→N/E, SE→E/S, SW→S/W, NW→W/N）
    ///   主方向贴图缺失 → 回退到 _north（不翻转）
    /// </summary>
    public class Graphic_Multi_8Dir_v2 : Graphic
    {
        // 方向索引: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
        private Material[] mats = new Material[8];

        private bool westFlipped;
        private bool eastFlipped;
        private float drawRotatedExtraAngleOffset;

        // ===== 材质属性 =====
        public override Material MatSingle => this.MatSouth;
        public override Material MatWest => this.mats[6];
        public override Material MatSouth => this.mats[4];
        public override Material MatEast => this.mats[2];
        public override Material MatNorth => this.mats[0];
        public override bool WestFlipped => this.westFlipped;
        public override bool EastFlipped => this.eastFlipped;
        public override float DrawRotatedExtraAngleOffset => this.drawRotatedExtraAngleOffset;

        public override bool ShouldDrawRotated
        {
            get
            {
                return (this.data == null || this.data.drawRotated)
                    && (this.MatEast == this.MatNorth || this.MatWest == this.MatNorth);
            }
        }

        // ===== 斜向检测 =====
        // 由于 RimWorld 寻路使用曼哈顿邻居（纯4方向），nextCell 与 Position 的差值
        // 永远只有一个分量非零。因此改为对比 pawn 朝向(Rotation) 与移动方向：
        // 若朝向为东西而移动为南北（或反之），即判定为斜向过渡状态。

        private bool TryGetDiagonalInfo(Rot4 rot, Thing thing,
            out int matIndex, out float renderAngle)
        {
            matIndex = -1;
            renderAngle = 0f;

            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.pather == null || !pawn.pather.Moving)
                return false;

            IntVec3 delta = pawn.pather.nextCell - pawn.Position;
            int dx = delta.x;
            int dz = delta.z;

            // 垂直移动(dz!=0) + 水平朝向 → 斜向
            if (dx == 0 && dz != 0)
            {
                if (rot == Rot4.East)
                {
                    matIndex = (dz > 0) ? 1 : 3;     // NE / SE
                    renderAngle = (dz > 0) ? 45f : 135f;
                    return true;
                }
                if (rot == Rot4.West)
                {
                    matIndex = (dz > 0) ? 7 : 5;     // NW / SW
                    renderAngle = (dz > 0) ? 315f : 225f;
                    return true;
                }
            }
            // 水平移动(dx!=0) + 垂直朝向 → 斜向
            else if (dx != 0 && dz == 0)
            {
                if (rot == Rot4.North)
                {
                    matIndex = (dx > 0) ? 1 : 7;     // NE / NW
                    renderAngle = (dx > 0) ? 45f : 315f;
                    return true;
                }
                if (rot == Rot4.South)
                {
                    matIndex = (dx > 0) ? 3 : 5;     // SE / SW
                    renderAngle = (dx > 0) ? 135f : 225f;
                    return true;
                }
            }
            // 真正的对角线步（若寻路支持8方向）
            else if (dx != 0 && dz != 0)
            {
                if (dx > 0 && dz > 0) { matIndex = 1; renderAngle = 45f; }
                else if (dx > 0 && dz < 0) { matIndex = 3; renderAngle = 135f; }
                else if (dx < 0 && dz < 0) { matIndex = 5; renderAngle = 225f; }
                else if (dx < 0 && dz > 0) { matIndex = 7; renderAngle = 315f; }
                return true;
            }

            return false;
        }

        // ===== MatAt 覆写 =====
        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            int matIndex;
            float renderAngle;
            if (TryGetDiagonalInfo(rot, thing, out matIndex, out renderAngle))
                return this.mats[matIndex];

            return this.mats[rot.AsInt * 2];
        }

        // ===== DrawWorker 覆写 =====
        // 斜向时使用标准尺寸 mesh + 对角线角度旋转
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef,
            Thing thing, float extraRotation)
        {
            int matIndex;
            float diagAngle;
            bool isDiagonal = TryGetDiagonalInfo(rot, thing, out matIndex, out diagAngle);

            Mesh mesh;
            Quaternion quaternion;
            if (isDiagonal)
            {
                mesh = MeshPool.GridPlane(this.drawSize);
                quaternion = Quaternion.AngleAxis(diagAngle, Vector3.up);
            }
            else
            {
                mesh = this.MeshAt(rot);
                quaternion = this.QuatFromRot(rot);
            }

            if (extraRotation != 0f)
                quaternion *= Quaternion.Euler(Vector3.up * extraRotation);
            if (this.data != null && this.data.addTopAltitudeBias)
                quaternion *= Quaternion.Euler(Vector3.left * 2f);

            loc += this.DrawOffset(rot);
            Material mat = isDiagonal ? this.mats[matIndex] : this.MatAt(rot, thing);
            this.DrawMeshInt(mesh, loc, quaternion, mat);

            if (this.ShadowGraphic != null)
                this.ShadowGraphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
        }

        // ===== Print 覆写 =====
        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            int matIndex;
            float diagAngle;
            bool isDiagonal = TryGetDiagonalInfo(thing.Rotation, thing,
                out matIndex, out diagAngle);

            if (isDiagonal)
            {
                // 对角线渲染：标准尺寸 + 对角线角度
                Vector2 vector = this.drawSize;
                float num = diagAngle + extraRotation;
                Vector3 center = thing.TrueCenter() + this.DrawOffset(thing.Rotation);
                Material mat = this.mats[matIndex];

                Vector2[] uvs;
                Color32 color;
                Graphic.TryGetTextureAtlasReplacementInfo(mat,
                    thing.def.category.ToAtlasGroup(), false, true,
                    out mat, out uvs, out color);

                Printer_Plane.PrintPlane(layer, center, vector, mat, num, false,
                    uvs, new Color32[] { color, color, color, color }, 0.01f, 0f);

                if (this.ShadowGraphic != null)
                    this.ShadowGraphic.Print(layer, thing, 0f);
                return;
            }

            base.Print(layer, thing, extraRotation);
        }

        // ===== Init =====
        public override void TryInsertIntoAtlas(TextureAtlasGroup groupKey)
        {
            foreach (Material material in this.mats)
            {
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

            string[] suffixes = { "_north", "_northeast", "_east", "_southeast",
                "_south", "_southwest", "_west", "_northwest" };
            Texture2D[] textures = new Texture2D[8];
            for (int i = 0; i < 8; i++)
                textures[i] = ContentFinder<Texture2D>.Get(req.path + suffixes[i], false);

            // 主方向回退（与 Graphic_Multi 一致）
            if (textures[0] == null) // _north
            {
                if (textures[4] != null)
                { textures[0] = textures[4]; this.drawRotatedExtraAngleOffset = 180f; }
                else if (textures[2] != null)
                { textures[0] = textures[2]; this.drawRotatedExtraAngleOffset = -90f; }
                else if (textures[6] != null)
                { textures[0] = textures[6]; this.drawRotatedExtraAngleOffset = 90f; }
                else
                    textures[0] = ContentFinder<Texture2D>.Get(req.path, false);
            }
            if (textures[0] == null)
            {
                Log.Error("Failed to find any textures at " + req.path
                    + " while constructing " + this.ToStringSafe<Graphic_Multi_8Dir_v2>());
                for (int i = 0; i < 8; i++) this.mats[i] = BaseContent.BadMat;
                return;
            }
            if (textures[4] == null) textures[4] = textures[0]; // _south
            if (textures[2] == null) // _east
            {
                if (textures[6] != null)
                { textures[2] = textures[6]; this.eastFlipped = base.DataAllowsFlip; }
                else textures[2] = textures[0];
            }
            if (textures[6] == null) // _west
            {
                if (textures[2] != null)
                { textures[6] = textures[2]; this.westFlipped = base.DataAllowsFlip; }
                else textures[6] = textures[0];
            }

            // 对角线回退（必须在主方向稳定后）
            ApplyDiagonalFallbacks(textures);

            // Mask 贴图
            Texture2D[] maskTextures = null;
            if (req.shader.SupportsMaskTex())
            {
                maskTextures = new Texture2D[8];
                string maskBase = this.maskPath.NullOrEmpty() ? this.path : this.maskPath;
                string maskExtra = this.maskPath.NullOrEmpty() ? "m" : string.Empty;
                for (int i = 0; i < 8; i++)
                    maskTextures[i] = ContentFinder<Texture2D>.Get(
                        maskBase + suffixes[i] + maskExtra, false);

                if (maskTextures[0] == null)
                {
                    if (maskTextures[4] != null) maskTextures[0] = maskTextures[4];
                    else if (maskTextures[2] != null) maskTextures[0] = maskTextures[2];
                    else if (maskTextures[6] != null) maskTextures[0] = maskTextures[6];
                }
                if (maskTextures[4] == null) maskTextures[4] = maskTextures[0];
                if (maskTextures[2] == null)
                    maskTextures[2] = maskTextures[6] ?? maskTextures[0];
                if (maskTextures[6] == null)
                    maskTextures[6] = maskTextures[2] ?? maskTextures[0];
                ApplyDiagonalFallbacks(maskTextures);
            }

            for (int i = 0; i < 8; i++)
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

        private void ApplyDiagonalFallbacks(Texture2D[] tex)
        {
            if (tex[1] == null) tex[1] = tex[0] ?? tex[2];  // NE→N, else E
            if (tex[3] == null) tex[3] = tex[2] ?? tex[4];  // SE→E, else S
            if (tex[5] == null) tex[5] = tex[4] ?? tex[6];  // SW→S, else W
            if (tex[7] == null) tex[7] = tex[6] ?? tex[0];  // NW→W, else N
        }

        // ===== 工具方法 =====
        public override Graphic GetColoredVersion(Shader newShader, Color newColor,
            Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_Multi_8Dir_v2>(this.path, newShader,
                this.drawSize, newColor, newColorTwo, this.data, this.maskPath);
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "Multi8DirV2(initPath=", this.path,
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


}
