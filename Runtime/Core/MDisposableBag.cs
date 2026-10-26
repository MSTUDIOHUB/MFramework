using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 持有一组可释放对象，并按照加入顺序的反向顺序释放它们。
    /// </summary>
    public sealed class MDisposableBag : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();
        private bool _isDisposed;

        /// <summary>获取该容器是否已经释放。</summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>获取该容器当前持有的对象数量。</summary>
        public int Count => _items.Count;

        /// <summary>
        /// 向容器中加入一个可释放对象。
        /// </summary>
        /// <typeparam name="T">可释放对象的类型。</typeparam>
        /// <param name="item">要由该容器持有的对象。</param>
        /// <returns>传入的同一个对象，便于链式注册。</returns>
        /// <remarks>如果该容器已经释放，传入的对象会被立即释放。</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> 为 null。</exception>
        public T Add<T>(T item)
            where T : IDisposable
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_isDisposed)
            {
                item.Dispose();
                return item;
            }

            _items.Add(item);
            return item;
        }

        /// <summary>
        /// 释放当前持有的全部对象，但允许该容器继续使用。
        /// </summary>
        public void Clear()
        {
            List<Exception> exceptions = DisposeItems();
            if (exceptions.Count > 0)
            {
                throw new AggregateException("MDisposableBag 中有一个或多个对象在释放时发生异常。", exceptions);
            }
        }

        /// <summary>
        /// 释放全部对象，并阻止该容器继续持有后续加入的对象。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            List<Exception> exceptions = DisposeItems();
            if (exceptions.Count > 0)
            {
                throw new AggregateException("MDisposableBag 中有一个或多个对象在释放时发生异常。", exceptions);
            }
        }

        private List<Exception> DisposeItems()
        {
            List<Exception> exceptions = new List<Exception>();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                try
                {
                    _items[i].Dispose();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _items.Clear();
            return exceptions;
        }
    }
}
