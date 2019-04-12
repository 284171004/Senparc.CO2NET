﻿/*----------------------------------------------------------------
    Copyright (C) 2018 Senparc

    文件名：RedisCacheLock.cs
    文件功能描述：本地锁


    创建标识：Senparc - 20160810

    修改标识：Senparc - 20170205
    修改描述：v0.2.0 重构分布式锁

    修改标识：spadark - 20170419
    修改描述：v0.3.0 Memcached同步锁改为使用StoreMode.Add方法

----------------------------------------------------------------*/


using System;
using System.Threading;
using System.Threading.Tasks;
using Enyim.Caching.Memcached;
using Senparc.CO2NET.Cache;
using Senparc.CO2NET.Trace;

namespace Senparc.CO2NET.Cache.Memcached
{
    public class MemcachedCacheLock : BaseCacheLock
    {
        private MemcachedObjectCacheStrategy _mamcachedStrategy;
        public MemcachedCacheLock(MemcachedObjectCacheStrategy strategy, string resourceName, string key, int retryCount, TimeSpan retryDelay)
            : base(strategy, resourceName, key, retryCount, retryDelay)
        {
            _mamcachedStrategy = strategy;
            LockNow();//立即等待并抢夺锁
        }

        private static Random _rnd = new Random();

        private string GetLockKey(string resourceName)
        {
            return string.Format("{0}:{1}", "Lock", resourceName);
        }

        #region 同步方法

        private bool RetryLock(string resourceName, int retryCount, TimeSpan retryDelay, Func<bool> action)
        {
            int currentRetry = 0;
            int maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            while (currentRetry++ < retryCount)
            {
                if (action())
                {
                    return true;//取得锁
                }
                Thread.Sleep(_rnd.Next(maxRetryDelay));
            }
            return false;
        }

        public override bool Lock(string resourceName)
        {
            return Lock(resourceName, 9999, new TimeSpan(0, 0, 0, 0, 20));
        }

        public override bool Lock(string resourceName, int retryCount, TimeSpan retryDelay)
        {
            var key = _mamcachedStrategy.GetFinalKey(resourceName);
            var successfull = RetryLock(key, retryCount, retryDelay, () =>
            {
                try
                {
                    var ttl = base.GetTotalTtl(retryCount, retryDelay);
                    if (_mamcachedStrategy.Cache.Store(StoreMode.Add, key, new object(), TimeSpan.FromMilliseconds(ttl)))
                    {
                        return true;//取得锁 
                    }
                    else
                    {
                        return false;//已被别人锁住，没有取得锁
                    }

                    //if (_mamcachedStrategy._cache.Get(key) != null)
                    //{
                    //    return false;//已被别人锁住，没有取得锁
                    //}
                    //else
                    //{
                    //    _mamcachedStrategy._cache.Store(StoreMode.set, key, new object(), new TimeSpan(0, 0, 10));//创建锁
                    //    return true;//取得锁
                    //}
                }
                catch (Exception ex)
                {
                    SenparcTrace.Log("Memcached同步锁发生异常：" + ex.Message);
                    return false;
                }
            }
              );
            return successfull;
        }

        public override void UnLock(string resourceName)
        {
            var key = _mamcachedStrategy.GetFinalKey(resourceName);
            _mamcachedStrategy.Cache.Remove(key);
        }

        #endregion

        #region 异步方法
#if !NET35 && !NET40


        private async Task<bool> RetryLockAsync(string resourceName, int retryCount, TimeSpan retryDelay, Func<Task<bool>> action)
        {
            int currentRetry = 0;
            int maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            while (currentRetry++ < retryCount)
            {
                if (await action())
                {
                    return true;//取得锁
                }
                Thread.Sleep(_rnd.Next(maxRetryDelay));
            }
            return false;
        }

        public override async Task<bool> LockAsync(string resourceName)
        {
            return await LockAsync(resourceName, 9999, new TimeSpan(0, 0, 0, 0, 20));
        }

        public override async Task<bool> LockAsync(string resourceName, int retryCount, TimeSpan retryDelay)
        {
            var key = _mamcachedStrategy.GetFinalKey(resourceName);
            var successfull = await RetryLockAsync(key, retryCount, retryDelay, async () =>
             {
                 try
                 {
                     var ttl = base.GetTotalTtl(retryCount, retryDelay);
#if NET45
                    if (_mamcachedStrategy.Cache.Store(StoreMode.Add, key, new object(), TimeSpan.FromMilliseconds(ttl)))
#else
                    if (await _mamcachedStrategy.Cache.StoreAsync(StoreMode.Add, key, new object(), TimeSpan.FromMilliseconds(ttl)))
#endif
                    {
                         return true;//取得锁 
                    }
                     else
                     {
                         return false;//已被别人锁住，没有取得锁
                    }

                    //if (_mamcachedStrategy._cache.Get(key) != null)
                    //{
                    //    return false;//已被别人锁住，没有取得锁
                    //}
                    //else
                    //{
                    //    _mamcachedStrategy._cache.Store(StoreMode.set, key, new object(), new TimeSpan(0, 0, 10));//创建锁
                    //    return true;//取得锁
                    //}
                }
                 catch (Exception ex)
                 {
                     SenparcTrace.Log("Memcached同步锁发生异常：" + ex.Message);
                     return false;
                 }
             }
              );
            return successfull;
        }

        public override async Task UnLockAsync(string resourceName)
        {
            var key = _mamcachedStrategy.GetFinalKey(resourceName);
            await _mamcachedStrategy.Cache.RemoveAsync(key);
        }


#endif
        #endregion
    }
}
