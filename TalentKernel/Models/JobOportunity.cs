using System;
using System.Collections.Generic;
using System.Text;

namespace TalentKernel.Models;

public class JobOpportunity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DescriptionUrl { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public double? SalaryMin { get; set; }
    public string Category { get; set; } = string.Empty;
}

