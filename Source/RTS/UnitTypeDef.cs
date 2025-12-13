using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RTS
{
    public class UnitTypeDef : Def
    {
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
    }
    public class UnitTraitDef : Def
    {
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
    }
}
