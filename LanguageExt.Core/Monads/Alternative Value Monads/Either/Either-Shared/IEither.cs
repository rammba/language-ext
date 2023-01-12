using System;
using System.Threading.Tasks;

namespace LanguageExt
{
    public interface IEither
    {
        bool IsRight
        {
            get;
        }

        bool IsLeft
        {
            get;
        }

        TResult MatchUntyped<TResult>(Func<object, TResult> Right, Func<object, TResult> Left);

        Type GetUnderlyingRightType();
        Type GetUnderlyingLeftType();
    }

    public interface IEitherAsync
    {
        Task<bool> IsRight
        {
            get;
        }

        Task<bool> IsLeft
        {
            get;
        }

        Type GetUnderlyingRightType();
        Type GetUnderlyingLeftType();
    }

}
