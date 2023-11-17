namespace Repository.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class JsonIndexAttribute : Attribute
    {

        public bool IsUnique { get; set; }

        public bool AllDescending { get; set; }

        public bool[]? IsDescending { get; set; }


        public List<string> PropertyNames = [];


        public JsonIndexAttribute(params string[] additionalPropertyNames)
        {
            var ss = additionalPropertyNames.Distinct().Count();

            if (ss != additionalPropertyNames.Length)
            {
                throw new ArgumentException("additionalPropertyNames 不允许存在重复的值");
            }

            PropertyNames.AddRange(additionalPropertyNames);
        }

    }
}
