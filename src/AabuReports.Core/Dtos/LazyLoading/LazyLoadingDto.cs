using System.ComponentModel.DataAnnotations;

namespace AabuReports.Core.Dtos.LazyLoading;

public class LazyLoadingDto
{
    [Range(1, 100)]
    public required int Take { get; set; }
    [Range(0, int.MaxValue)]
    public required int Skip { get; set; }
}