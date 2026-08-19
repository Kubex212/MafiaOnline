using MafiaOnline.Api.Models;
using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Roles;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace MafiaOnline.Tests;
public class LobbySerializationTests
{
    private JsonSerializerSettings Settings => new JsonSerializerSettings { 
        TypeNameHandling = TypeNameHandling.Auto,
        Converters = { new StringEnumConverter() }
    };

    [Fact]
    public void Lobby_Should_Survive_Serialization_Roundtrip()
    {
        // arrange
        var lobby = new Lobby("ABC123");

        var player = new LobbyPlayer(Guid.NewGuid(), "Bob", true);
        lobby.AddPlayer(player);

        // act
        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        // assert
        Assert.NotNull(restored);
        Assert.Equal(lobby.Id, restored.Id);
        Assert.Equal(lobby.Code, restored.Code);

        Assert.Single(restored.Players);

        var restoredPlayer = restored.Players.First();

        Assert.Equal(player.Id, restoredPlayer.Id);
        Assert.Equal(player.Nickname, restoredPlayer.Nickname);
        Assert.Equal(player.IsHost, restoredPlayer.IsHost);
    }

    [Fact]
    public void StartedLobby_Should_Restore_Game()
    {
        var lobby = new Lobby("ABC123");

        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Bob", true));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Alice", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Tom", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "John", false));

        lobby.Start(lobby.Players.First().Id);

        var json = JsonConvert.SerializeObject(lobby, Settings);

        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Game);

        Assert.Equal(lobby.Game.Id, restored.Game.Id);
        Assert.Equal(lobby.Game.Players.Count, restored.Game.Players.Count);
    }

    [Fact]
    public void StartedLobby_GameStateProperties_AreRestoredAfterSerialization()
    {
        var lobby = new Lobby("ABC123");

        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Bob", true));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Alice", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Tom", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "John", false));

        lobby.Start(lobby.Players.First().Id);

        var beforeStatus = lobby.Game!.Status;
        var beforePhase = lobby.Game.Phase;
        var beforeWinner = lobby.Game.Winner;

        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.Game);

        Assert.Equal(beforeStatus, restored.Game.Status);
        Assert.Equal(beforePhase, restored.Game.Phase);
        Assert.Equal(beforeWinner, restored.Game.Winner);
    }

    [Fact]
    public void StartedLobby_PlayerSeatIndexes_SerializedAndRestored()
    {
        var lobby = new Lobby("ABC123");

        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Bob", true));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Alice", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "Tom", false));
        lobby.AddPlayer(new LobbyPlayer(Guid.NewGuid(), "John", false));

        lobby.Start(lobby.Players.First().Id);

        var beforeSeats = lobby.Players.Select(p => p.SeatIndex).ToList();

        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.Game);

        var afterSeats = restored.Players.Select(p => p.SeatIndex).ToList();
        Assert.Equal(beforeSeats, afterSeats);
    }

    [Fact]
    public void Lobby_With_NightSession_Should_Serialize_And_Restore()
    {
        var lobby = new Lobby("ABC123");
        var host = new LobbyPlayer(Guid.NewGuid(), "Host", true);
        var p2 = new LobbyPlayer(Guid.NewGuid(), "Alice", false);
        var p3 = new LobbyPlayer(Guid.NewGuid(), "Bob", false);
        var p4 = new LobbyPlayer(Guid.NewGuid(), "Tom", false);

        lobby.AddPlayer(host);
        lobby.AddPlayer(p2);
        lobby.AddPlayer(p3);
        lobby.AddPlayer(p4);

        lobby.Start(host.Id);

        lobby.BeginNightSession();

        var active = lobby.NightSession!.ActivePlayer!;
        // submit an action and advance
        lobby.NightSession.SubmitAction(new Engine.Night.NightAction(active.Id, new List<Guid> { p2.Id }, Engine.Night.ActionType.Ability));

        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.NightSession);
        Assert.True(restored.NightSession.SubmittedActions.SingleOrDefault(sa => sa.Actor == active.Id && sa.ActionType == Engine.Night.ActionType.Ability) != null);
    }

    [Fact]
    public void Lobby_With_DaySession_Should_Serialize_And_Restore()
    {
        var lobby = new Lobby("ABC123");
        var host = new LobbyPlayer(Guid.NewGuid(), "Host", true);
        var p2 = new LobbyPlayer(Guid.NewGuid(), "Alice", false);
        var p3 = new LobbyPlayer(Guid.NewGuid(), "Bob", false);
        var p4 = new LobbyPlayer(Guid.NewGuid(), "Tom", false);

        lobby.AddPlayer(host);
        lobby.AddPlayer(p2);
        lobby.AddPlayer(p3);
        lobby.AddPlayer(p4);

        lobby.Start(host.Id);

        lobby.BeginDaySession();

        lobby.CastVote(p2.Id, p3.Id);
        lobby.CastVote(p3.Id, p2.Id);

        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.DaySession);
        Assert.Equal(2, restored.DaySession.Votes.Count);
        Assert.True(restored.DaySession.HasVoted(p2.Id));
    }

    [Fact]
    public void Lobby_Game_Player_State_Restored_After_Serialization()
    {
        var lobby = new Lobby("ABC123");
        var host = new LobbyPlayer(Guid.NewGuid(), "Host", true);
        var p2 = new LobbyPlayer(Guid.NewGuid(), "Alice", false);
        var p3 = new LobbyPlayer(Guid.NewGuid(), "Bob", false);
        var p4 = new LobbyPlayer(Guid.NewGuid(), "Tom", false);

        lobby.AddPlayer(host);
        lobby.AddPlayer(p2);
        lobby.AddPlayer(p3);
        lobby.AddPlayer(p4);

        lobby.Start(host.Id);

        // kill a player in the game
        var anyGamePlayer = lobby.Game!.Players.First();
        anyGamePlayer.Kill();

        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.Game);
        Assert.Equal(lobby.Game.Players.Count, restored.Game.Players.Count);
        // ensure the killed flag was restored
        Assert.Equal(lobby.Game.Players.First().IsAlive, restored.Game.Players.First().IsAlive);
    }

    [Fact]
    public void Lobby_Should_Survive_Config_Serialization_Roundtrip()
    {
        // arrange
        var lobby = new Lobby("ABC123");

        var host = new LobbyPlayer(Guid.NewGuid(), "Host", true);
        var p2 = new LobbyPlayer(Guid.NewGuid(), "Alice", false);
        var p3 = new LobbyPlayer(Guid.NewGuid(), "Bob", false);
        var p4 = new LobbyPlayer(Guid.NewGuid(), "Tom", false);

        lobby.AddPlayer(host);
        lobby.AddPlayer(p2);
        lobby.AddPlayer(p3);
        lobby.AddPlayer(p4);

        lobby.Start(host.Id);

        lobby.Game!.Config.EnabledRoles = new HashSet<RoleType>
        {
            RoleType.Guardian,
            RoleType.Detective,
            RoleType.Dracula,
            RoleType.Fool
        };

        lobby.Game.Config.WerewolfCount = 2;
        lobby.Game.Config.TurnDurationSeconds = 45;

        // act
        var json = JsonConvert.SerializeObject(lobby, Settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, Settings);

        // assert
        Assert.NotNull(restored);
        Assert.NotNull(restored.Game);

        Assert.Equal(
            lobby.Game.Config.EnabledRoles,
            restored.Game.Config.EnabledRoles);

        Assert.Equal(
            lobby.Game.Config.WerewolfCount,
            restored.Game.Config.WerewolfCount);

        Assert.Equal(
            lobby.Game.Config.TurnDurationSeconds,
            restored.Game.Config.TurnDurationSeconds);
    }

    [Fact]
    public void GameConfig_Should_Deserialize_EnabledRoles()
    {
        var json = """
        {
            "WerewolfCount": 2,
            "TurnDurationSeconds": 45,
            "EnabledRoles": ["Guardian", "Detective", "Dracula", "Fool"]
        }
        """;

        var config = JsonConvert.DeserializeObject<GameConfig>(json, Settings)!;

        Assert.Equal(4, config.EnabledRoles.Count);
        Assert.Equal(
            new HashSet<RoleType>
            {
            RoleType.Guardian,
            RoleType.Detective,
            RoleType.Dracula,
            RoleType.Fool
            },
            config.EnabledRoles);
    }
}
