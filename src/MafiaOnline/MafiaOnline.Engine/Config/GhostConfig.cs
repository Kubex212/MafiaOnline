using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Config;

public class GhostConfig
{
    /// <summary>
    /// Since Ghost is probably the most powerful werewolf role you should be able to specify whether it should obtain alibi.
    /// Of course it also depends on the alibi game setting.
    /// </summary>
    public bool ShouldGetAlibi { get; set; } = false;
}
