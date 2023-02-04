using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt.Effects;
using LanguageExt.Effects.Traits;
using static LanguageExt.Pipes.Proxy;
using static LanguageExt.Prelude;

namespace LanguageExt.Pipes
{
    /// <summary>
    /// Producers can only `yield`
    /// </summary>
    /// <remarks>
    ///       Upstream | Downstream
    ///           +---------+
    ///           |         |
    ///     Void <==       <== Unit
    ///           |         |
    ///     Unit ==>       ==> OUT
    ///           |    |    |
    ///           +----|----+
    ///                |
    ///                A
    /// </remarks>
    public static class Producer
    {
        /// <summary>
        /// Monad return / pure
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> Pure<RT, OUT, R>(R value) where RT : struct, HasCancel<RT> =>
            new Pure<RT, Void, Unit, Unit, OUT, R>(value).ToProducer();
        
        /// <summary>
        /// Send a value downstream (whilst in a producer)
        /// </summary>
        /// <remarks>
        /// This is the simpler version (fewer generic arguments required) of `yield` that works
        /// for producers. 
        /// </remarks>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, Unit> yield<RT, OUT>(OUT value) where RT : struct, HasCancel<RT> =>
            respond<RT, Void, Unit, Unit, OUT>(value).ToProducer();

        [Pure, MethodImpl(mops)]
        internal static Producer<RT, X, Unit> yieldAll<RT, X>(EnumerateData<X> xs)
            where RT : struct, HasCancel<RT> =>
            new Enumerate<RT, Void, Unit, Unit, X, X, Unit>(
                xs, 
                yield<RT, X>,
                Pure<RT, X, Unit>).ToProducer();

        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, Unit> yieldAll<RT, X>(IEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            yieldAll<RT, X>(new EnumerateEnumerable<X>(xs));
        
        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, Unit> yieldAll<RT, X>(IAsyncEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            yieldAll<RT, X>(new EnumerateAsyncEnumerable<X>(xs));
        
        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, Unit> yieldAll<RT, X>(IObservable<X> xs)
            where RT : struct, HasCancel<RT> =>
            yieldAll<RT, X>(new EnumerateObservable<X>(xs));
        
        /*
        [Pure, MethodImpl(mops)]
        internal static Producer<RT, OUT, X> enumerate<RT, OUT, X>(EnumerateData<X> xs)
            where RT : struct, HasCancel<RT> =>
            new Enumerate<RT, Void, Unit, Unit, OUT, X, X>(xs, Pure<RT, OUT, X>).ToProducer();
        
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, X> enumerate<RT, OUT, X>(IEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, OUT, X>(new EnumerateEnumerable<X>(xs));
        
        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, X> enumerate2<RT, X>(IEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, X, X>(new EnumerateEnumerable<X>(xs));

        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, X> enumerate<RT, OUT, X>(IAsyncEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, OUT, X>(new EnumerateAsyncEnumerable<X>(xs));

        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, X> enumerate2<RT, X>(IAsyncEnumerable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, X, X>(new EnumerateAsyncEnumerable<X>(xs));

        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, X> observe<RT, OUT, X>(IObservable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, OUT, X>(new EnumerateObservable<X>(xs));
     
        [Pure, MethodImpl(mops)]
        public static Producer<RT, X, X> observe2<RT, X>(IObservable<X> xs)
            where RT : struct, HasCancel<RT> =>
            enumerate<RT, X, X>(new EnumerateObservable<X>(xs));
            */

        
        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, R> repeatM<RT, A, R>(Aff<RT, A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, R>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, Unit> repeatM<RT, A>(Aff<RT, A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, Unit>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, R> repeatM<RT, A, R>(Eff<RT, A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, R>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, Unit> repeatM<RT, A>(Eff<RT, A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, Unit>());
        
        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, R> repeatM<RT, A, R>(Aff<A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, R>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, Unit> repeatM<RT, A>(Aff<A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, Unit>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, R> repeatM<RT, A, R>(Eff<A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, R>());

        /// <summary>
        /// Repeat a monadic action indefinitely, yielding each result
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, A, Unit> repeatM<RT, A>(Eff<A> ma) where RT : struct, HasCancel<RT> =>
            Proxy.compose(lift<RT, A, A>(ma), Proxy.cat<RT, A, Unit>());

        
        /// <summary>
        /// Lift the IO monad into the Producer monad transformer (a specialism of the Proxy monad transformer)
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> lift<RT, OUT, R>(Eff<R> ma) where RT : struct, HasCancel<RT> =>
            lift<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift the IO monad into the Producer monad transformer (a specialism of the Proxy monad transformer)
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> lift<RT, OUT, R>(Aff<R> ma) where RT : struct, HasCancel<RT> =>
            lift<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift the IO monad into the Producer monad transformer (a specialism of the Proxy monad transformer)
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> lift<RT, OUT, R>(Eff<RT, R> ma) where RT : struct, HasCancel<RT> =>
            lift<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift the IO monad into the Producer monad transformer (a specialism of the Proxy monad transformer)
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> lift<RT, OUT, R>(Aff<RT, R> ma) where RT : struct, HasCancel<RT> =>
            lift<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();
        
        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Aff<R> ma) 
            where RT : struct, HasCancel<RT>
            where R : IDisposable =>
            use<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Eff<R> ma) 
            where RT : struct, HasCancel<RT>
            where R : IDisposable =>
            use<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Aff<RT, R> ma) 
            where RT : struct, HasCancel<RT> 
            where R : IDisposable =>
            use<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Eff<RT, R> ma) 
            where RT : struct, HasCancel<RT> 
            where R : IDisposable =>
            use<RT, Void, Unit, Unit, OUT, R>(ma).ToProducer();
        
        
        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Aff<R> ma, Func<R, Unit> dispose) 
            where RT : struct, HasCancel<RT> =>
            use<RT, Void, Unit, Unit, OUT, R>(ma, dispose).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Eff<R> ma, Func<R, Unit> dispose) 
            where RT : struct, HasCancel<RT> =>
            use<RT, Void, Unit, Unit, OUT, R>(ma, dispose).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Aff<RT, R> ma, Func<R, Unit> dispose) 
            where RT : struct, HasCancel<RT> =>
            use<RT, Void, Unit, Unit, OUT, R>(ma, dispose).ToProducer();

        /// <summary>
        /// Lift am IO monad into the `Proxy` monad transformer
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, R> use<RT, OUT, R>(Eff<RT, R> ma, Func<R, Unit> dispose) 
            where RT : struct, HasCancel<RT> =>
            use<RT, Void, Unit, Unit, OUT, R>(ma, dispose).ToProducer();        

        /// <summary>
        /// Release a previously used resource
        /// </summary>
        [Pure, MethodImpl(mops)]
        public static Producer<RT, OUT, Unit> release<RT, OUT, R>(R dispose) 
            where RT : struct, HasCancel<RT> =>
            Proxy.release<RT, Void, Unit, Unit, OUT, R>(dispose).ToProducer();
 
        /// <summary>
        /// Folds values coming down-stream, when the predicate returns true the folded value is yielded 
        /// </summary>
        /// <param name="Initial">Initial state</param>
        /// <param name="Fold">Fold operation</param>
        /// <param name="UntilValue">Predicate</param>
        /// <returns>A pipe that folds</returns>
        public static Producer<RT, S, Unit> FoldUntil<RT, S, A>(this Producer<RT, S, A> ma, S Initial, Func<S, A, S> Fold, Func<A, bool> UntilValue)
            where RT : struct, HasCancel<RT>
        {
            var state = Initial;
            return ma.Bind(
                x =>
                {
                    if (UntilValue(x))
                    {
                        var nstate = state;
                        state = Initial;
                        return yield<RT, S>(nstate);
                    }
                    else
                    {
                        state = Fold(state, x);
                        return Pure<RT, S, Unit>(unit);
                    }
                });
        }
 
        /// <summary>
        /// Folds values coming down-stream, when the predicate returns true the folded value is yielded 
        /// </summary>
        /// <param name="Initial">Initial state</param>
        /// <param name="Fold">Fold operation</param>
        /// <param name="UntilValue">Predicate</param>
        /// <returns>A pipe that folds</returns>
        public static Producer<RT, S, Unit> FoldWhile<RT, S, A>(this Producer<RT, S, A> ma, S Initial, Func<S, A, S> Fold, Func<A, bool> WhileValue)
            where RT : struct, HasCancel<RT>
        {
            var state = Initial;
            return ma.Bind(
                x =>
                {
                    if (WhileValue(x))
                    {
                        state = Fold(state, x);
                        return Pure<RT, S, Unit>(unit);
                    }
                    else
                    {
                        var nstate = state;
                        state = Initial;
                        return yield<RT, S>(nstate);
                    }
                });
        }
         
        /// <summary>
        /// Folds values coming down-stream, when the predicate returns true the folded value is yielded 
        /// </summary>
        /// <param name="Initial">Initial state</param>
        /// <param name="Fold">Fold operation</param>
        /// <param name="UntilValue">Predicate</param>
        /// <returns>A pipe that folds</returns>
        public static Producer<RT, S, Unit> FoldUntil<RT, S, A>(this Producer<RT, S, A> ma, S Initial, Func<S, A, S> Fold, Func<S, bool> UntilState)
            where RT : struct, HasCancel<RT>
        {
            var state = Initial;
            return ma.Bind(
                x =>
                {
                    state = Fold(state, x);
                    if (UntilState(state))
                    {
                        var nstate = state;
                        state = Initial;
                        return yield<RT, S>(nstate);
                    }
                    else
                    {
                        return Pure<RT, S, Unit>(unit);
                    }
                });
        }
 
        /// <summary>
        /// Folds values coming down-stream, when the predicate returns true the folded value is yielded 
        /// </summary>
        /// <param name="Initial">Initial state</param>
        /// <param name="Fold">Fold operation</param>
        /// <param name="UntilValue">Predicate</param>
        /// <returns>A pipe that folds</returns>
        public static Producer<RT, S, Unit> FoldWhile<RT, S, A>(this Producer<RT, S, A> ma, S Initial, Func<S, A, S> Fold, Func<S, bool> WhileState)
            where RT : struct, HasCancel<RT>
        {
            var state = Initial;
            return ma.Bind(
                x =>
                {
                    state = Fold(state, x);
                    if (WhileState(state))
                    {
                        return Pure<RT, S, Unit>(unit);
                    }
                    else
                    {
                        var nstate = state;
                        state = Initial;
                        return yield<RT, S>(nstate);
                    }
                });
        }

 
        
        
        /// <summary>
        /// Merge a sequence of producers into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component producers have completed</remarks>
        /// <param name="ms">Sequence of producers to merge</param>
        /// <returns>Merged producers</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(Seq<Producer<RT, OUT, A>> ms) where RT : struct, HasCancel<RT>
        {
            return from e in lift<RT, OUT, RT>(runtime<RT>())
                   from x in yieldAll<RT, OUT>(go(e))
                   select default(A);
            
            async IAsyncEnumerable<OUT> go(RT env)
            {
                var queue   = new ConcurrentQueue<OUT>();
                var wait    = new AutoResetEvent(true);
                var running = true;

                // Create a consumer that simply awaits a value and then puts it in our concurrent queue 
                // to be re-yielded
                var enqueue = Consumer.awaiting<RT, OUT>()
                                      .Map(x =>
                                           {
                                               queue.Enqueue(x);
                                               wait.Set();
                                               return default(A);
                                           })
                                      .ToConsumer();

                // Compose the enqueue Consumer with the Producer to create an Effect that can be run 
                var mme = ms.Map(m => m | enqueue).Strict();

                // Run the producing effects
                // We should NOT be awaiting these 
                var mmt = mme.Map(m => m.RunEffect().Run(env).AsTask());

                // When all tasks are done, we're done
                // We should NOT be awaiting this 
                #pragma warning disable CS4014             
                Task.WhenAll(mmt.ToArray())
                    .Iter(_ =>
                          {
                              running = false;
                              wait.Set();
                          });
                #pragma warning restore CS4014                

                do
                {
                    await wait.WaitOneAsync(env.CancellationToken).ConfigureAwait(false);
                    while (queue.TryDequeue(out var item))
                    {
                        yield return item;
                    }
                }
                // Keep processing until we're cancelled or all of the Producers have stopped producing
                while (running && !env.CancellationToken.IsCancellationRequested);
            }
        }
        
        /// <summary>
        /// Merge an array of queues into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component queues have completed</remarks>
        /// <param name="ms">Sequence of queues to merge</param>
        /// <returns>Queues merged into a single producer</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(params Queue<RT, OUT, A>[] ms) where RT : struct, HasCancel<RT> =>
            merge(toSeq(ms.Map(m => (Producer<RT, OUT, A>)m)));
 
        /// <summary>
        /// Merge an array of producers into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component producers have completed</remarks>
        /// <param name="ms">Sequence of producers to merge</param>
        /// <returns>Merged producers</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(params Producer<RT, OUT, A>[] ms) where RT : struct, HasCancel<RT> =>
            merge(toSeq(ms));
 
        /// <summary>
        /// Merge an array of producers into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component producers have completed</remarks>
        /// <param name="ms">Sequence of producers to merge</param>
        /// <returns>Merged producers</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(params Proxy<RT, Void, Unit, Unit, OUT, A>[] ms) where RT : struct, HasCancel<RT> =>
            merge(toSeq(ms).Map(m => m.ToProducer()));
 
        /// <summary>
        /// Merge a sequence of queues into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component queues have completed</remarks>
        /// <param name="ms">Sequence of queues to merge</param>
        /// <returns>Queues merged into a single producer</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(Seq<Queue<RT, OUT, A>> ms) where RT : struct, HasCancel<RT> =>
            merge(ms.Map(m => (Producer<RT, OUT, A>)m));
 
        /// <summary>
        /// Merge a sequence of producers into a single producer
        /// </summary>
        /// <remarks>The merged producer completes when all component producers have completed</remarks>
        /// <param name="ms">Sequence of producers to merge</param>
        /// <returns>Merged producers</returns>
        public static Producer<RT, OUT, A> merge<RT, OUT, A>(Seq<Proxy<RT, Void, Unit, Unit, OUT, A>> ms) where RT : struct, HasCancel<RT> =>
            merge(ms.Map(m => m.ToProducer()));
    }
}
