using MafiaOnline.Engine;
using MafiaOnline.Engine.Commands;
using MafiaOnline.Engine.Day;
using MafiaOnline.Engine.Night;
using MafiaOnline.Engine.Roles;
using Newtonsoft.Json;

namespace MafiaOnline.Api.Models;

public class RoleModel
{
    public RoleType Type { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// 0 = Town, 1 = Werewolves, 2 = neutral.
    /// </summary>
    public int TeamId { get; set; }
}