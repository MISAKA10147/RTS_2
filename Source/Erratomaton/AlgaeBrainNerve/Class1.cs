using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;




// 无进度条太阳能板

namespace EM
{
    public class CompPowerPlantSolar : CompPowerPlant
    {
        private const float NightPower = 0f;

        private static readonly Vector2 BarSize = new Vector2(2.3f, 0.14f);

        private static readonly Material PowerPlantSolarBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.5f, 0.475f, 0.1f));

        private static readonly Material PowerPlantSolarBarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.15f, 0.15f, 0.15f));

        protected override float DesiredPowerOutput => Mathf.Lerp(0f, 0f - base.Props.PowerConsumption, parent.Map.skyManager.CurSkyGlow) * RoofedPowerOutputFactor;

        private float RoofedPowerOutputFactor
        {
            get
            {
                int num = 0;
                int num2 = 0;
                foreach (IntVec3 item in parent.OccupiedRect())
                {
                    num++;
                    if (parent.Map.roofGrid.Roofed(item))
                    {
                        num2++;
                    }
                }
                return (float)(num - num2) / (float)num;
            }
        }
    }




    // 循环特效
    [StaticConstructorOnStartup]
    public class CompMoteReleaser : ThingComp
    {
        private Mote mote;

        public CompProperties_MoteReleaser Props => (CompProperties_MoteReleaser)props;

        public override void CompTick()
        {
            if (parent.Map != null)
            {
                if (mote == null)
                {
                    mote = MoteMaker.MakeStaticMote(parent.DrawPos, parent.Map, Props.moteDef);
                    mote.instanceColor = parent.DrawColor;
                }
                if (mote.def.mote.needsMaintenance)
                {
                    mote.Maintain();
                }
            }
        }

        public void Notify_ColorChanged()
        {
            mote = null;
        }
    }






    public class CompProperties_MoteReleaser : CompProperties
    {
        public ThingDef moteDef;

        public CompProperties_MoteReleaser()
        {
            compClass = typeof(CompMoteReleaser);
        }
    }



    [StaticConstructorOnStartup]
    public class CompAttachEffecter : ThingComp
    {
        private Effecter effecter;

        public CompProperties_AttachEffecter Props => (CompProperties_AttachEffecter)props;

        public override void CompTick()
        {
            if (parent.Map != null)
            {
                if (effecter == null)
                {
                    effecter = Props.effecterDef.SpawnAttached(parent, parent.Map);
                }
                effecter?.EffectTick(parent, parent);
            }
        }
    }


    public class CompProperties_AttachEffecter : CompProperties
    {
        public EffecterDef effecterDef;

        public CompProperties_AttachEffecter()
        {
            compClass = typeof(CompAttachEffecter);
        }
    }





}
