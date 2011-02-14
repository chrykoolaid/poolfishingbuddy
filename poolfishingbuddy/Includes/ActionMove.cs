using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Tripper.Tools.Math;
using Action = TreeSharp.Action;

namespace PoolFishingBuddy
{
    class ActionMove : Action
    {
        
        protected override RunStatus Run(object context)
        {
            Logging.WriteDebug("ActionMove");

            WoWPoint destination = WoWPoint.Empty;
            
            /*
            if (context is WoWObject)
            {
                destination = ((WoWObject)context).Location;
            }
            else*/
            if (context is WoWPoint)
            {
                destination = (WoWPoint)context;
                destination.Z = Helpers.NormalizeGroundZ(destination) + PoolFisherSettings.Instance.HeightModifier;
            }
            else
            {
                Logging.Write("Movement is of unknown type: {0}", context);
                return RunStatus.Failure;
            }
            if (StyxWoW.Me.Mounted)
            {
                //Logging.Write("Destination: {0}, Distance: {1}", destination, StyxWoW.Me.Location.Distance(destination));
                Flightor.MoveWithTrace(destination);
                //Flightor.MoveTo(destination);
            }
            else
            {
                Logging.Write("Not Mounted! Return: {0}", RunStatus.Failure);
                
            }
            return RunStatus.Failure;
        }
    }
}
