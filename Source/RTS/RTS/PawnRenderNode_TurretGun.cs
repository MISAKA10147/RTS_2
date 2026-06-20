using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RTS
{
    public class PawnRenderNode_TurretGun : PawnRenderNode
    { 
        public PawnRenderNode_TurretGun(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        } 
        public override Graphic GraphicFor(Pawn pawn)
        {
            return GraphicDatabase.Get<Graphic_Single>(this.turretComp.Props.turretDef.graphicData.texPath, ShaderDatabase.Cutout);
        } 
        public CompTurretGun turretComp;
    }
    public class PawnRenderNodeWorker_TurretGun : PawnRenderNodeWorker
    { 
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion quaternion = base.RotationFor(node, parms);
            PawnRenderNode_TurretGun pawnRenderNode_TurretGun = node as PawnRenderNode_TurretGun;
            if (pawnRenderNode_TurretGun != null)
            {
                quaternion *= pawnRenderNode_TurretGun.turretComp.curRotation.ToQuat();
            }
            return quaternion;
        }
    }
}
