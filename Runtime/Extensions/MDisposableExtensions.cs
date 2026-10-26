using System;

namespace MFramework
{
    /// <summary>
    /// 为可释放的框架句柄与资源请求提供链式生命周期归属注册。
    /// </summary>
    public static class MDisposableExtensions
    {
        /// <summary>
        /// 将可释放值加入 <see cref="MDisposableBag"/>，并返回同一个值。
        /// </summary>
        /// <typeparam name="T">可释放值的类型。</typeparam>
        /// <param name="disposable">生命周期将交给 Bag 管理的值。</param>
        /// <param name="bag">负责释放该值的 Bag。</param>
        /// <returns>原 <paramref name="disposable"/> 实例。</returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="disposable"/> 或 <paramref name="bag"/> 为 null 时抛出。
        /// </exception>
        public static T AddTo<T>(this T disposable, MDisposableBag bag)
            where T : IDisposable
        {
            if (ReferenceEquals(disposable, null))
            {
                throw new ArgumentNullException(nameof(disposable));
            }

            if (bag == null)
            {
                throw new ArgumentNullException(nameof(bag));
            }

            return bag.Add(disposable);
        }

        /// <summary>
        /// 将可释放资源或请求加入 <see cref="MAssetScope"/>，并返回同一个实例。
        /// </summary>
        /// <typeparam name="T">可释放资源或请求的类型。</typeparam>
        /// <param name="disposable">生命周期将交给 Scope 管理的实例。</param>
        /// <param name="scope">负责释放该实例的资源作用域。</param>
        /// <returns>原 <paramref name="disposable"/> 实例。</returns>
        /// <exception cref="ArgumentNullException">
        /// 当 <paramref name="disposable"/> 或 <paramref name="scope"/> 为 null 时抛出。
        /// </exception>
        public static T AddTo<T>(this T disposable, MAssetScope scope)
            where T : IDisposable
        {
            if (ReferenceEquals(disposable, null))
            {
                throw new ArgumentNullException(nameof(disposable));
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return scope.Own(disposable);
        }
    }
}
