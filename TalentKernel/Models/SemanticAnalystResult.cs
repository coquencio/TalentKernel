namespace TalentKernel.Models;
public class SemanticAnalystResult
{
    public bool MeetsCriteria { get; set; }
    public double ConfidenceScore { get; set; } // 0.0 to 1.0
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, string> FoundDetails { get; set; } = new();
}

