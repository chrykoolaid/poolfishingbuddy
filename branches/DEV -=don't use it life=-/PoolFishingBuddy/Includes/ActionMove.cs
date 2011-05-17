using System.Collections.Generic;
using System.Linq;
using Styx;
using Styx.Helpers;
using Styx.Logic;
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
            Logging.WriteNavigator("{0} - Navigation: ActionMove", Helpers.TimeNow);

            WoWPoint destination = WoWPoint.Empty;
            
            
            if (context is WoWObject)
            {
                destination = ((WoWObject)context).Location;
            }
            else if (context is WoWPoint)
            {
                destination = (WoWPoint)context;
                destination.Z = Helpers.increaseGroundZ(destination);
            }
            else
            {
                Logging.Write(System.Drawing.Color.Red, "Movement is of unknown type: {0}", context);
                return RunStatus.Failure;
            }
            if (StyxWoW.Me.Mounted)
            {
                if (StyxWoW.Me.ZoneId == 1537)
                {
                    float groundz;
                    Navigator.FindHeight(destination.X, destination.Y, out groundz);
                    destination.Z = groundz;
                    while (StyxWoW.Me.ZoneId == 1537)
                        Navigator.MoveTo(destination);
                }
                //Logging.Write("Destination: {0}, Distance: {1}", destination, StyxWoW.Me.Location.Distance(destination));
                Flightor.MoveTo(destination);
            }
            else if (!StyxWoW.Me.Mounted && (!Mount.CanMount() || StyxWoW.Me.IsIndoors || StyxWoW.Me.ZoneId == 1537))
            {
                float groundz;
                Navigator.FindHeight(destination.X, destination.Y, out groundz);
                destination.Z = groundz;
                while ((!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat) && (!Mount.CanMount() || StyxWoW.Me.IsIndoors || StyxWoW.Me.ZoneId == 1537))
                    Navigator.MoveTo(destination);
            }
            else
            {
                Logging.Write(System.Drawing.Color.Red, "Not Mounted! Return: {0}", RunStatus.Failure);
                
            }
            return RunStatus.Failure;
        }
    }
}
