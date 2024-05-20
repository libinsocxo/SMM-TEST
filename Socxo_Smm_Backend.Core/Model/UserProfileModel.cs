namespace Socxo_Smm_Backend.Core.Model;

public class UserProfileModel
{
    public required string firstName { get; set; }
    public required string secondName { get; set; }
    public string? about { get; set; }
    public string? profileUrl { get; set; }
    
}