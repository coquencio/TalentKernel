namespace TalentKernel.Models;
public class CandidateProfile
{
    public string FullName { get; set; } = string.Empty;
    public List<string> CoreSkills { get; set; } = new();
    public int YearsOfExperience { get; set; }
    public List<string> PreferredRoles { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

