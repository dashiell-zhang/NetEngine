namespace SourceGenerator.Abstraction.Attributes
{


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class AutoProxyAttribute : Attribute
    {


        public bool EnableLogging { get; set; } = true;



        public bool CaptureArguments { get; set; } = true;


        public bool MeasureTime { get; set; } = true;

    }

}
