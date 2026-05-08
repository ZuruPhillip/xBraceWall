using BimWallData.Public;
using Infrastructure.Maths;

namespace CncWallStation.Transforms
{
    public static class WallElevationConverter
    {
        public static IEnumerable<Vec2> ToVec2Outline(this List<PointXyDto> dto)
        {
            if (dto == null)
                return Enumerable.Empty<Vec2>();

            return dto
                      .Where(p => p != null)
                      .Select(p => new Vec2(p.X, p.Y));
        }
    }
}
