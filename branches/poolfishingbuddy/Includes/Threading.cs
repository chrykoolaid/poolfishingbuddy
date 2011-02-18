using System.Collections.Generic;
using System.Threading;

using Styx.Logic.Pathing;

namespace PoolFishingBuddy.Threading
{
    class CalcState
    {
        public CalcState(ManualResetEvent reset, WoWPoint input)
        {
            Reset = reset;
            Input = input;
        }
        public ManualResetEvent Reset { get; private set; }
        public WoWPoint Input { get; set; }
    }

}