using System;
using System.Collections.Generic;
using TreeSharp;

namespace PoolFishingBuddy
{
    class DecoratorSetContext<T> : Decorator
    {
        public delegate T DecoratorContextDelegate();
        public delegate bool CanRunDecoratorContextDelegate(T context);
        protected DecoratorContextDelegate newcontext { get; private set; }
        protected CanRunDecoratorContextDelegate runner { get; private set; }
        public DecoratorSetContext(DecoratorContextDelegate context, CanRunDecoratorContextDelegate func, Composite decorated)
            : base(decorated)
        {
            newcontext = context;
            runner = func;
        }
        protected override bool CanRun(object context)
        {
            if (runner == null || newcontext == null)
            {
                return false;
            }
            T obj = default(T);
            try
            {
                obj = newcontext();
            }
            catch (Exception) { }
            return runner(obj);
        }
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            T obj = default(T);
            try
            {
                obj = newcontext();
            }
            catch (Exception) { }
            return base.Execute(obj);
        }
    }
}

