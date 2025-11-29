using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebAPI.Core.Libraries.Validators;
public class EnumValidator : IModelValidator
{
    public IEnumerable<ModelValidationResult> Validate(ModelValidationContext context)
    {
        if (context.Model != null)
        {
            var value = context.Model;
            var enumType = value.GetType();

            if (!Enum.IsDefined(enumType, value))
            {
                if (!flagsEnumCheck())
                {
                    var propertyName = context.ModelMetadata.PropertyName;

                    string errMsg;
                    if (propertyName != null)
                    {
                        errMsg = $"The value {value} in field {propertyName} is invalid.";
                    }
                    else
                    {
                        errMsg = $"The value {value} is invalid.";
                    }

                    ModelValidationResult validationResult = new("", errMsg);

                    return [validationResult];
                }
            }


            bool flagsEnumCheck()
            {
                var isFlags = enumType.IsDefined(typeof(FlagsAttribute), false);

                if (isFlags)
                {
                    var enumValues = Enum.GetValues(enumType).Cast<Enum>();
                    int intValue = Convert.ToInt32(value);

                    foreach (var enumValue in enumValues)
                    {
                        int enumValueInt = Convert.ToInt32(enumValue);
                        if ((intValue & enumValueInt) == enumValueInt)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        return [];
    }
}
