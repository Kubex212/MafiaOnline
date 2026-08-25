using MafiaOnline.Engine;
using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MafiaOnline.Tests;

public class SorcererTests
{
    [Fact]
    public void Sorcerer_Counts_Werewolves_Among_Selection()
    {
        // Arrange: seats 0 Sorcerer, 1 Werewolf, 2 Villager, 3 Werewolf
        var sorc = new Player(Guid.NewGuid(), "Sorc");
        sorc.InitializeRole(RoleType.Sorcerer, GameConfig.GetDefault());
        sorc.SeatIndex = 0;

        var wolf1 = new Player(Guid.NewGuid(), "W1");
        wolf1.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault());
        wolf1.SeatIndex = 1;

        var vill = new Player(Guid.NewGuid(), "V");
        vill.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        vill.SeatIndex = 2;

        var wolf2 = new Player(Guid.NewGuid(), "W2");
        wolf2.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault());
        wolf2.SeatIndex = 3;

        var alive = new List<Player> { sorc, wolf1, vill, wolf2 };

        // Sorcerer selects wolf1 and vill
        var actions = new List<NightAction>
        {
            new NightAction(sorc.Id, new[] { wolf1.Id, vill.Id }, ActionType.Ability)
        };

        // Act
        var result = new NightResolver().Resolve(alive, actions, new Game(Guid.NewGuid(), GameConfig.GetDefault()));

        // Assert: sorcerer gets a clue with count = 1
        var clue = result.Events.OfType<ClueEvent>().SingleOrDefault(e => e.ClueType == ClueType.Sorcerer) as ClueEvent<int>;
        Assert.NotNull(clue);
        Assert.Equal(1, clue!.Clue);
    }
}
