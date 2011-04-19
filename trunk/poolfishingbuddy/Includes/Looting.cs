using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

using Styx.WoWInternals;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.LootFrame;
using Styx.WoWInternals.WoWObjects;

namespace PoolFishingBuddy
{
    class Looting
    {
        static public Dictionary<string, int> Cache = new Dictionary<string, int>();
        static Stopwatch lootTimer = new Stopwatch();
        static LootFrame _current = new LootFrame();

        static public void interact()
        {
            Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Bobber is bobbing!", Helpers.TimeNow);
            WoWGameObject Bobber = Helpers.FishingBobber;

            Bobber.Interact();
            Bobber = null;

            Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Interact!", Helpers.TimeNow);

            lootTimer.Reset();
            lootTimer.Start();

            while (!LootFrame.Instance.IsVisible)
            {
                Thread.Sleep(50);

                
                if (lootTimer.ElapsedMilliseconds > 5000)
                {
                    Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Loot timer elapsed!", Helpers.TimeNow);
                    PoolFisher.castAttempts = 0;
                    return;
                }
            }

            if (LootFrame.Instance.IsVisible)
            {
                _current = LootFrame.Instance;
                track(_current);

                Thread.Sleep((PoolFisher.Ping * 2) + 200);

                Lua.DoString("for i=1,GetNumLootItems() do ConfirmLootSlot(i) LootSlot(i) end");
                // wait for lootframe to close
                while (LootFrame.Instance != null && LootFrame.Instance.IsVisible)
                {
                    Thread.Sleep((PoolFisher.Ping * 2) + 200);
                }

                Logging.Write(System.Drawing.Color.DarkCyan, "{0} - Looting done!", Helpers.TimeNow);
                PoolFisher.castAttempts = 0;
                PoolFisher.catches++;
            }
        }

        static public void track(LootFrame l)
        {
            for (int i = 0; i < l.LootItems; i++)
            {
                var info = l.LootInfo(i);

                if (info.LootQuantity != 0)
                {
                    if (Cache.ContainsKey(info.LootName))
                    {
                        Cache[info.LootName] = Cache[info.LootName] + info.LootQuantity;
                    }
                    else
                    {
                        Cache.Add(info.LootName, info.LootQuantity);
                    }

                    Logging.Write(System.Drawing.Color.DarkCyan, "---------------- Loots: ----------------", PoolFisher.catches);
                    foreach (KeyValuePair<string, int> pair in Looting.Cache)
                    {
                        Logging.Write(System.Drawing.Color.Black, "{0}x [{1}]", pair.Value, pair.Key);
                    }
                    Logging.Write(System.Drawing.Color.DarkCyan, "----------------------------------------", PoolFisher.catches);
                }
            }
        }


    }
}
