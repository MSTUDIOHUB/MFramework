using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 统一持有 Addressables 请求与租约，让页面、场景或玩法功能可以一次性释放相关资源。
    /// </summary>
    /// <remarks>
    /// 资源会按登记顺序的逆序释放。释放尚未完成的请求不会中止平台下载；框架会停止回调，
    /// 并在 Addressables 完成后立即释放得到的结果。
    /// </remarks>
    public sealed class MAssetScope : IDisposable
    {
        private readonly List<IDisposable> _resources = new List<IDisposable>();

        /// <summary>获取此作用域是否已经释放资源。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>把一个可释放资源交给此作用域管理。</summary>
        /// <typeparam name="T">可释放资源的类型。</typeparam>
        /// <param name="resource">需要托管的资源。</param>
        /// <returns>原资源本身，便于在一行代码中完成创建和登记。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> 为 null。</exception>
        public T Own<T>(T resource)
            where T : IDisposable
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (IsDisposed)
            {
                resource.Dispose();
                return resource;
            }

            _resources.Add(resource);
            return resource;
        }

        /// <summary>按登记顺序的逆序释放全部托管资源；单个资源失败不会阻止后续资源释放。</summary>
        /// <exception cref="AggregateException">一个或多个资源在释放时抛出异常。</exception>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            List<Exception> exceptions = null;
            for (int i = _resources.Count - 1; i >= 0; i--)
            {
                try
                {
                    _resources[i]?.Dispose();
                }
                catch (Exception exception)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(exception);
                }
            }

            _resources.Clear();
            if (exceptions != null)
            {
                throw new AggregateException("一个或多个资源在释放 MAssetScope 时失败。", exceptions);
            }
        }
    }
}
