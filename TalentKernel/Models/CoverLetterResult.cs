using System;
using System.Collections.Generic;
using System.Text;

namespace TalentKernel.Models;
public class CoverLetterResult
{
    public string Content { get; set; } = string.Empty;
    public List<string> MatchingSkillsUsed { get; set; } = new();
    public string Tone { get; set; } = "Professional";
}

