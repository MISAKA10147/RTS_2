using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace RTS
{
    public class Gizmo_SetMassLevel : Gizmo_Slider
    {
        public Gizmo_SetMassLevel(CompHiveSystem comp) 
        {
            this.comp = comp;
        }
        protected override float Target
        {
            get
            {
                return this.comp.targetMass;
            }
            set
            {
                this.comp.targetMass = value;
            }
        } 
        protected override float ValuePercent
        {
            get
            {
                return this.comp.targetMass / 1f;
            }
        }
        protected override string Title
        {
            get
            {
                return "TargetMass".Translate();
            }
        } 
        protected override bool IsDraggable
        {
            get
            {
                return true;
            }
        } 
        protected override string BarLabel
        {
            get
            {
                return (this.comp.targetMass * 100f+"/100%");
            }
        } 
        protected override bool DraggingBar
        {
            get
            {
                return draggingBar;
            }
            set
            {
                draggingBar = value;
            }
        }
        protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        {
            headerRect.xMax -= 24f;
            Rect rect = new Rect(headerRect.xMax, headerRect.y, 24f, 24f);
            GUI.DrawTexture(rect, this.comp.Props.resource?.uiIcon ?? ThingDefOf.Steel.uiIcon);
            GUI.DrawTexture(new Rect(rect.center.x, rect.y, rect.width / 2f, rect.height / 2f), 
                this.comp.allowAutoRefuel ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
            if (Widgets.ButtonInvisible(rect, true))
            {
                this.comp.allowAutoRefuel = !this.comp.allowAutoRefuel;
            }
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, new Func<string>(this.RefuelTip), 828267373);
                mouseOverElement = true;
            }
            base.DrawHeader(headerRect, ref mouseOverElement);
        }
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (SteamDeck.IsSteamDeckInNonKeyboardMode)
            {
                return base.GizmoOnGUI(topLeft, maxWidth, parms);
            }
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }
        private string RefuelTip()
        {
            return "MassTarget".Translate(this.comp.targetMass);
        }
        protected override string GetTooltip()
        {
            return "";
        }
         
        private CompHiveSystem comp; 
        private static bool draggingBar;

    }
}
