using Common.RedisLock.RedLock;
using StackExchange.Redis;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.RedisLock.Primitives
{
    internal class RedisScript<TArgument>
    {
        private readonly LuaScript _script;
        private readonly Func<TArgument, object> _parameters;

        public RedisScript(string script, Func<TArgument, object> parameters)
        {
            this._script = LuaScript.Prepare(RemoveExtraneousWhitespace(script));
            this._parameters = parameters;
        }

        public RedisResult Execute(IDatabase database, TArgument argument, bool fireAndForget = false) =>
            // 为了尊重数据库的键前缀，必须调用 database.ScriptEvaluate 而不是 _script.Evaluate
            database.ScriptEvaluate(this._script, this._parameters(argument), flags: RedLockHelper.GetCommandFlags(fireAndForget));

        public Task<RedisResult> ExecuteAsync(IDatabaseAsync database, TArgument argument, bool fireAndForget = false) =>
            // 为了尊重数据库的键前缀，必须调用 database.ScriptEvaluate 而不是 _script.Evaluate
            database.ScriptEvaluateAsync(this._script, this._parameters(argument), flags: RedLockHelper.GetCommandFlags(fireAndForget));

        // 将尽可能小的脚本发送到服务器
        private static string RemoveExtraneousWhitespace(string script) => Regex.Replace(script.Trim(), @"\s+", " ");
    }
}
