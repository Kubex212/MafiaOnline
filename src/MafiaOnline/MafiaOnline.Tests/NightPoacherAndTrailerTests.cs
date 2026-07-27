using MafiaOnline.Engine;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MafiaOnline.Tests;

public class NightPoacherAndTrailerTests
{
    [Fact]
    public void PoacherTrap_Triggers_WerewolfDeath_WhenVictimKilled()
    {
        // Arrange: build players seat-ordered: 0 Poacher, 1 Werewolf A, 2 Town victim, 3 Werewolf B
        var poacher = new Player(Guid.NewGuid(), "Poacher") { Role = new Poacher(), SeatIndex = 0 };
        var wolfA = new Player(Guid.NewGuid(), "WolfA") { Role = new Werewolf(), SeatIndex = 1 };
        var victim = new Player(Guid.NewGuid(), "Victim") { Role = new Villager(), SeatIndex = 2 };
        var wolfB = new Player(Guid.NewGuid(), "WolfB") { Role = new Werewolf(), SeatIndex = 3 };

        var alive = new List<Player> { poacher, wolfA, victim, wolfB };

        // Poacher sets trap on victim and wolfA attacks victim
        var actions = new List<SubmittedNightAction>
        {
            new SubmittedNightAction(poacher.Id, new NightAction(new[] { victim.Id }, ActionType.Ability)),
            new SubmittedNightAction(wolfA.Id, new NightAction(new[] { victim.Id }, ActionType.Attack)),
        };

        // Act
        var result = new NightResolver().Resolve(alive, actions);

        // Assert: victim and one werewolf should be in killed
        Assert.Contains(victim.Id, result.Killed);
        // exactly one werewolf killed
        var wolvesKilled = result.Killed.Where(id => id == wolfA.Id || id == wolfB.Id).ToList();
        Assert.Single(wolvesKilled);
    }

    [Fact]
    public void Trailer_Gives_LeftOrRight_Clue()
    {
        // Arrange: seat order 0 Trailer, 1 Werewolf, 2 Villager
        var trailer = new Player(Guid.NewGuid(), "Trailer") { Role = new Trailer(), SeatIndex = 0 };
        var wolf = new Player(Guid.NewGuid(), "Wolf") { Role = new Werewolf(), SeatIndex = 1 };
        var vill = new Player(Guid.NewGuid(), "Vill") { Role = new Villager(), SeatIndex = 2 };
        var alive = new List<Player> { trailer, wolf, vill };

        var resolver = new NightResolver();
        // Run resolver to collect automatic clues
        var result = resolver.Resolve(alive, new List<SubmittedNightAction>());

        var trailerEvent = result.Events.OfType<TrailerClueEvent>().FirstOrDefault(e => e.TrailerId == trailer.Id);
        Assert.NotNull(trailerEvent);
        Assert.True(trailerEvent.Direction == "Left" || trailerEvent.Direction == "Right" || trailerEvent.Direction == "None");
    }
}
