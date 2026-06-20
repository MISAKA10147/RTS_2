using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RTS
{
    [StaticConstructorOnStartup]
    public class Window_HiveSystem : Window
    {
        public Window_HiveSystem(CompHiveSystem comp)
        {
            this.draggable = true;
            this.comp = comp;
            this.doCloseX = true;
            this.forcePause = false;
            this.focusWhenOpened = false;
            this.preventCameraMotion = false;
            this.cacheTypes = DefDatabase<UnitTypeDef>.AllDefsListForReading;
            this.curType = this.cacheTypes.First();
            this.units = comp.Props.units.FindAll(u => u.type == this.curType); 
        }
        public override Vector2 InitialSize => new Vector2(250f + this.Margin *2f, 500f + this.Margin *2f);
        protected override float Margin => base.Margin / 2f;
        public static bool CustomButtonText(ref Rect rect, string label, Color bgColor, Color textColor, Color borderColor, Color unfilledBgColor = default(Color), bool cacheHeight = false, float borderSize = 1f, bool doMouseoverSound = true, bool active = true, float fillPercent = 1f)
        {
            if (cacheHeight)
            {
                Widgets.LabelCacheHeight(ref rect, label, false, false);
            }
            Rect position = new Rect(rect);
            position.x += borderSize;
            position.y += borderSize;
            position.width -= borderSize * 2f;
            position.height -= borderSize * 2f;
            GUI.color = borderColor;
            Widgets.DrawBox(rect,1);
            GUI.color = Color.white;
            if (unfilledBgColor != default(Color))
            {
                Widgets.DrawRectFast(position, unfilledBgColor, null);
            }
            position.width *= fillPercent; 
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            if (doMouseoverSound)
            {
                MouseoverSounds.DoRegion(rect);
            }
            GUI.color = textColor;
            if (Mouse.IsOver(rect))
            {
                GUI.color = Widgets.MouseoverOptionColor;
            }
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = anchor;
            GUI.color = color;
            return active && Widgets.ButtonInvisible(rect, false);
        }
        public override void DoWindowContents(Rect inRect)
        {
            if (this.curType == null)
            {
                this.curType = this.cacheTypes.First();
            }
            Text.Font = GameFont.Small;
            Rect typeButton = new Rect(inRect.x, inRect.y + 15f, 120f, 30f);
            if (CustomButtonText(ref typeButton, "RTS_Build".Translate(),
            TransparentWhite,Color.white, !this.isBuilding ? Color.grey : Color.white))
            {
                this.isBuilding = true;
            }
            typeButton.x += 120f + 10f;
            if (CustomButtonText(ref typeButton, "RTS_Launch".Translate(),
TransparentWhite, Color.white, this.isBuilding ? Color.grey : Color.white))
            {
                this.isBuilding = false;
            }
            Widgets.DrawLineHorizontal(0f, inRect.y + 10f + 40f, inRect.width + this.Margin * 2f,
                TransparentWhite);
            if (this.isBuilding)
            {
                DrawBuilding(inRect);
            }
            else 
            {
                DrawLaunch(inRect);
            }
            Widgets.DrawLineHorizontal(0f, inRect.height - 70f, inRect.width + this.Margin * 2f,
    ColorLibrary.SkyBlue);
            Rect bar = new Rect(inRect.x, inRect.height - 60f, inRect.width, 25f);
            Rect barLabel = bar;
            barLabel.x += 10f;
            barLabel.y += 5f;
            Widgets.FillableBar(bar, (float)this.comp.cost / (float)this.comp.CostLimit
                , CostFrame);
            Widgets.Label(barLabel, "RTS_Cost".Translate(this.comp.cost, this.comp.CostLimit));
            bar.y += 30f;
            barLabel.y += 30f;
            Widgets.FillableBar(bar, this.comp.Mass / this.comp.Props.massLimit, SteelFrame);
            Widgets.Label(barLabel, "RTS_Mass".Translate(this.comp.Mass, this.comp.Props.massLimit));
            if (this.comp.healCost != 0f) 
            {
                string healCost = this.comp.healCost + "/" + this.comp.Props.healInterval +"T";
                float width = Text.CalcSize(healCost).x;
                Widgets.Label(new Rect(bar.x + bar.width - width - 5f,bar.y,
                  width,25f), healCost);
            }
        }
        private void DrawLaunch(Rect inRect)
        {
            string launch = "RTS_AutoLaunch".Translate();
            float width = Text.CalcSize(launch).x;
            Widgets.DrawTextureFitted(new Rect(inRect.width - 25f, inRect.y + 55f, 25f, 25f)
    , WidgetsWork.WorkBoxBGTex_Mid, 1);
            Widgets.CheckboxLabeled(
                new Rect(inRect.width - width - 25f,
                inRect.y + 55f, width + 25f, 25f)
                , launch, ref this.comp.autoLaunch);
            Widgets.DrawTextureFitted(new Rect(inRect.x + 5f + width, inRect.y + 55f, 25f, 25f)
, WidgetsWork.WorkBoxBGTex_Mid, 1);
            Widgets.CheckboxLabeled(
                new Rect(inRect.x + 5f,
                inRect.y + 55f, width + 25f, 25f)
                , "RTS_AutoRepair".Translate(), ref this.comp.autoRepair);

            Rect unitRect = new Rect(inRect.x + 5f, inRect.y + 85f, inRect.width - 10f, 25f);
            Widgets.BeginScrollView(
    new Rect(inRect.x, unitRect.y, inRect.width, 300f),
                     ref pos3,
    new Rect(inRect.x, unitRect.y,
    inRect.width - 20f, this.comp.inner.Count * 30f));
            List<Pawn> pawns = new List<Pawn>();
            List<Pawn> removePawns = new List<Pawn>();
            foreach (var pawn in comp.inner)
            {
                if (this.comp.autoRepair && pawn.health.summaryHealth.SummaryHealthPercent != 1f) 
                {
                    Widgets.DrawTextureFitted(new Rect(unitRect.x + 5f,unitRect.y,25f,25f)
                        , Maintenance,1);
                }
                Rect pawnRect = new Rect(unitRect.width - 30f - 75f, unitRect.y, 70f, 25f);
                pawnRect.x -= 70f;
                Widgets.Label(pawnRect
    , pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent());
                pawnRect.x += 75f;
                Widgets.Label(pawnRect
                    , pawn.kindDef.label);
                if (Widgets.ButtonImage(new Rect(unitRect.width - 30f, unitRect.y, 25f, 25f),
                    TexButton.Delete)) 
                {
                    removePawns.Add(pawn);
                }
                if (Widgets.ButtonImage(unitRect, Button, Transparent, TransparentWhite))
                {
                    pawns.Add(pawn);
                }
                unitRect.y += 30f;
                Widgets.DrawLineHorizontal(0f, unitRect.y - 5f,
                    inRect.width + this.Margin * 2f);
            }
            foreach (var pawn in removePawns)
            {
                Recycle(pawn);
            }
            pawns.ForEach(p => 
            {
                this.comp.Launch(p);
            });
            Widgets.EndScrollView();
            Rect r = new Rect(inRect.x, inRect.y + 85f + 300f, inRect.width, 30f);
            if (CustomButtonText(ref r, "RTS_LaunchAll".Translate(),
            TransparentWhite, Color.white, !this.isBuilding ? Color.grey : Color.white))
            {
                this.comp.LaunchAll();
            }
        }

        private void Recycle(Pawn pawn)
        {
            this.comp.Recycle(pawn);
        }

        private void DrawBuilding(Rect inRect)
        {
            Rect typeRect = new Rect(inRect.x + 5f, inRect.y + 55f, 30f, 30f);
            foreach (var type in cacheTypes)
            {
                if (Widgets.ButtonImage(typeRect, type.Icon, true, type.label))
                {
                    this.curType = type;
                    this.units = comp.Props.units.FindAll(u => u.type == this.curType);
                }
                Widgets.DrawBox(typeRect, 2,this.curType == type ? null : UnSelectedBar);
                typeRect.x += 35f;
            }
            Rect typeLabelRect = new Rect(0f, typeRect.y + 35f, inRect.width, 25f);
            Widgets.DrawBoxSolid(typeLabelRect,
                new Color(0.15f,0.15f,0.17f,0.6f));
            typeLabelRect.x += 5f;
            typeLabelRect.y += 5f;
            Widgets.LabelFit(typeLabelRect, this.curType.label);
            float unitsFrameHeight = 150f;
            Rect unitRect = new Rect(inRect.x, inRect.y + 55f + 35f + 30f,
                inRect.width,
                50f);
            float unitsY = unitRect.y;
            Widgets.BeginScrollView(
                new Rect(inRect.x, inRect.y + 55f + 35f + 30f, inRect.width, unitsFrameHeight),
                                 ref pos,
                new Rect(inRect.x, inRect.y + 55f + 35f + 30f,
                inRect.width, this.units.Count * 50f + (this.units.Count - 1) * 10f),false);
            foreach (var unit in units)
            {
                if (Widgets.ButtonImage(unitRect, Button, Transparent, TransparentWhite, true)) 
                {
                    int count = 1;
                    if (Input.GetKey(KeyCode.LeftShift)) 
                    {
                        count = 5;
                    }
                    for (int i = 0; i < count; i++)
                    {
                        this.comp.StartBuild(unit);
                    }
                }
                Widgets.DrawBox(unitRect, 2, UnitFrame);
                if (unit.unitDesc != null) 
                {
                    TooltipHandler.TipRegion(unitRect,unit.unitDesc);
                }
                Widgets.DrawTextureFitted(
                    new Rect(unitRect.x + 5f, unitRect.y + 5f, 40f, 40f)
                    , unit.Icon, 1f);
                if (unit.traits != null && unit.traits.Any()) 
                {
                    Rect traitRect = new Rect(unitRect.x + 60f,unitRect.y + 5f,20f,20f);
                    foreach (var trait in unit.traits)
                    {
                        Widgets.DrawTextureFitted(traitRect
    , trait.Icon, 1f);
                        TooltipHandler.TipRegion(traitRect, trait.label);
                        traitRect.x += 25f;
                    }
                }
                Rect labelRect =
                    new Rect(unitRect.width - Text.CalcSize(unit.kind.label).x - 5f
                    , unitRect.y + 5f, 120f, 25f);
                Widgets.Label(labelRect, unit.kind.label);
                labelRect.x = inRect.width - 80f;
                labelRect.y += 25f;
                labelRect.width = 50f;
                Widgets.Label(labelRect, unit.Mass.ToString()
                    .Colorize(ColorLibrary.Purple));
                labelRect.x += 60f;
                Widgets.Label(labelRect, unit.UnitCost.ToString().Colorize(ColorLibrary.Yellow));
                unitRect.y += unitRect.height + 5f;
            }
            Widgets.EndScrollView();
            Widgets.DrawLineHorizontal(0f, unitsY + unitsFrameHeight + 5f, inRect.width + this.Margin * 2f,
ColorLibrary.SkyBlue);
            Widgets.BeginScrollView(
    new Rect(inRect.x, unitsY + unitsFrameHeight + 5f, inRect.width, inRect.height
    - 80f - (unitsY + unitsFrameHeight + 5f)),
                     ref pos2,
    new Rect(inRect.x, unitsY + unitsFrameHeight + 5f,
    inRect.width, this.comp.spawnings.Count * 30f + (this.units.Count - 1) * 10f),false);
            Rect spawningRect = new Rect((inRect.width / 2) - 75f - 40f,
                unitsY + unitsFrameHeight + 10f
                ,30f,30f);
            if (this.comp.spawnings.Any()) 
            {
                Rect functionRect = new Rect((inRect.width / 2) - 95f - 25f, spawningRect.y,30f,30f);
                if (Widgets.ButtonImage(functionRect,this.comp.pause ? Continue : Pause)) 
                {
                    this.comp.pause = !this.comp.pause;
                }
                Widgets.DrawBox(functionRect, 2, UnitFrame);
                functionRect.x = (inRect.width / 2) + 95f;
                functionRect.size = new Vector2(30f,30f);
                if (Widgets.ButtonImage(functionRect, Cancel))
                {
                    var spawning = this.comp.spawnings.First();
                    this.comp.spawnings.Remove(spawning);
                    this.comp.Mass += spawning.count * spawning.data.Mass;
                }  
                Widgets.DrawBox(functionRect, 2, UnitFrame);
            }
            spawningRect.x = (inRect.width / 2) - 85f;
            spawningRect.width = 170f;
            Rect spawningLabelRect = spawningRect;
            spawningLabelRect.y += 5f;
            spawningLabelRect.width = 150f;
            Rect spawningCountRect = spawningLabelRect; 
            Text.Font = GameFont.Small;
            foreach (var spawning in this.comp.spawnings) 
            {
                string name = spawning.data.kind.label;
                Widgets.FillableBar(spawningRect,(float)spawning.spawnTime
                    / (float)spawning.data.spawnTime, UnitFrame, TransparentTex,false);
                Widgets.DrawBox(spawningRect, 2, UnitFrame);
                if (spawning != this.comp.spawnings.First()) 
                {
                    Rect prioritizeRect = new Rect((inRect.width / 2) + 95f,spawningRect.y,30f,30f);
                    if (Widgets.ButtonImage(prioritizeRect, Prioritize))
                    {
                        var first = this.comp.spawnings.First();
                        int varCount = first.count;
                        var kind = first.data;
                        var time = first.spawnTime;
                        first.data = spawning.data;
                        first.count = spawning.count;
                        first.spawnTime = spawning.spawnTime;
                        spawning.data = kind;
                        spawning.count = varCount;
                        spawning.spawnTime = time;
                    } 
                    prioritizeRect.size = new Vector2(30f,30f);
                    Widgets.DrawBox(prioritizeRect, 2, UnitFrame);
                }
                spawningLabelRect.x = (inRect.width / 2) - ((Text.CalcSize(name).x)/2);
                Widgets.Label(spawningLabelRect, name);
                string count = "*" + spawning.count.ToString();
                spawningCountRect.x = spawningRect.x +
                    (spawningRect.width) - ((Text.CalcSize(count).x)) - 5f;
                Widgets.Label(spawningCountRect, count);
                spawningCountRect.y += 30f;
                spawningRect.y += 30f;
                spawningLabelRect.y += 30f;
            } 
            Widgets.EndScrollView();
        }

        public List<UnitData> units = new List<UnitData>();
        public UnitTypeDef curType = null;
        public bool isBuilding = true;
        public CompHiveSystem comp;
        public List<UnitTypeDef> cacheTypes = new List<UnitTypeDef>();
        Vector2 pos;
        Vector2 pos2;
        Vector2 pos3;
        public Color Transparent = new Color(0,0,0,0);
        public Color TransparentWhite = new Color(255,255,255,0.15f);
        public static readonly Texture2D TransparentTex
            = SolidColorMaterials.NewSolidColorTexture(new Color(0, 0, 0, 0));
        public static readonly Texture2D Button = SolidColorMaterials.NewSolidColorTexture(Color.white);
        public static readonly Texture2D YellowBar = SolidColorMaterials.NewSolidColorTexture(ColorLibrary.Yellow);
        public static readonly Texture2D PurpleBar = SolidColorMaterials.NewSolidColorTexture(ColorLibrary.Purple); 
        public static readonly Texture2D UnSelectedBar = SolidColorMaterials.NewSolidColorTexture(new Color(0.17f,0.27f,0.4f));
        public static readonly Texture2D UnitFrame = 
            SolidColorMaterials.NewSolidColorTexture(new Color(0.17f, 0.27f, 0.4f));
        public static readonly Texture2D CostFrame =
    SolidColorMaterials.NewSolidColorTexture(new Color(0.97f,0.54f,0));
        public static readonly Texture2D SteelFrame =
    SolidColorMaterials.NewSolidColorTexture(new Color(0,0.37f,0.62f));
        public static readonly Texture2D Maintenance = ContentFinder<Texture2D>.Get("UI/Maintenance");
        public static readonly Texture2D Pause = ContentFinder<Texture2D>.Get("UI/Icon/Pause");
        public static readonly Texture2D Continue = ContentFinder<Texture2D>.Get("UI/Icon/Continue");
        public static readonly Texture2D Prioritize = ContentFinder<Texture2D>.Get("UI/Icon/Prioritize");
        public static readonly Texture2D Cancel = ContentFinder<Texture2D>.Get("UI/Icon/Cancel");
    }
}

