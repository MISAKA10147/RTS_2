using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RTS
{
    public class Command_SpecialAction : Command_Action
    {
        public void GiveAction(Verb AttackVerb) 
        {
            this.action = () =>
            {
                Find.Targeter.BeginTargeting(AttackVerb.targetParams,
            t =>
            {
                postAction(t);
            }, (t) => AttackVerb.DrawHighlight(t), t => AttackVerb.ValidateTarget(t)
            , null, null, null, true, (t) => AttackVerb.OnGUI(t));
            };
        }
        public void GiveActionHive(Map map)
        {
            this.action = () =>
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building.TryGetComp<CompHiveSystem>() is CompHiveSystem comp)
                    {
                        options.Add(new FloatMenuOption(building.Label, () =>
                        {
                            this.postActionHive(comp);
                        }));
                    }
                }
                if (options.Any())
                {
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
        public override void MergeWith(Gizmo other)
        {
            base.MergeWith(other);
            if (other is Command_SpecialAction command) 
            {
                if (this.postAction != null) 
                { 
                    this.postAction += command.postAction;
                }
                if (this.postActionHive != null)
                {
                    this.postActionHive += command.postActionHive;
                } 
            }
        }

        public Action<CompHiveSystem> postActionHive;
        public Action<LocalTargetInfo> postAction;
    }
}
