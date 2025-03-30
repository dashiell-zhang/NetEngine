using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI.Core.Libraries.JsonConverters
{


    public class StringConverter : JsonConverter<string>
    {

        //用于webapi入参层面,对于 string? 类型的字段入参如果是空字符串则直接设置为null，对于 string 类型的则默认为必填本身就会被API拦截
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            else
            {
                return value;
            }
        }


        //为了保持返回信息的准确原则性质，所以 write 部分不做任何调整
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

}
