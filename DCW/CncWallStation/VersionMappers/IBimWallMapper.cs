using CncWallStation.MomWallData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CncWallStation.VersionMappers
{
    public interface IBimWallMapper
    {
        string SupportedVersion { get; }
        MomWall Map(string json);
    }
}
