//using CncWallStation.Features;
//using CncWallStation.Features.Grooves;
//using CncWallStation.Features.MepSlots;
//using CncWallStation.MomWallData;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace CncWallStation.Helpers
//{
//    /// <summary>
//    /// MomWall → DObject JSON 映射工具（Controller3D.html / MomWall3D.html 加载渲染共用管道）
//    /// </summary>
//    public static class MomWallDObjectMapper
//    {
//        /// <summary>
//        /// 将 MomWall 对象转换为 HTML 3D 引擎所需的 DObject JSON 字符串
//        /// </summary>
//        public static string MapToDObject(MomWall momWall)
//        {
//            // 提取轮廓顶点
//            var outline = momWall.Outline
//                .Select(p => new { x = p.X, y = p.Y })
//                .ToList();

//            float actualLength = momWall.ActualLength;
//            float actualWidth = momWall.ActualWidth;
//            float actualThickness = momWall.ActualThickness;

//            // 提取特征: Groove、MepSlot、Pocket、Hole、RebarSlot
//            var features = new List<object>();
//            foreach (var feature in momWall.Features)
//            {
//                if (feature is Groove groove)
//                    features.Add(SerializeGroove(groove));
//                else if (feature is MepSlot mepSlot)
//                    features.Add(SerializeMepSlot(mepSlot));
//                else if (feature is Pocket pocket)
//                    features.Add(SerializePocket(pocket));
//                else if (feature is Hole hole)
//                    features.Add(SerializeHole(hole));
//                else if (feature is RebarSlot rebarSlot)
//                    features.Add(SerializeRebarSlot(rebarSlot));
//            }

//            var material = momWall.Material ?? "AAC";

//            var dObject = new
//            {
//                outline,
//                actualLength,
//                actualWidth,
//                actualThickness,
//                material,
//                thickness = actualThickness,
//                length = actualLength,
//                obbLength = momWall.ObbLength,
//                obbWidth = momWall.ObbWidth,
//                features
//            };

//            return JsonConvert.SerializeObject(dObject, Formatting.None);
//        }

//        private static string MapGrooveTypeKey(GrooveType grooveType)
//        {
//            return grooveType switch
//            {
//                GrooveType.SteelColumn => "steelColumn",
//                GrooveType.TopBracket => "topBracket",
//                GrooveType.BaseBracket => "baseBracket",
//                GrooveType.TopPlate => "topPlate",
//                GrooveType.GlueSeal => "glueSeal",
//                GrooveType.XBraceSteel => "xBraceSteel",
//                GrooveType.Custom => "default",
//                _ => "default"
//            };
//        }

//        private static object SerializeGroove(Groove groove)
//        {
//            var outlinePoints = new List<object>();
//            var (p0, p1, p2, p3) = groove.GetCorners();
//            outlinePoints.Add(new { x = p0.X, y = p0.Y });
//            outlinePoints.Add(new { x = p1.X, y = p1.Y });
//            outlinePoints.Add(new { x = p2.X, y = p2.Y });
//            outlinePoints.Add(new { x = p3.X, y = p3.Y });

//            var normal = groove.CurrentNormal;

//            return new
//            {
//                id = groove.Id,
//                featureType = "Groove",
//                grooveType = MapGrooveTypeKey(groove.GrooveType),
//                startPt = new { x = groove.StartPt.X, y = groove.StartPt.Y },
//                endPt = new { x = groove.EndPt.X, y = groove.EndPt.Y },
//                width = groove.Width,
//                depth = groove.Depth,
//                length = groove.Length,
//                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
//                initialSide = groove.InitialSide.ToString(),
//                currentSide = groove.CurrentSide.ToString(),
//                outlinePoints
//            };
//        }

//        private static object SerializeMepSlot(MepSlot mepSlot)
//        {
//            var normal = mepSlot.CurrentNormal;

//            var segments = mepSlot.Segments.Select(seg =>
//            {
//                if (seg is LineSegment line)
//                {
//                    return (object)new
//                    {
//                        type = "Line",
//                        startPoint = new { x = line.StartPoint.X, y = line.StartPoint.Y },
//                        endPoint = new { x = line.EndPoint.X, y = line.EndPoint.Y },
//                        depth = line.Depth,
//                        length = line.Length,
//                        overrideWidth = line.OverrideWidth
//                    };
//                }
//                else if (seg is ArcSegment arc)
//                {
//                    return (object)new
//                    {
//                        type = "Arc",
//                        center = new { x = arc.Center.X, y = arc.Center.Y },
//                        radius = arc.Radius,
//                        StartAngleDeg = arc.StartAngleDeg,
//                        EndAngleDeg = arc.EndAngleDeg,
//                        isClockwise = arc.IsClockwise,
//                        depth = arc.Depth,
//                        length = arc.Length,
//                        overrideWidth = arc.OverrideWidth
//                    };
//                }
//                return null;
//            }).Where(s => s != null).ToList();

//            return new
//            {
//                id = mepSlot.Id,
//                featureType = "MepSlot",
//                width = mepSlot.Width,
//                depth = mepSlot.MinDepth,
//                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
//                initialSide = mepSlot.InitialSide.ToString(),
//                currentSide = mepSlot.CurrentSide.ToString(),
//                totalLength = mepSlot.TotalLength,
//                segmentCount = mepSlot.SegmentCount,
//                isUniformDepth = mepSlot.IsUniformDepth,
//                pathStart = mepSlot.PathStart != null
//                    ? new { x = mepSlot.PathStart.Value.X, y = mepSlot.PathStart.Value.Y }
//                    : null,
//                pathEnd = mepSlot.PathEnd != null
//                    ? new { x = mepSlot.PathEnd.Value.X, y = mepSlot.PathEnd.Value.Y }
//                    : null,
//                segments
//            };
//        }

//        private static object SerializeHole(Hole hole)
//        {
//            var normal = hole.CurrentNormal;

//            return new
//            {
//                id = hole.Id,
//                featureType = "Hole",
//                shape = hole.Shape.ToString(),
//                radius = hole.Radius,
//                depth = hole.Depth,
//                slotLength = hole.SlotLength,
//                slotAngleDeg = hole.SlotAngleDeg,
//                throughHole = hole.ThroughHole,
//                localPos = new { x = hole.LocalPos.X, y = hole.LocalPos.Y },
//                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
//                initialSide = hole.InitialSide.ToString(),
//                currentSide = hole.CurrentSide.ToString()
//            };
//        }

//        private static object SerializePocket(Pocket pocket)
//        {
//            var normal = pocket.CurrentNormal;
//            float halfLen = pocket.Length / 2f;

//            var startPt = new { x = pocket.LocalPos.X - halfLen, y = pocket.LocalPos.Y };
//            var endPt = new { x = pocket.LocalPos.X + halfLen, y = pocket.LocalPos.Y };

//            return new
//            {
//                id = pocket.Id,
//                featureType = "Groove",
//                grooveType = "pocket",
//                startPt,
//                endPt,
//                width = pocket.Width,
//                depth = pocket.Depth,
//                length = pocket.Length,
//                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
//                initialSide = pocket.InitialSide.ToString(),
//                currentSide = pocket.CurrentSide.ToString(),
//                outlinePoints = (object?)null
//            };
//        }

//        private static object SerializeRebarSlot(RebarSlot rebarSlot)
//        {
//            var normal = rebarSlot.CurrentNormal;

//            return new
//            {
//                id = rebarSlot.Id,
//                featureType = "RebarSlot",
//                localPos = new { x = rebarSlot.LocalPos.X, y = rebarSlot.LocalPos.Y },
//                endPos = new { x = rebarSlot.EndPos.X, y = rebarSlot.EndPos.Y },
//                diameter = rebarSlot.Diameter,
//                depth = rebarSlot.Depth,
//                length = rebarSlot.Length,
//                direction = rebarSlot.Direction.ToString(),
//                startThreading = rebarSlot.StartThreading,
//                endThreading = rebarSlot.EndThreading,
//                pn = rebarSlot.Pn,
//                currentNormal = new { x = normal.X, y = normal.Y, z = normal.Z },
//                initialSide = rebarSlot.InitialSide.ToString(),
//                currentSide = rebarSlot.CurrentSide.ToString()
//            };
//        }
//    }
//}
