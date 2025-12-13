using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RTS
{ 
    public class UnitData : IExposable
    {
        public UnitCompProperties Extension => this.kind.race.GetCompProperties<UnitCompProperties>();
        public float Mass 
        {
            get 
            {
                UnitCompProperties ex = this.Extension;
                return ex != null ?ex.mass : 10f;
            }
        }
        public float UnitCost
        {
            get
            {
                UnitCompProperties ex = this.Extension;
                return ex != null ? ex.cost : 1f;
            }
        }
        public void ExposeData()
        {
            Scribe_Defs.Look(ref this.kind,"Kind");
            Scribe_Defs.Look(ref this.type, "type");
            Scribe_Values.Look(ref this.spawnTime, "spawnTime");
            Scribe_Values.Look(ref this.unitDesc, "unitDesc");
            Scribe_Collections.Look(ref this.traits, "traits",LookMode.Def);
        }
        public Texture2D Icon
        {
            get
            {
                if (this.icon == null) this.icon = ContentFinder<Texture2D>.Get(this.iconPath);
                return this.icon;
            }
        }

        [Unsaved]
        private Texture2D icon;
        [NoTranslate]
        public string iconPath;
        public PawnKindDef kind;
        public int spawnTime = 100;
        public UnitTypeDef type;
        public string unitDesc;
         
        public List<UnitTraitDef> traits = new List<UnitTraitDef>();
    }
}