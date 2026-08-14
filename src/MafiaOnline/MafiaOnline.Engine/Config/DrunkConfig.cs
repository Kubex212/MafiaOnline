using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Config;

public class DrunkConfig
{
    public int DrunkOccuranceChance { get; set; } = 0;
    public int DrunkEffectChance { get; set; } = 40;
    public bool AnnounceDrunkOccurance { get; set; } = true;
}
