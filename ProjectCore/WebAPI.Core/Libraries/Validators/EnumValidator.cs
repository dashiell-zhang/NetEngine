using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebAPI.Core.Libraries.Validators
{
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
                    string errMsg = "";

                    var propertyName = context.ModelMetadata.PropertyName;

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

            return Enumerable.Empty<ModelValidationResult>();
        }
    }
}
