﻿using System;
using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;
using static LanguageExt.TypeClass;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using LanguageExt.TypeClasses;
using System.Runtime.CompilerServices;
using LanguageExt.ClassInstances;
using LanguageExt.Common;

/// <summary>
/// Extension methods for the TryOptionAsync monad
/// </summary>
public static class TryOptionAsyncExtensions
{
    /// <summary>
    /// Use for pattern-matching the case of the target
    /// </summary>
    /// <remarks>
    ///     TryOptionAsync Some = result is A
    ///     TryOptionAsync None = result is null
    ///     TryOptionAsync Fail = result is LanguageExt.Common.Error
    /// </remarks>
    [Pure]
    public static async ValueTask<object> Case<A>(this TryOptionAsync<A> ma)
    {
        if (ma == null) return Errors.Bottom;
        var res = await ma.Try().ConfigureAwait(false);
        return res.IsSome
            ? res.Value.Value
            : res.IsNone
                ? null
                : Error.New(res.Exception);
    }

    /// <summary>
    /// Memoize the computation so that it's only run once
    /// </summary>
    public static TryOptionAsync<A> Memo<A>(this TryOptionAsync<A> ma)
    {
        var run = false;
        var result = OptionalResult<A>.Bottom;
        return new TryOptionAsync<A>(async () =>
        {
            if (run) return result;
            var ra = await ma.Try().ConfigureAwait(false);
            if (ra.IsSome || ra.IsNone)
            {
                run = true;
                result = ra;
            }
            return ra;
        });
    }
        
    /// <summary>
    /// If the TryOptionAsync fails, retry `amount` times
    /// </summary>
    /// <param name="ma">TryOptionAsync</param>
    /// <param name="amount">Amount of retries</param>
    /// <typeparam name="A">Type of bound value</typeparam>
    /// <returns>TryOptionAsync</returns>
    public static TryOptionAsync<A> Retry<A>(TryOptionAsync<A> ma, int amount = 3) => async () =>
    {
        while (true)
        {
            var ra = await ma.Try().ConfigureAwait(false);
            if (ra.IsSome || ra.IsNone)
            {
                return ra;
            }

            amount--;
            if (amount <= 0) return ra;
        }
    };
        
    /// <summary>
    /// If the TryOptionAsync fails, retry `amount` times whilst backing off `backOffMilliSeconds`
    /// </summary>
    /// <param name="ma">TryOptionAsync</param>
    /// <param name="backOffMilliSeconds">Amount of time in milliseconds to back-off upon failure.  The back-off
    /// time is added to itself on each retry.  i.e. 100, 200, 400, 800, 1600...</param>
    /// <param name="amount">Amount of retries</param>
    /// <typeparam name="A">Type of bound value</typeparam>
    /// <returns>TryOptionAsync</returns>
    public static TryOptionAsync<A> RetryBackOff<A>(TryOptionAsync<A> ma, int backOffMilliSeconds, int amount = 3) => async () =>
    {
        while (true)
        {
            var ra = await ma.Try().ConfigureAwait(false);
            if (ra.IsSome || ra.IsNone)
            {
                return ra;
            }
            amount--;
            if (amount <= 0) return ra;
            await Task.Delay(backOffMilliSeconds).ConfigureAwait(false);
            backOffMilliSeconds += backOffMilliSeconds;
        }
    };      

    /// <summary>
    /// Forces evaluation of the lazy TryOptionAsync
    /// </summary>
    /// <typeparam name="A">Bound value type</typeparam>
    /// <param name="ma">Computation to evaluate</param>
    /// <returns>The TryOption with the computation executed</returns>
    public static TryOptionAsync<A> Strict<A>(this TryOptionAsync<A> ma)
    {
        var res = ma.Try();
        return () => res;
    }
    
    /// <summary>
    /// Custom awaiter that turns an TryOptionAsync into an TryOption
    /// </summary>
    public static TaskAwaiter<TryOption<A>> GetAwaiter<A>(this TryOptionAsync<A> ma) =>
        ma.Match(
            Some: TryOption,
            None: () => TryOption<A>(None),
            Fail: TryOption<A>).GetAwaiter();

    /// <summary>
    /// Test if the TryOptionAsync is in a success state
    /// </summary>
    /// <typeparam name="A">Bound value type</typeparam>
    /// <param name="ma">Computation to evaluate</param>
    /// <returns>True if computation has succeeded</returns>
    public static async Task<bool> IsSome<A>(this TryOptionAsync<A> ma) =>
        (await ma.Try().ConfigureAwait(false)).IsSome;

    /// <summary>
    /// Test if the TryOptionAsync is in a Fail state
    /// </summary>
    /// <typeparam name="A">Bound value type</typeparam>
    /// <param name="ma">Computation to evaluate</param>
    /// <returns>True if computation is faulted</returns>
    public static async Task<bool> IsFail<A>(this TryOptionAsync<A> ma) =>
        (await ma.Try().ConfigureAwait(false)).IsFaulted;

    /// <summary>
    /// Test if the TryOptionAsync is in a None or Fail state
    /// </summary>
    /// <typeparam name="A">Bound value type</typeparam>
    /// <param name="ma">Computation to evaluate</param>
    /// <returns>True if computation is faulted</returns>
    public static async Task<bool> IsNoneOrFail<A>(this TryOptionAsync<A> ma) =>
        (await ma.Try().ConfigureAwait(false)).IsFaultedOrNone;

    /// <summary>
    /// Test if the TryOptionAsync is in a None state
    /// </summary>
    /// <typeparam name="A">Bound value type</typeparam>
    /// <param name="ma">Computation to evaluate</param>
    /// <returns>True if computation is faulted</returns>
    public static async Task<bool> IsNone<A>(this TryOptionAsync<A> ma) =>
        (await ma.Try().ConfigureAwait(false)).IsNone;

    /// <summary>
    /// Invoke a delegate if the computation returns a value successfully
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<Unit> IfSome<A>(this TryOptionAsync<A> self, Action<A> Some)
    {
        if (isnull(self)) throw new ArgumentNullException(nameof(self));
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));

        try
        {
            var res = await self.Try().ConfigureAwait(false);
            if (!res.IsFaulted)
            {
                Some(res.Value.Value);
            }
            return unit;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return unit;
        }
    }

    /// <summary>
    /// Return a default value if the computation fails or completes successfully 
    /// but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, A defaultValue) =>
        self.IfNoneOrFail(() => defaultValue);

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<Unit> IfNoneOrFail<A>(this TryOptionAsync<A> self, Action None)
    {
        await self.IfNoneOrFail(() => { None(); return default(A); } ).ConfigureAwait(false);
        return unit;
    }

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<A> None) =>
        self.IfNoneOrFail(None, _ => None());

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<Task<A>> None)
    {
        Task<A> fail(Exception _) => None();
        return self.IfNoneOrFail(None, fail);
    }

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<A> None, Func<Exception, A> Fail)
    {
        if (isnull(self)) throw new ArgumentNullException(nameof(self));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        try
        {
            var res = await self.Try().ConfigureAwait(false);
            return res.IsFaulted 
                ? Fail(res.Exception)
                : res.Value.IsNone
                    ? None()
                    : res.Value.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return None();
        }
    }

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<Task<A>> None, Func<Exception, A> Fail)
    {
        if (isnull(self)) throw new ArgumentNullException(nameof(self));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        try
        {
            var res = await self.Try().ConfigureAwait(false);
            return res.IsFaulted
                ? Fail(res.Exception)
                : res.Value.IsNone
                    ? await None().ConfigureAwait(false)
                    : res.Value.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return await None().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<A> None, Func<Exception, Task<A>> Fail)
    {
        if (isnull(self)) throw new ArgumentNullException(nameof(self));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        try
        {
            var res = await self.Try().ConfigureAwait(false);
            return res.IsFaulted
                ? await Fail(res.Exception).ConfigureAwait(false)
                : res.Value.IsNone
                    ? None()
                    : res.Value.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return None();
        }
    }

    /// <summary>
    /// Invoke a delegate if the computation fails or completes successfully but returns None
    /// </summary>
    /// <param name="Some">Delegate to invoke if successful</param>
    public static async Task<A> IfNoneOrFail<A>(this TryOptionAsync<A> self, Func<Task<A>> None, Func<Exception, Task<A>> Fail)
    {
        if (isnull(self)) throw new ArgumentNullException(nameof(self));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        try
        {
            var res = await self.Try().ConfigureAwait(false);
            return res.IsFaulted
                ? await Fail(res.Exception).ConfigureAwait(false)
                : res.Value.IsNone
                    ? await None().ConfigureAwait(false)
                    : res.Value.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return await None().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Provides a fluent exception matching interface which is invoked
    /// when the computation fails.
    /// </summary>
    /// <returns>Fluent exception matcher</returns>
    [Pure]
    public static ExceptionMatchOptionalAsync<A> IfFail<A>(this TryOptionAsync<A> self) =>
        new ExceptionMatchOptionalAsync<A>(self.Try());

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Value to use if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static Task<R> Match<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, R Fail) =>
        self.Match(a => Succ(a), () => Fail).Map(Check.NullReturn);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Value to use if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static Task<R> MatchUnsafe<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, R Fail) =>
        self.Match(a => Succ(a), () => Fail);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<Unit> Match<A>(this TryOptionAsync<A> self, Action<A> Succ, Action Fail) =>
        await Match(self, a => { Succ(a); return unit; }, () => { Fail(); return unit; }).ConfigureAwait(false);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<Unit> MatchAsync<A>(this TryOptionAsync<A> self, Func<A, Task> SuccAsync, Action Fail) =>
        await MatchAsync( self,
            async a => { await SuccAsync(a); return unit; }, 
            () => { Fail(); return unit; }
        ).ConfigureAwait(false);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<Unit> MatchAsync<A>(this TryOptionAsync<A> self, Action<A> Succ, Func<Task> FailAsync) =>
        await MatchAsync(self, a => { Succ(a); return unit; }, async () => { await FailAsync(); return unit; }).ConfigureAwait(false);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<Unit> MatchAsync<A>(this TryOptionAsync<A> self, Func<A, Task> SuccAsync, Func<Task> FailAsync) =>
        await MatchAsync(self, async a => { await SuccAsync(a); return unit; }, async () => { await FailAsync(); return unit; }).ConfigureAwait(false);

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> Match<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, Func<R> Fail)
    {
        if (isnull(Succ)) throw new ArgumentNullException(nameof(Succ));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Check.NullReturn(Fail())
            : Check.NullReturn(Succ(res.Value.Value));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafe<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, Func<R> Fail)
    {
        if (isnull(Succ)) throw new ArgumentNullException(nameof(Succ));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Fail()
            : Succ(res.Value.Value);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafe<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, Func<R> None, Func<Exception, R> Fail)
    {
        if (isnull(Succ)) throw new ArgumentNullException(nameof(Succ));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.IsNone
                ? None()
                : Succ(res.Value.Value);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, Func<Task<R>> FailAsync)
    {
        if (isnull(Succ)) throw new ArgumentNullException(nameof(Succ));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Check.NullReturn(await FailAsync().ConfigureAwait(false))
            : Check.NullReturn(Succ(res.Value.Value));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Succ">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Succ, Func<Task<R>> FailAsync)
    {
        if (isnull(Succ)) throw new ArgumentNullException(nameof(Succ));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? await FailAsync().ConfigureAwait(false)
            : Succ(res.Value.Value);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SuccAsync, Func<R> Fail)
    {
        if (isnull(SuccAsync)) throw new ArgumentNullException(nameof(SuccAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Check.NullReturn(Fail())
            : Check.NullReturn(await SuccAsync(res.Value.Value).ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SuccAsync, Func<R> Fail)
    {
        if (isnull(SuccAsync)) throw new ArgumentNullException(nameof(SuccAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Fail()
            : await SuccAsync(res.Value.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SuccAsync, Func<Task<R>> FailAsync)
    {
        if (isnull(SuccAsync)) throw new ArgumentNullException(nameof(SuccAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Check.NullReturn(await FailAsync().ConfigureAwait(false))
            : Check.NullReturn(await SuccAsync(res.Value.Value).ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SuccAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SuccAsync, Func<Task<R>> FailAsync)
    {
        if (isnull(SuccAsync)) throw new ArgumentNullException(nameof(SuccAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? await FailAsync().ConfigureAwait(false)
            : await SuccAsync(res.Value.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> Match<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, Func<Exception, R> Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Check.NullReturn(Fail(res.Exception))
            : res.Value.Match(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUntypedUnsafe<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, Func<Exception, R> Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.MatchUnsafe(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, Func<Exception, R> Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, Func<Exception, R> Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, Func<Exception, R> Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None());
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, Func<Exception, R> Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None();
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, Func<Exception, R> Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, Func<Exception, R> Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Check.NullReturn(await FailAsync(res.Exception).ConfigureAwait(false))
            : res.Value.Match(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.MatchUnsafe(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None());
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None();
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="FailAsync">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, Func<Exception, Task<R>> FailAsync)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(FailAsync)) throw new ArgumentNullException(nameof(FailAsync));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await FailAsync(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> Match<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, R Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Check.NullReturn(Fail)
            : res.Value.Match(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafe<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<R> None, R Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail
            : res.Value.MatchUnsafe(Some, None);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, R Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, R> Some, Func<Task<R>> NoneAsync, R Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, R Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None());
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<R> None, R Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : None();
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, R Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return Check.NullReturn(res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="SomeAsync">Delegate to invoke if the computation completes successfully</param>
    /// <param name="NoneAsync">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    [Pure]
    public static async Task<R> MatchUnsafeAsync<A, R>(this TryOptionAsync<A> self, Func<A, Task<R>> SomeAsync, Func<Task<R>> NoneAsync, R Fail)
    {
        if (isnull(SomeAsync)) throw new ArgumentNullException(nameof(SomeAsync));
        if (isnull(NoneAsync)) throw new ArgumentNullException(nameof(NoneAsync));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail
            : res.Value.IsSome
                ? await SomeAsync(res.Value.Value).ConfigureAwait(false)
                : await NoneAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Pattern matches the three possible states of the computation
    /// </summary>
    /// <typeparam name="R">Type of the resulting bound value</typeparam>
    /// <param name="Some">Delegate to invoke if the computation completes successfully</param>
    /// <param name="None">Delegate to invoke if the computation completes successfully but returns no value</param>
    /// <param name="Fail">Delegate to invoke if the computation fails</param>
    /// <returns>The result of either the Succ, None, or Fail delegate</returns>
    public static async Task<Unit> Match<A>(this TryOptionAsync<A> self, Action<A> Some, Action None, Action<Exception> Fail)
    {
        if (isnull(Some)) throw new ArgumentNullException(nameof(Some));
        if (isnull(None)) throw new ArgumentNullException(nameof(None));
        if (isnull(Fail)) throw new ArgumentNullException(nameof(Fail));

        var res = await self.Try().ConfigureAwait(false);

        if (res.IsFaulted)
            Fail(res.Exception);
        else
            res.Value.Match(Some, None);

        return unit;
    }

    [Pure]
    public static Task<Validation<Exception, Option<A>>> ToValidation<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: v => Success<Exception, Option<A>>(Option<A>.Some(v)),
            None: () => Success<Exception, Option<A>>(Option<A>.None),
            Fail: e => Fail<Exception, Option<A>>(e));
    
    /// <summary>
    /// Convert the structure to an Aff
    /// </summary>
    /// <returns>An Aff representation of the structure</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aff<A> ToAff<A>(this TryOptionAsync<A> ma) =>
        AffMaybe(async () => await
            ma.Match(Some: Fin<A>.Succ,
                     None: () => Fin<A>.Fail(Errors.None),
                     Fail: e => Fin<A>.Fail(e)));
    
    /// <summary>
    /// Convert the structure to an Aff
    /// </summary>
    /// <returns>An Aff representation of the structure</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aff<A> ToAff<A>(this TryOptionAsync<A> ma, Error None) =>
        AffMaybe(async () => await
            ma.Match(Some: Fin<A>.Succ,
                     None: () => Fin<A>.Fail(None),
                     Fail: e => Fin<A>.Fail(e)));
    
    /// <summary>
    /// Convert the structure to an Aff
    /// </summary>
    /// <returns>An Aff representation of the structure</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aff<A> ToAff<A>(this TryOptionAsync<A> ma, Func<Error> None) =>
        AffMaybe(async () => await
            ma.Match(Some: Fin<A>.Succ,
                     None: () => Fin<A>.Fail(None()),
                     Fail: e => Fin<A>.Fail(e)));
    
    /// <summary>
    /// Convert the structure to an Aff
    /// </summary>
    /// <returns>An Aff representation of the structure</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aff<A> ToAff<A>(this TryOptionAsync<A> ma, A None) =>
        AffMaybe(async () => await
             ma.Match(Some: Fin<A>.Succ,
                      None: () => Fin<A>.Succ(None),
                      Fail: e => Fin<A>.Fail(e)));
    
    /// <summary>
    /// Convert the structure to an Aff
    /// </summary>
    /// <returns>An Aff representation of the structure</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aff<A> ToAff<A>(this TryOptionAsync<A> ma, Func<A> None) =>
        AffMaybe(async () => await
            ma.Match(Some: Fin<A>.Succ,
                     None: () => Fin<A>.Succ(None()),
                     Fail: e => Fin<A>.Fail(e)));

    [Pure]
    public static Task<Option<A>> ToOption<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: Option<A>.Some,
            None: () => Option<A>.None,
            Fail: _  => Option<A>.None);

    [Pure]
    public static Task<OptionUnsafe<A>> ToOptionUnsafe<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: OptionUnsafe<A>.Some,
            None: () => OptionUnsafe<A>.None,
            Fail: _ => OptionUnsafe<A>.None);

    [Pure]
    public static Task<Either<Exception, Option<A>>> ToEither<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: v  => Either<Exception,Option<A>>.Right(v),
            None: () => Either<Exception,Option<A>>.Right(None),
            Fail: ex => Either<Exception, Option<A>>.Left(ex));

    [Pure]
    public static Task<Either<L, Option<A>>> ToEither<A, L>(this TryOptionAsync<A> self, Func<Exception, L> Fail) =>
        self.Match(
            Some: v => Either<L, Option<A>>.Right(v),
            None: () => Either<L, Option<A>>.Right(None),
            Fail: ex => Either<L, Option<A>>.Left(Fail(ex)));

    [Pure]
    public static Task<EitherUnsafe<Exception, Option<A>>> ToEitherUnsafe<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: v  => EitherUnsafe<Exception,Option<A>>.Right(v),
            None: () => EitherUnsafe<Exception,Option<A>>.Right(None),
            Fail: ex => EitherUnsafe<Exception, Option<A>>.Left(ex));

    [Pure]
    public static Task<EitherUnsafe<L, Option<A>>> ToEitherUnsafe<A, L>(this TryOptionAsync<A> self, Func<Exception, L> Fail) =>
        self.Match(
            Some: v => EitherUnsafe<L, Option<A>>.Right(v),
            None: () => EitherUnsafe<L, Option<A>>.Right(None),
            Fail: ex => EitherUnsafe<L, Option<A>>.Left(Fail(ex)));

    [Pure]
    public static TryAsync<Option<A>> ToTry<A>(this TryOptionAsync<A> self) => () =>
        self.Match(
            Some: x => new Result<Option<A>>(Option<A>.Some(x)),
            None: () => new Result<Option<A>>(Option<A>.None),
            Fail: ex => new Result<Option<A>>(ex));

    [Pure]
    public static TryAsync<A> ToTry<A>(this TryOptionAsync<A> self, Func<A> None) => () =>
        self.Match(
            Some: x => new Result<A>(x),
            None: () => new Result<A>(None()),
            Fail: ex => new Result<A>(ex));

    [Pure]
    public static async Task<A> IfFailThrow<A>(this TryOptionAsync<A> self)
    {
        try
        {
            var res = await self.Try().ConfigureAwait(false);
            if (res.IsFaulted) res.Exception.Rethrow();
            if (res.Value.IsNone) throw new ValueIsNoneException();
            return res.Value.Value;
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            throw;
        }
    }

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="select">Delegate to map the bound value</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> Select<A, B>(this TryOptionAsync<A> self, Func<A, B> select) =>
        Map(self, select);

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="select">Delegate to map the bound value</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> Select<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> select) =>
        MapAsync(self, select);


    /// <summary>
    /// Apply computation values to a computation function of arity 2
    /// </summary>
    /// <param name="self">computation function</param>
    /// <param name="arg1">computation argument</param>
    /// <param name="arg2">computation argument</param>
    /// <returns>Returns the result of applying the computation arguments to the computation function</returns>
    public static Task<Unit> Iter<A>(this TryOptionAsync<A> self, Action<A> action) =>
        IfSome(self, action);

    /// <summary>
    /// Counts the number of bound values.  
    /// </summary>
    /// <typeparam name="T">Type of the bound value</typeparam>
    /// <param name="self">computation</param>
    /// <returns>1 if the computation is successful, 0 otherwise.</returns>
    [Pure]
    public static Task<int> Count<A>(this TryOptionAsync<A> self) =>
        Map(self, _ => 1).IfNoneOrFail(0);

    /// <summary>
    /// Tests that a predicate holds for all values of the bound value T
    /// </summary>
    /// <typeparam name="T">Type of the bound value</typeparam>
    /// <param name="self">computation</param>
    /// <param name="pred">Predicate to test the bound value against</param>
    /// <returns>True if the predicate holds for the bound value, or if the computation
    /// fails.  False otherwise.</returns>
    [Pure]
    public static Task<bool> ForAll<A>(this TryOptionAsync<A> self, Func<A, bool> pred) =>
        Map(self, pred).IfNoneOrFail(true);

    /// <summary>
    /// Tests that a predicate holds for all values of the bound value T
    /// </summary>
    /// <typeparam name="T">Type of the bound value</typeparam>
    /// <param name="self">computation</param>
    /// <param name="pred">Predicate to test the bound value against</param>
    /// <returns>True if the predicate holds for the bound value, or if the computation
    /// fails.  False otherwise.</returns>
    [Pure]
    public static Task<bool> ForAllAsync<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> pred) =>
        MapAsync(self, pred).IfNoneOrFail(true);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="folder">Fold function</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> Fold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> folder) =>
        Map(self, v => folder(state, v)).IfNoneOrFail(state);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="folder">Fold function</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> FoldAsync<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> folder) =>
        MapAsync(self, v => folder(state, v)).IfNoneOrFail(state);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Succ">Fold function for Success</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> BiFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Succ, Func<S, S> Fail) =>
        BiMap(self,
            Succ: v  => Succ(state, v),
            Fail: () => Fail(state))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Succ">Fold function for Success</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> BiFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Succ, Func<S, Task<S>> Fail) =>
        BiMap(self,
            Succ: v  => Succ(state, v),
            Fail: () => Fail(state))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Succ">Fold function for Success</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> BiFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> Succ, Func<S, S> Fail) =>
        BiMap(self,
            Succ: v  => Succ(state, v),
            Fail: () => Fail(state))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds computation value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">computation to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Succ">Fold function for Success</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> BiFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> Succ, Func<S, Task<S>> Fail) =>
        BiMap(self,
            Succ: v  => Succ(state, v),
            Fail: () => Fail(state))
        .IfNoneOrFail(state);


    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Some, Func<S, S> None, Func<S, Exception, S> Fail) =>
        TriMap(self,
            Some: v  => Some(state, v),
            None: () => None(state),
            Fail: x  => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> Some, Func<S, S> None, Func<S, Exception, S> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Some, Func<S, Task<S>> None, Func<S, Exception, S> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Some, Func<S, S> None, Func<S, Exception, Task<S>> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> Some, Func<S, Task<S>> None, Func<S, Exception, S> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, S> Some, Func<S, Task<S>> None, Func<S, Exception, Task<S>> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Folds Try value into an S.
    /// https://en.wikipedia.org/wiki/Fold_(higher-order_function)
    /// </summary>
    /// <param name="self">Try to fold</param>
    /// <param name="state">Initial state</param>
    /// <param name="Some">Fold function for Success</param>
    /// <param name="None">Fold function for None</param>
    /// <param name="Fail">Fold function for Failure</param>
    /// <returns>Folded state</returns>
    [Pure]
    public static Task<S> TriFold<A, S>(this TryOptionAsync<A> self, S state, Func<S, A, Task<S>> Some, Func<S, Task<S>> None, Func<S, Exception, Task<S>> Fail) =>
        TriMap(self,
            Some: v => Some(state, v),
            None: () => None(state),
            Fail: x => Fail(state, x))
        .IfNoneOrFail(state);

    /// <summary>
    /// Tests that a predicate holds for any value of the bound value T
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <param name="self">Try computation</param>
    /// <param name="pred">Predicate to test the bound value against</param>
    /// <returns>True if the predicate holds for the bound value.  False otherwise.</returns>
    [Pure]
    public static Task<bool> Exists<A>(this TryOptionAsync<A> self, Func<A, bool> pred) =>
        self.Map(pred).IfNoneOrFail(false);

    /// <summary>
    /// Tests that a predicate holds for any value of the bound value T
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <param name="self">Try computation</param>
    /// <param name="pred">Predicate to test the bound value against</param>
    /// <returns>True if the predicate holds for the bound value.  False otherwise.</returns>
    [Pure]
    public static Task<bool> ExistsAsync<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> pred) =>
        self.MapAsync(pred).IfNoneOrFail(false);

    /// <summary>
    /// Impure iteration of the bound value in the structure
    /// </summary>
    /// <returns>
    /// Returns the original unmodified structure
    /// </returns>
    public static TryOptionAsync<A> Do<A>(this TryOptionAsync<A> ma, Action<A> f) => new TryOptionAsync<A>(async () =>
    {
        var r = await ma.Try().ConfigureAwait(false);
        if (!r.IsFaultedOrNone)
        {
            f(r.Value.Value);
        }
        return r;
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="mapper">Delegate to map the bound value</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> Map<A, B>(this TryOptionAsync<A> self, Func<A, B> f) =>
        Memo(async () => (await self.Try().ConfigureAwait(false)).Map(f));

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="mapper">Delegate to map the bound value</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> MapAsync<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> f) =>
        Memo(async () => await (await self.Try().ConfigureAwait(false)).MapAsync(f).ConfigureAwait(false));

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Succ">Delegate to map the bound value</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> BiMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Succ, Func<B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Fail()
            : Succ(res.Value.Value);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Succ">Delegate to map the bound value</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> BiMap<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> Succ, Func<B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Fail()
            : await Succ(res.Value.Value).ConfigureAwait(false);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Succ">Delegate to map the bound value</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> BiMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Succ, Func<Task<B>> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? await Fail().ConfigureAwait(false)
            : Succ(res.Value.Value);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Succ">Delegate to map the bound value</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> BiMap<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> Succ, Func<Task<B>> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? await Fail().ConfigureAwait(false)
            : await Succ(res.Value.Value).ConfigureAwait(false);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Some, Func<B> None, Func<Exception, B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : None();
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> Some, Func<B> None, Func<Exception, B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await Some(res.Value.Value).ConfigureAwait(false)
                : None();
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Some, Func<Task<B>> None, Func<Exception, B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await None().ConfigureAwait(false);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Some, Func<B> None, Func<Exception, Task<B>> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await Fail(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : None();
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> Some, Func<Task<B>> None, Func<Exception, B> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail(res.Exception)
            : res.Value.IsSome
                ? await Some(res.Value.Value).ConfigureAwait(false)
                : await None().ConfigureAwait(false);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, B> Some, Func<Task<B>> None, Func<Exception, Task<B>> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await Fail(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? Some(res.Value.Value)
                : await None().ConfigureAwait(false);
    });

    /// <summary>
    /// Maps the bound value
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <typeparam name="B">Resulting bound value type</typeparam>
    /// <param name="self">computation</param>
    /// <param name="Some">Delegate to map the bound value</param>
    /// <param name="None">Delegate to map the None to the desired bound result type</param>
    /// <param name="Fail">Delegate to map the exception to the desired bound result type</param>
    /// <returns>Mapped computation</returns>
    [Pure]
    public static TryOptionAsync<B> TriMap<A, B>(this TryOptionAsync<A> self, Func<A, Task<B>> Some, Func<Task<B>> None, Func<Exception, Task<B>> Fail) => Memo<B>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await Fail(res.Exception).ConfigureAwait(false)
            : res.Value.IsSome
                ? await Some(res.Value.Value).ConfigureAwait(false)
                : await None().ConfigureAwait(false);
    });

    /// <summary>
    /// Partial application map
    /// </summary>
    [Pure]
    public static TryOptionAsync<Func<B, R>> ParMap<A, B, R>(this TryOptionAsync<A> self, Func<A, B, R> func) =>
        self.Map(curry(func));

    /// <summary>
    /// Partial application map
    /// </summary>
    [Pure]
    public static TryOptionAsync<Func<B, Func<C, R>>> ParMap<A, B, C, R>(this TryOptionAsync<A> self, Func<A, B, C, R> func) =>
        self.Map(curry(func));

    [Pure]
    public static TryOptionAsync<A> Filter<A>(this TryOptionAsync<A> self, Func<A, bool> pred) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        if (res.IsFaultedOrNone) return res;
        return pred(res.Value.Value)
            ? res
            : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> Filter<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> pred) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        if (res.IsFaultedOrNone) return res;
        return await pred(res.Value.Value).ConfigureAwait(false)
            ? res
            : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> BiFilter<A>(this TryOptionAsync<A> self, Func<A, bool> Succ, Func<bool> Fail) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? Fail()
                ? res.Value
                : Option<A>.None
            : Succ(res.Value.Value)
                ? res.Value
                : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> BiFilter<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> Succ, Func<bool> Fail) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? Fail()
                ? res.Value
                : Option<A>.None
            : await Succ(res.Value.Value).ConfigureAwait(false)
                ? res.Value
                : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> BiFilter<A>(this TryOptionAsync<A> self, Func<A, bool> Succ, Func<Task<bool>> Fail) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await Fail().ConfigureAwait(false)
                ? res.Value
                : Option<A>.None
            : Succ(res.Value.Value)
                ? res.Value
                : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> BiFilter<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> Succ, Func<Task<bool>> Fail) => Memo<A>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaulted
            ? await Fail().ConfigureAwait(false)
                ? res.Value
                : Option<A>.None
            : await Succ(res.Value.Value).ConfigureAwait(false)
                ? res.Value
                : Option<A>.None;
    });

    [Pure]
    public static TryOptionAsync<A> Where<A>(this TryOptionAsync<A> self, Func<A, bool> pred) =>
        self.Filter(pred);

    [Pure]
    public static TryOptionAsync<A> Where<A>(this TryOptionAsync<A> self, Func<A, Task<bool>> pred) =>
        self.Filter(pred);

    [Pure]
    public static TryOptionAsync<B> Bind<A, B>(this TryOptionAsync<A> ma, Func<A, TryOptionAsync<B>> f) => Memo(async () =>
    {
        try
        {
            var ra = await ma().ConfigureAwait(false);
            if (ra.IsSome)
            {
                return await f(ra.Value.Value)().ConfigureAwait(false);
            }
            else if(ra.IsNone)
            {
                return OptionalResult<B>.None;
            }
            else
            {
                return new OptionalResult<B>(ra.Exception);
            }
        }
        catch (Exception e)
        {
            return new OptionalResult<B>(e);
        }

    });

    [Pure]
    public static TryOptionAsync<B> BindAsync<A, B>(this TryOptionAsync<A> self, Func<A, Task<TryOptionAsync<B>>> binder) =>
        default(MTryOptionAsync<A>).BindAsync<MTryOptionAsync<B>, TryOptionAsync<B>, B>(self, binder);

    [Pure]
    public static TryOptionAsync<R> BiBind<A, R>(this TryOptionAsync<A> self, Func<A, TryOptionAsync<R>> Succ, Func<TryOptionAsync<R>> Fail) => Memo<R>(async () =>
    {
        var res = await self.Try().ConfigureAwait(false);
        return res.IsFaultedOrNone
            ? await Fail().Try().ConfigureAwait(false)
            : await Succ(res.Value.Value).Try().ConfigureAwait(false);
    });

    [Pure]
    public static TryOptionAsync<A> Plus<A>(this TryOptionAsync<A> ma, TryOptionAsync<A> mb) =>
        default(MTryOptionAsync<A>).Plus(ma, mb);

    [Pure]
    public static TryOptionAsync<A> PlusFirst<A>(this TryOptionAsync<A> ma, TryOptionAsync<A> mb) =>
        default(MTryOptionFirstAsync<A>).Plus(ma, mb);

    [Pure]
    public static Task<Seq<A>> ToSeq<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: v => v.Cons(Empty),
            None: () => Empty,
            Fail: x => Empty);

    [Pure]
    public static Task<Seq<A>> AsEnumerable<A>(this TryOptionAsync<A> self) =>
        self.ToSeq();

    [Pure]
    public static async Task<Lst<A>> ToList<A>(this TryOptionAsync<A> self) =>
        toList(await self.AsEnumerable().ConfigureAwait(false));

    [Pure]
    public static async Task<Arr<A>> ToArray<A>(this TryOptionAsync<A> self) =>
        toArray(await self.AsEnumerable().ConfigureAwait(false));

    [Pure]
    public static TryOptionAsyncSuccContext<A, R> Some<A, R>(this TryOptionAsync<A> self, Func<A, R> succHandler) =>
        new(self, succHandler, () => default);

    [Pure]
    public static TryOptionAsyncSuccContext<A> Some<A>(this TryOptionAsync<A> self, Action<A> succHandler) =>
        new(self, succHandler, () => { });

    [Pure]
    public static Task<string> AsString<A>(this TryOptionAsync<A> self) =>
        self.Match(
            Some: v => isnull(v)
                      ? "Some(null)"
                      : $"Some({v})",
            None: () => "None",
            Fail: ex => $"Fail({ex.Message})");

    [Pure]
    public static TryOptionAsync<C> SelectMany<A, B, C>(
        this TryOptionAsync<A> ma,
        Func<A, TryOptionAsync<B>> bind,
        Func<A, B, C> project) => Memo(async () =>
        {
            try
            {
                var ra = await ma().ConfigureAwait(false);
                if (ra.IsSome)
                {
                    var rb = await bind(ra.Value.Value)().ConfigureAwait(false);
                    if (rb.IsSome)
                    {
                        return new OptionalResult<C>(project(ra.Value.Value, rb.Value.Value));
                    }
                    else if (rb.IsNone)
                    {
                        return OptionalResult<C>.None;
                    }
                    else
                    {
                        return new OptionalResult<C>(rb.Exception);
                    }

                }
                else if (ra.IsNone)
                {
                    return OptionalResult<C>.None;
                }
                else
                {
                    return new OptionalResult<C>(ra.Exception);
                }
            }
            catch (Exception e)
            {
                return new OptionalResult<C>(e);
            }

        });

    [Pure]
    public static TryOptionAsync<C> SelectMany<A, B, C>(
        this TryOptionAsync<A> ma,
        Func<A, Task<TryOptionAsync<B>>> bind,
        Func<A, B, C> project) => Memo(async () =>
        {
            try
            {
                var ra = await ma().ConfigureAwait(false);
                if (ra.IsSome)
                {
                    var rb = await (await bind(ra.Value.Value).ConfigureAwait(false))().ConfigureAwait(false);
                    if (rb.IsSome)
                    {
                        return new OptionalResult<C>(project(ra.Value.Value, rb.Value.Value));
                    }
                    else if (rb.IsNone)
                    {
                        return OptionalResult<C>.None;
                    }
                    else
                    {
                        return new OptionalResult<C>(rb.Exception);
                    }

                }
                else if (ra.IsNone)
                {
                    return OptionalResult<C>.None;
                }
                else
                {
                    return new OptionalResult<C>(ra.Exception);
                }
            }
            catch (Exception e)
            {
                return new OptionalResult<C>(e);
            }

        });

    [Pure]
    public static TryOptionAsync<C> SelectMany<A, B, C>(
        this TryOptionAsync<A> ma,
        Func<A, Task<TryOptionAsync<B>>> bind,
        Func<A, B, Task<C>> project) => Memo(async () =>
        {
            try
            {
                var ra = await ma().ConfigureAwait(false);
                if (ra.IsSome)
                {
                    var rb = await (await bind(ra.Value.Value).ConfigureAwait(false))().ConfigureAwait(false);
                    if (rb.IsSome)
                    {
                        return new OptionalResult<C>(await project(ra.Value.Value, rb.Value.Value).ConfigureAwait(false));
                    }
                    else if (rb.IsNone)
                    {
                        return OptionalResult<C>.None;
                    }
                    else
                    {
                        return new OptionalResult<C>(rb.Exception);
                    }

                }
                else if (ra.IsNone)
                {
                    return OptionalResult<C>.None;
                }
                else
                {
                    return new OptionalResult<C>(ra.Exception);
                }
            }
            catch (Exception e)
            {
                return new OptionalResult<C>(e);
            }

        });

    [Pure]
    public static TryOptionAsync<C> SelectMany<A, B, C>(
        this TryOptionAsync<A> ma,
        Func<A, TryOptionAsync<B>> bind,
        Func<A, B, Task<C>> project) => Memo(async () =>
        {
            try
            {
                var ra = await ma().ConfigureAwait(false);
                if (ra.IsSome)
                {
                    var rb = await bind(ra.Value.Value)().ConfigureAwait(false);
                    if (rb.IsSome)
                    {
                        return new OptionalResult<C>(await project(ra.Value.Value, rb.Value.Value).ConfigureAwait(false));
                    }
                    else if (rb.IsNone)
                    {
                        return OptionalResult<C>.None;
                    }
                    else
                    {
                        return new OptionalResult<C>(rb.Exception);
                    }

                }
                else if (ra.IsNone)
                {
                    return OptionalResult<C>.None;
                }
                else
                {
                    return new OptionalResult<C>(ra.Exception);
                }
            }
            catch (Exception e)
            {
                return new OptionalResult<C>(e);
            }

        });

    [Pure]
    public static TryOptionAsync<D> Join<A, B, C, D>(
        this TryOptionAsync<A> self,
        TryOptionAsync<B> inner,
        Func<A, C> outerKeyMap,
        Func<B, C> innerKeyMap,
        Func<A, B, D> project) =>
            Memo(async () =>
            {
                var selfTask = self.Try();
                var innerTask = inner.Try();
                await Task.WhenAll(selfTask, innerTask).ConfigureAwait(false);

                if (selfTask.IsFaulted) return new OptionalResult<D>(selfTask.Exception);
                if (selfTask.Result.IsFaultedOrNone) return new OptionalResult<D>(selfTask.Result.Exception);
                if (innerTask.IsFaulted) return new OptionalResult<D>(innerTask.Exception);
                if (innerTask.Result.IsFaultedOrNone) return new OptionalResult<D>(innerTask.Result.Exception);
                return default(EqDefault<C>).Equals(outerKeyMap(selfTask.Result.Value.Value), innerKeyMap(innerTask.Result.Value.Value))
                    ? project(selfTask.Result.Value.Value, innerTask.Result.Value.Value)
                    : throw new BottomException();
            });

    [Pure]
    public static async Task<OptionalResult<T>> Try<T>(this TryOptionAsync<T> self)
    {
        try
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            try
            {
                return await self().ConfigureAwait(false);
            }
            catch(Exception e)
            {
                TryConfig.ErrorLogger(e);
                return new OptionalResult<T>(e);
            }
        }
        catch (Exception e)
        {
            TryConfig.ErrorLogger(e);
            return new OptionalResult<T>(e);
        }
    }

    [Pure]
    public static TryOptionAsync<U> Use<T, U>(this TryOptionAsync<T> self, Func<T, U> select)
        where T : IDisposable => async () =>
            {
                var t = default(T);
                try
                {
                    var res = await self.Try().ConfigureAwait(false);
                    if (res.IsFaultedOrNone) return default(U);
                    t = res.Value.Value;
                    return select(t);
                }
                finally
                {
                    t?.Dispose();
                }
            };

    [Pure]
    public static TryOptionAsync<U> Use<T, U>(this TryOptionAsync<T> self, Func<T, TryOptionAsync<U>> select)
        where T : IDisposable => async () =>
        {
            var t = default(T);
            try
            {
                var res = await self.Try().ConfigureAwait(false);
                if (res.IsFaultedOrNone) return new OptionalResult<U>(res.Exception);
                t = res.Value.Value;
                return await select(t).Try().ConfigureAwait(false);
            }
            finally
            {
                t?.Dispose();
            }
        };

    [Pure]
    public static Task<int> Sum(this TryOptionAsync<int> self) =>
        from x in self.Try()
        select x.IfFailOrNone(0);

    [Pure]
    public static TryOptionAsync<T> Flatten<T>(this TryOptionAsync<TryOptionAsync<T>> self) =>
        from x in self
        from y in x
        select y;

    [Pure]
    public static TryOptionAsync<T> Flatten<T>(this TryOptionAsync<TryOptionAsync<TryOptionAsync<T>>> self) =>
        from x in self
        from y in x
        from z in y
        select z;

    [Pure]
    public static TryOptionAsync<T> Flatten<T>(this TryOptionAsync<TryOptionAsync<TryOptionAsync<TryOptionAsync<T>>>> self) =>
        from w in self
        from x in w
        from y in x
        from z in y
        select z;

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type FB derived from Applicative of B</returns>
    [Pure]
    public static TryOptionAsync<B> Apply<A, B>(this TryOptionAsync<Func<A, B>> fab, TryOptionAsync<A> fa) =>
        ApplTryOptionAsync<A, B>.Inst.Apply(fab, fa);

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type FB derived from Applicative of B</returns>
    [Pure]
    public static TryOptionAsync<B> Apply<A, B>(this Func<A, B> fab, TryOptionAsync<A> fa) =>
        ApplTryOptionAsync<A, B>.Inst.Apply(TryOptionAsync(fab), fa);

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative a to apply</param>
    /// <param name="fb">Applicative b to apply</param>
    /// <returns>Applicative of type FC derived from Applicative of C</returns>
    [Pure]
    public static TryOptionAsync<C> Apply<A, B, C>(this TryOptionAsync<Func<A, B, C>> fabc, TryOptionAsync<A> fa, TryOptionAsync<B> fb) =>
        fabc.Bind(f => ApplTryOptionAsync<A, B, C>.Inst.Apply(MTryOptionAsync<Func<A, Func<B, C>>>.Inst.ReturnAsync(curry(f).AsTask()), fa, fb));

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative a to apply</param>
    /// <param name="fb">Applicative b to apply</param>
    /// <returns>Applicative of type FC derived from Applicative of C</returns>
    [Pure]
    public static TryOptionAsync<C> Apply<A, B, C>(this Func<A, B, C> fabc, TryOptionAsync<A> fa, TryOptionAsync<B> fb) =>
        ApplTryOptionAsync<A, B, C>.Inst.Apply(MTryOptionAsync<Func<A, Func<B, C>>>.Inst.ReturnAsync(curry(fabc).AsTask()), fa, fb);

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type f(b -> c) derived from Applicative of Func<B, C></returns>
    [Pure]
    public static TryOptionAsync<Func<B, C>> Apply<A, B, C>(this TryOptionAsync<Func<A, B, C>> fabc, TryOptionAsync<A> fa) =>
        fabc.Bind(f => ApplTryOptionAsync<A, B, C>.Inst.Apply(MTryOptionAsync<Func<A, Func<B, C>>>.Inst.ReturnAsync(curry(f).AsTask()), fa));

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type f(b -> c) derived from Applicative of Func<B, C></returns>
    [Pure]
    public static TryOptionAsync<Func<B, C>> Apply<A, B, C>(this Func<A, B, C> fabc, TryOptionAsync<A> fa) =>
        ApplTryOptionAsync<A, B, C>.Inst.Apply(MTryOptionAsync<Func<A, Func<B, C>>>.Inst.ReturnAsync(curry(fabc).AsTask()), fa);

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type f(b -> c) derived from Applicative of Func<B, C></returns>
    [Pure]
    public static TryOptionAsync<Func<B, C>> Apply<A, B, C>(this TryOptionAsync<Func<A, Func<B, C>>> fabc, TryOptionAsync<A> fa) =>
        ApplTryOptionAsync<A, B, C>.Inst.Apply(fabc, fa);

    /// <summary>
    /// Apply
    /// </summary>
    /// <param name="fab">Function to apply the applicative to</param>
    /// <param name="fa">Applicative to apply</param>
    /// <returns>Applicative of type f(b -> c) derived from Applicative of Func<B, C></returns>
    [Pure]
    public static TryOptionAsync<Func<B, C>> Apply<A, B, C>(this Func<A, Func<B, C>> fabc, TryOptionAsync<A> fa) =>
        ApplTryOptionAsync<A, B, C>.Inst.Apply(TryOptionAsync(fabc), fa);

    /// <summary>
    /// Evaluate fa, then fb, ignoring the result of fa
    /// </summary>
    /// <param name="fa">Applicative to evaluate first</param>
    /// <param name="fb">Applicative to evaluate second and then return</param>
    /// <returns>Applicative of type Option<B></returns>
    [Pure]
    public static TryOptionAsync<B> Action<A, B>(this TryOptionAsync<A> fa, TryOptionAsync<B> fb) =>
        ApplTryOptionAsync<A, B>.Inst.Action(fa, fb);

    /// <summary>
    /// Compare the bound value of Try(x) to Try(y).  If either of the
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>1 if lhs > rhs, 0 if lhs == rhs, -1 if lhs < rhs</returns>
    [Pure]
    public static async Task<int> Compare<ORD, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where ORD : struct, Ord<A> 
    {
        var x = lhs.Try();
        var y = rhs.Try();
        await Task.WhenAll(x, y).ConfigureAwait(false);

        if (x.IsFaulted && y.IsFaulted) return 0;
        if (x.IsFaulted && !y.IsFaulted) return -1;
        if (!x.IsFaulted && y.IsFaulted) return 1;
        if (x.Result.IsFaulted && y.Result.IsFaulted) return 0;
        if (x.Result.IsFaulted && !y.Result.IsFaulted) return -1;
        if (!x.Result.IsFaulted && y.Result.IsFaulted) return 1;
        return compare<ORD, A>(x.Result.Value, y.Result.Value);
    }

    /// <summary>
    /// Append the bound value of TryOptionAsync(x) to TryOptionAsync(y).  If either of the
    /// Trys are Fail then the result is Fail
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>lhs ++ rhs</returns>
    [Pure]
    public static TryOptionAsync<A> Append<SEMI, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where SEMI : struct, Semigroup<A> =>
        Memo(async () =>
        {
            var x = lhs.Try();
            var y = rhs.Try();
            await Task.WhenAll(x, y).ConfigureAwait(false);
            if (x.IsFaulted || x.Result.IsFaulted) return x.Result;
            if (y.IsFaulted || y.Result.IsFaulted) return y.Result;
            return append<SEMI, A>(x.Result.Value, y.Result.Value);
        });

    /// <summary>
    /// Add the bound value of Try(x) to Try(y).  If either of the
    /// Trys are Fail then the result is Fail
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>lhs + rhs</returns>
    [Pure]
    public static TryOptionAsync<A> Add<NUM, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where NUM : struct, Num<A> =>
        Memo(async () =>
        {
            var x = lhs.Try();
            var y = rhs.Try();
            await Task.WhenAll(x, y).ConfigureAwait(false);
            if (x.IsFaulted || x.Result.IsFaulted) return x.Result;
            if (y.IsFaulted || y.Result.IsFaulted) return y.Result;
            return add<NUM, A>(x.Result.Value, y.Result.Value);
        });

    /// <summary>
    /// Find the subtract of the bound value of Try(x) and Try(y).  If either of 
    /// the Trys are Fail then the result is Fail
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>lhs + rhs</returns>
    [Pure]
    public static TryOptionAsync<A> Subtract<NUM, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where NUM : struct, Num<A> =>
        Memo(async () =>
        {
            var x = lhs.Try();
            var y = rhs.Try();
            await Task.WhenAll(x, y).ConfigureAwait(false);
            if (x.IsFaulted || x.Result.IsFaulted) return x.Result;
            if (y.IsFaulted || y.Result.IsFaulted) return y.Result;
            return subtract<NUM, A>(x.Result.Value, y.Result.Value);
        });

    /// <summary>
    /// Multiply the bound value of Try(x) and Try(y).  If either of the
    /// Trys are Fail then the result is Fail
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>lhs + rhs</returns>
    [Pure]
    public static TryOptionAsync<A> Product<NUM, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where NUM : struct, Num<A> =>
        Memo(async () =>
        {
            var x = lhs.Try();
            var y = rhs.Try();
            await Task.WhenAll(x, y).ConfigureAwait(false);
            if (x.IsFaulted || x.Result.IsFaulted) return x.Result;
            if (y.IsFaulted || y.Result.IsFaulted) return y.Result;
            return product<NUM, A>(x.Result.Value, y.Result.Value);
        });

    /// <summary>
    /// Multiply the bound value of Try(x) and Try(y).  If either of the
    /// Trys are Fail then the result is Fail
    /// </summary>
    /// <param name="lhs">Left-hand side of the operation</param>
    /// <param name="rhs">Right-hand side of the operation</param>
    /// <returns>lhs + rhs</returns>
    [Pure]
    public static TryOptionAsync<A> Divide<NUM, A>(this TryOptionAsync<A> lhs, TryOptionAsync<A> rhs) where NUM : struct, Num<A> =>
        Memo(async () =>
        {
            var x = lhs.Try();
            var y = rhs.Try();
            await Task.WhenAll(x, y).ConfigureAwait(false);
            if (x.IsFaulted || x.Result.IsFaulted) return x.Result;
            if (y.IsFaulted || y.Result.IsFaulted) return y.Result;
            return divide<NUM, A>(x.Result.Value, y.Result.Value);
        });

    /// <summary>
    /// Convert the computation type to a Nullable of A
    /// </summary>
    /// <typeparam name="A">Type of the bound value</typeparam>
    /// <param name="ma">computation to convert</param>
    /// <returns>Nullable of A</returns>
    [Pure]
    public static async Task<A?> ToNullable<A>(this TryOptionAsync<A> ma) where A : struct
    {
        var x = await ma.Try().ConfigureAwait(false);
        return x.IsFaultedOrNone
            ? null
            : x.Value.Value;
    }
}
