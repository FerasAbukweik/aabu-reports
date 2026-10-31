using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Enums;
using AabuReports.Core.ValueObjects;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AabuReports.Services.UnitTests;

public class ImageUploadValidationTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 5 * 1024 * 1024, true)]
    [InlineData(6, 1024, false)]
    [InlineData(1, 5 * 1024 * 1024 + 1, false)]
    public void ReportCreateDtos_ShouldApplyInheritedImageLimits(int count, long size, bool valid)
    {
        ReportCreateDto[] requests =
        [
            new ReportCreateForLocationDto
            {
                Category = ReportCategory.Cleaning, Severity = ReportSeverity.Small,
                Description = "Test report", LocationId = Guid.NewGuid()
            },
            new ReportCreateForCoordinatesDto
            {
                Category = ReportCategory.Cleaning, Severity = ReportSeverity.Small,
                Description = "Test report", Coordinates = new MapCoordinates { Lat = 32, Lng = 36 }
            }
        ];

        foreach (var request in requests)
        {
            request.Images = Enumerable.Range(0, count)
                .Select(_ => Mock.Of<IFormFile>(file => file.Length == size)).ToList();
            var context = new ValidationContext(request) { MemberName = nameof(request.Images) };

            Validator.TryValidateProperty(request.Images, context, new List<ValidationResult>())
                .Should().Be(valid);
        }
    }
}
