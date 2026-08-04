using MafiaOnline.Engine.Roles;

namespace Api.Dtos;

public class UserStatsResponse
{
    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public double WinRate { get; set; }

    public int SurvivalRate { get; set; }

    public Dictionary<string, RoleStatisticsResponse> Roles { get; set; } = [];
}

public class RoleStatisticsResponse
{
    public int Games { get; set; }

    public int Wins { get; set; }

    public double WinRate { get; set; }
}

public class UserStatisticsResponse
{
    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public double WinRate { get; set; }

    public double SurvivalRate { get; set; }

    public Dictionary<RoleType, RoleStatisticsResponse> Roles { get; set; } = [];
}
