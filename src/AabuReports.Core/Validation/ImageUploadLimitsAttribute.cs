using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AabuReports.Core.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ImageUploadLimitsAttribute(int maxCount, int maxFileSizeMb) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;

        if (value is IReadOnlyCollection<IFormFile> images &&
            images.Count <= maxCount &&
            images.All(image => image is not null && image.Length <= maxFileSizeMb * 1024L * 1024))
            return ValidationResult.Success;

        return new ValidationResult($"Attach at most {maxCount} images, no larger than {maxFileSizeMb} MB each");
    }
}
