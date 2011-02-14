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
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.LootFrame;

using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

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
            FormFishMonitoring form = new FormFishMonitoring();
            form.ShowDialog();
        }

        /// <summary>
        /// Does things on init.
        /// </summary>
        static public void Init(System.EventArgs args)
        {
            if (!StyxWoW.IsLifetimeUser)
            {
                Logging.Write(System.Drawing.Color.Red, "{0} - You don't have lifetime subscription. Stopping..", Helpers.TimeNow);
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

            if (TreeRoot.IsRunning)
            {
                Logging.Write(System.Drawing.Color.Blue, "{0} - Looking for weapons and fishing pole and add them to protected items.", Helpers.TimeNow);

                PoolFisher.BagItems = StyxWoW.Me.BagItems;

                bool hasPole = false;
                bool hasMainhand = false;

                foreach (WoWItem i in PoolFisher.BagItems)
                {
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
                    
                    /*
                    if (i.Entry == PoolFisherSettings.Instance.Offhand && PoolFisherSettings.Instance.Offhand != 0 && TreeRoot.IsRunning)
                    {
                        if (!ProtectedItemsManager.Contains((uint)PoolFisherSettings.Instance.FishingPole)) ProtectedItemsManager.Add((uint)PoolFisherSettings.Instance.FishingPole);
                        Logging.Write(System.Drawing.Color.Green, "{0} is valid.", i.Name);
                    }
                    */
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
        }

        /// <summary>
        /// Does things on final.
        /// </summary>
        static public void Final(System.EventArgs args)
        {
            //Lua.DoString("run ConsoleExec('Autointeract 0')"); // This is also used by FishingBuddy/FishingBuddy.lua (Fishing Buddy Addon)
            //PoolFisher.GetValuesThread.Abort();
            //PoolFisher.MonitoringThread.Abort();
            //Logging.Write("{0} - Pool Fischer Stopped!", TimeNow);
        }

        /// <summary>
        /// Not in use.
        /// </summary>
        static public void findAndProtectWeapons()
        {
            TreeRoot.StatusText = "Protecting equiped Weapons";
            List<WoWItem> _items = StyxWoW.Me.BagItems;
            List<WoWItem> mainhandList = new List<WoWItem>();
            List<WoWItem> offhandList = new List<WoWItem>();
            int mainHand, offHand;
            foreach (WoWItem i in _items)
            {
                if (i.ItemInfo.IsWeapon && (i.ItemInfo.InventoryType == InventoryType.WeaponMainHand ||
                    i.ItemInfo.InventoryType == InventoryType.TwoHandWeapon) && StyxWoW.Me.CanEquipItem(i))
                {
                    if (!mainhandList.Contains(i)) mainhandList.Add(i);
                }
                if (i.ItemInfo.IsWeapon && (i.ItemInfo.InventoryType == InventoryType.WeaponOffHand ||
                    i.ItemInfo.InventoryType == InventoryType.Weapon) && StyxWoW.Me.CanEquipItem(i))
                {
                    if (!offhandList.Contains(i)) offhandList.Add(i);
                }
            }
            if (mainhandList.Count > 0)
            {
                mainhandList.Sort((i1, i2) => i2.ItemInfo.Level.CompareTo(i1.ItemInfo.Level));
                mainHand = (int)mainhandList[0].Entry;
                if (!ProtectedItemsManager.Contains((uint)mainHand)) ProtectedItemsManager.Add((uint)mainHand);
                PoolFisherSettings.Instance.Mainhand = mainHand;
            }
            if (offhandList.Count > 0)
            {
                offhandList.Sort((i1, i2) => i2.ItemInfo.Level.CompareTo(i1.ItemInfo.Level));
                offHand = (int)offhandList[0].Entry;
                if (!ProtectedItemsManager.Contains((uint)offHand)) ProtectedItemsManager.Add((uint)offHand);
                PoolFisherSettings.Instance.Offhand = offHand;
            }
            PoolFisherSettings.Instance.Save();
        }

        /// <summary>
        /// Not in use.
        /// </summary>
        static public bool equipPole
        {
            get
            {
                List<WoWItem> _items = StyxWoW.Me.BagItems;
                int mainHand, offHand;

                if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
                {
                    return true;
                }
                else if (StyxWoW.Me.Inventory.Equipped.MainHand == null && PoolFisherSettings.Instance.Mainhand == 0)
                {
                    findAndProtectWeapons();
                }
                mainHand = (int)(StyxWoW.Me.Inventory.Equipped.MainHand != null ? StyxWoW.Me.Inventory.Equipped.MainHand.Entry : 0);
                offHand = (int)(StyxWoW.Me.Inventory.Equipped.OffHand != null ? StyxWoW.Me.Inventory.Equipped.OffHand.Entry : 0);
                if (mainHand != 0)
                {
                    if (!ProtectedItemsManager.Contains((uint)mainHand)) ProtectedItemsManager.Add((uint)mainHand);
                    PoolFisherSettings.Instance.Mainhand = mainHand;
                }
                if (offHand != 0)
                {
                    if (!ProtectedItemsManager.Contains((uint)offHand)) ProtectedItemsManager.Add((uint)offHand);
                    PoolFisherSettings.Instance.Offhand = offHand;
                }
                PoolFisherSettings.Instance.Save();

                foreach (WoWItem i in _items)
                {
                    if ((i.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole) && StyxWoW.Me.CanEquipItem(i))
                    {
                        Logging.Write(System.Drawing.Color.Blue, "{0} - Equipping: {1}.", TimeNow, i.Name);
                        Lua.DoString("EquipItemByName (\"" + i.Name + "\")", "fishingbuddy.lua");
                        Thread.Sleep(1000);
                        return true;
                    }
                }
                return false;
            }
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
                    Thread.Sleep((PoolFisher.Ping * 2) + 500);
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

        static public bool IsLureOnPole { get { return Lua.GetReturnVal<bool>("return GetWeaponEnchantInfo()", 0); } }

        static public void applylure()
        {
            //TreeRoot.StatusText = "Luring";
            //if (StyxWoW.Me.Inventory.Equipped.MainHand != null && StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole || StyxWoW.Me.Mounted)

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
                        while ((!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat) && StyxWoW.Me.IsCasting)
                            Thread.Sleep(100);
                        Thread.Sleep((PoolFisher.Ping * 4) + 50);
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
                WoWItem _lureInBag = GetIteminBag((uint)PoolFisherSettings.Instance.LureID);
                if (_lureInBag != null && _lureInBag.Use())
                {
                    TreeRoot.StatusText = "Luring";
                    Logging.Write(System.Drawing.Color.Blue, "{0} - Appling lure to fishing pole", TimeNow);
                    Thread.Sleep(PoolFisher.Ping * 2 + 50);
                    while ((!StyxWoW.Me.Combat || !StyxWoW.Me.PetInCombat) && StyxWoW.Me.IsCasting)
                        Thread.Sleep(100);
                    Thread.Sleep(PoolFisher.Ping * 2 + 50);
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

        static public WoWItem GetIteminBag(uint entry)
        {
            return StyxWoW.Me.BagItems.Where(i => i.Entry == entry).FirstOrDefault();
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
                    Logging.Write("{0} - Looking for pools..", TimeNow);

                    ObjectManager.Update();
                    List<WoWGameObject> poolList = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.FishingHole && !Blacklist.Contains(o.Guid) && !PoolFisher.PermaBlacklist.Contains(o.Entry) && o.Distance2D <= 150 && o.Location.X != 0).OrderBy(o => o.Distance).ToList();

                    //Logging.Write("poolList.Count: {0}", poolList.Count);

                    if (poolList.Count > 0)
                    {
                        foreach (WoWGameObject p in poolList)
                            Logging.Write("{0} - Found - {1} - at a distance of {2}. Guid: {3}. Entry: {4}.", TimeNow, p.Name, p.Distance, p.Guid, p.Entry);

                        if (PoolFisher.BlackspotList.Count != 0)
                        {
                            foreach (WoWPoint Blackspot in PoolFisher.BlackspotList)
                            {
                                if (poolList[0].Location.Distance2D(Blackspot) < 20)
                                {
                                    Logging.Write(System.Drawing.Color.Red, "{0} - Pool is in range of {1} (Blackspot)! Skipping.", TimeNow, Blackspot);
                                    PoolFisher.looking4NewPool = true;
                                    return false;
                                }
                            }
                        }
                        PoolFisher.Pool = poolList[0];
                        PoolFisher.looking4NewPool = false;
                        PoolFisher.looking4NewPoint = true;
                        return true;
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
        
        /// <summary>
        /// Credits to
        /// </summary>
        /// <returns>Array List of WoWPoints around the pool we might try to fish from.</returns>
        static public bool findPoolPoint()
        {

            Logging.Write("{0} - Looking for pool point. Pool location: {1}.", TimeNow, PoolFisher.Pool.Location);
            int traceStep = 15;
            int minRange = 10;
            int maxRange = PoolFisherSettings.Instance.MaxCastRange + 1;
            int maxAttempts = PoolFisherSettings.Instance.MaxNewLocAttempts;
            float _PIx2 = 3.14159f * 2f;

            WoWPoint p = new WoWPoint();
            WoWPoint hPoint = new WoWPoint();
            WoWPoint lPoint = new WoWPoint();

            PoolFisher.PoolPoints.Clear();
            PoolFisher.tempPoolPoints.Clear();

            for (int i = 0, x = minRange; i < traceStep && x < maxRange; i++)
            {
                p = PoolFisher.Pool.Location.RayCast((i * _PIx2) / traceStep, x);

                lPoint = p;
                lPoint.Z -= 0.4f;
                hPoint = p;
                hPoint.Z += 15f;
                p.Z = NormalizeGroundZ(p);

                //Logging.Write("{0} - lPoint.Z: {1}, hPoint.Z: {2}, p.Z: {3}.", TimeNow, lPoint.Z, hPoint.Z, p.Z);

                if (p.Z > lPoint.Z && p.Z < hPoint.Z && PoolFisher.Pool.Location.Distance(p) <= maxRange && GameWorld.IsInLineOfSight(p, PoolFisher.Pool.Location))
                {
                    PoolFisher.tempPoolPoints.Add(p);
                    //Logging.Write("{0} - Added {1} to tempPoolPoints. Distance from pool: {2}", TimeNow, p, PoolFisher.Pool.Location.Distance(p));
                }

                if (i == (traceStep - 1))
                {
                    i = 0;
                    x++;
                }
            }


            Logging.Write("{0} - We got {1} tempPoolPoints for this pool.", TimeNow, PoolFisher.tempPoolPoints.Count);

            Stopwatch test = new Stopwatch();
            test.Start();

            if (PoolFisher.tempPoolPoints.Count > 0)
            {
                foreach (WoWPoint point in PoolFisher.tempPoolPoints)
                {
                    ThreadStart GetSlopesThread = delegate { sloapCheck(point); };
                    PoolFisher.GetSlopesThread = new Thread(GetSlopesThread);
                    PoolFisher.GetSlopesThread.Start();
                }
            }
            else
            {
                Logging.Write("{0} - No suitable point found für {1} , blacklisting for 2 minutes.", TimeNow, PoolFisher.Pool.Name);
                BlackListPool();
                return false;
            }

            while (PoolFisher.GetSlopesThread.IsAlive)
            {
                Logging.Write("{0} - Sleeping while sloapCheck is running.", TimeNow, PoolFisher.PoolPoints.Count);
                Thread.Sleep(100);
            }

            Logging.Write("{0} - Elapsed time (ms): {1}", TimeNow, test.ElapsedMilliseconds);
            test.Stop();

            Logging.Write("{0} - We got {1} suitable points for this pool.", TimeNow, PoolFisher.PoolPoints.Count);

            PoolFisher.PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z));

            if (PoolFisherSettings.Instance.DescendHigher)
            {
                // Let's try the higher Z-Coords first! No more swimming ftw..
                PoolFisher.PoolPoints.Reverse();
            }

            if (StyxWoW.Me.IsFlying && PoolFisher.PoolPoints.Count > 0)
            {
                Logging.Write("{0} - PoolPoint: {1}. Count total: {2}", TimeNow, PoolFisher.PoolPoints[0], PoolFisher.PoolPoints.Count);
                PoolFisher.looking4NewPoint = false;
                return true;
            }

            for (int i = 0; i < PoolFisher.PoolPoints.Count; )
            {
                //Logging.Write("{0} - Looking for nearest point...", TimeNow);
                WoWPoint[] testP = Navigator.GeneratePath(StyxWoW.Me.Location, PoolFisher.PoolPoints[i]);
                if (testP.Length > 0)
                {
                    PoolFisher.looking4NewPoint = false;
                    return true;
                }
                else
                {
                    PoolFisher.PoolPoints.Remove(PoolFisher.PoolPoints[i]);
                    PoolFisher.PoolPoints.Sort((a, b) => a.Distance(StyxWoW.Me.Location).CompareTo(b.Distance(StyxWoW.Me.Location)));
                }
            }
            Logging.Write("{0} - No suitable point found für {1} , blacklisting for 2 minutes.", TimeNow, PoolFisher.Pool.Name);
            BlackListPool();
            return false;
        }

        /// <summary>
        /// Checks for sloaps on multiple WoWPoints by multithreading and adds them to PoolPoints.
        /// </summary>
        /// <param name="p">WoWPoint</param>
        public static void sloapCheck(WoWPoint p)
        {
            //Logging.Write("{0} - New Thread.", TimeNow);
            if (GetHighestSurroundingSlope(p) < 1.2f)
                PoolFisher.PoolPoints.Add(p);
        }

        /// <summary>
        /// Credits to exemplar.
        /// </summary>
        /// <returns>Z-Coordinates for PoolPoints so we don't jump into the water.</returns>
        public static float NormalizeGroundZ(WoWPoint p)
        {
            try
            {
                return Navigator.FindHeights(p.X, p.Y).Max();
            }
            catch (Exception) { }

            WoWPoint ground = WoWPoint.Empty;

            GameWorld.TraceLine(new WoWPoint(p.X, p.Y, 10000), new WoWPoint(p.X, p.Y, -10000), GameWorld.CGWorldFrameHitFlags.HitTestGroundAndStructures | GameWorld.CGWorldFrameHitFlags.HitTestBoundingModels | GameWorld.CGWorldFrameHitFlags.HitTestWMO, out ground);
            if (ground != WoWPoint.Empty)
            {
                return ground.Z;
            }
            return float.MinValue;
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <returns>Highest slope of surrounding terrain, returns 100 if the slope can't be determined</returns>
        public static float GetHighestSurroundingSlope(WoWPoint p)
        {
            float _PIx2 = 3.14159f * 2f;
            float highestSlope = -100;
            float slope = 0;
            int traceStep = 6;
            float range = 1.2f;
            WoWPoint p2;
            for (int i = 0; i < traceStep; i++)
            {
                // scans 1 yard from p for cliffs at every 18 degress 
                p2 = p.RayCast((i * _PIx2) / traceStep, range);
                p2.Z = NormalizeGroundZ(p2);
                slope = Math.Abs( GetSlope(p, p2) );
                if( slope > highestSlope )
                {
                    highestSlope = slope;
                }
            }
            //Logging.Write("Point: {0}, Distance: {1}, Sloap: {2}", p, PoolFisher.Pool.Location.Distance(p), highestSlope);
            //Logging.Write(" Highslope {0}", highestSlope);
            return Math.Abs( highestSlope );
        }

        /// <summary>
        /// Credits to funkescott.
        /// </summary>
        /// <param name="p1">from WoWPoint</param>
        /// <param name="p2">to WoWPoint</param>
        /// <returns>Return slope from WoWPoint to WoWPoint.</returns>
        public static float GetSlope(WoWPoint p1, WoWPoint p2)
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
        public static float GetWaterSurface(WoWPoint p)
        {
            try
            {
                return Navigator.FindHeights(p.X, p.Y).Max();
            }
            catch (Exception) { }
            WoWPoint watersurface = WoWPoint.Empty;
            GameWorld.TraceLine(new WoWPoint(p.X, p.Y, 10000), new WoWPoint(p.X, p.Y, -10000), GameWorld.CGWorldFrameHitFlags.HitTestLiquid | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2, out watersurface);
            if (watersurface != WoWPoint.Empty)
            {
                return watersurface.Z;
            }
            return float.MinValue;
        }

        /// <summary>
        /// Blacklists current pool and resets all variables.
        /// </summary>
        static public void BlackListPool()
        {
            if (!Blacklist.Contains(PoolFisher.Pool.Guid)) Blacklist.Add(PoolFisher.Pool.Guid, new TimeSpan(0, 2, 0));
            {
                //Logging.Write("{0} - Blacklisted Guid {1}", TimeNow, PoolFisher.Pool.Guid);
                equipWeapon();
                PoolFisher.newLocAttempts = 0;
                PoolFisher.MeIsFishing = false;
                PoolFisher.looking4NewPool = true;
                PoolFisher.looking4NewPoint = true;
                PoolFisher.PoolPoints.Clear();
            }
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
                        return true;
                }
                BlackListPool();
                PoolFisher.MeIsFishing = false;
                PoolFisher.looking4NewPool = true;
                PoolFisher.looking4NewPoint = true;
                //PoolFisher.PoolPoints.Clear();
                return false;
            }
        }

        /// <summary>
        /// Blacklists cataclysm pools from user settings.
        /// </summary>
        /// Albino Cavefish School      - 202778
        /// Algaefin Rockfish School    - 202781
        /// Blackbelly Mudfish School   - 202779
        /// Fathom Eel School           - 202780
        /// Highland Guppy School       - 202777
        /// Mountain Trout School       - 202776
        /// Pool of Fire                - 207734
        /// Shipwreck Debris            - 207724
        public static void blacklistSchoolsFromSettings()
        {
            if (PoolFisherSettings.Instance.BlacklistSchools)
            {
                Logging.Write(System.Drawing.Color.Red, "Ignoring Schools from Settings:");
                Logging.Write(System.Drawing.Color.Red, "-------------------------------------------");
                if (PoolFisherSettings.Instance.BLAlbinoCavefish && !Styx.Logic.Blacklist.Contains(202778))
                {
                    Logging.Write(System.Drawing.Color.Red, "Albino Cavefish");
                    PoolFisher.PermaBlacklist.Add(202778);
                }
                if (PoolFisherSettings.Instance.BLAlgaefinRockfish && !Styx.Logic.Blacklist.Contains(202781))
                {
                    Logging.Write(System.Drawing.Color.Red, "Algaefin Rockfish");
                    PoolFisher.PermaBlacklist.Add(202781);
                }
                if (PoolFisherSettings.Instance.BLBlackbellyMudfish && !Styx.Logic.Blacklist.Contains(202779))
                {
                    Logging.Write(System.Drawing.Color.Red, "Blackbelly Mudfish");
                    PoolFisher.PermaBlacklist.Add(202779);
                }
                if (PoolFisherSettings.Instance.BLFathomEel && !Styx.Logic.Blacklist.Contains(202780))
                {
                    Logging.Write(System.Drawing.Color.Red, "Fathom Eel");
                    PoolFisher.PermaBlacklist.Add(202780);
                }

                if (PoolFisherSettings.Instance.BLHighlandGuppy && !Styx.Logic.Blacklist.Contains(202777))
                {
                    Logging.Write(System.Drawing.Color.Red, "Highland Guppy");
                    PoolFisher.PermaBlacklist.Add(202777);
                }
                if (PoolFisherSettings.Instance.BLMountainTrout && !Styx.Logic.Blacklist.Contains(202776))
                {
                    Logging.Write(System.Drawing.Color.Red, "Mountain Trout");
                    PoolFisher.PermaBlacklist.Add(202776);
                }
                if (PoolFisherSettings.Instance.BLPoolofFire && !Styx.Logic.Blacklist.Contains(207734))
                {
                    Logging.Write(System.Drawing.Color.Red, "Pool of Fire");
                    PoolFisher.PermaBlacklist.Add(207734);
                }
                if (PoolFisherSettings.Instance.BLShipwreckDebris && !Styx.Logic.Blacklist.Contains(207724))
                {
                    Logging.Write(System.Drawing.Color.Red, "Shipwreck Debris");
                    PoolFisher.PermaBlacklist.Add(207724);
                }
                Logging.Write(System.Drawing.Color.Red, "-------------------------------------------");
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
                    if (PoolFisher.Pool.Location.Distance(unit.Location) < 30)
                    {
                        Logging.Write(System.Drawing.Color.Red, "{0} - Player detected!", TimeNow);
                        return true;
                    }
                }
                return false;
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

        #region Test

        /*
        public static int LootItems
        {
            get
            {
                try
                {
                    return int.Parse(Lua.GetReturnValues("return GetNumLootItems()", "fishingbuddy.lua")[0]);
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static void Loot(int slot)
        {
            Lua.DoString("LootSlot({0})", slot + 1);
        }
        
        public static void LootAll()
        {
            for (int i = 0; i < LootItems; i++)
            {
                var info = new LootSlotInfo(i);

                if (!info.Locked)
                {
                    Logging.Write("Trying to loot #{0} of {1} Rarity:{2}",
                        info.LootQuantity,
                        info.LootName,
                        info.LootRarity);

                    Loot(i);
                }
            }

            //InfoPanel.LootedMob();
        }
        */

        #endregion
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
