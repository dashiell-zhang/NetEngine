using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebAPI.Core.Libraries.Validators
{
    public class EnumValidationProvider : IModelValidatorProvider
    {
        public void CreateValidators(ModelValidatorProviderContext context)
        {
            var modelType = context.ModelMetadata.ModelType;

            if (modelType.IsEnum || Nullable.GetUnderlyingType(modelType)?.IsEnum == true)
            {
                context.Results.Add(new ValidatorItem
                {
                    Validator = new EnumValidator(),
                    IsReusable = true
                });
            }

        }
    }
}
