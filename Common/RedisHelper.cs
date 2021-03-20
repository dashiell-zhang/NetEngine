using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    public static class RedisHelper
    {

        private static string ConnectionString = IO.Config.Get().GetConnectionString("redisConnection");



        /// <summary>
        /// 删除指定key
        /// </summary>
        /// <param name="key"></param>
        public static void RemoveKey(string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.KeyDelete(key);
        }



        /// <summary>
        /// 设置string类型的key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void StrSet(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.StringSet(key, value);
        }



        /// <summary>
        /// 设置string类型key,包含有效时间
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeout"></param>
        public static void StrSet(string key, string value, TimeSpan timeout)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.StringSet(key, value, timeout);
        }




        /// <summary>
        /// 读取string类型的key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string StrGet(string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.StringGet(key);
        }



        /// <summary>
        /// 通过key判断是否存在指定Str
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool IsContainStr(string key)
        {
            var info = StrGet(key);

            if (string.IsNullOrEmpty(info))
            {
                return false;
            }
            else
            {
                return true;
            }
        }



        /// <summary>
        /// 给value追加值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long StrAppend(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.StringAppend(key, value);
        }




        /// <summary>
        /// 给value加上指定值,适用于value是long类型的
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long LongIncrement(string key, long value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.StringIncrement(key, value);
        }


        /// <summary>
        /// 给value减去指定值,适用于value是long类型的
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long LongDecrement(string key, long value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.StringDecrement(key, value);
        }




        /// <summary>
        /// 设置List，Value可重复
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long ListSet(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.ListRightPush(key, value);
        }




        /// <summary>
        /// 设置List,Value不可重复
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void SetAdd(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.SetAdd(key, value);
        }



        /// <summary>
        /// 通过key获取list中指定row的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public static string ListGetRV(string key, int row)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.ListGetByIndex(key, row);
        }




        /// <summary>
        /// 通过key获取list中的所有值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static List<RedisValue> ListGetKV(string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            List<RedisValue> list = db.ListRange(key).ToList();
            return list;
        }




        /// <summary>
        /// 删除List中指定的value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void ListDelV(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.ListRemove(key, value);
        }




        /// <summary>
        /// 返回List的总行数
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static long ListCount(string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.ListLength(key);
        }




        /// <summary>
        /// 设置Hash，传入Hash的主name,里面的key,value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void HashSet(string name, string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.HashSet(name, key, value);
        }




        /// <summary>
        /// 读取Hash中某个key的值,传入Hash的name和key
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string HashGet(string name, string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.HashGet(name, key);
        }




        /// <summary>
        /// 读取Hash中的所有值,传入Hash的name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static List<HashEntry> HashGet(string name)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            List<HashEntry> list = db.HashGetAll(name).ToList();
            return list;
        }




        /// <summary>
        /// 删除Hash中指定key,传入Hash的name和要删除的key
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        public static void HashDelK(string name, string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            db.HashDelete(name, key);
        }




        /// <summary>
        /// 验证Hash中是否存在指定key,传入Hash的name和查询的key
        /// </summary>
        /// <param name="name"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool HashLike(string name, string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.HashExists(name, key);
        }





        /// <summary>
        /// 返回Hash中总行数
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static long HashCount(string key)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();
            return db.HashLength(key);
        }




        /// <summary>
        /// 获取锁
        /// </summary>
        /// <param name="key">锁的名称，不可重复</param>
        /// <param name="value">解锁密钥</param>
        /// <param name="timeOut">失效时长</param>
        /// <returns></returns>
        public static bool Lock(string key, string value, TimeSpan timeOut)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();

            return db.LockTake(key, value, timeOut);
        }




        /// <summary>
        /// 释放锁
        /// </summary>
        /// <param name="key">锁的名称</param>
        /// <param name="value">解锁密钥</param>
        /// <returns></returns>
        public static bool UnLock(string key, string value)
        {
            var redis = ConnectionMultiplexer.Connect(ConnectionString);
            var db = redis.GetDatabase();

            return db.LockRelease(key, value);
        }

    }
}
