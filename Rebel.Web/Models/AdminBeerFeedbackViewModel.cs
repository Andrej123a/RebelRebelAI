namespace Rebel.Web.Models;

public class AdminBeerFeedbackViewModel
{
    public int RatedAnswers { get; set; }

    public int PositiveAnswers { get; set; }

    public int NegativeAnswers { get; set; }

    public List<AdminBeerFeedbackItemViewModel> Beers { get; set; } = [];
}

public class AdminBeerFeedbackItemViewModel
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Ratings { get; set; }

    public int PositiveRatings { get; set; }

    public int NegativeRatings { get; set; }

    public int PositivePercent { get; set; }

    public string? MostCommonIssue { get; set; }

    public string? BestContext { get; set; }
}
