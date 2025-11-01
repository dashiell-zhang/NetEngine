namespace SourceGenerator.Abstraction.Attributes
{

    /// <summary>
    /// 标记此类或接口需要生成代理类�?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class AutoProxyAttribute : Attribute
    {

        /// <summary>
        /// 是否启用方法调用日志（默�?true）�?
        /// </summary>
        public bool EnableLogging { get; set; } = true;


        /// <summary>
        /// 是否记录参数（默�?true）�?
        /// </summary>
        public bool CaptureArguments { get; set; } = true;


        /// <summary>
        /// 是否记录执行耗时（默�?true）�?
        /// </summary>
        public bool MeasureTime { get; set; } = true;


        // �������׺�̶�Ϊ "_Proxy"�������ṩ�������
    }

}
