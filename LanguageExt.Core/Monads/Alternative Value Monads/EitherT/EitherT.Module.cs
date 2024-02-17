﻿using System;
using LanguageExt.Traits;

namespace LanguageExt;

public partial class EitherT<L, M>
{
    public static EitherT<L, M, A> Right<A>(A value) => 
        EitherT<L, M, A>.Right(value);

    public static EitherT<L, M, A> Left<A>(L value) => 
        EitherT<L, M, A>.Left(value);

    public static EitherT<L, M, A> lift<A>(Either<L, A> ma) => 
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> lift<A>(Pure<A> ma) => 
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> lift<A>(Fail<L> ma) => 
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> liftIO<A>(IO<A> ma) =>  
        EitherT<L, M, A>.Lift(M.LiftIO(ma));
}

public partial class EitherT
{
    public static EitherT<L, M, B> bind<L, M, A, B>(EitherT<L, M, A> ma, Func<A, EitherT<L, M, B>> f) 
        where M : Monad<M> =>
        ma.As().Bind(f);

    public static EitherT<L, M, B> map<L, M, A, B>(Func<A, B> f, EitherT<L, M, A> ma)  
        where M : Monad<M> =>
        ma.As().Map(f);

    public static EitherT<L, M, A> Right<L, M, A>(A value)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Right(value);

    public static EitherT<L, M, A> Left<L, M, A>(L value)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Left(value);

    public static EitherT<L, M, B> apply<L, M, A, B>(EitherT<L, M, Func<A, B>> mf, EitherT<L, M, A> ma) 
        where M : Monad<M> =>
        mf.Apply(ma);

    public static EitherT<L, M, B> action<L, M, A, B>(EitherT<L, M, A> ma, EitherT<L, M, B> mb) 
        where M : Monad<M> =>
        ma.Action(mb);

    public static EitherT<L, M, A> lift<L, M, A>(Either<L, A> ma)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> lift<L, M, A>(K<M, A> ma)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> lift<L, M, A>(Pure<A> ma)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> lift<L, M, A>(Fail<L> ma)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Lift(ma);

    public static EitherT<L, M, A> liftIO<L, M, A>(IO<A> ma)  
        where M : Monad<M> =>
        EitherT<L, M, A>.Lift(M.LiftIO(ma));
    
    public static K<M, B> match<L, M, A, B>(EitherT<L, M, A> ma, Func<L, B> Left, Func<A, B> Right) 
        where M : Monad<M> =>
        ma.Match(Left, Right);
}
