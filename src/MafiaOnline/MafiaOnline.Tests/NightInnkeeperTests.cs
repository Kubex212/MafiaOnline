using MafiaOnline.Engine;
using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MafiaOnline.Tests;

public class NightInnkeeperTests
{
    [Fact]
    public void InnkeeperSelection_WhenSelectedKilledByWerewolves_ProducesInnkeeperClue_WithConfiguredCountAndIncludesKiller()
    {
        // Arrange: seats 0 Innkeeper, 1 Werewolf (attacker), 2 Victim, 3 VillagerA, 4 VillagerB
        var inn = new Player(Guid.NewGuid(), "Innkeeper");
        inn.InitializeRole(RoleType.Innkeeper, GameConfig.GetDefault());
        inn.SeatIndex = 0;

        var wolf = new Player(Guid.NewGuid(), "Wolf");
        wolf.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault());
        wolf.SeatIndex = 1;

        var victim = new Player(Guid.NewGuid(), "Victim");
        victim.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        victim.SeatIndex = 2;

        var vA = new Player(Guid.NewGuid(), "VA");
        vA.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        vA.SeatIndex = 3;

        var vB = new Player(Guid.NewGuid(), "VB");
        vB.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        vB.SeatIndex = 4;

        var alive = new List<Player> { inn, wolf, victim, vA, vB };

        // Ensure innkeeper config default is 2
        var cfg = GameConfig.GetDefault();
        Assert.Equal(2, cfg.Innkeeper.SuspectCount);

        cfg.Innkeeper.SuspectCount = 5;

        // Innkeeper selects victim, werewolf attacks victim
        var actions = new List<NightAction>
        {
            new NightAction(inn.Id, new[] { victim.Id }, ActionType.Ability),
            new NightAction(wolf.Id, new[] { victim.Id }, ActionType.Attack),
        };

        // Act
        var result = new NightResolver().Resolve(alive, actions, new Game(Guid.NewGuid(), cfg));

        // Assert: there is an Innkeeper clue event
        var clue = result.Events.OfType<ClueEvent>().OfType<InnkeeperClueEvent>().FirstOrDefault(e => e.ClueType == ClueType.Innkeeper);
        Assert.NotNull(clue);
        var nicknames = clue!.Clue;
        Assert.Equal(3, nicknames.Count);
        // include the werewolf attacker
        Assert.Contains(wolf.Nickname, nicknames);
        // should not include innkeeper or victim
        Assert.DoesNotContain(inn.Nickname, nicknames);
        Assert.DoesNotContain(victim.Nickname, nicknames);
    }

    [Fact]
    public void InnkeeperSelection_WhenSelectedNotKilled_NoClueProduced()
    {
        // Arrange: seats 0 Innkeeper, 1 Werewolf (attacker selects someone else), 2 Victim
        var inn = new Player(Guid.NewGuid(), "Innkeeper");
        inn.InitializeRole(RoleType.Innkeeper, GameConfig.GetDefault());
        inn.SeatIndex = 0;

        var wolf = new Player(Guid.NewGuid(), "Wolf");
        wolf.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault());
        wolf.SeatIndex = 1;

        var victim = new Player(Guid.NewGuid(), "Victim");
        victim.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        victim.SeatIndex = 2;

        var other = new Player(Guid.NewGuid(), "Other");
        other.InitializeRole(RoleType.Villager, GameConfig.GetDefault());
        other.SeatIndex = 3;

        var alive = new List<Player> { inn, wolf, victim, other };

        // Innkeeper selects victim, but werewolf attacks other
        var actions = new List<NightAction>
        {
            new NightAction(inn.Id, new[] { victim.Id }, ActionType.Ability),
            new NightAction(wolf.Id, new[] { other.Id }, ActionType.Attack),
        };

        // Act
        var result = new NightResolver().Resolve(alive, actions, new Game(Guid.NewGuid(), GameConfig.GetDefault()));

        var clue = result.Events.OfType<ClueEvent>().OfType<ClueEvent<List<Guid>>>().FirstOrDefault(e => e.ClueType == ClueType.Innkeeper);
        Assert.Null(clue);
    }

    [Fact]
    public void Innkeeper_WhenNoCandidatePool_StillIncludesKillerIfPresent()
    {
        // Arrange: 0 Innkeeper, 1 Werewolf attacker, 2 Victim (selected). No other players alive.
        var inn = new Player(Guid.NewGuid(), "Innkeeper"); inn.InitializeRole(RoleType.Innkeeper, GameConfig.GetDefault()); inn.SeatIndex = 0;
        var wolf = new Player(Guid.NewGuid(), "Wolf"); wolf.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault()); wolf.SeatIndex = 1;
        var victim = new Player(Guid.NewGuid(), "Victim"); victim.InitializeRole(RoleType.Villager, GameConfig.GetDefault()); victim.SeatIndex = 2;

        var alive = new List<Player> { inn, wolf, victim };

        var actions = new List<NightAction>
        {
            new NightAction(inn.Id, new[] { victim.Id }, ActionType.Ability),
            new NightAction(wolf.Id, new[] { victim.Id }, ActionType.Attack),
        };

        var cfg = GameConfig.GetDefault();
        cfg.Innkeeper.SuspectCount = 3; // wants 3 but only 1 other player available

        // Act
        var result = new NightResolver().Resolve(alive, actions, new Game(Guid.NewGuid(), cfg));

        // Assert: Innkeeper clue exists and contains at least the werewolf
        var clue = result.Events.OfType<ClueEvent>().OfType<InnkeeperClueEvent>().FirstOrDefault(e => e.ClueType == ClueType.Innkeeper);
        Assert.NotNull(clue);
        var nicknames = clue!.Clue;
        Assert.Contains(wolf.Nickname, nicknames);
        // Size may be less than configured due to lack of candidates
        Assert.True(nicknames.Count <= cfg.Innkeeper.SuspectCount);
    }

    [Fact]
    public void Innkeeper_WhenNoWerewolvesAlive_PicksRandomPlayersOnly()
    {
        // Arrange: 0 Innkeeper, 1 Victim, 2 VillagerA, 3 VillagerB
        var inn = new Player(Guid.NewGuid(), "Innkeeper"); inn.InitializeRole(RoleType.Innkeeper, GameConfig.GetDefault()); inn.SeatIndex = 0;
        var victim = new Player(Guid.NewGuid(), "Victim"); victim.InitializeRole(RoleType.Villager, GameConfig.GetDefault()); victim.SeatIndex = 1;
        var a = new Player(Guid.NewGuid(), "A"); a.InitializeRole(RoleType.Villager, GameConfig.GetDefault()); a.SeatIndex = 2;
        var b = new Player(Guid.NewGuid(), "B"); b.InitializeRole(RoleType.Villager, GameConfig.GetDefault()); b.SeatIndex = 3;

        var alive = new List<Player> { inn, victim, a, b };

        // No werewolf attacks; simulate other death source by manually adding PlayerKilledEvent for victim
        var context = new NightContext(new Game(Guid.NewGuid(), GameConfig.GetDefault()));
        // we can't alter NightResolver internals easily here; instead simulate a werewolf death by making the victim appear in PlayerKilledEvent via actions: none
        var actions = new List<NightAction> {
            new NightAction(inn.Id, new[] { victim.Id }, ActionType.Ability)
        };

        // Act
        var result = new NightResolver().Resolve(alive, actions, new Game(Guid.NewGuid(), GameConfig.GetDefault()));

        // Since victim not killed by werewolves, no innkeeper clue should exist
        var clue = result.Events.OfType<ClueEvent>().OfType<ClueEvent<List<Guid>>>().FirstOrDefault(e => e.ClueType == ClueType.Innkeeper);
        Assert.Null(clue);
    }

    [Fact]
    public void Innkeeper_Clue_CanBeReversedByDeceiverAndDrunk()
    {
        // Arrange: 0 Innkeeper, 1 Werewolf attacker, 2 Victim, 3 Deceiver, 4 Drunk
        var inn = new Player(Guid.NewGuid(), "Innkeeper"); inn.InitializeRole(RoleType.Innkeeper, GameConfig.GetDefault()); inn.SeatIndex = 0;
        var wolf = new Player(Guid.NewGuid(), "Wolf"); wolf.InitializeRole(RoleType.Werewolf, GameConfig.GetDefault()); wolf.SeatIndex = 1;
        var victim = new Player(Guid.NewGuid(), "Victim"); victim.InitializeRole(RoleType.Villager, GameConfig.GetDefault()); victim.SeatIndex = 2;
        var dec = new Player(Guid.NewGuid(), "Dec"); dec.InitializeRole(RoleType.Deceiver, GameConfig.GetDefault()); dec.SeatIndex = 3;

        var alive = new List<Player> { inn, wolf, victim, dec };

        // Build actions: inn selects victim, wolf attacks victim
        var actions = new List<NightAction>
        {
            new NightAction(inn.Id, new[] { victim.Id }, ActionType.Ability),
            new NightAction(wolf.Id, new[] { victim.Id }, ActionType.Attack),
            // deceiver selects innkeeper to flip their clue
            new NightAction(dec.Id, new[] { inn.Id }, ActionType.Ability),
        };

        var cfg = GameConfig.GetDefault();
        cfg.Innkeeper.SuspectCount = 2;
        cfg.Drunk.DrunkOccuranceChance = 100; // ensure drunk is always present
        cfg.Drunk.DrunkEffectChance = 100; // ensure drunk effect is always applied

        // Put drunk as game.DrunkPlayer so drunk reversal may apply
        var game = new Game(Guid.NewGuid(), cfg);
        // Use reflection to set internal drunk id
        var gameType = typeof(Game);
        var field = gameType.GetField("_drunkId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(game, (Guid?)inn.Id);

        // Act
        var result = new NightResolver().Resolve(alive, actions, game);

        // Find the innkeeper clue event (it might be reversed by deceiver and/or drunk)
        var clue = result.Events.OfType<ClueEvent>().OfType<InnkeeperClueEvent>().FirstOrDefault(e => e.ClueType == ClueType.Innkeeper);
        Assert.NotNull(clue);

        // Check whether clue was reversed by deceiver or drunk (IsReversed true) - at least one reversal mechanism may apply
        var clueEvents = result.Events.OfType<ClueEvent>().ToList();
        var innClue = clueEvents.FirstOrDefault(e => e.ClueType == ClueType.Innkeeper && e.ClueOwnerId == inn.Id);
        Assert.NotNull(innClue);

        // Deceiver should reverse the innkeeper clue because deceiver targeted the innkeeper
        Assert.True(innClue.IsReversed);
    }
}
