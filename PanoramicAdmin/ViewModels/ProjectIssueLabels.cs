namespace PanoramicAdmin.ViewModels;

public static class ProjectIssueLabels
{
    public static string GetActionPlanLetter(int index)
    {
        var label = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
        {
            label = $"{(char)('A' + (value - 1) % 26)}{label}";
        }

        return label;
    }
}
