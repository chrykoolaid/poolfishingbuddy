using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using PoolFishingBuddy.Forms;

using Styx;
using Styx.Helpers;

using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.LootFrame;

using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Styx.Logic.Inventory.Frames.Taxi;

using TreeSharp;
using Action = TreeSharp.Action;


namespace PoolFishingBuddy
{
    class Helpers
    {
        /// <summary>
        /// Opens a new Form.
        /// </summary>
        static public void StartMonitoring()
        {
            try
            {
                FormFishMonitoring form = new FormFishMonitoring();
                form.ShowDialog();
            }
            catch (ThreadAbortException e)
            {
                Console.WriteLine("Exception message: {0}", e.Message);
                Thread.ResetAbort();
            }
            catch (Exception e) 
            {
                Console.WriteLine("Exception message: {0}", e.Message);
                Thread.ResetAbort();
            }
        }

        /// <summary>
        /// Does things on init.
        /// </summary>
        static public void Init(System.EventArgs args)
        {
            PoolFisher.runTimer.Start();
            PoolFisherSettings.Instance.Load();

            if (!StyxWoW.FlightChecksDisabled)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - You don't have lifetime subscription or valid paid plugin key. Stopping..", Helpers.TimeNow);
                TreeRoot.Stop();
            }
            
            if (PoolFisherSettings.Instance.FlyingMountID == 0 && TreeRoot.IsRunning)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - You did not select any flying mount, please go to settings first. Stopping..", Helpers.TimeNow);
                TreeRoot.Stop();
            }

            if (PoolFisherSettings.Instance.FishingPole == 0 || PoolFisherSettings.Instance.Mainhand == 0 && TreeRoot.IsRunning)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - You did not select your weapons and/or fishing pole, please go to settings first. Stopping..", Helpers.TimeNow);
                TreeRoot.Stop();
            }

            if (PoolFisherSettings.Instance.ShouldMail == true && PoolFisherSettings.Instance.MailRecipient != "")
            {
                LevelbotSettings.Instance.Load();
                LevelbotSettings.Instance.MailRecipient = PoolFisherSettings.Instance.MailRecipient;
                LevelbotSettings.Instance.Save();
            }

            if (TreeRoot.IsRunning)
            {
                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Looking for weapons and fishing pole and add them to protected items.", Helpers.TimeNow);

                PoolFisher.BagItems = StyxWoW.Me.BagItems;

                bool hasPole = false;
                bool hasMainhand = false;

                foreach (WoWItem i in PoolFisher.BagItems)
                {
                    //if (!ProtectedItemsManager.Contains(i.Name)) ProtectedItemsManager.Add(i.Name);

                    if (i.Entry == PoolFisherSettings.Instance.FishingPole && TreeRoot.IsRunning)
                    {
                        if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.FishingPole)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.FishingPole);
                        hasPole = true;
                        Logging.Write(System.Drawing.Color.Green, "{0} is valid.", i.Name);
                    }
                    
                    if (i.Entry == PoolFisherSettings.Instance.Mainhand && TreeRoot.IsRunning)
                    {
                        if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.FishingPole)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.FishingPole);
                        hasMainhand = true;
                        Logging.Write(System.Drawing.Color.Green, "{0} is valid.", i.Name);
                    }
                    
                    if (PoolFisherSettings.Instance.Offhand != 0 && i.Entry == PoolFisherSettings.Instance.Offhand && TreeRoot.IsRunning)
                    {
                        if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.FishingPole)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.FishingPole);
                        Logging.Write(System.Drawing.Color.Green, "{0} is valid.", i.Name);
                    }
                }

                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.Entry == PoolFisherSettings.Instance.FishingPole)
                {
                    if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.FishingPole)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.FishingPole);
                    hasPole = true;
                    Logging.Write(System.Drawing.Color.Green, "{0} is valid.", StyxWoW.Me.Inventory.Equipped.MainHand.Name);
                }

                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.Entry == PoolFisherSettings.Instance.Mainhand)
                {
                    if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.Mainhand)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.Mainhand);
                    hasMainhand = true;
                    Logging.Write(System.Drawing.Color.Green, "{0} is valid.", StyxWoW.Me.Inventory.Equipped.MainHand.Name);
                }

                //Logging.Write(System.Drawing.Color.Red, "{0} - ProtectedItems count: {1}", Helpers.TimeNow, ProtectedItemsManager.GetAllItemIds().Count);

                if (!hasPole)
                {
                    Logging.Write(System.Drawing.Color.Red, "Fishing pole not found in bags/equip! Please go to settings to select your new weapons. Stopping..");
                    TreeRoot.Stop();
                }
                if (!hasMainhand)
                {
                    Logging.Write(System.Drawing.Color.Red, "Weapons not found in bags/equip! Please go to settings to select your new weapons. Stopping..");
                    TreeRoot.Stop();
                }
            }

            WoWSpell Mount = WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID);

            Logging.WriteDebug(System.Drawing.Color.Green, "Current Settings:");
            Logging.WriteDebug(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.WriteDebug(System.Drawing.Color.Green, "Flying Mount: {0}", Mount.Name);
            Logging.WriteDebug(System.Drawing.Color.Green, "Height: {0}", PoolFisherSettings.Instance.HeightModifier);
            Logging.WriteDebug(System.Drawing.Color.Green, "Bouncemode: {0}", PoolFisherSettings.Instance.BounceMode);
            Logging.WriteDebug(System.Drawing.Color.Green, "Min. range to cast: {0}", PoolFisherSettings.Instance.MinCastRange);
            Logging.WriteDebug(System.Drawing.Color.Green, "Max. range to cast: {0}", PoolFisherSettings.Instance.MaxCastRange);
            Logging.WriteDebug(System.Drawing.Color.Green, "Max. attempts to cast: {0}", PoolFisherSettings.Instance.MaxCastAttempts);
            Logging.WriteDebug(System.Drawing.Color.Green, "Ninja Pools: {0}", PoolFisherSettings.Instance.NinjaPools);
            Logging.WriteDebug(System.Drawing.Color.Green, "Blacklist Schools: {0}", PoolFisherSettings.Instance.BlacklistSchools);
            Logging.WriteDebug(System.Drawing.Color.Green, "Use Lure: {0}", PoolFisherSettings.Instance.useLure);
            Logging.WriteDebug(System.Drawing.Color.Green, "Max. attempts to reach pool: {0}", PoolFisherSettings.Instance.MaxNewLocAttempts);

            Logging.WriteDebug(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.WriteDebug(System.Drawing.Color.Green, "Current Profile:");
            Logging.WriteDebug(System.Drawing.Color.Green, "-------------------------------------------");
            try
            {
                Logging.WriteDebug(System.Drawing.Color.Green, "Name: {0}", ProfileManager.CurrentProfile.Name);
                Logging.WriteDebug(System.Drawing.Color.Green, "Hotspots: {0}", ProfileManager.CurrentProfile.HotspotManager.Hotspots.Count);
                Logging.WriteDebug(System.Drawing.Color.Green, "Blackspots: {0}", ProfileManager.CurrentProfile.Blackspots.Count);
                //Logging.WriteDebug(System.Drawing.Color.Green, "Vendor: {0}", ProfileManager.CurrentProfile.VendorManager.Vendors.Count);
                //Logging.WriteDebug(System.Drawing.Color.Green, "Mailbox: {0}", ProfileManager.CurrentProfile.MailboxManager.Mailboxes.Count);
                Logging.WriteDebug(System.Drawing.Color.Green, "Protected Items: {0}", ProtectedItemsManager.GetAllItemIds().Count);
            }
            catch (Exception e)
            {
                Logging.Write(System.Drawing.Color.Red, "ProfileExeption: {0}. Please check the Profile you are using!", e.ToString());
                Logging.WriteDebug(System.Drawing.Color.Red, "ProfileExeption: {0}. Please check the Profile you are using!", e.ToString());
            }

            Logging.WriteDebug(System.Drawing.Color.Green, "-------------------------------------------");

            Helpers.blacklistSchoolsFromSettings();
        }

        /// <summary>
        /// Does things on final.
        /// </summary>
        static public void Final(System.EventArgs args)
        {
            
            /*
            PoolFisher.looking4NewPoint = false;
            PoolFisher.looking4NewPool = true;
            PoolFisher.MeIsFishing = false;
            PoolFisher.need2Lure = false;
            PoolFisher.newLocAttempts = 0;
            PoolFisher.castAttempts = 0;
            PoolFisher.PoolPoints.Clear();
            PoolFisher.Pool = null;
            PoolFisher.GrindArea = null;
            PoolFisher.HotspotList.Clear();
            PoolFisher.BlackspotList.Clear();
            PoolFisher._currenthotspot = -1;
            equipWeapon();
            */
        }

        static public void quitWoW()
        {
            Lua.DoString("Logout()", "fishingbuddy.lua"); // Quit gives WoW Errors for some reason

            while (StyxWoW.IsInGame && (!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat))
                Thread.Sleep(500);
        }


        /// <summary>
        /// EquipItemByName(itemId or "itemName" or "itemLink"[, slot]) - Equips an item, optionally into a specified slot. 
        /// </summary>
        static public void equipWeapon()
        {
            if (PoolFisherSettings.Instance.Mainhand != 0)
                Lua.DoString("EquipItemByName (\"" + PoolFisherSettings.Instance.Mainhand.ToString() + "\")", "fishingbuddy.lua");
            if (PoolFisherSettings.Instance.Offhand != 0)
                Lua.DoString("EquipItemByName (\"" + PoolFisherSettings.Instance.Offhand.ToString() + "\")", "fishingbuddy.lua");
        }

        /// <summary>
        /// EquipItemByName(itemId or "itemName" or "itemLink"[, slot]) - Equips an item, optionally into a specified slot. 
        /// </summary>
        static public bool equipFishingPole
        {
            get
            {
                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
                {
                    return true;
                }
                else if (PoolFisherSettings.Instance.FishingPole != 0)
                {
                    Lua.DoString("EquipItemByName (\"" + PoolFisherSettings.Instance.FishingPole.ToString() + "\")", "fishingbuddy.lua");
                    while (StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole)
                        Thread.Sleep((PoolFisher.Ping * 2) + 100);
                    return true;
                }
                return false;
            }
        }

        #region Lures

        public struct LureType
        {
            public String l_name;
            public uint l_ID;

            public LureType(String name, uint ID)
            {
                this.l_name = name;
                this.l_ID = ID;
            }
            public string name { get { return this.l_name; } }
            public uint ID { get { return this.l_ID; } }
        }

        static public List<LureType> Lures = new List<LureType>
        {
            new LureType("Heat-Treated Spinning Lure",68049),
            new LureType("Feathered Lure",62673),
            new LureType("Sharpened Fish Hook",34861),
            new LureType("Glow Worm",46006),
            new LureType("Aquadynamic Fish Attractor",6533),
            new LureType("Flesh Eating Worm",7307),
            new LureType("Bright Baubles",6532),
            new LureType("Nightcrawlers",6530),
            new LureType("Aquadynamic Fish Lens",6811),
            new LureType("Shiny Bauble",6529),
            new LureType("Weather-Beaten Fishing Hat",33820)
        };

        static public bool LureIsOnPole { get { return Lua.GetReturnValues("return GetWeaponEnchantInfo()", "fishingbuddy.lua")[0] == "1"; } }

        static public void applylure()
        {
            if (PoolFisher.need2Lure && (!StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId == 0))
            {
                if (PoolFisherSettings.Instance.LureID == 33820)
                {
                    WoWItem head = StyxWoW.Me.Inventory.GetItemBySlot((uint)Styx.WoWEquipSlot.Head);

                    if (head != null && head.Entry == 33820)
                    {
                        if (head.Cooldown == 0)
                        {
                            TreeRoot.StatusText = "Luring";
                            Logging.Write(System.Drawing.Color.Blue, "Appling Weather-Beaten Fishing Hat to fishing pole");
                            head.Use();
                            Thread.Sleep(PoolFisher.Ping * 2 + 50);
                            while ((!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat) && (StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId != 0))
                                Thread.Sleep(150);
                            Thread.Sleep(PoolFisher.Ping * 2 + 50);
                            PoolFisher.need2Lure = false;
                        }
                        else
                        {
                            Logging.Write(System.Drawing.Color.Red, "{0} - Weather-Beaten Fishing Hat is on cooldown!", TimeNow);
                        }
                    }
                    else
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Weather-Beaten Fishing Hat is not euqipped, won't lure anymore!", TimeNow);
                        PoolFisherSettings.Instance.Load();
                        PoolFisherSettings.Instance.useLure = false;
                        PoolFisherSettings.Instance.LureID = 0;
                        PoolFisherSettings.Instance.Save();
                    }
                }
                else
                {
                    WoWItem _lureInBag = getIteminBag((uint)PoolFisherSettings.Instance.LureID);
                    if (_lureInBag != null && _lureInBag.Use())
                    {
                        TreeRoot.StatusText = "Luring";
                        Logging.Write(System.Drawing.Color.Blue, "{0} - Appling lure to fishing pole", TimeNow);
                        Thread.Sleep(PoolFisher.Ping * 2 + 50);
                        while ((!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat) && (StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId != 0))
                            Thread.Sleep(150);
                        Thread.Sleep(PoolFisher.Ping * 2 + 50);
                        PoolFisher.need2Lure = false;
                    }
                    else
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Could not find lure, won't lure anymore!", TimeNow);
                        PoolFisherSettings.Instance.Load();
                        PoolFisherSettings.Instance.useLure = false;
                        PoolFisherSettings.Instance.LureID = 0;
                        PoolFisherSettings.Instance.Save();
                    }
                }
            }
        }

        static public WoWItem getIteminBag(uint entry)
        {
            return StyxWoW.Me.BagItems.Where(i => i.Entry == entry).FirstOrDefault();
        }

        static public bool IsItemInBag(uint entry)
        {
            return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry) != null;
        }

        #endregion

        /// <summary>
        /// IsUsableSpell(spell) 
        /// </summary>
        /// <param name="id">WoWSpell</param>
        /// <returns>true or false</returns>
        static public bool IsUsableSpell(int id)
        {
            return Lua.GetReturnVal<bool>("return IsUsableSpell(" + id + "); ", 0);
        }

        /// <summary>
        /// Returns true if one or more pools are in a distance (2D) of 150 yards and not blacklisted. 
        /// </summary>
        static public bool findPool
        {
            get
            {
                if (PoolFisher.looking4NewPool)
                {
                    //Logging.Write("{0} - Looking for pools..", TimeNow);

                    ObjectManager.Update();
                    List<WoWGameObject> poolList = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.FishingHole && !Blacklist.Contains(o.Guid) && !PoolFisher.PermaBlacklist.Contains(o.Entry) && o.Distance2D <= 150 && o.Location.X != 0).OrderBy(o => o.Distance).ToList();

                    //Logging.Write("poolList.Count: {0}", poolList.Count);

                    if (poolList.Count > 0)
                    {
                        foreach (WoWGameObject o in poolList)
                        {
                            WoWPoint ground = WoWPoint.Empty;
                            GameWorld.TraceLine(new WoWPoint(o.X, o.Y, StyxWoW.Me.Location.Z), new WoWPoint(o.X, o.Y, o.Z), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO, out ground);


                            if (ground == WoWPoint.Empty)
                            {
                                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - {1} at a Distance of {2} yards.", Helpers.TimeNow, o.Name, string.Format("{0:##.#}", o.Distance));
                                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - To Blacklist it, add this to your profile:", Helpers.TimeNow);
                                Logging.Write(System.Drawing.Color.DarkCyan, "<Blackspot X=\"{0}\" Y=\"{1}\" Z=\"{2}\" Radius=\"1\" />", o.X, o.Y, o.Z);

                                if (PoolFisher.BlackspotList.Count != 0)
                                {
                                    foreach (WoWPoint Blackspot in PoolFisher.BlackspotList)
                                    {
                                        if (poolList[0].Location.Distance2D(Blackspot) < 20)
                                        {
                                            Logging.Write(System.Drawing.Color.Red, "{0} - Pool is in range of {1} (Blackspot), blacklisting for 2 minutes.", TimeNow, Blackspot);
                                            BlackListPool(o);
                                            return false;
                                        }
                                    }
                                }

                                PoolFisher.Pool = poolList[0];
                                PoolFisher.looking4NewPool = false;
                                PoolFisher.looking4NewLoc = true;
                                return true;
                            }
                            else
                            {
                                Logging.Write(System.Drawing.Color.Red, "{0} - Pool is underground, blacklisting for 2 minutes.", Helpers.TimeNow);
                                BlackListPool(o);
                                return false;
                            }
                        }
                    }
                    else
                    {
                        PoolFisher.looking4NewPool = true;
                        return false;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Returns current time "hh:mm:ss tt"
        /// </summary>
        static public string TimeNow { get { return DateTime.Now.ToString("hh:mm:ss tt", System.Globalization.DateTimeFormatInfo.InvariantInfo); } }



        static public WoWPoint getSaveLocation(WoWPoint Location, int minDist, int maxDist, int traceStep)
        {
            Logging.WriteNavigator("{0} - Navigation: Looking for save Location around {1}.", TimeNow, Location);

            float _PIx2 = 3.14159f * 2f;

            for (int i = 0, x = minDist; i < traceStep && x < maxDist && PoolFisher.looking4NewLoc == true; i++)
            {
                WoWPoint p = Location.RayCast((i * _PIx2) / traceStep, x);

                p.Z = getGroundZ(p);
                WoWPoint pLoS = p;
                pLoS.Z = p.Z + 0.5f;

                if (p.Z != float.MinValue && !PoolFisher.badLocations.Contains(p) && StyxWoW.Me.Location.Distance(p) > 1)
                {
                    if (getHighestSurroundingSlope(p) < 1.2f && GameWorld.IsInLineOfSight(pLoS, Location) /*&& Navigator.CanNavigateFully(StyxWoW.Me.Location, Location)*/)
                    {
                        PoolFisher.looking4NewLoc = false;
                        Logging.WriteNavigator("{0} - Navigation: Moving to {1}. Distance: {2}", TimeNow, p, Location.Distance(p));
                        return p;
                    }
                }

                if (i == (traceStep - 1))
                {
                    i = 0;
                    x++;
                }
            }

            if (PoolFisher.Pool != null)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - No valid points returned by RayCast, blacklisting for 2 minutes.", TimeNow);
                BlackListPool(PoolFisher.Pool);
                return WoWPoint.Empty;
            }
            else
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - No valid points returned by RayCast, can't navigate without user interaction. Stopping!", TimeNow);
                TreeRoot.Stop();
                return WoWPoint.Empty;
            }
            
        }

        /// <summary>
        /// Credits to exemplar.
        /// </summary>
        /// <returns>Z-Coordinates for PoolPoints so we don't jump into the water.</returns>
        public static float getGroundZ(WoWPoint p)
        {
            WoWPoint ground = WoWPoint.Empty;

            GameWorld.TraceLine(new WoWPoint(p.X, p.Y, (p.Z + (float)PoolFisherSettings.Instance.MaxCastRange)), new WoWPoint(p.X, p.Y, (p.Z - 0.8f)), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures/* | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO*/, out ground);

            if (ground != WoWPoint.Empty)
            {
                Logging.WriteDebug("{0} - Ground Z: {1}.", TimeNow, ground.Z);
                return ground.Z;
            }
            Logging.WriteDebug("{0} - Ground Z returned float.MinValue.", TimeNow);
            return float.MinValue;
        }

        /// <summary>
        /// Height modifier to increase Z-Coordinates by the value from settings.
        /// </summary>
        /// <param name="p">WoWPoint</param>
        /// <returns>Z-Coordinates increased by the value from settings.</returns>
        public static float increaseGroundZ(WoWPoint p)
        {
            float ground = p.Z + PoolFisherSettings.Instance.HeightModifier;
            return ground;
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <returns>Highest slope of surrounding terrain, returns 100 if the slope can't be determined</returns>
        public static float getHighestSurroundingSlope(WoWPoint p)
        {
            Logging.WriteNavigator("{0} - Navigation: Sloapcheck on Point: {1}", TimeNow, p);
            float _PIx2 = 3.14159f * 2f;
            float highestSlope = -100;
            float slope = 0;
            int traceStep = 15;
            float range = 0.5f;
            WoWPoint p2;
            for (int i = 0; i < traceStep; i++)
            {
                p2 = p.RayCast((i * _PIx2) / traceStep, range);
                p2.Z = getGroundZ(p2);
                slope = Math.Abs( getSlope(p, p2) );
                if( slope > highestSlope )
                {
                    highestSlope = (float)slope;
                }
            }
            Logging.WriteNavigator("{0} - Navigation: Highslope {1}", TimeNow, highestSlope);
            return Math.Abs( highestSlope );
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <param name="p1">from WoWPoint</param>
        /// <param name="p2">to WoWPoint</param>
        /// <returns>Return slope from WoWPoint to WoWPoint.</returns>
        public static float getSlope(WoWPoint p1, WoWPoint p2)
        {
            float rise = p2.Z - p1.Z;
            float run = (float)Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            return rise / run;
        }

        /// <summary>
        /// Traceline to get watersurface.
        /// </summary>
        /// <param name="p">WoWPoint</param>
        /// <returns>Returns z-coords of watersurface for the current location.</returns>
        public static float getWaterSurface(WoWPoint p)
        {
            WoWPoint watersurface = WoWPoint.Empty;
            GameWorld.TraceLine(new WoWPoint(p.X, p.Y, 10000), new WoWPoint(p.X, p.Y, -10000), GameWorld.CGWorldFrameHitFlags.HitTestLiquid | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2, out watersurface);
            if (watersurface != WoWPoint.Empty)
            {
                return watersurface.Z;
            }
            return float.MinValue;
        }

        static public void blacklistLocation(WoWPoint p)
        {
            if (!PoolFisher.badLocations.Contains(p))
                PoolFisher.badLocations.Add(p);
            Logging.WriteNavigator("{0} - Navigation: Added {1} to badLocations List.", TimeNow, p);
            PoolFisher.looking4NewLoc = true;
        }

        /// <summary>
        /// Blacklists current pool and resets all variables.
        /// </summary>
        static public void BlackListPool(WoWObject o)
        {
            if (!Blacklist.Contains(o.Guid)) 
            {
                Blacklist.Add(o.Guid, new TimeSpan(0, 2, 0));
                //Logging.Write("{0} - Blacklisted Guid {1}", TimeNow, o.Guid);
                resetVars();
            }
        }

        static public void resetVars()
        {
            PoolFisher.looking4NewLoc = false;
            PoolFisher.looking4NewPool = true;
            PoolFisher.MeIsFishing = false;
            PoolFisher.need2Lure = false;
            PoolFisher.need2Train = false;
            PoolFisher.castAttempts = 0;
            PoolFisher.newLocAttempts = 0;
            PoolFisher.saveLocation.Clear();
            PoolFisher.mountLocation.Clear();
            PoolFisher.Pool = null;
            PoolFisher._currenthotspot = -1;
            Mailing.isDone = true;
            PoolFisher.need2Mail = false;
            PoolFisher.movetopoolTimer.Reset();
        }

        /// <summary>
        /// Checks if the bobber is in distance of 3.6 to location of the pool.
        /// </summary>
        static public bool BobberIsInTheHole
        {
            get
            {
                Thread.Sleep((PoolFisher.Ping * 2) + 300);
                if (FishingBobber != null && PoolFisher.Pool != null && FishingBobber != null)
                {
                    if (FishingBobber.Location.Distance2D(PoolFisher.Pool.Location) <= 3.6f)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Checks if the pool phased out or despawned.
        /// </summary>
        static public bool PoolIsStillThere
        {
            get
            {
                ObjectManager.Update();
                foreach (WoWObject o in ObjectManager.ObjectList)
                {
                    if (PoolFisher.Pool.Guid != 0 && o.Guid == PoolFisher.Pool.Guid && PoolFisher.Pool.IsValid)
                    {
                        return true;
                    }
                }
                BlackListPool(PoolFisher.Pool);
                
                return false;
            }
        }

        /// <summary>
        /// Blacklists pools from user settings.
        /// </summary>
        /// Cataclysm
        /// ----------------
        /// Albino Cavefish      - Entry: 202778
        /// Algaefin Rockfish    - Entry: 202781
        /// Blackbelly Mudfish   - Entry: 202779
        /// Fathom Eel           - Entry: 202780
        /// Highland Guppy       - Entry: 202777
        /// Mountain Trout       - Entry: 202776
        /// Pool of Fire         - Entry: 207734
        /// Shipwreck Debris     - Entry: 207724
        /// ----------------
        /// Wrath of the Lichking
        /// ----------------
        /// Borean Man O' War / Boreanische Galeeren     - Entry: 192051
        /// Deep Sea Monsterbelly / Tiefseemonsterbauch  - Entry: 192053
        /// Dragonfin Angelfish / Engelsdrachenfisch     - Entry: 192048
        /// Fangtooth Herring / Fangzahnhering           - Entry: 192049
        /// Glacial Salmon / Winterlachs                 - Entry: 192050
        /// Glassfin Minnow / Glasflossenelritze         - Entry: 192059
        /// Imperial Manta Ray / Imperialer Mantarochen  - Entry: 192052
        /// Moonglow Cuttlefish / Mondlichtsepia         - Entry: 192054
        /// Musselback Sculpin / Muschelrückengroppe     - Entry: 192046
        /// Nettlefish / Nesselfisch                     - Entry: 192057
        /// ----------------
        /// Burning Crusade
        /// ----------------
        /// Bluefish / Ein Schwarm Blauflossen              - Entry: 182959
        /// Brackish Mixed / Brackwasserschwarm             - Entry: 182954
        /// Highland Mixed / Hochlandschwarm                - Entry: 182957
        /// Mudfish / Ein Schwarm Matschflosser             - Entry: 182958
        /// Pure Water / Reines Wasser                      - Entry: 182951
        /// Darter / Ein Schwarm Stachelflosser             - Entry: 182956
        /// Sporefish / Ein Schwarm Sporenfische            - Entry: 182953
        /// Steam Pump Flotsam / Treibgut der Dampfpumpe    - Entry: 182952
        /// ----------------
        /// Old Azeroth
        /// ----------------
        /// Bloodsail Wreckage / Blutsegelwrackteile        - Entry: 180901
        /// Firefin Snapper / Feuerflossenschnapper         - Entry: 180657, 180683, 180752, 180902 
        /// Floating Debris / Treibgut                      - Entry: 
        /// Floating Wreckage / Treibende Wrackteile        - Entry: 
        /// Greater Sagefish / Großer Weisenfisch           - Entry: 180684
        /// Sagefish / Weisenfisch                          - Entry: 180663, 180656
        /// Oily Blackmouth / Öliges Schwarzmaul            - Entry: 180750, 180664, 180682, 180900
        /// Deviate Fish / Deviatfisch                      - Entry: 180658
        /// Speckled Tastyfish / Gesprenkelter Leckerfisch  - Entry: 180248
        /// Schooner Wreckage / Schiffswrackteile           - Entry: 
        /// Stonescale Eel / Steinschuppenaal               - Entry: 180712
        /// Waterlogged Wreckage / Schwimmende Wrackteile   - Entry: 180685
        /// Oil Spill / Ölpfütze                            - Entry: 180661
        /// Elemental Water / Elementarwasser               - Entry: 180753
        /// 

        public static void blacklistSchoolsFromSettings()
        {
            if (PoolFisherSettings.Instance.BlacklistSchools)
            {
                PoolFisher.PermaBlacklist.Clear();
                Logging.WriteDebug(System.Drawing.Color.Red, "Ignoring Schools from Settings:");
                Logging.WriteDebug(System.Drawing.Color.Red, "-------------------------------------------");
                /// Cataclysm
                if (PoolFisherSettings.Instance.BLAlbinoCavefish && !PoolFisher.PermaBlacklist.Contains(202778))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Albino Cavefish");
                    PoolFisher.PermaBlacklist.Add(202778);
                }
                if (PoolFisherSettings.Instance.BLAlgaefinRockfish && !PoolFisher.PermaBlacklist.Contains(202781))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Algaefin Rockfish");
                    PoolFisher.PermaBlacklist.Add(202781);
                }
                if (PoolFisherSettings.Instance.BLBlackbellyMudfish && !PoolFisher.PermaBlacklist.Contains(202779))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Blackbelly Mudfish");
                    PoolFisher.PermaBlacklist.Add(202779);
                }
                if (PoolFisherSettings.Instance.BLFathomEel && !PoolFisher.PermaBlacklist.Contains(202780))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Fathom Eel");
                    PoolFisher.PermaBlacklist.Add(202780);
                }

                if (PoolFisherSettings.Instance.BLHighlandGuppy && !PoolFisher.PermaBlacklist.Contains(202777))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Highland Guppy");
                    PoolFisher.PermaBlacklist.Add(202777);
                }
                if (PoolFisherSettings.Instance.BLMountainTrout && !PoolFisher.PermaBlacklist.Contains(202776))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Mountain Trout");
                    PoolFisher.PermaBlacklist.Add(202776);
                }
                if (PoolFisherSettings.Instance.BLPoolofFire && !PoolFisher.PermaBlacklist.Contains(207734))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Pool of Fire");
                    PoolFisher.PermaBlacklist.Add(207734);
                }
                if (PoolFisherSettings.Instance.BLShipwreckDebris && !PoolFisher.PermaBlacklist.Contains(207724))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Shipwreck Debris");
                    PoolFisher.PermaBlacklist.Add(207724);
                }
                /// Northrend
                if (PoolFisherSettings.Instance.BLBoreanManOWar && !PoolFisher.PermaBlacklist.Contains(192051))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Borean Man O' War");
                    PoolFisher.PermaBlacklist.Add(192051);
                }
                if (PoolFisherSettings.Instance.BLDeepSeaMonsterbelly && !PoolFisher.PermaBlacklist.Contains(192053))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Deep Sea Monsterbelly");
                    PoolFisher.PermaBlacklist.Add(192053);
                }
                if (PoolFisherSettings.Instance.BLDragonfinAngelfish && !PoolFisher.PermaBlacklist.Contains(192048))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Dragonfin Angelfish");
                    PoolFisher.PermaBlacklist.Add(192048);
                }
                if (PoolFisherSettings.Instance.BLFangtoothHerring && !PoolFisher.PermaBlacklist.Contains(192049))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Fangtooth Herring");
                    PoolFisher.PermaBlacklist.Add(192049);
                }
                if (PoolFisherSettings.Instance.BLGlacialSalmon && !PoolFisher.PermaBlacklist.Contains(192050))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Glacial Salmon");
                    PoolFisher.PermaBlacklist.Add(192050);
                }
                if (PoolFisherSettings.Instance.BLGlassfinMinnow && !PoolFisher.PermaBlacklist.Contains(192059))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Glassfin Minnow");
                    PoolFisher.PermaBlacklist.Add(192059);
                }
                if (PoolFisherSettings.Instance.BLImperialMantaRay && !PoolFisher.PermaBlacklist.Contains(192052))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Imperial Manta Ray");
                    PoolFisher.PermaBlacklist.Add(192052);
                }
                if (PoolFisherSettings.Instance.BLMoonglowCuttlefish && !PoolFisher.PermaBlacklist.Contains(192054))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Moonglow Cuttlefish");
                    PoolFisher.PermaBlacklist.Add(192054);
                }
                if (PoolFisherSettings.Instance.BLMusselbackSculpin && !PoolFisher.PermaBlacklist.Contains(192046))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Musselback Sculpin");
                    PoolFisher.PermaBlacklist.Add(192046);
                }
                if (PoolFisherSettings.Instance.BLNettlefish && !PoolFisher.PermaBlacklist.Contains(192057))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Nettlefish");
                    PoolFisher.PermaBlacklist.Add(192057);
                }
                /// Outlands
                if (PoolFisherSettings.Instance.BLBluefish && !PoolFisher.PermaBlacklist.Contains(182959))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Bluefish");
                    PoolFisher.PermaBlacklist.Add(182959);
                }
                if (PoolFisherSettings.Instance.BLBrackishMix && !PoolFisher.PermaBlacklist.Contains(182954))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Brackish Mixed");
                    PoolFisher.PermaBlacklist.Add(182954);
                }
                if (PoolFisherSettings.Instance.BLHighlandMix && !PoolFisher.PermaBlacklist.Contains(182957))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Highland Mixed");
                    PoolFisher.PermaBlacklist.Add(182957);
                }
                if (PoolFisherSettings.Instance.BLMudfish && !PoolFisher.PermaBlacklist.Contains(182958))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Mudfish");
                    PoolFisher.PermaBlacklist.Add(182958);
                }
                if (PoolFisherSettings.Instance.BLPureWater && !PoolFisher.PermaBlacklist.Contains(182951))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Pure Water");
                    PoolFisher.PermaBlacklist.Add(182951);
                }
                if (PoolFisherSettings.Instance.BLDarter && !PoolFisher.PermaBlacklist.Contains(182956))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Darter");
                    PoolFisher.PermaBlacklist.Add(182956);
                }
                if (PoolFisherSettings.Instance.BLSporefish && !PoolFisher.PermaBlacklist.Contains(182953))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Sporefish");
                    PoolFisher.PermaBlacklist.Add(182953);
                }
                if (PoolFisherSettings.Instance.BLSteamPumpFlotsam && !PoolFisher.PermaBlacklist.Contains(182952))
                {
                    Logging.WriteDebug(System.Drawing.Color.Red, "Steam Pump Flotsam");
                    PoolFisher.PermaBlacklist.Add(182952);
                }
                Logging.WriteDebug(System.Drawing.Color.Red, "-------------------------------------------");
            }
        }

        /// <summary>
        /// Scans for other player with a distance of 30 from pool location.
        /// </summary>
        static public bool PlayerDetected
        {
            get
            {
                ObjectManager.Update();
                List<WoWPlayer> players = new List<WoWPlayer>(ObjectManager.GetObjectsOfType<WoWPlayer>(false));
                foreach (WoWUnit unit in players)
                {
                    if (PoolFisher.Pool.Location.Distance2D(unit.Location) < 25 && !unit.Mounted && !unit.IsFlying)
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Player detected!", TimeNow);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Credits to HighVoltz 
        /// </summary>
        public static bool hasWaterWalking
        {
            get
            { // DKs have 2 Path of Frost auras. only one can be stored in WoWAuras at any time. 
                if (StyxWoW.Me.Auras.Values.Count(a => (a.SpellId == 11319 || a.SpellId == 1706 || a.SpellId == 546) &&
                    a.TimeLeft >= new System.TimeSpan(0, 0, 20)) > 0 || StyxWoW.Me.HasAura("Path of Frost"))
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Credits to HighVoltz 
        /// </summary>
        ///
        
        public static bool CanWaterWalk
        {
            get
            {
                if (SpellManager.CanCast(1706) ||// priest levitate
                    SpellManager.CanCast(546) || // shaman water walking
                    SpellManager.CanCast(3714) ||// Dk Path of frost
                    (PoolFisherSettings.Instance.useWaterWalkingPot && IsItemInBag(8827)))//isItemInBag(8827);
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        

        /// <summary>
        /// Credits to HighVoltz 
        /// </summary>
        public static void WaterWalk()
        {
            if (!hasWaterWalking && PoolFisherSettings.Instance.useWaterWalking)
            {
                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Applying water walking aura.", TimeNow);

                if (!PoolFisherSettings.Instance.useWaterWalkingPot)
                {
                    int waterwalkingSpellID = 0;
                    switch (StyxWoW.Me.Class)
                    {
                        case Styx.Combat.CombatRoutine.WoWClass.Priest:
                            waterwalkingSpellID = 1706;
                            break;
                        case Styx.Combat.CombatRoutine.WoWClass.Shaman:
                            waterwalkingSpellID = 546;
                            break;
                        case Styx.Combat.CombatRoutine.WoWClass.DeathKnight:
                            waterwalkingSpellID = 3714;
                            break;
                    }
                    if (SpellManager.CanCast(waterwalkingSpellID))
                    {
                        SpellManager.Cast(waterwalkingSpellID);
                    }
                }
                else if (PoolFisherSettings.Instance.useWaterWalkingPot)
                {
                    WoWItem waterPot = getIteminBag(8827);
                    if (waterPot != null)
                    {
                        waterPot.Use();
                    }
                    else
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Could not find water walking pot, won't attempt to use anymore!", TimeNow);
                        PoolFisherSettings.Instance.Load();
                        PoolFisherSettings.Instance.useWaterWalking = false;
                        PoolFisherSettings.Instance.useWaterWalkingPot = false;
                        PoolFisherSettings.Instance.Save();

                        PoolFisher.saveLocation.Clear();
                        PoolFisher.looking4NewLoc = true;
                        PoolFisher.castAttempts = 0;
                        PoolFisher.newLocAttempts = 0;
                    }
                }
                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Finished!", TimeNow);
            }
        }

        /// <summary>
        /// List of spell Ids for fishing for isFishing.
        /// </summary>
        static readonly List<int> FishingIds = new List<int> { 7620, 7731, 7732, 18248, 33095, 51294, 88868 };

        /// <summary>
        /// Returns true if you are fishing
        /// </summary>
        public static bool IsFishing { get { return FishingIds.Contains(ObjectManager.Me.ChanneledCastingSpellId); } }

        /// <summary>
        /// Returns true if the fishing bobber is bobbing
        /// </summary>
        public static bool BobberIsBobbing { get { return FishingBobber != null && FishingBobber.IsBobbing(); } }

        /// <summary>
        /// Returns the current fishing bobber in use, null otherwise
        /// </summary>
        public static WoWGameObject FishingBobber
        {
            get
            {

                ObjectManager.Update();
                return (ObjectManager.GetObjectsOfType<WoWGameObject>().Where(
                    obj =>
                    obj.SubType == WoWGameObjectType.FishingBobber
                    && obj.CreatedByGuid == ObjectManager.Me.Guid).FirstOrDefault());
            
            }
        }
    }

    internal static class Extensions
    {
        public static bool IsBobbing(this WoWGameObject value)
        {
            if (value == null || value.SubType != WoWGameObjectType.FishingBobber)
                return false;

            return ((WoWFishingBobber)value.SubObj).IsBobbing;
        }
    }
}
