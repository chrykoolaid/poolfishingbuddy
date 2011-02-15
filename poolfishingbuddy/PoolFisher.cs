using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Bots.Grind;
using Bots.ArchaeologyBuddy;
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
        private int _currenthotspot = -1;
        static public WoWPoint _modHotspot;
        
        static public bool looking4NewPoint;
        static public bool looking4NewPool;
        static public bool MeIsFishing;
        static public bool bounceBack = false;
        static public Thread MonitoringThread;
        static public Thread GetValuesThread;

        static public Thread GetSlopesThread;

        static public int Ping;
        static public int newLocAttempts = 0;
        static public int castAttempts = 0;
        static public Stopwatch lootTimer = new Stopwatch();
        static public Stopwatch movetopoolTimer = new Stopwatch();
        static public volatile List<ulong> PermaBlacklist = new List<ulong>();
        static public WoWGameObject Pool = null;
        static public WoWFishingBobber Bobber = null;
        static public List<WoWPoint> PoolPoints = new List<WoWPoint>(100);
        static public List<WoWPoint> tempPoolPoints = new List<WoWPoint>(100);
        static public WoWPoint WaterSurface;

        static public List<WoWItem> mainhandList = new List<WoWItem>();
        static public List<WoWItem> offhandList = new List<WoWItem>();
        static public List<WoWItem> poleList = new List<WoWItem>();
        static public List<WoWItem> BagItems = new List<WoWItem>();
        
        #endregion

        static public List<WoWPoint> HotspotList;
        static public List<WoWPoint> BlackspotList;

        static public GrindArea GrindArea { get; set; }


        #region Overrides of BotBase

        private readonly Version _version = new Version(1, 0, 13);

        public override string Name
        {
            get { return "PoolFisher " + _version; }
        }

        public override PulseFlags PulseFlags { get { return PulseFlags.All; } }

        public override Form ConfigurationForm { get { return new FormFishConfig(); } }

        public override void Start()
        {
            GrindArea = ProfileManager.CurrentProfile.GrindArea;
            HotspotList = GrindArea.Hotspots.ConvertAll<WoWPoint>(hs => hs.ToWoWPoint());
            BlackspotList = ProfileManager.CurrentProfile.Blackspots.ConvertAll<WoWPoint>(bs => bs.Location);

            WoWSpell Mount = WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID);

            Logging.Write(System.Drawing.Color.Green, "Current Settings:");
            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.Write(System.Drawing.Color.Green, "Flying Mount: {0}", Mount.Name);
            Logging.Write(System.Drawing.Color.Green, "Height: {0}", PoolFisherSettings.Instance.HeightModifier);
            Logging.Write(System.Drawing.Color.Green, "Mode: {0}", PoolFisherSettings.Instance.BounceMode);
            Logging.Write(System.Drawing.Color.Green, "Max. range to cast: {0}", PoolFisherSettings.Instance.MaxCastRange);
            Logging.Write(System.Drawing.Color.Green, "Max. attempts to cast: {0}", PoolFisherSettings.Instance.MaxCastAttempts);
            Logging.Write(System.Drawing.Color.Green, "Ninja Pools: {0}", PoolFisherSettings.Instance.NinjaPools);
            Logging.Write(System.Drawing.Color.Green, "Blacklist Schools: {0}", PoolFisherSettings.Instance.BlacklistSchools);
            Logging.Write(System.Drawing.Color.Green, "Use Lure: {0}", PoolFisherSettings.Instance.useLure);
            Logging.Write(System.Drawing.Color.Green, "Descend higher: {0}", PoolFisherSettings.Instance.DescendHigher);
            Logging.Write(System.Drawing.Color.Green, "Max. attempts to reach pool: {0}", PoolFisherSettings.Instance.MaxNewLocAttempts);

            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            Logging.Write(System.Drawing.Color.Green, "Current Profile:");
            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");
            try
            {
                Logging.Write(System.Drawing.Color.Green, "Name: {0}", ProfileManager.CurrentProfile.Name);
                Logging.Write(System.Drawing.Color.Green, "Hotspots: {0}", ProfileManager.CurrentProfile.HotspotManager.Hotspots.Count);
                Logging.Write(System.Drawing.Color.Green, "Blackspots: {0}", ProfileManager.CurrentProfile.Blackspots.Count);
                //Logging.Write(System.Drawing.Color.Green, "Vendor: {0}", ProfileManager.CurrentProfile.VendorManager.Vendors.Count);
                //Logging.Write(System.Drawing.Color.Green, "Mailbox: {0}", ProfileManager.CurrentProfile.MailboxManager.Mailboxes.Count);
            }
            catch (Exception e) 
            {
                Logging.Write(System.Drawing.Color.Red, "ProfileExeption: {0}", e.ToString());
            }

            Logging.Write(System.Drawing.Color.Green, "-------------------------------------------");

            Helpers.blacklistSchoolsFromSettings();

            looking4NewPoint = true;
            looking4NewPool = true;
            MeIsFishing = false;
            newLocAttempts = 0;
            PoolPoints.Clear();

            Styx.BotEvents.OnBotStart += Helpers.Init;
            Styx.BotEvents.OnBotStop += Helpers.Final;
            if (TreeRoot.IsRunning)
                Helpers.Init(new System.EventArgs());

            StyxSettings.Instance.LogoutForInactivity = false;

            Logging.Write(System.Drawing.Color.Blue, "{0} - Pool Fisher {1} starting!", Helpers.TimeNow, _version);
        }

        public override void Stop()
        {
            Logging.Write(System.Drawing.Color.Blue, "{0} - Pool Fisher {1} stopped!", Helpers.TimeNow, _version);
            StyxSettings.Instance.LogoutForInactivity = true;
        }

        public override Composite Root
        {
            get
            {
                return _root ?? (_root = 
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me == null || !StyxWoW.IsInGame || !StyxWoW.IsInWorld,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - [INVALID] Waiting", Helpers.TimeNow)),
                                new ActionSleep(10000))),

                        new Decorator(ret => StyxWoW.Me.IsFalling,
                            new ActionSleep(1000)),


                        LevelBot.CreateDeathBehavior(),

                        new Decorator(ret => !StyxWoW.Me.Mounted && StyxWoW.Me.Combat,
                            new Sequence(
                                new Action(ret => MeIsFishing = false),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => castAttempts = 0),
                                new Action(ret => Helpers.equipWeapon()),
                                LevelBot.CreateCombatBehavior())),

                        // ToDo: own need to rest check on top of this..
                        CreateFishingBehaviour(),
                        CreateRestBehavior(),


                        #region Buffs

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
                                        ))),

                        #endregion
                        
                        LevelBot.CreateLootBehavior(),
                        LevelBot.CreateVendorBehavior(),

                        new Decorator(ret => !MeIsFishing,
                            new PrioritySelector(

                                CreateMoveToPoolBehaviour(),

                                new Decorator(ret => PoolPoints.Count == 0,
                                    new PrioritySelector(

                                        

                                        CreatePathBehaviour(),
                                        CreateLookForPoolBehaviour()
                                    
                                    ))
                            ))
                        ));
            }
        }

        #endregion

        private Composite CreateLookForPoolBehaviour()
        {
            return new Decorator(ret => Helpers.findPool && looking4NewPool && Pool.X != 0,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateLookForPoolsBehaviour", Helpers.TimeNow))
                    ));
        }

        private Composite CreateMoveToPoolBehaviour()
        {
            return new Decorator(ret => Pool != null && !MeIsFishing && !Blacklist.Contains(Pool.Guid) && !PoolFisher.PermaBlacklist.Contains(Pool.Entry),
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveToPoolBehaviour", Helpers.TimeNow)),
                    new PrioritySelector(

                        // Get PoolPoint
                        new Decorator(ret => looking4NewPoint,
                            new Sequence(
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Helpers.findPoolPoint()))),

                        // Blacklist if Navigator can't generate Path
                        new Decorator(ret => PoolPoints.Count == 0,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - No path found to {1}, blacklisting for 2 minutes. (PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                    new Action(ret => Helpers.BlackListPool()),
                                    new Action(delegate { return RunStatus.Success; })
                        )),

                        // Blacklist if other Player is detected
                        new Decorator(ret => Helpers.PlayerDetected && !PoolFisherSettings.Instance.NinjaPools,
                            new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Detected another player in pool range, blacklisting for 2 minutes.", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool()),
                                    new Action(delegate { return RunStatus.Success; })
                        )),


                        // Move to PoolPoint
                        new Decorator(pool => PoolPoints.Count > 0 && !looking4NewPoint,
                            new PrioritySelector(

                                // Blacklist if Navigator can't generate Path
                                new Decorator(ret => PoolPoints.Count == 0,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - No path found to {1}, blacklisting for 2 minutes. (PoolFisher)", Helpers.TimeNow, Pool.Name)),
                                        new Action(ret => Helpers.BlackListPool()),
                                        new Action(delegate { return RunStatus.Success; })
                                )),

                                // Pool still there?
                                new Decorator(ret => !Helpers.PoolIsStillThere,
                                    new Sequence(
                                        new Action(ret => Logging.Write("{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow))
                                        )),

                                // reached max attempts for new locations?
                                new Decorator(ret => newLocAttempts == PoolFisherSettings.Instance.MaxNewLocAttempts + 1,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Reached max. attempts for new locations!", Helpers.TimeNow)),
                                    new Action(ret => Helpers.BlackListPool())
                                    
                                    //new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                    //new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                                    //new Action(ret => PoolPoints.Reverse()),
                                    //new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0])))
                                )),

                                // tries++
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 1.5 && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        new Wait(2, ret => MeIsFishing, new ActionIdle()),
                                        new Action(ret => newLocAttempts++),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving to new location.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))
                                )),
                                            

                                // Dismount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 1.5 && StyxWoW.Me.Mounted, //&& !StyxWoW.Me.IsMoving,
                                    new Sequence(
                                        //new Action(ret => newLocAttempts = 0),
                                        new Action(ret => Mount.Dismount()),
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Location {1}, PoolPoint: {2}, Distance: {3}", Helpers.TimeNow, StyxWoW.Me.Location, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                        new Wait(3, ret => StyxWoW.Me.Mounted, new ActionIdle()),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 500))
                                )),

                                // in Line Line of sight?
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 2 && !Pool.InLineOfSight && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight!", Helpers.TimeNow)),
                                    new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                    new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                                    //new Action(ret => PoolPoints.Reverse()),
                                    new PrioritySelector(

                                            new Decorator(ret => PoolPoints.Count == 0 && StyxWoW.Me.Location.Distance(PoolPoints[0]) > 2,
                                                new Action(ret => Helpers.BlackListPool())),

                                            new Decorator(ret => PoolPoints.Count > 0,
                                                new Sequence(
                                                    new Action(ret => PoolPoints.Sort((p1, p2) => PoolFisher.Pool.Location.Distance(p1).CompareTo(PoolFisher.Pool.Location.Distance(p2)))),
                                                    //new Action(ret => PoolPoints.Reverse()),
                                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                                    new Action(ret => newLocAttempts++),
                                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))))
                                        )
                                )),

                                // swimming?
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 2 && StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming at {1}...", Helpers.TimeNow, StyxWoW.Me.Location)),
                                        new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                        new PrioritySelector(

                                            new Decorator(ret => PoolPoints.Count == 0 && StyxWoW.Me.Location.Distance(PoolPoints[0]) > 2,
                                                new Action(ret => Helpers.BlackListPool())),

                                            new Decorator(ret => PoolPoints.Count > 0,
                                                new Sequence(
                                                    new Action(ret => PoolPoints.Sort((p1, p2) => PoolFisher.Pool.Location.Distance(p1).CompareTo(PoolFisher.Pool.Location.Distance(p2)))),
                                                    //new Action(ret => PoolPoints.Reverse()),
                                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                                    new Action(ret => newLocAttempts++),
                                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts))))
                                        )
                                )),

                                // Move without Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance2D(PoolPoints[0]) > 2.5 && StyxWoW.Me.Location.Distance2D(PoolPoints[0]) <= 40 && !StyxWoW.Me.Mounted && GameWorld.IsInLineOfSight(StyxWoW.Me.Location, PoolPoints[0]),
                                    new PrioritySelector(

                                        new Decorator(ret => !Navigator.CanNavigateFully(StyxWoW.Me.Location, PoolPoints[0]),
                                            CreateMountBehaviour()),

                                        new Sequence(
                                            new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + "(Not Mounted)"),
                                            new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, Location: {2}, Distance: {3}. (Not Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                            // Move
                                            new Action(ret => Navigator.MoveTo(PoolPoints[0]))
                                            //new Wait(2, ret => !StyxWoW.Me.IsMoving || StyxWoW.Me.Location.Distance(PoolPoints[0]) < 1.5, new ActionIdle())
                                            //new Action(ret => newLocAttempts++),
                                            //new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)))
                                        )
                                )),

                                // Move with Mount
                                new Decorator(ret => StyxWoW.Me.Location.Distance(PoolPoints[0]) > 40 || StyxWoW.Me.Mounted || ( StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 40 && !GameWorld.IsInLineOfSight(StyxWoW.Me.Location, PoolPoints[0])),
                                    new Sequence(
                                        new ActionSetActivity(ret => "Moving to PoolPoint: " + PoolPoints[0] + ", Location: " + StyxWoW.Me.Location + ", distance: " + PoolPoints[0].Distance(StyxWoW.Me.Location) + "(Mounted)"),
                                        new Action(ret => Logging.Write("{0} - Moving to PoolPoint: {1}, Location: {2}, Distance: {3}. (Mounted)", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location, PoolPoints[0].Distance(StyxWoW.Me.Location))),
                                        new PrioritySelector(
                                            // Mount if not mounted
                                            CreateMountBehaviour(),

                                            // Move
                                            new Sequence(
                                                new Action(ret => Flightor.MoveWithTrace(PoolPoints[0]))
                                                //new Wait(2, ret => !StyxWoW.Me.IsMoving || StyxWoW.Me.Location.Distance(PoolPoints[0]) < 2, new ActionIdle())
                                                //new Action(ret => newLocAttempts++),
                                                //new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)))
                                            )
                                        )
                                ))
                            ))
                )));
        }

        private Composite CreateFishingBehaviour()
        {
            return new Decorator(ret => PoolPoints.Count > 0 && StyxWoW.Me.Location.Distance(PoolPoints[0]) <= 5 && !looking4NewPool && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Combat,// && Pool.InLineOfSight,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateFishingBehaviour", Helpers.TimeNow)),
                    new Action(ret => MeIsFishing = true),
                    new PrioritySelector(

                        // Wait for GCD
                        new Decorator(ret => StyxWoW.GlobalCooldown,
                            new ActionIdle()),

                        new Decorator(ret => Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096),
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Sleep while red mist is on me.", Helpers.TimeNow)),
                                new Action(ret => castAttempts = 0),
                                new Wait(30, ret => !Styx.StyxWoW.Me.GetAllAuras().Any(Aura => Aura.SpellId == 81096) || StyxWoW.Me.Combat, new ActionIdle())
                                )),

                        // Pool locations left?
                        new Decorator(ret => PoolPoints.Count == 0,
                            new Sequence(
                                new Action(ret => Helpers.BlackListPool()),
                                new Action(delegate { return RunStatus.Success; }))),

                        // Pool still there?
                        new Decorator(ret => !Helpers.PoolIsStillThere,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Fishing Pool is gone, moving on.", Helpers.TimeNow)),
                                new Action(ret => newLocAttempts = 0),
                                new Action(ret => Helpers.equipWeapon())
                                )),

                        // reached max attempts for casting?
                        new Decorator(ret => castAttempts == PoolFisherSettings.Instance.MaxCastAttempts,
                            new Sequence(
                            new Action(ret => Logging.Write("{0} - Tried to cast {1} times. Moving to new location.", Helpers.TimeNow, PoolFisherSettings.Instance.MaxCastAttempts)),
                            new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                            
                                new PrioritySelector(

                                    new Decorator(ret => PoolPoints.Count == 0,
                                        new Action(ret => Helpers.BlackListPool())),

                                    new Decorator(ret => PoolPoints.Count > 0,
                                        new Sequence(
                                            new Action(ret => PoolPoints.Sort((p1, p2) => PoolFisher.Pool.Location.Distance(p1).CompareTo(PoolFisher.Pool.Location.Distance(p2)))),
                                            //new Action(ret => PoolPoints.Reverse()),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                            new Action(ret => MeIsFishing = false),
                                            new Action(ret => castAttempts = 0),
                                            new Action(ret => newLocAttempts++),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                            CreateMoveToPoolBehaviour()))
                            ))),

                        // in Line Line of sight?
                        new Decorator(ret => !Pool.InLineOfSight,
                            new Sequence(
                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Pool is not in Line of Sight! Moving to new point.", Helpers.TimeNow)),
                            new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                            new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                            new PrioritySelector(

                                new Decorator(ret => PoolPoints.Count == 0,
                                        new Action(ret => Helpers.BlackListPool())),

                                    new Decorator(ret => PoolPoints.Count > 0,
                                        new Sequence(
                                            new Action(ret => PoolPoints.Sort((p1, p2) => PoolFisher.Pool.Location.Distance(p1).CompareTo(PoolFisher.Pool.Location.Distance(p2)))),
                                            //new Action(ret => PoolPoints.Reverse()),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                            new Action(ret => MeIsFishing = false),
                                            new Action(ret => castAttempts = 0),
                                            new Action(ret => newLocAttempts++),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                            CreateMoveToPoolBehaviour()))
                        ))),

                        // swimming?
                        new Decorator(ret => StyxWoW.Me.IsSwimming,
                            new Sequence(
                                new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Swimming at {1}...", Helpers.TimeNow, StyxWoW.Me.Location)),
                                new Action(ret => PoolPoints.Remove(PoolPoints[0])),
                                new Action(ret => PoolPoints.Sort((p1, p2) => p1.Z.CompareTo(p2.Z))),
                                new PrioritySelector(

                                    new Decorator(ret => PoolPoints.Count == 0,
                                        new Action(ret => Helpers.BlackListPool())),

                                    new Decorator(ret => PoolPoints.Count > 0,
                                        new Sequence(
                                            new Action(ret => PoolPoints.Sort((p1, p2) => PoolFisher.Pool.Location.Distance(p1).CompareTo(PoolFisher.Pool.Location.Distance(p2)))),
                                            //new Action(ret => PoolPoints.Reverse()),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - New PoolPoint: {1}, Distance: {2}", Helpers.TimeNow, PoolPoints[0], StyxWoW.Me.Location.Distance(PoolPoints[0]))),
                                            new Action(ret => MeIsFishing = false),
                                            new Action(ret => castAttempts = 0),
                                            new Action(ret => newLocAttempts++),
                                            new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Moving.. Attempt: {1} of {2}.", Helpers.TimeNow, newLocAttempts, PoolFisherSettings.Instance.MaxNewLocAttempts)),
                                            CreateMoveToPoolBehaviour()))
                        ))),

                        // Do we need to interact with the bobber?
                        new Decorator(ret => Helpers.BobberIsBobbing,
                            new Sequence(
                                // Interact with the bobber
                                new Action(delegate
                                {
                                    Logging.Write("{0} - Bobber is bobbing!", Helpers.TimeNow);
                                    WoWGameObject Bobber = Helpers.FishingBobber;

                                    Bobber.Interact();
                                    Bobber = null;
                                    Thread.Sleep((Ping * 2) + 200);
                                    // wait for loot frame to apear
                                    Logging.Write("{0} - Interact done, looting!", Helpers.TimeNow);

                                    if (LootFrame.Instance.IsVisible)
                                        Logging.Write("{0} - LootFrame is visible!", Helpers.TimeNow);

                                    lootTimer.Reset();
                                    lootTimer.Start();
                                    while (LootFrame.Instance == null || !LootFrame.Instance.IsVisible)
                                    {
                                        if (lootTimer.ElapsedMilliseconds > 5000)
                                        {
                                            Logging.Write("{0} - Loot timer elapsed!", Helpers.TimeNow);
                                            castAttempts = 0;
                                            return RunStatus.Failure;
                                        }
                                        Thread.Sleep(100);
                                    }
                                    /*
                                    for (int i = 0; i < Helpers.LootItems; i++)
                                    {
                                        var info = new LootSlotInfo(i);

                                        if (!info.Locked)
                                        {
                                            Logging.Write("Trying to loot #{0} of {1} Rarity:{2}",
                                                info.LootQuantity,
                                                info.LootName,
                                                info.LootRarity);

                                            Helpers.Loot(i);
                                        }
                                    }
                                    */
                                    Lua.DoString("for i=1,GetNumLootItems() do ConfirmLootSlot(i) LootSlot(i) end");
                                    // wait for lootframe to close
                                    while (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
                                    {
                                        Thread.Sleep(100);
                                    }

                                    Logging.Write("{0} - Looting done!", Helpers.TimeNow);
                                    castAttempts = 0;
                                    return RunStatus.Success;
                                })

                                    /*
                                    Logging.Write("Got a bite!");
                                    WoWGameObject bobber = Helpers.FishingBobber;

                                    if (bobber != null)
                                        bobber.Interact();

                                    else
                                    {
                                        Logging.Write("bobber is null");
                                        return RunStatus.Failure;
                                    }
                                    triesCast = 0;
                                    return RunStatus.Success;
                                }),

                                // Wait for the lootframe
                                new Wait(5, ret => LootFrame.Instance.IsVisible,
                                    new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Looting"),
                                        new Action(ret => StyxWoW.SleepForLagDuration())
                                        ))*/
                        )),

                        // Recast
                        new Decorator(ret => Helpers.IsFishing && !Helpers.BobberIsInTheHole && Helpers.PoolIsStillThere && castAttempts <= PoolFisherSettings.Instance.MaxCastAttempts,
                            new PrioritySelector(

                                // Luring
                                new Decorator(ret => PoolFisherSettings.Instance.useLure && !Helpers.IsLureOnPole,
                                    new Sequence(
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                        new Action(ret => Helpers.applylure()),
                                        new Wait(5, ret => !StyxWoW.Me.IsCasting, new ActionIdle()))),

                                // cast fishing
                                new Sequence(
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Missed the pool!", Helpers.TimeNow)),
                                    new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                    //new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                    new Action(ret => castAttempts++),
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Casting.. Attempt: {1} of {2}.", Helpers.TimeNow, castAttempts, PoolFisherSettings.Instance.MaxCastAttempts)),
                                    new Action(ret => TreeRoot.StatusText = "Cast Fishing."),
                                    new Action(ret => SpellManager.Cast("Fishing")),
                                    new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                    new Wait(1, ret => !StyxWoW.Me.IsCasting, new ActionIdle()))
                                )),

                        // Poolfishing
                        new Decorator(ret => !Helpers.IsFishing && !StyxWoW.Me.IsMoving && !StyxWoW.Me.Mounted && !StyxWoW.Me.IsSwimming && Helpers.PoolIsStillThere,
                            new PrioritySelector(

                                // equip pole
                                new Decorator(ret => !Helpers.equipFishingPole,
                                    new Sequence(
                                        new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Could not find any fishing Poles!", Helpers.TimeNow)),
                                        new Action(ret => TreeRoot.Stop()))),

                                // Luring
                                new Decorator(ret => PoolFisherSettings.Instance.useLure && !Helpers.IsLureOnPole,
                                    new Sequence(
                                        new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                        new Action(ret => Thread.Sleep((Ping * 2) + 200)),
                                        new Action(ret => Helpers.applylure()),
                                        new Wait(5, ret => !StyxWoW.Me.IsCasting, new ActionIdle()))),

                                // cast fishing
                                new Sequence(
                                    new Action(ret => StyxWoW.Me.SetFacing(Pool.Location)),
                                    new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                    new Sequence(
                                    new Action(ret => castAttempts++),
                                    new Action(ret => Logging.Write(System.Drawing.Color.Red, "{0} - Casting.. Attempt: {1} of {2}.", Helpers.TimeNow, castAttempts, PoolFisherSettings.Instance.MaxCastAttempts)),
                                    new Action(ret => TreeRoot.StatusText = "Cast Fishing"),
                                    new Action(ret => SpellManager.Cast("Fishing")),
                                    new Action(ret => Thread.Sleep((Ping * 2) + 250)),
                                    new Wait(1, ret => !StyxWoW.Me.IsCasting, new ActionIdle())
                                    )))),

                        // The pool phased out
                        new Decorator(ret => PoolPoints.Count > 0 && PoolPoints[0].X == 0 && PoolPoints[0].Y == 0,
                            new Sequence(
                                new Action(ret => Helpers.BlackListPool()),
                                new Action(delegate { return RunStatus.Success; }))),

                        new Sequence(
                            new Action(ret => TreeRoot.StatusText = "Waiting for bobber to splash"),
                            new ActionIdle())

                        )));
        }


        private Composite CreatePathBehaviour()
        {
            return new PrioritySelector(

                new Decorator(ret => HotspotList.Count <= 0,
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
                                    //new Action(ret => Logging.Write("_currenthotspot: {0}, HotspotList.Count: {1}.", _currenthotspot, HotspotList.Count)),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                ))
                            )),


                    new Decorator(ret => PoolFisherSettings.Instance.BounceMode,
                        new PrioritySelector(
                            
                            new Decorator(ret => _currenthotspot >= HotspotList.Count,
                                new Sequence(
                                    new Action(ret => bounceBack = true),
                                    new Action(ret => _currenthotspot--),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                    )),

                            new Decorator(ret => _currenthotspot == 0,
                                new Sequence(
                                    new Action(ret => bounceBack = false),
                                    new Action(ret => _currenthotspot++),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                    )),

                            new Decorator(ret => bounceBack && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => _currenthotspot--),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot))                                
                                )),

                            new Decorator(ret => !bounceBack && StyxWoW.Me.Location.Distance(_modHotspot) < 30,
                                new Sequence(
                                    new Action(ret => _currenthotspot++),
                                    new Action(ret => _modHotspot = HotspotList.ElementAt(_currenthotspot)),
                                    new Action(ret => _modHotspot.Z = Helpers.increaseGroundZ(_modHotspot)) 
                                ))
                            
                            ))
                ),

                new DecoratorSetContext<WoWPoint>(() => HotspotList[_currenthotspot], hotspot => true,
                    new Sequence(
                        //new Action(ret => Logging.Write("Moving to hotspot: {0}", ret)),
                        new ActionSetActivity(hotspot => "Moving to hotspot: " + _modHotspot + ", distance: " + _modHotspot.Distance(StyxWoW.Me.Location)),
                        CreateMoveBehaviour())));
        }

        private Composite CreateMoveBehaviour()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveBehaviour", Helpers.TimeNow)),

                new PrioritySelector(

                    new Decorator(ret => !StyxWoW.Me.Mounted,
                        CreateMountBehaviour()),
                    new ActionMove()));
        }

        private Composite CreateMountBehaviour()
        {
            return new Decorator(ret => !StyxWoW.Me.Mounted && !StyxWoW.Me.Combat,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMountBehaviour", Helpers.TimeNow)),
                    new PrioritySelector(

                        new Decorator(ret => StyxWoW.Me.IsSwimming && StyxWoW.Me.Location.Z >= (Helpers.GetWaterSurface(StyxWoW.Me.Location) - 1) && PoolFisherSettings.Instance.FlyingMountID != 0 && WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).IsValid,
                            new Sequence(
                                new Action(ret => Logging.Write("{0} - Location: {1}", Helpers.TimeNow, StyxWoW.Me.Location.Z)),
                                new Action(ret => Logging.Write("{0} - Water Suface: {1}", Helpers.TimeNow, WaterSurface.Z)),
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Logging.Write("{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                                new ActionSetActivity(spell => "Casting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                                new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                                new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                                new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                                new Action(ret => StyxWoW.SleepForLagDuration())
                                )),

                        new Decorator(ret => StyxWoW.Me.IsSwimming && StyxWoW.Me.Location.Z < Helpers.GetWaterSurface(StyxWoW.Me.Location),
                            new Sequence(
                                    new Action(ret => WaterSurface = StyxWoW.Me.Location),
                                    new Action(ret => WaterSurface.Z = Helpers.GetWaterSurface(StyxWoW.Me.Location)),
                                    new Action(ret => Logging.Write("{0} - Location: {1}, Water Suface: {2}", Helpers.TimeNow, StyxWoW.Me.Location, WaterSurface)),
                                    new Action(ret => WoWMovement.ClickToMove(WaterSurface))
                                    //new Action(ret => Navigator.MoveTo(WaterSurface))
                                    
                                )),

                        new Decorator(ret => PoolFisherSettings.Instance.FlyingMountID != 0 && WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).IsValid,
                            new Sequence(
                                new Action(ret => WoWMovement.MoveStop()),
                                new Action(ret => Logging.Write("{0} - Mounting {1}.", Helpers.TimeNow, WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name)),
                                new ActionSetActivity(spell => "Casting " + WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Name),
                                new Action(spell => WoWSpell.FromId(PoolFisherSettings.Instance.FlyingMountID).Cast()),
                                new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                                new Action(ret => Thread.Sleep(2000 + (Ping * 3))),
                                new Action(ret => StyxWoW.SleepForLagDuration())
                                    ))
                        )));
        }

        private Composite CreateCastBehaviour()
        {
            return new Sequence(
                new Action(ret => Logging.WriteDebug("{0} - Composit: CreateCastBehaviour", Helpers.TimeNow)),
                CreateMoveStopBehaviour(),
                new ActionSetActivity(spell => "Casting " + ((WoWSpell)spell).Name),
                new Action(spell => ((WoWSpell)spell).Cast()),
                new Wait(2, ret => StyxWoW.Me.IsCasting, new ActionIdle()),
                new Action(ret => StyxWoW.SleepForLagDuration()));
        }

        private Composite CreateMoveStopBehaviour()
        {
            return new Decorator(ret => StyxWoW.Me.IsMoving,
                new Sequence(
                    new Action(ret => Logging.WriteDebug("{0} - Composit: CreateMoveStopBehaviour", Helpers.TimeNow)),
                    new Action(ret => Logging.Write("{0} - IsMoving returned {1}. Stop moving!", Helpers.TimeNow, StyxWoW.Me.IsMoving)),
                    new Action(ret => WoWMovement.MoveStop()),
                    new Decorator(ret => !StyxWoW.Me.IsMoving,
                        new Action(delegate { return RunStatus.Success; }))));
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
