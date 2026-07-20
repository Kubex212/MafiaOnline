using Newtonsoft.Json;
using Xunit;
using MafiaOnline.Api.Models;

namespace MafiaOnline.Tests;
public class LobbySerializationTests
{
    [Fact]
    public void Lobby_Should_Survive_Serialization_Roundtrip()
    {
        // arrange
        var lobby = new Lobby("ABC123");

        var player = new LobbyPlayer("Bob", true);
        lobby.AddPlayer(player);

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        // act
        var json = JsonConvert.SerializeObject(lobby, settings);
        var restored = JsonConvert.DeserializeObject<Lobby>(json, settings);

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

        lobby.AddPlayer(new LobbyPlayer("Bob", true));
        lobby.AddPlayer(new LobbyPlayer("Alice", false));
        lobby.AddPlayer(new LobbyPlayer("Tom", false));
        lobby.AddPlayer(new LobbyPlayer("John", false));

        lobby.Start(lobby.Players.First().Id);

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        var json = JsonConvert.SerializeObject(lobby, settings);

        var restored = JsonConvert.DeserializeObject<Lobby>(json, settings);

        Assert.NotNull(restored);
        Assert.NotNull(restored.Game);

        Assert.Equal(lobby.Game.Id, restored.Game.Id);
        Assert.Equal(lobby.Game.Players.Count, restored.Game.Players.Count);
    }
}