using MafiaOnline.Engine.Config;
using MafiaOnline.Engine.Core;
using MafiaOnline.Engine.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Tests;

public class RoleTests
{
    [Fact]
    public void RoleFactory_Should_Support_All_Roles()
    {
        var config = new GameConfig();

        foreach (var roleType in Enum.GetValues<RoleType>())
        {
            var role = RoleFactory.Create(roleType, config);

            Assert.Equal(roleType, role.Type);
            Assert.Equal(roleType.GetTeam(), role.Team);
        }
    }
}
