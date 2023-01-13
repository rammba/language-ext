using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LanguageExt
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct OptionAsyncMethodBuilder<A>
    {
        IAsyncStateMachine stateMachine;
        
        public static OptionAsyncMethodBuilder<A> Create() =>
            new();

        public void Start<TStateMachine>(ref TStateMachine machine)
            where TStateMachine : IAsyncStateMachine =>
            machine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine machine) =>
            stateMachine = machine;

        public void SetException(Exception _) =>
            Task = OptionAsync<A>.None;

        public void SetResult(A result) =>
            Task = OptionAsync<A>.Some(result);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine machine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine machine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
        }

        public OptionAsync<A> Task { get; private set; }
    }
}
