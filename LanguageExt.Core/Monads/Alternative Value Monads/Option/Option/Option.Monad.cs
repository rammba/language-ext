using System;
using LanguageExt.HKT;

namespace LanguageExt;

public class Option : MonadIO<Option>, Traversable<Option>, Alternative<Option>
{
    static K<Option, B> Monad<Option>.Bind<A, B>(K<Option, A> ma, Func<A, K<Option, B>> f) =>
        ma.As().Bind(f);

    static K<Option, B> Functor<Option>.Map<A, B>(Func<A, B> f, K<Option, A> ma) => 
        ma.As().Map(f);

    static K<Option, A> Applicative<Option>.Pure<A>(A value) =>
        Option<A>.Some(value);

    static K<Option, B> Applicative<Option>.Apply<A, B>(K<Option, Func<A, B>> mf, K<Option, A> ma) =>
        mf.As().Bind(ma.As().Map);

    static K<Option, B> Applicative<Option>.Action<A, B>(K<Option, A> ma, K<Option, B> mb) =>
        mb;

    static K<Option, A> MonadIO<Option>.LiftIO<A>(IO<A> ma) => 
        MonadIO.liftNoIO<Option, A>(ma);

    static S Foldable<Option>.Fold<A, S>(Func<A, Func<S, S>> f, S initialState, K<Option, A> ta) =>
        ta.As().Fold(initialState, (s, a) => f(a)(s));

    static S Foldable<Option>.FoldBack<A, S>(Func<S, Func<A, S>> f, S initialState, K<Option, A> ta) => 
        ta.As().FoldBack(initialState, (s, a) => f(s)(a));

    static K<F, K<Option, B>> Traversable<Option>.Traverse<F, A, B>(Func<A, K<F, B>> f, K<Option, A> ta) =>
        ta.As().Match(Some: a => F.Map(Some, f(a)),
                      None: () => F.Pure(None<B>()));

    static K<Option, A> Alternative<Option>.Empty<A>() =>
        None<A>();

    static K<Option, A> Alternative<Option>.Or<A>(K<Option, A> ma, K<Option, A> mb) =>
        ma.As() || mb.As();

    static K<Option, X> Some<X>(X value) =>
        Option<X>.Some(value);

    static K<Option, X> None<X>() =>
        Option<X>.None;
}
