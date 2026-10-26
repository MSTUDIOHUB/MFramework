using System;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 为显式配置的 <see cref="MFrameworkApp"/> 提供 Unity 场景入口。
    /// </summary>
    /// <remarks>
    /// 请派生游戏专用的入口组件，并在 <see cref="Configure"/> 中注册服务。
    /// 该基类不会扫描场景或程序集来寻找模块。
    /// </remarks>
    public abstract class MGameEntry : MonoBehaviour
    {
        /// <summary>获取由该场景入口持有的框架应用。</summary>
        protected MFrameworkApp App { get; private set; }

        /// <summary>
        /// 注册该入口所需的框架服务与游戏服务。
        /// </summary>
        /// <param name="app">新创建的框架应用。</param>
        protected abstract void Configure(MFrameworkApp app);

        /// <summary>创建并初始化框架应用。</summary>
        protected virtual void Awake()
        {
            App = new MFrameworkApp();
            try
            {
                Configure(App);
                App.Initialize();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                try
                {
                    // Configure 也可能在创建资源后失败；立即关闭可避免等到场景销毁才释放。
                    App.Shutdown();
                }
                catch (Exception shutdownException)
                {
                    Debug.LogException(shutdownException, this);
                }

                enabled = false;
            }
        }

        /// <summary>驱动常规帧更新服务。</summary>
        protected virtual void Update()
        {
            App?.Tick(new MFrameTime(Time.deltaTime, Time.unscaledDeltaTime));
        }

        /// <summary>驱动固定时间步长服务。</summary>
        protected virtual void FixedUpdate()
        {
            App?.FixedTick(Time.fixedDeltaTime);
        }

        /// <summary>驱动帧末更新服务。</summary>
        protected virtual void LateUpdate()
        {
            App?.LateTick(new MFrameTime(Time.deltaTime, Time.unscaledDeltaTime));
        }

        /// <summary>关闭框架应用。</summary>
        protected virtual void OnDestroy()
        {
            if (App == null)
            {
                return;
            }

            try
            {
                App.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
