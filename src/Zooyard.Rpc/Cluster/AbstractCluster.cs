﻿using Common.Logging;
using System;
using System.Collections.Generic;
using Zooyard.Core;

namespace Zooyard.Rpc.Cluster
{
    public abstract class AbstractCluster : ICluster
    {
        private static readonly ILog _logger = LogManager.GetLogger("AbstractCluster");
        public const string TIMEOUT_KEY = "timeout";
        public const int DEFAULT_TIMEOUT = 1000;
        /// <summary>
        /// 集群时是否启用sticky策略
        /// </summary>
        public const string CLUSTER_STICKY_KEY = "sticky";

        /// <summary>
        /// sticky默认值.
        /// </summary>
        public const bool DEFAULT_CLUSTER_STICKY = false;

        private volatile URL stickyInvoker = null;
        protected bool availablecheck;
        protected ILog logger = LogManager.GetLogger<AbstractCluster>();

        public AbstractCluster()
        {
        }


        /// <summary>
        /// 使用loadbalance选择invoker.</br>
        /// a)先lb选择，如果在selected列表中 或者 不可用且做检验时，进入下一步(重选),否则直接返回</br>
        ///  b)重选验证规则：selected > available .保证重选出的结果尽量不在select中，并且是可用的
        /// </summary>
        /// <param name="loadbalance"></param>
        /// <param name="invocation"></param>
        /// <param name="invokers"></param>
        /// <param name="selected"> 已选过的invoker.注意：输入保证不重复</param>
        /// <returns></returns>
        protected URL select(ILoadBalance loadbalance, IInvocation invocation, IList<URL> invokers, IList<URL> selected)
        {
            if (invokers == null || invokers.Count == 0)
                return null;
            string methodName = invocation == null ? "" : invocation.MethodInfo.Name;

            var sticky = invokers[0].GetMethodParameter<bool>(methodName, CLUSTER_STICKY_KEY, DEFAULT_CLUSTER_STICKY);

            {
                //ignore overloaded method
                if (invokers != null && !invokers.Contains(stickyInvoker))
                {
                    stickyInvoker = null;
                }
                //ignore cucurrent problem
                if (sticky && stickyInvoker != null && (selected == null || !selected.Contains(stickyInvoker)))
                {
                    if (availablecheck)//&& stickyInvoker.IsAvailable()
                    {
                        return stickyInvoker;
                    }
                }
            }
            var invoker = doselect(loadbalance, invocation, invokers, selected);

            if (sticky)
            {
                stickyInvoker = invoker;
            }
            return invoker;
        }

        private URL doselect(ILoadBalance loadbalance, IInvocation invocation, IList<URL> invokers, IList<URL> selected)
        {
            if (invokers == null || invokers.Count == 0)
                return null;
            if (invokers.Count == 1)
                return invokers[0];
            // 如果只有两个invoker，退化成轮循
            if (invokers.Count == 2 && selected != null && selected.Count > 0)
            {
                return selected[0] == invokers[0] ? invokers[1] : invokers[0];
            }
            var invoker = loadbalance.Select(invokers, invocation);

            //如果 selected中包含（优先判断） 或者 不可用&&availablecheck=true 则重试.
            if ((selected != null && selected.Contains(invoker)) || availablecheck)
            {
                try
                {
                    var rinvoker = reselect(loadbalance, invocation, invokers, selected, availablecheck);
                    if (rinvoker != null)
                    {
                        invoker = rinvoker;
                    }
                    else
                    {
                        //看下第一次选的位置，如果不是最后，选+1位置.
                        int index = invokers.IndexOf(invoker);
                        try
                        {
                            //最后在避免碰撞
                            invoker = index < invokers.Count - 1 ? invokers[index + 1] : invoker;
                        }
                        catch (Exception e)
                        {
                            _logger.Warn(e.Message+ " may because invokers list dynamic change, ignore.", e);
                        }
                    }
                }
                catch (Exception t)
                {
                    _logger.Error("clustor relselect fail reason is :" + t.Message + " if can not slove ,you can set cluster.availablecheck=false in url", t);
                }
            }
            return invoker;
        }

        /// <summary>
        /// 重选，先从非selected的列表中选择，没有在从selected列表中选择.
        /// </summary>
        /// <param name="loadbalance"></param>
        /// <param name="invocation"></param>
        /// <param name="invokers"></param>
        /// <param name="selected"></param>
        /// <param name="availablecheck"></param>
        /// <returns></returns>
        private URL reselect(ILoadBalance loadbalance, IInvocation invocation, IList<URL> invokers, IList<URL> selected, bool availablecheck)
        {

            //预先分配一个，这个列表是一定会用到的.
            List<URL> reselectInvokers = new List<URL>(invokers.Count > 1 ? (invokers.Count - 1) : invokers.Count);

            //先从非select中选
            if (availablecheck)
            { //选isAvailable 的非select
                foreach (var invoker in invokers)
                {
                    if (selected == null || !selected.Contains(invoker))
                    {
                        reselectInvokers.Add(invoker);
                    }
                }
                if (reselectInvokers.Count > 0)
                {
                    return loadbalance.Select(reselectInvokers, invocation);
                }
            }
            else
            { //选全部非select
                foreach (var invoker in invokers)
                {
                    if (selected == null || !selected.Contains(invoker))
                    {
                        reselectInvokers.Add(invoker);
                    }
                }
                if (reselectInvokers.Count > 0)
                {
                    return loadbalance.Select(reselectInvokers, invocation);
                }
            }
            //最后从select中选可用的. 
            {
                if (selected != null)
                {
                    foreach (var invoker in selected)
                    {
                        if (!reselectInvokers.Contains(invoker))
                        {
                            reselectInvokers.Add(invoker);
                        }
                    }
                }
                if (reselectInvokers.Count > 0)
                {
                    return loadbalance.Select(reselectInvokers, invocation);
                }
            }
            return null;
        }

        protected void checkInvokers(IList<URL> invokers, IInvocation invocation)
        {
            if (invokers == null || invokers.Count == 0)
            {
                throw new RpcException("Failed to invoke the method "
                        + invocation.MethodInfo.Name + " in the service " + invocation.TargetType.Name
                        //+ ". No provider available for the service " + directory.getUrl().getServiceKey()
                        //+ " from registry " + directory.getUrl().getAddress()
                        //+ " on the consumer " + NetUtils.getLocalHost()
                        //+ " using the zooyard version " + Version.getVersion()
                        + ". Please check if the providers have been started and registered.");
            }
        }

        public abstract IClusterResult DoInvoke(IClientPool pool, ILoadBalance loadbalance, URL address, IList<URL> urls, IInvocation invocation);
    }
}
