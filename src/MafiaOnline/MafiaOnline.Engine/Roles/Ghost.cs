using MafiaOnline.Engine.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Roles
{
    /// <summary>
    /// Ghost has a passive ability which makes him appear as a villager to all investigative roles.
    /// </summary>
    public class Ghost(GhostConfig config) : IRole
    {
        public RoleType Type => RoleType.Ghost;
        public Team Team => Team.Werewolves;
        public string Name => "Ghost";
        public Team VisibleTeam => Team.Villagers; // Ghost appears as a villager to the villagers
    }
}
