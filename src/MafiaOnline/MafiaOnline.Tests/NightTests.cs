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
    public void AllAlivePlayers_ShouldNeedAction_DuringNight()
    {
        var lobby = new Lobby("TEST123");
        var firstPlayer = new LobbyPlayer(Guid.NewGuid(), "Player1", true);
        var secondPlayer = new LobbyPlayer(Guid.NewGuid(), "Player2", false);
        var thirdPlayer = new LobbyPlayer(Guid.NewGuid(), "Player3", false);
        var fourthPlayer = new LobbyPlayer(Guid.NewGuid(), "Player4", false);

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

        // assert - all alive players should need to submit an action
        Assert.Equal(4, night.PlayersPendingAction.Count);
        Assert.Contains(firstPlayerId, night.PlayersPendingAction);
        Assert.Contains(secondPlayerId, night.PlayersPendingAction);
        Assert.Contains(thirdPlayerId, night.PlayersPendingAction);
        Assert.Contains(fourthPlayerId, night.PlayersPendingAction);
    }
}
