using MafiaOnline.Api.Models;
using MafiaOnline.Engine;
using MafiaOnline.Engine.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Tests;
public class NightTests
{
    [Fact]
    public void FirstPlayer_ShouldNotBeSkipped_DuringNight()
    {
        var lobby = new Lobby("TEST123");
        var firstPlayer = new LobbyPlayer("Player1", true);
        var secondPlayer = new LobbyPlayer("Player2", false);
        var thirdPlayer = new LobbyPlayer("Player3", false);
        var fourthPlayer = new LobbyPlayer("Player4", false);

        lobby.AddPlayer(firstPlayer);
        lobby.AddPlayer(secondPlayer);
        lobby.AddPlayer(thirdPlayer);
        lobby.AddPlayer(fourthPlayer);

        // arrange
        var game = new Game(Guid.NewGuid());

        var firstPlayerId = firstPlayer.Id;
        var secondPlayerId = secondPlayer.Id;
        var thirdPlayerId = thirdPlayer.Id;
        var fourthPlayerId = fourthPlayer.Id;

        game.Execute(new AddPlayerCommand(firstPlayerId, "Player1"));
        game.Execute(new AddPlayerCommand(secondPlayerId, "Player2"));
        game.Execute(new AddPlayerCommand(thirdPlayerId, "Player3"));
        game.Execute(new AddPlayerCommand(fourthPlayerId, "Player4"));

        game.Execute(new StartGameCommand(firstPlayerId, new[]
        {
            firstPlayerId,
            secondPlayerId,
            thirdPlayerId,
            fourthPlayerId,
        }));

        // act
        var night = new NightSession(lobby.Players);

        // assert
        Assert.Equal(firstPlayerId, night.ActivePlayer.Id);
    }
}
