using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Json
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {


        /// <summary>
        /// 获取或设置DateTime格式
        /// </summary>
        /// <remarks>默认为: yyyy-MM-dd HH:mm:ss</remarks>
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";


        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (DateTime.TryParse(reader.GetString(), out DateTime date))
                {
                    return date;
                }
            }
            return reader.GetDateTime();
        }


        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(this.DateTimeFormat));
        }
    }

}
