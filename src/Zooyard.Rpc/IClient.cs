using System;

namespace Zooyard.Rpc
{
    public interface IClient : IDisposable
    {
        #region 属性
        /// <summary>
        /// 连接是否打开
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// 连接拥有者
        /// </summary>
        IPool Owner { get; }

        /// <summary>
        /// 连接版本，创建连接时的版本
        /// </summary>
        int Version { get; }
        #endregion

        #region 公共方法
        /// <summary>
        /// 连接打开
        /// </summary>
        /// <returns>是否开启成功</returns>
        void Open();

        /// <summary>
        /// 连接关闭
        /// </summary>
        /// <returns>是否关闭成功</returns>
        void Close();

        /// <summary>
        /// 重置，连接归还连接池前操作
        /// </summary>
        void Reset();

        ///// <summary>
        ///// 获取桩子代理
        ///// </summary>
        ///// <typeparam name="T">桩子类型</typeparam>
        ///// <returns>RPC桩子</returns>
        //T GetStub<T>();

        ///// <summary>
        ///// 获取桩子代理
        ///// </summary>
        ///// <param name="stubType">桩子类型</param>
        ///// <returns>RPC桩子</returns>
        //object GetStub(Type stubType);

        ///// <summary>
        ///// 获取通用桩子代理
        ///// </summary>
        ///// <returns>通用桩子代理</returns>
        //IStub GetStub();
        #endregion
    }
}
