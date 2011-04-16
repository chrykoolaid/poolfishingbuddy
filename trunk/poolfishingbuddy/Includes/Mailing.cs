using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Styx;
using Styx.Helpers;

using Styx.Logic;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Profiles;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.MailBox;

using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

using Styx.Logic.Inventory.Frames.Taxi;

namespace PoolFishingBuddy
{
    class Mailing
    {
        static public List<WoWPoint> Mailboxes = new List<WoWPoint>();
        static public WoWPoint closestMailbox = new WoWPoint();

        static public bool isDone = false;

        static public bool hasMailbox()
        {
            Mailboxes.Clear();
            Mailboxes = ProfileManager.CurrentProfile.MailboxManager.AllMailboxes.ConvertAll<WoWPoint>(mb => mb.Location);
            Mailboxes.OrderBy(t => t.Distance(StyxWoW.Me.Location)).ToList();

            if (Mailboxes[0] != WoWPoint.Empty)
            {
                closestMailbox = Mailboxes[0];
                return true;
            }
            Logging.Write(System.Drawing.Color.Red, "{0} - There's no Mailbox inside the Profile you are using. Mailing disabled now!", Helpers.TimeNow);
            PoolFisherSettings.Instance.Load();
            PoolFisherSettings.Instance.ShouldMail = false;
            PoolFisherSettings.Instance.Save();
            return false;
        }

        public static void InteractWithMailbox()
        {
            ObjectManager.Update();
            List<WoWGameObject> Objects = ObjectManager.GetObjectsOfType<WoWGameObject>();
            List<WoWItem> itemList = StyxWoW.Me.BagItems;
            List<WoWItem> tempList = new List<WoWItem>();

            foreach (WoWGameObject o in Objects)
            {
                if (o.SubType == WoWGameObjectType.Mailbox && StyxWoW.Me.Location.Distance(o.Location) < o.InteractRange)
                {
                    Logging.Write("Name: {0}, Distance: {1}.", o.Name, StyxWoW.Me.Location.Distance(o.Location));
                    o.Interact();

                    while (MailFrame.Instance == null || !MailFrame.Instance.IsVisible)
                    {
                        Thread.Sleep((PoolFisher.Ping * 2) + 50);
                    }

                    foreach (WoWItem item in itemList)
                    {
                            if (item.ItemInfo.ItemClass == WoWItemClass.TradeGoods && (item.ItemInfo.SubClassId == 8 || item.ItemInfo.SubClassId == 10))
                            {
                                tempList.Add(item);
                            }
                    }

                    if (tempList == null || tempList.Count == 0)
                    {
                            isDone = true;
                    }
                    using (new FrameLock())
                    {
                            MailFrame.Instance.SwitchToSendMailTab();
                            foreach (WoWItem item in tempList)
                            {
                                item.UseContainerItem();
                                //Thread.Sleep((PoolFisher.Ping * 2) + 500);
                            }
                            Lua.DoString(string.Format("SendMail ('{0}',' ','');SendMailMailButton:Click();", PoolFisherSettings.Instance.MailRecipient));
                            Thread.Sleep((PoolFisher.Ping * 2) + 2000);
                    }
                    if (isDone)
                    {
                            Lua.DoString("CloseMail()");
                            isDone = false;
                            PoolFisher.need2Mail = false;
                    }
                }
            }
        }
    }
}
