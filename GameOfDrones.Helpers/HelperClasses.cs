using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfDrones
{
    public static class MathHelper
    {
        public static T Min<T>(params T[] values)
        {
            return values.Min();
        }

        public static T Max<T>(params T[] values)
        {
            return values.Max();
        }

        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> selector)
        {
            return source.MaxBy(selector, Comparer<TKey>.Default);
        }

        public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> selector, IComparer<TKey> comparer)
        {
            if(source == null) throw new ArgumentNullException("source");
            if(selector == null) throw new ArgumentNullException("selector");
            if(comparer == null) throw new ArgumentNullException("comparer");

            using(var sourceIterator = source.GetEnumerator())
            {
                if(!sourceIterator.MoveNext())
                    throw new InvalidOperationException("Sequence contains no elements");

                var max = sourceIterator.Current;
                var maxKey = selector(max);
                while(sourceIterator.MoveNext())
                {
                    var candidate = sourceIterator.Current;
                    var candidateProjected = selector(candidate);
                    if(comparer.Compare(candidateProjected, maxKey) > 0)
                    {
                        max = candidate;
                        maxKey = candidateProjected;
                    }
                }
                return max;
            }
        }

        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> selector)
        {
            return source.MinBy(selector, Comparer<TKey>.Default);
        }

        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> selector, IComparer<TKey> comparer)
        {
            if(source == null) throw new ArgumentNullException("source");
            if(selector == null) throw new ArgumentNullException("selector");
            if(comparer == null) throw new ArgumentNullException("comparer");

            using(var sourceIterator = source.GetEnumerator())
            {
                if(!sourceIterator.MoveNext())
                    throw new InvalidOperationException("Sequence contains no elements");

                var min = sourceIterator.Current;
                var minKey = selector(min);
                while(sourceIterator.MoveNext())
                {
                    var candidate = sourceIterator.Current;
                    var candidateProjected = selector(candidate);
                    if(comparer.Compare(candidateProjected, minKey) < 0)
                    {
                        min = candidate;
                        minKey = candidateProjected;
                    }
                }
                return min;
            }
        }

        public static int IndexOfMax<TSource>(this IList<TSource> source)
        {
            return Enumerable.Range(0, source.Count).MaxBy(id => source[id]);
        }

        public static int IndexOfMaxBy<TSource, TKey>(this IList<TSource> source,
            Func<TSource, TKey> selector)
        {
            return Enumerable.Range(0, source.Count).MaxBy(id => selector(source[id]));
        }

        public static int IndexOfMin<TSource>(this IList<TSource> source)
        {
            return Enumerable.Range(0, source.Count).MinBy(id => source[id]);
        }

        public static int IndexOfMinBy<TSource, TKey>(this IList<TSource> source,
            Func<TSource, TKey> selector)
        {
            return Enumerable.Range(0, source.Count).MinBy(id => selector(source[id]));
        }

        public static double DotProduct(Point v1a, Point v1b, Point v2a, Point v2b)
        {
            var x1 = v1b.X - v1a.X;
            var y1 = v1b.Y - v1a.Y;
            var x2 = v2b.X - v2a.X;
            var y2 = v2b.Y - v2a.Y;
            return x1 * x2 + y1 * y2;
        }
    }

    public static class HelperExtensions
    {
        public static double DistanceTo(this Point aPoint, Point anotherPoint)
        {
            var dx = aPoint.X - anotherPoint.X;
            var dy = aPoint.Y - anotherPoint.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double DistanceToLine(this Point point, Point linePointA, Point linePointB)
        {
            if(linePointA.X == linePointB.X)
                return Math.Abs(linePointA.X - point.X);
            if(linePointA.Y == linePointB.Y)
                return Math.Abs(linePointA.Y - point.Y);

            var dx = linePointB.X - linePointA.X;
            var dy = linePointB.Y - linePointA.Y;
            var slope = dy / dx;
            return Math.Abs((slope * point.X - point.Y) - (slope * linePointA.X - linePointA.Y)) / Math.Sqrt(1 + slope * slope);
        }

        public static double[] RespectiveDistancesToLine(Point[] points, Point linePointA, Point linePointB)
        {
            if(linePointA.X == linePointB.X)
                return points.Select(p => (double)Math.Abs(linePointA.X - p.X)).ToArray();
            if(linePointA.Y == linePointB.Y)
                return points.Select(p => (double)Math.Abs(linePointA.Y - p.Y)).ToArray();

            var dx = linePointB.X - linePointA.X;
            var dy = linePointB.Y - linePointA.Y;
            var slope = dy / dx;
            var c = slope * linePointA.X - linePointA.Y;
            var denom = Math.Sqrt(1 + slope * slope);
            return points.Select(p => Math.Abs((slope * p.X - p.Y) - c) / denom).ToArray();
        }

        public static Point GetReachablePoint(this Point position, Point destination)
        {
            if(position.X == destination.X)
            {
                if(position.Y < destination.Y)
                    destination.Y = MathHelper.Min(destination.Y, position.Y + GameContext.MaxMoveDistance, GameContext.FieldHeight);
                else
                    destination.Y = MathHelper.Max(destination.Y, position.Y - GameContext.MaxMoveDistance, 0);
                return destination;
            }

            if(position.Y == destination.Y)
            {
                if(position.X < destination.X)
                    destination.X = MathHelper.Min(destination.X, position.X + GameContext.MaxMoveDistance, GameContext.FieldWidth);
                else
                    destination.X = MathHelper.Max(destination.X, position.X - GameContext.MaxMoveDistance, 0);
                return destination;
            }

            var dx = (double)(destination.X - position.X);
            var dy = (double)(destination.Y - position.Y);
            var slope = dy / dx;

            // apply constraint: max move distance
            if(position.DistanceTo(destination) > GameContext.MaxMoveDistance)
            {
                dx = GameContext.MaxMoveDistance / Math.Sqrt(1 + slope * slope) * Math.Sign(dx);
                dy = slope * dx;
            }

            // apply constraint: field boundaries
            if(position.X + (int)dx < 0)
            {
                dx = 0 - position.X;
                dy = slope * dx;
            }
            else if(position.X + (int)dx >= GameContext.FieldWidth)
            {
                dx = (GameContext.FieldWidth - 1) - position.X;
                dy = slope * dx;
            }

            if(position.Y + (int)dy < 0)
            {
                dy = 0 - position.Y;
                dx = dy / slope;
            }
            else if(position.Y + (int)dy >= GameContext.FieldHeight)
            {
                dy = (GameContext.FieldHeight - 1) - position.Y;
                dx = dy / slope;
            }

            destination.X = position.X + (int)dx;
            destination.Y = position.Y + (int)dy;
            return destination;
        }

        public static Point GetIntermediatePoint(this Point position, Point destination, int distanceFromDest)
        {
            if(position.X == destination.X)
            {
                if(position.Y < destination.Y)
                    destination.Y -= distanceFromDest;
                else
                    destination.Y += distanceFromDest;
                return destination;
            }

            if(position.Y == destination.Y)
            {
                if(position.X < destination.X)
                    destination.X -= distanceFromDest;
                else
                    destination.X += distanceFromDest;
                return destination;
            }

            var dx = (double)(destination.X - position.X);
            var dy = (double)(destination.Y - position.Y);
            var slope = dy / dx;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            dx = (dist - distanceFromDest) / Math.Sqrt(1 + slope * slope) * Math.Sign(dx);
            dy = slope * dx;

            destination.X = position.X + (int)dx;
            destination.Y = position.Y + (int)dy;
            return destination;
        }

        public static Point GetMedianPoint(params Point[] points)
        {
            return new Point((int)points.Select(p => p.X).Average(), (int)points.Select(p => p.Y).Average());
        }

        public static Point GetZoneBorderPoint(this Point position, Zone zone)
        {
            return position.GetIntermediatePoint(zone.Center, Zone.Radius - 5);
        }

        public static bool IsInField(this Point aPoint)
        {
            return 0 <= aPoint.X && aPoint.X < GameContext.FieldWidth
                && 0 <= aPoint.Y && aPoint.Y < GameContext.FieldHeight;
        }

        public static bool Contains(this Zone zone, Point point)
        {
            return zone.Center.DistanceTo(point) <= Zone.Radius;
        }

        public static IEnumerable<Drone> GetDronesInZone(this GameContext context, Zone zone)
        {
            return context.Teams.SelectMany(t => t.Drones).Where(d => zone.Contains(d.Position));
        }

        public static IList<Drone> GetDronesOfTeam(this GameContext context, int teamId)
        {
            return context.Teams[teamId].Drones;
        }

        public static IList<Drone> GetDronesOfOtherTeams(this GameContext context, int teamId)
        {
            return context.Teams.Where(t => t.Id != teamId).SelectMany(t => t.Drones).ToArray();
        }

        public static IEnumerable<Drone> GetDronesCloseTo(this GameContext context, Point point, int maxDistance)
        {
            return context.Teams.SelectMany(t => t.Drones).Where(d => d.Position.DistanceTo(point) <= maxDistance).ToArray();
        }

        public static IList<Zone> GetZonesByDistance(this GameContext context, Point p)
        {
            return context.Zones.OrderBy(z => z.Center.DistanceTo(p)).ToList();
        }
    }

}
