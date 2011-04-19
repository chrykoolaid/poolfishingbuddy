using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Bots.Grind;
//using Gatherbuddy;
using Levelbot;
using CommonBehaviors.Actions;
using PoolFishingBuddy.Forms;
using Styx;
using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.AreaManagement;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.Inventory.Frames.LootFrame;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals.World;
using TreeSharp;
using Action = TreeSharp.Action;

namespace PoolFishingBuddy
{
    public class PoolFisher : BotBase
    {
        #region variables

        private Composite _root;
        static public int _currenthotspot;
        static public WoWPoint _modHotspot;
        
        static public bool looking4NewLoc;
        static public bool looking4NewPool;
        static public bool need2Lure;
        static public bool MeIsFishing;

        static public bool bounceBack = false;

        static public Thread MonitoringThread;
        static public Thread GetValuesThread;

        static public Thread TempThread1;
        static public Thread TempThread2;

        static public int Ping;
        static public int newLocAttempts = 0;
        static public int castAttempts = 0;
        static public int catches = 0;

        static public Stopwatch runTimer = new Stopwatch();
        static public Stopwatch movetopoolTimer = new Stopwatch();

        static public volatile List<ulong> PermaBlacklist = new List<ulong>();
        static public WoWGameObject Pool = null;
        static public WoWFishingBobber Bobber = null;

        static public List<WoWPoint> saveLocation = new List<WoWPoint>(100);
        static public List<WoWPoint> badLocations = new List<WoWPoint>(100);
        static public List<WoWPoint> mountLocation = new List<WoWPoint>(100);

        static public WoWPoint WaterSurface;

        static public List<WoWItem> mainhandList = new List<WoWItem>();
        static public List<WoWItem> offhandList = new List<WoWItem>();
        static public List<WoWItem> poleList = new List<WoWItem>();
        static public List<WoWItem> BagItems = new List<WoWItem>();
        static public WoWItem Heartstone = Helpers.getIteminBag(6948);
        static public WoWObject DalaraTeleport;

        static public bool need2Train = false;
        static public bool back2Path = false;
        static public WoWUnit Trainer;
        static public uint TrainerID;
        static public WoWPoint TrainerDestination;
        static public WoWPoint TrainerLocation;
        static public bool FlyToTrainer = true;

        static public bool need2Mail = false;
        
        #endregion

        static public List<WoWPoint> HotspotList;
        static public List<WoWPoint> BlackspotList;

        static public GrindArea GrindArea { get; set; }
        

        #region Overrides of BotBase

        private readonly Version _version = new Version(1, 1, 01);

        public override string Name
        {
            get { return "PoolFisher " + _version; }
        }

        public override PulseFlags PulseFlags { get { return PulseFlags.All; } }

        public override Form ConfigurationForm { get { return new FormFishConfig(); } }

        public override void Start()
        {
            Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Pool Fisher {1} starting!", Helpers.TimeNow, _version);

            
            bool autoLootDefault = Lua.GetReturnVal<bool>("GetCVar(\"autoLootDefault\")", 0);

            

            if (autoLootDefault)
            {
                Lua.DoString("SetCVar(\"autoLootDefault\",0)", "fishingbuddy.lua");
            }
            
            GrindArea = ProfileManager.CurrentProfile.GrindArea;
            HotspotList = GrindArea.Hotspots.ConvertAll<WoWPoint>(hs => hs.ToWoWPoint());
            BlackspotList = ProfileManager.CurrentProfile.Blackspots.ConvertAll<WoWPoint>(bs => bs.Location);

            ProtectedItemsManager.ReloadProtectedItems();
            ForceMailManager.ReloadProtectedItems();

            Looting.Cache.Clear();
            Helpers.resetVars();

            WoWSpell Mount = WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID);

            LevelbotSettings.Instance.MountName = Mount.Name;
            CharacterSettings.Instance.FindMountAutomatically = false;
            CharacterSettings.Instance.UseRandomMount = false;

            Styx.BotEvents.OnBotStart += Helpers.Init;
            Styx.BotEvents.OnBotStop += Helpers.Final;

            if (TreeRoot.IsRunning)
                Helpers.Init(new System.EventArgs());

            StyxSettings.Instance.LogoutForInactivity = false;
        }

        public override void Stop()
        {
            if (StyxWoW.Me.IsCasting)
            {
                SpellManager.StopCasting();
                while (StyxWoW.Me.IsCasting)
                    Thread.Sleep((Ping * 2) + 50);
            }

            //WoWMovement.MoveStop();

            if (StyxWoW.Me.Inventory.Equipped.MainHand.IsValid && StyxWoW.Me.Inventory.Equipped.MainHand.Entry == PoolFisherSettings.Instance.FishingPole)
                Helpers.equipWeapon();

            if (runTimer.Elapsed.TotalMinutes >= 1)
            {
                double average = catches / (runTimer.Elapsed.TotalMinutes / 60);
                Logging.Write(System.Drawing.Color.DarkCyan, "Average catches/h: {0}", (int)average);
            }

            if (Looting.Cache.Count > 0)
            {
                Logging.Write(System.Drawing.Color.DarkCyan, "---------------- Loots: ----------------");
                foreach (KeyValuePair<string, int> pair in Looting.Cache)
                {
                    Logging.Write(System.Drawing.Color.Black, "{0}x [{1}]", pair.Value, pair.Key);
                }
                Logging.Write(System.Drawing.Color.DarkCyan, "---------------------------------------------");
            }
            else
            {
                Logging.Write(System.Drawing.Color.DarkCyan, "No catches done.");
            }

            int seconds = (int)PoolFisher.runTimer.ElapsedMilliseconds / 1000;
            int minutes = seconds / 60;
            int hours = minutes / 60;
            

            Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Runtime: {1}h {2}m {3}s.", Helpers.TimeNow, runTimer.Elapsed.Hours, runTimer.Elapsed.Minutes, runTimer.Elapsed.Seconds);
            Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Pool Fisher stopped!", Helpers.TimeNow);
            StyxSettings.Instance.LogoutForInactivity = true;
            PoolFisher.runTimer.Reset();

            if (!TreeRoot.IsRunning)
                Helpers.Final(new System.EventArgs());
        }

        public override Composite Root
        {
            get
            {
                return _root ?? (_root = 
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me == null || !StyxWoW.IsInGame || !StyxWoW.IsInWorld,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - [INVALID] Waiting..", Helpers.TimeNow)),
                                new ActionSleep(10000))),

                        new Decorator(ret => StyxWoW.Me.OnTaxi || StyxWoW.Me.IsOnTransport,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - [On Taxi] Waiting..", Helpers.TimeNow)),
                                new ActionSleep(10000))),

                        new Decorator(ret => StyxWoW.Me.IsFalling,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - [Falling] Waiting..", Helpers.TimeNow)),
                                new ActionSleep(500))),

                        new Decorator(ret => StyxWoW.Me.Dead || StyxWoW.Me.IsGhost,
                            LevelBot.CreateDeathBehavior()),

                        new Decorator(ret => !StyxWoW.Me.Mounted && StyxWoW.Me.Combat,
                            new Sequence(
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => castAttempts = 0),
                                new Action(ret => Helpers.equipWeapon()),
                                LevelBot.CreateCombatBehavior())),

                        new Decorator(ret => StyxWoW.Me.IsSwimming,
                            CreateSwimmingBehavior()
                            ),

                        new Decorator(ret => PoolFisherSettings.Instance.TrainingEnabled,
                            new PrioritySelector(
                                
                                CreateBackToPathBehavior(),

                                new Decorator(ret => Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)), 
                                    CreateTrainBehavior())
                                    
                                    )),

                        // ToDo: own need to rest check on top of this..
                        CreateFishingBehavior(),

                        CreateRestBehavior(),


                        #region Buffs
                        new Decorator(ret => !StyxWoW.Me.IsFlying,
                            new PrioritySelector(
                                // Use the bt
                                new Decorator(ctx => RoutineManager.Current.PreCombatBuffBehavior != null,
                                    RoutineManager.Current.PreCombatBuffBehavior),
                                // don't use the bt
                                new Decorator(
                                    ctx => RoutineManager.Current.NeedPreCombatBuffs,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Applying pre-combat buffs"),
                                        new Action(ret => RoutineManager.Current.PreCombatBuff())
                                        )))),

                        #endregion


                        new Decorator(ret => (StyxWoW.Me.FreeBagSlots <= ProfileManager.CurrentProfile.MinFreeBagSlots && PoolFisherSettings.Instance.ShouldMail) || need2Mail == true,
                            new Sequence(
                                new Action(ret => Helpers.resetVars()),
                                new Action(ret => Helpers.equipWeapon()),
                                CreateMailBehavior()                            
                            )),

                        new Decorator(ret => StyxWoW.Me.FreeBagSlots <= ProfileManager.CurrentProfile.MinFreeBagSlots && !PoolFisherSettings.Instance.ShouldMail,
                            new PrioritySelector(

                                new Decorator(ret => Heartstone.Cooldown == 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Bags are full. Heart and Exit now!", Helpers.TimeNow)),
                                        new Action(ret => Heartstone.Use())
                                        //new Wait(30, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                    )),

                                new Decorator(ret => Heartstone.Cooldown > 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Bags are full. Exit now!", Helpers.TimeNow)),
                                        new Action(ret => Helpers.quitWoW()),
                                        //new Wait(30, ret => !StyxWoW.IsInGame, new ActionIdle()),
                                        new Action(ret => TreeRoot.Stop())
                                    ))
                        )),

                        new Decorator(ret => !MeIsFishing && !StyxWoW.Me.IsSwimming && !need2Train && StyxWoW.Me.FreeBagSlots > ProfileManager.CurrentProfile.MinFreeBagSlots,
                            new PrioritySelector(

                                CreateLookForPoolBehavior(),
                                CreateMoveToPoolBehavior(),

                                new Decorator(ret => (saveLocation.Count == 0 || saveLocation[0] == WoWPoint.Empty) && (StyxWoW.Me.Mounted || !Mount.CanMount()),
                                    CreatePathBehavior())
                            ))

                        
                        ));
            }
        }

        #endregion

        private Composite CreateLookForPoolBehavior()
        {
            return new Decorator(ret => looking4NewPool,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateLookForPoolsBehaviour", Helpers.TimeNow)),
                    new ActionSetActivity(ret => "Looking for Pools"),
                    new PrioritySelector(

                        new Decorator(ret => Helpers.findPool,
                            new ActionSetActivity(ret => "Looking for valid Location")),

                        CreateMountBehavior())
                    ));
        }

        private Composite CreateMoveToPoolBehavior()
        {
            return new Decorator(ret => Pool != null && !MeIsFishing && !Blacklist.Contains(Pool.Guid) && !PoolFisher.PermaBlacklist.Contains(Pool.Entry),
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveToPoolBehaviour", Helpers.TimeNow)),
                    new Action(ret => movetopoolTimer.Start()),

                    new PrioritySelector(

                        // Timer
                        new Decorator(ret => movetopoolTimer.ElapsedMilliseconds > 30000,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Timer for moving to ground elapsed, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                new Action(ret => Helpers.BlackListPool(Pool))                                
                        )),

                        // Blacklist if other Player is detected
                        new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Detected another player in pool range, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool(Pool)),
                                    new Action(delegate { return RunStatus.Success; })
                        )),

                        // Get PoolPoint
                        new Decorator(ret => looking4NewLoc,
                            new Sequence(
                                new ActionSetActivity(ret => "Looking for valid Location"),
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => saveLocation.Add(Helpers.getSaveLocation(Pool.Location, PoolFisherSettings.Instance.MinCastRange, PoolFisherSettings.Instance.MaxCastRange, 50))),
                                new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Green, "{0} - Added {1} to saveLocations.", Helpers.TimeNow, saveLocation[0]))
                        )),

                        // Move to PoolPoint
                        new Decorator(pool => Pool != null && saveLocation.Count > 0 && !looking4NewLoc,
                            new PrioritySelector(

                                // Pool still there?
                                new Decorator(ret => !Helpers.PoolIsStillThere,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow))
                                )),

                                // reached max attempts for new locations?
                                new Decorator(ret => newLocAttempts == PoolFisherSettings.Instance.MaxNewLocAttempts + 1,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Reached max. attempts for new locations, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool(Pool))
                                )),

                                // tries++
                                new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Wait(2, ret => MeIsFishing, new ActionIdle()),
                                        new Action(ret => newLocAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new Location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))
                                )),
                                            

                                // Dismount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) <= 1.5 && StyxWoW.Me.Mounted, //&& !StyxWoW.Me.IsMoving,
                                    new PrioritySelector(

                                        new Decorator(ret => Helpers.CanWaterWalk && !Helpers.hasWaterWalking,
                                            new Sequence(
                                                new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                                new Action(ret => Helpers.WaterWalk()),
                                                new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                                new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                                                new Action(ret => Mount.Dismount()),
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Red, "{0} - Navigation: Dismount. Current Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
                                                new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                                new Action(ret => Thread.Sleep((Ping * 2) + 500)))),

                                        new Decorator(ret => !Helpers.CanWaterWalk || (Helpers.CanWaterWalk && Helpers.hasWaterWalking),
                                            new Sequence(
                                                new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                                new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.Descend)),
                                                new Action(ret => Mount.Dismount()),
                                                new Action(ret => WoWMovement.MoveStop()),
                                                new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Red, "{0} - Navigation: Dismount. Current Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2), StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))),
                                                new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                                new Action(ret => Thread.Sleep((Ping * 2) + 500))))
                                        
                                )),

                                // in Line Line of sight?
                                new Decorator(ret => StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight!", Helpers.TimeNow)),
                                    new Action(ret => Helpers.blacklistLocation(saveLocation[0])),
                                    new Action(ret => saveLocation.Clear()),
                                    new Action(ret => newLocAttempts++),
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new Location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                    new Action(ret => looking4NewLoc = true)
                                )),

                                // Move without Mount
                                new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 1 && StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !StyxWoW.Me.Mounted && !Helpers.hasWaterWalking && GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming,
                                    new PrioritySelector(

                                        // Mount if not mounted and Navigator is not able to generate a path
                                        new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, saveLocation[0]),
                                            CreateMountBehavior()),

                                        new Sequence(
                                            new ActionSetActivity(ret => "Moving to new Location"),
                                            new Action(ret => Logging.WriteNavigator(Helpers.TimeNow + " - Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + saveLocation[0].Distance(StyxWoW.Me.Location) + " (Not Mounted)")),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            // Move
                                            new Action(ret => Navigator.MoveTo(saveLocation[0]))
                                        )
                                )),

                                // Move with Mount
                                new Decorator(ret => (StyxWoW.Me.Location.Distance(saveLocation[0]) > 10 || StyxWoW.Me.Mounted || (StyxWoW.Me.Location.Distance(saveLocation[0]) <= 10 && !GameWorld.IsInLineOfSight(StyxWoW.Me.Location, saveLocation[0])) && !StyxWoW.Me.IsSwimming),
                                    new PrioritySelector(

                                        // Mount if not mounted
                                        CreateMountBehavior(),

                                        // Move
                                        new Sequence(
                                            new ActionSetActivity(ret => "Moving to Ground"),
                                            new Action(ret => Logging.WriteNavigator(Helpers.TimeNow + " - Navigation: Moving to Pool: " + saveLocation[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + StyxWoW.Me.Location.Distance(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)) + " (Mounted)")),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Moving to Pool: {1}, Location: {2}, Distance: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            new Action(ret => Flightor.MoveTo(new WoWPoint(saveLocation[0].X, saveLocation[0].Y, saveLocation[0].Z + 2)))
                                        )
                                ))
                            ))
                )));
        }

        private Composite CreateFishingBehavior()
        {
            return new Decorator(ret => saveLocation.Count > 0 && StyxWoW.Me.Location.Distance(saveLocation[0]) <= 2.5 && !looking4NewPool && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && !StyxWoW.Me.Combat && (!StyxWoW.Me.IsMoving || Helpers.hasWaterWalking),// && Pool.InLineOfSight,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateFishingBehaviour", Helpers.TimeNow)),
                    new Action(ret => movetopoolTimer.Reset()),

                    new Action(ret => MeIsFishing = true),
                    new PrioritySelector(

                        // Wait for GCD
                        new Decorator(ret => StyxWoW.GlobalCooldown,
                            new ActionIdle()),

                        new Decorator(ret => Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096),
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Sleeping while red mist is on me.", Helpers.TimeNow)),
                                new Action(ret => castAttempts = 0),
                                new Wait(30, ret => !Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096) || StyxWoW.Me.Combat, new ActionIdle())
                                )),

                        // Pool still there?
                        new Decorator(ret => !Helpers.PoolIsStillThere,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow)),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => Helpers.equipWeapon())
                                )),

                        // reached max attempts for casting?
                        new Decorator(ret => castAttempts == PoolFisherSettings.Instance.MaxCastAttempts,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Tried to cast {1} times. Moving to new Location.", Helpers.TimeNow, PoolFisherSettings.Instance.MaxCastAttempts)),
                                new Action(ret => Helpers.blacklistLocation(saveLocation[0])),
                                new Action(ret => saveLocation.Clear()),
                                new Action(ret => newLocAttempts++),
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new Location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                new Action(ret => looking4NewLoc = true),
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => castAttempts = 0),
                                CreateMoveToPoolBehavior()
                        )),

                        // reached max attempts for new locations?
                                new Decorator(ret => newLocAttempts == PoolFisherSettings.Instance.MaxNewLocAttempts + 1,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Reached max. attempts for new locations, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool(Pool))
                                )),

                        // in Line Line of sight?
                        new Decorator(ret => !Pool.InLineOfSight,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight! Moving to new Location.", Helpers.TimeNow)),
                                new Action(ret => Helpers.blacklistLocation(saveLocation[0])),
                                new Action(ret => saveLocation.Clear()),
                                new Action(ret => newLocAttempts++),
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new Location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                new Action(ret => looking4NewLoc = true),
                                new Action(ret => MeIsFishing = false),
                                CreateMoveToPoolBehavior()
                        )),

                        

                        // Do we need to interact with the bobber?
                        new Decorator(ret => Helpers.BobberIsBobbing,
                            new Sequence(
                                new ActionSetActivity(ret => "Looting"),
                                new PrioritySelector(
                                    new Action(ret => Looting.interact())
                                    /*
                                    new Decorator(ret => LootFrame.Instance.IsVisible,
                                        new Sequence(
                                            new Action(ret => Looting.track()),
                                            new Action(delegate { return RunStatus.Success; })
                                        ))
                                    */
                                    )
                        )),
                        

                        // Poolfishing
                        new Decorator(ret => castAttempts <= PoolFisherSettings.Instance.MaxCastAttempts, // && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming,
                            new PrioritySelector(

                                // equip pole
                                new Decorator(ret => !Helpers.equipFishingPole,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Could not find any fishing Poles!", Helpers.TimeNow)),
                                        new Action(ret => TreeRoot.Stop()))),

                                // Luring
                                new Decorator(ret => PoolFisherSettings.Instance.useLure && !Helpers.LureIsOnPole && (!StyxWoW.Me.IsCasting || StyxWoW.Me.ChanneledCastingSpellId == 0),
                                    new Sequence(
                                        new Action(ret => need2Lure = true),
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                        new Action(ret => Helpers.applylure()),
                                        new Wait(3, ret => Helpers.LureIsOnPole, new ActionIdle()))),

                                // cast fishing
                                new Decorator(ret => (PoolFisherSettings.Instance.useLure && Helpers.LureIsOnPole) || !PoolFisherSettings.Instance.useLure && ((!Helpers.IsFishing && Helpers.PoolIsStillThere) || (Helpers.IsFishing && !Helpers.BobberIsInTheHole && Helpers.PoolIsStillThere)),
                                    new Sequence(
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                        new Action(ret => castAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Casting.. Attempt: {1} of {2}.", Helpers.TimeNow, castAttempts, PoolFisherSettings.Instance.MaxCastAttempts)),
                                        new Action(ret => TreeRoot.StatusText = "Cast Fishing"),
                                        new Action(ret => SpellManager.Cast("Fishing")),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 500))
                                        //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "Bobber in Hole: {0}.", Helpers.BobberIsInTheHole))
                                    ))
                        )),

                        // The pool phased out
                        new Decorator(ret => saveLocation.Count > 0 && saveLocation[0].X == 0 && saveLocation[0].Y == 0,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool {1} is not valid, blacklisting for 2 minutes. (Phasing - PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                new Action(ret => Helpers.BlackListPool(Pool)),
                                new Action(delegate { return RunStatus.Success; }))),

                        
                        new Decorator(ret => StyxWoW.Me.IsCasting && !StyxWoW.Me.IsSwimming,
                            new Sequence(
                                new Action(ret => TreeRoot.StatusText = "Waiting for Bite"),
                                new Wait(1, ret => StyxWoW.Me.IsCasting || !StyxWoW.Me.IsSwimming, new ActionIdle())
                                ))
                        
                        )));
        }


        private Composite CreatePathBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreatePathBehavior", Helpers.TimeNow)),
                new PrioritySelector(
                    new Decorator(ret => HotspotList.Count == 0,
                        new Sequence(
                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Profile has no hotspots!", Helpers.TimeNow)),
                            new Action(ret => TreeRoot.Stop()))),

                    new Decorator(ret => _currenthotspot < 0,
                        new Sequence(
                            new Action(ret => _currenthotspot = HotspotList.IndexOf(HotspotList.OrderBy(hs => StyxWoW.Me.Location.Distance(hs)).FirstOrDefault())),
                            new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                            new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                            )),

                    // bounce or circle mode?
                    new PrioritySelector(

                        new Decorator(ret => !PoolFisherSettings.Instance.BounceMode,
                            new PrioritySelector(

                                new Decorator(ret => _currenthotspot >= HotspotList.Count,
                                    new Sequence(
                                        new Action(ret => _currenthotspot = 0),
                                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                    )),

                                new Decorator(ret => StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                    new Sequence(
                                        new Action(ret => _currenthotspot++),
                                        //new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "_currenthotspot: {0}, HotspotList.Count: {1}.", _currenthotspot, HotspotList.Count)),
                                        new Decorator(ret => _currenthotspot <= HotspotList.Count,
                                            new Sequence(
                                                new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                                new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                        ))
                                    ))
                                )),


                        new Decorator(ret => PoolFisherSettings.Instance.BounceMode,
                            new PrioritySelector(

                                new Decorator(ret => _currenthotspot == HotspotList.Count && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                    new Sequence(
                                        new Action(ret => bounceBack = true),
                                        new Action(ret => _currenthotspot--),
                                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                        )),

                                new Decorator(ret => bounceBack && _currenthotspot > 0 && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                    new Sequence(
                                        new Action(ret => _currenthotspot--),
                                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                    )),

                                new Decorator(ret => _currenthotspot == 0 && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                    new Sequence(
                                        new Action(ret => bounceBack = false),
                                        new Action(ret => _currenthotspot++),
                                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                        )),

                                new Decorator(ret => !bounceBack && _currenthotspot < HotspotList.Count && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                    new Sequence(
                                        new Action(ret => _currenthotspot++),
                                        new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                        new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))
                                    ))
                            
                                ))
                    ),

                    new DecoratorSetContext<WoWPoint>(() => HotspotList[_currenthotspot], hotspot => true,
                        new Decorator(ret => _modHotspot.X != 0,
                            new Sequence(
                                new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to Hotspot: {1} (Distance: {2})", Helpers.TimeNow, _modHotspot, _modHotspot.Distance(StyxWoW.Me.Location))),
                                //new ActionSetActivity(hotspot => "Moving to hotspot: " + _modHotspot + ", distance: " + _modHotspot.Distance(StyxWoW.Me.Location)),
                                CreateMoveBehavior())))
                                
            ));
        }

        private Composite CreateMoveBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveBehaviour", Helpers.TimeNow)),

                new PrioritySelector(

                    new Decorator(ret => !StyxWoW.Me.Mounted && Mount.CanMount(),
                        CreateMountBehavior()),
                    new ActionMove()));
        }

        private Composite CreateMountBehavior()
        {
            return new Decorator(ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.Combat && !StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsIndoors && StyxWoW.Me.MapId != 530 && StyxWoW.Me.ZoneId != 1537, // 1537 = Ironforge
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMountBehaviour", Helpers.TimeNow)),
                    new Action(ret => WoWMovement.MoveStop()),
                    new Action(ret => mountLocation.Clear()),
                    new Action(ret => Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                    new ActionSetActivity(spell => "Mounting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                    new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                    new Wait(2, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                    new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                    new Action(ret => StyxWoW.SleepForLagDuration())

                    ));
        }

        private Composite CreateSwimmingBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming..", Helpers.TimeNow)),

                new PrioritySelector(

                    new Decorator(ret => mountLocation.Count > 0 && (!Navigator.CanNavigateFully(StyxWoW.Me.Location, mountLocation[0]) || StyxWoW.Me.Location.Distance2D(mountLocation[0]) < 2.5),
                        new Sequence(
                            new Action(ret => Helpers.blacklistLocation(mountLocation[0])),
                            new Action(ret => mountLocation.Clear())
                    )),
                    
                    new Decorator(ret => Helpers.CanWaterWalk && PoolFisherSettings.Instance.useWaterWalking,
                        new Sequence(
                            new Decorator(ret => !Helpers.hasWaterWalking,
                                new Action(ret => Helpers.WaterWalk())),
                            new Action(ret => WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend)),
                            new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                            new Action(ret => WoWMovement.MoveStop()),
                            new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Red, "{0} - Navigation: WaterWalk. Current Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, saveLocation[0], StyxWoW.Me.Location.Distance(saveLocation[0])))
                    )),

                    new Decorator(ret => (!Helpers.CanWaterWalk || !PoolFisherSettings.Instance.useWaterWalking) && saveLocation.Count > 0,
                        new Sequence(
                            new Action(ret => Helpers.blacklistLocation(saveLocation[0])),
                            new Action(ret => saveLocation.Clear()),
                            new Action(ret => newLocAttempts++),
                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new Location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))
                    )),

                    new Decorator(ret => (!Helpers.CanWaterWalk || !PoolFisherSettings.Instance.useWaterWalking) && saveLocation.Count == 0,
                        new Sequence(
                            new PrioritySelector(

                                new Decorator(ret => mountLocation.Count == 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Get new Location to move to.", Helpers.TimeNow)),
                                        new Action(ret => looking4NewLoc = true),
                                        new Action(ret => mountLocation.Add(Helpers.getSaveLocation(StyxWoW.Me.Location, 10, 500, 25)))
                                )),

                                new Decorator(ret => mountLocation.Count > 0 && StyxWoW.Me.Location.Distance(mountLocation[0]) >= 2.5,
                                    new Sequence(
                                        new Action(ret => Logging.WriteNavigator(System.Drawing.Color.Red, "{0} - Moving to new Location: Current Location {1}, New Location: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, mountLocation[0], StyxWoW.Me.Location.Distance(mountLocation[0]))),
                                        new Action(ret => Navigator.MoveTo(mountLocation[0]))))

                            )
                        ))

                ));
        }

        private Composite CreateTrainBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateTrainBehavior", Helpers.TimeNow)),
                new Action(ret => need2Train = true),
                new PrioritySelector(

                    //new Decorator(ret => !Helpers.needToTrain(StyxWoW.Me.GetSkill(SkillLine.Fishing)) && StyxWoW.Me.Location.Distance(TrainerDestination) <= 2,
                        //new Action(ret => needTrain = false)),

                    //new Decorator(ret => !Helpers.needToTrain(StyxWoW.Me.GetSkill(SkillLine.Fishing)) && StyxWoW.Me.Location.Distance(TrainerDestination) > 2,
                        //new Action(ret => Navigator.MoveTo(TrainerDestination))),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && TrainerID == 0,
                        new Sequence(
                            new ActionSetActivity(ret => "Looking for Trainer"),
                            new Action(ret => Training.getTrainer())
                            )),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerLocation) <= 2,
                        new Sequence(
                            new Action(ret => Training.InteractWithTrainer())
                            )),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerLocation) > 2 && !FlyToTrainer,
                        new PrioritySelector(
                            new Sequence(
                                new ActionSetActivity(ret => "Moving to Trainer"),
                                new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to {1}, Distance: {2}. Training. (Not Mounted)", Helpers.TimeNow, TrainerDestination, StyxWoW.Me.Location.Distance(TrainerDestination))),
                                new Action(ret => Navigator.MoveTo(TrainerLocation))
                                )
                            )),

                    // use teleport to Dalaran because flightor has issues with navigation
                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerDestination) < 2 && Training.gotDalaranTeleport && TrainerID == 28742,
                        new PrioritySelector(
                            new Sequence(
                                new ActionSetActivity(ret => "Moving to Trainer"),
                                new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to {1}, Distance: {2}. Training. (Not Mounted, DalaranTeleport)", Helpers.TimeNow, TrainerDestination, StyxWoW.Me.Location.Distance(TrainerDestination))),
                                new Action(ret => Mount.Dismount()),
                                new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                new Action(ret => DalaraTeleport.Interact()),
                                new Action(ret => FlyToTrainer = false),
                                new Wait(5, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                )
                            )),

                    // use flight master to Rut'theran
                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerDestination) < 2 && TrainerID == 3607 && StyxWoW.Me.ZoneId != 141,
                        new PrioritySelector(
                            new Sequence(
                                new ActionSetActivity(ret => "Moving to Trainer"),
                                new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to {1}, Distance: {2}. Training. (Taxi to Rut'theran)", Helpers.TimeNow, TrainerDestination, StyxWoW.Me.Location.Distance(TrainerDestination))),
                                new Action(ret => Mount.Dismount()),
                                new Wait(3, ret => !StyxWoW.Me.Mounted, new ActionIdle()),
                                new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                new Action(ret => Training.useRuttheranFlightPath()),
                                new Action(ret => FlyToTrainer = false)
                                )
                            )),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerDestination) < 2 && TrainerID == 3607 && StyxWoW.Me.ZoneId == 141,
                        new PrioritySelector(
                            new Sequence(
                                new ActionSetActivity(ret => "Moving to Trainer"),
                                new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to {1}, Distance: {2}. Training. (Taxi to Rut'theran)", Helpers.TimeNow, TrainerDestination, StyxWoW.Me.Location.Distance(TrainerDestination))),
                                new Action(ret => Mount.Dismount()),
                                new Wait(3, ret => !StyxWoW.Me.Mounted, new ActionIdle()),
                                new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                new Action(ret => FlyToTrainer = false)
                                )
                            )),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerDestination) < 2,
                        new PrioritySelector(
                            new Sequence(
                                new Action(ret => Mount.Dismount()),
                                new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                new Action(ret => Thread.Sleep((Ping * 2) + 500)),
                                new Action(ret => FlyToTrainer = false)
                                )
                            )),

                    new Decorator(ret => (Training.needTraining(StyxWoW.Me.GetSkill(SkillLine.Fishing)) || need2Train == true) && StyxWoW.Me.Location.Distance(TrainerDestination) > 2 && FlyToTrainer,
                        new Sequence(
                            new ActionSetActivity(ret => "Moving to Trainer"),
                            new Action(ret => Logging.WriteNavigator("{0} - Navigation: Moving to {1}, Distance: {2}. Training. (Mounted)", Helpers.TimeNow, TrainerDestination, StyxWoW.Me.Location.Distance(TrainerDestination))),
                            new Action(ret => Flightor.MoveTo(TrainerDestination))
                            ))
            ));
        }

        private Composite CreateBackToPathBehavior()
        {
            return new Decorator(ret => back2Path && !need2Train,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateBackToPathBehavior", Helpers.TimeNow)),
                    new Action(ret => Logging.WriteDebug("{0} - back2Path: {1}. FlyToTrainer: {2}", Helpers.TimeNow, back2Path, FlyToTrainer)),
                new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me.Location.Distance(PoolFisher.TrainerDestination) <= 5 || TrainerID == 28742,
                            new Sequence(
                                new Action(ret => TrainerID = 0),
                                new Action(ret => FlyToTrainer = true),
                                new Action(ret => back2Path = false)
                            )),

                        new Decorator(ret => StyxWoW.Me.ZoneId == 141,
                            new Action(ret => Training.useLordanelFlightPath())),

                        new Decorator(ret => StyxWoW.Me.Location.Distance(PoolFisher.TrainerDestination) > 5,
                            new Action(ret => Navigator.MoveTo(PoolFisher.TrainerDestination)))

                    )));
        }

        private Composite CreateMailBehavior()
        {
            return new Decorator(ret => Mailing.hasMailbox(),
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMailBehavior", Helpers.TimeNow)),
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me.Location.Distance(Mailing.closestMailbox) <= 2.5,
                            new Sequence(
                                new Action(ret => Mailing.InteractWithMailbox())
                        )),

                        new Decorator(ret => StyxWoW.Me.Location.Distance(Mailing.closestMailbox) > 2.5,
                            new Sequence(
                                new Action(ret => Flightor.MoveTo(Mailing.closestMailbox))
                        ))


                    )
            ));
        }

        private Composite CreateCastBehavior()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateCastBehaviour", Helpers.TimeNow)),
                new ActionSetActivity(spell => "Casting " + ((WoWSpell)spell).Name),
                new Action(spell => ((WoWSpell)spell).Cast()),
                new Wait(2, ret => !StyxWoW.Me.IsCasting, new ActionIdle()),
                new Action(ret => StyxWoW.SleepForLagDuration()));
        }

        private Composite CreateRestBehavior()
        {
            return new Decorator(ret => !StyxWoW.Me.IsFlying && !StyxWoW.Me.Combat,
                new PrioritySelector(
                    new Decorator(ret => RoutineManager.Current.RestBehavior != null,
                        RoutineManager.Current.RestBehavior),
                    new Decorator(ret => RoutineManager.Current.NeedRest,
                        new Action(ret => RoutineManager.Current.Rest()))));
        }
    }


    internal class ActionIdle : Action
    {
        protected override RunStatus Run(object context)
        {
            if (Parent is Selector || Parent is Decorator)
                return RunStatus.Success;

            return RunStatus.Failure;
        }
    }


}
