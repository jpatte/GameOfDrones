using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfDrones
{
    public enum TaskType { Attack, Defend, Unknown }
    public enum Level { Medium, Low, High }

    public class Task
    {
        public TaskType Type { get; set; }
        public Level Difficulty { get; set; }
        public Level Importance { get; set; }
        public double Priority { get; set; }
        public int NbrRequiredDrones { get; set; }
        public Zone AssociatedZone { get; set; }
        public bool IsStillValid { get; set; }
    }

    public class ContextInfo
    {
        public ContextInfo(int myTeamId, GameContext context)
        {
            this.MyTeamId = myTeamId;
            this.Context = context;
        }

        public int MyTeamId { get; set; }
        public GameContext Context { get; set; }
        public ZoneInfo[] Zones { get; set; }
        public MyDroneInfo[] MyDrones { get; set; }
        public EnemyDroneInfo[] EnemyDrones { get; set; }
    }

    public class ZoneInfo
    {
        public ZoneInfo(Zone zone)
        {
            this.Zone = zone;
        }

        public Zone Zone { get; set; }
        public Level StrategicValue { get; set; }
    }

    public class MyDroneInfo
    {
        public MyDroneInfo(Drone drone)
        {
            this.Drone = drone;
        }

        public Drone Drone { get; set; }
        public Task CurrentTask { get; set; }
        public Point Destination { get; set; }
    }

    public class EnemyDroneInfo
    {
        public EnemyDroneInfo(Drone drone)
        {
            this.Drone = drone;
            this.TaskType = TaskType.Unknown;
            this.AssociatedZone = null;
        }

        public Drone Drone { get; set; }
        public TaskType TaskType { get; set; }
        public Zone AssociatedZone { get; set; }
    }

    public interface IZoneEvaluator
    {
        void GiveInitialZoneEvaluations(IList<ZoneInfo> zones);
        void UpdateZoneEvaluations(ContextInfo context);
    }

    class StupidZoneEvaluator : IZoneEvaluator
    {
        public void GiveInitialZoneEvaluations(IList<ZoneInfo> zones)
        {
            foreach(var zone in zones)
                zone.StrategicValue = 0;
        }

        public void UpdateZoneEvaluations(ContextInfo context)
        {       
        }
    }

    public class BasicZoneEvaluator : IZoneEvaluator
    {
        private double GetPointsForDistanceToEdge(double distanceRatio, int nbrZones)
        {
            return (1 - distanceRatio) * 100 * nbrZones;
        }

        private double GetPointsForDistanceToOtherZone(double distanceRatio)
        {
            return (1 - distanceRatio) * 100;
        }

        private Level GetLevelFromScore(double score, int nbrZones)
        {
            var tier1 = 100 * nbrZones * 1.2;
            var tier2 = 100 * nbrZones * 0.6;
            if(score >= tier1)
                return Level.High;
            if(score < tier2)
                return Level.Low;
            return Level.Medium;
        }

        public void GiveInitialZoneEvaluations(IList<ZoneInfo> zones)
        {
            var scores = new double[zones.Count];

            // identify the best edge (left/right) of the (relative) field
            int xMin = GameContext.FieldWidth, xMax = 0, xSum = 0;
            foreach(var zone in zones)
            {
                var x = zone.Zone.Center.X;
                if(x < xMin)
                    xMin = x;
                if(x > xMax)
                    xMax = x;
                xSum += x;
            }

            var xAvg = xSum / (double) zones.Count;
            var xSpan = (double)(xMax - xMin);

            // 1. give base score to privilegiate proximity to this edge
            bool preferLeft = (xAvg < xMin + xSpan / 2);
            if(preferLeft)
            {
                for(int i = 0; i < zones.Count; i++)
                    scores[i] = this.GetPointsForDistanceToEdge((zones[i].Zone.Center.X - xMin) / xSpan, zones.Count);
            }
            else
            {
                for(int i = 0; i < zones.Count; i++)
                    scores[i] = this.GetPointsForDistanceToEdge((xMax - zones[i].Zone.Center.X) / xSpan, zones.Count);
            }

            // 2. add extra points to zones which are close to each others
            double distMin = GameContext.FieldWidth, distMax = 0;
            var distances = new double[zones.Count, zones.Count];
            for(int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                for(int j = i + 1; j < zones.Count; j++)
                {
                    var dist = zone.Zone.Center.DistanceTo(zones[j].Zone.Center);
                    distances[i, j] = distances[j, i] = dist;

                    if(dist < distMin)
                        distMin = dist;
                    if(dist > distMax)
                        distMax = dist;
                }
            }
            var distSpan = distMax - distMin;

            for(int i = 0; i < zones.Count; i++)
            {
                for(int j = i + 1; j < zones.Count; j++)
                {
                    var dist = distances[i, j];
                    var points = this.GetPointsForDistanceToOtherZone((distMax - dist) / distSpan);
                    scores[i] += points;
                    scores[j] += points;
                }
            }

            for(int i = 0; i < zones.Count; i++)
                zones[i].StrategicValue = this.GetLevelFromScore(scores[i], zones.Count);
        }

        public void UpdateZoneEvaluations(ContextInfo context)
        {
            // do nothing
        }
    }

    public interface IDroneActivityObserver
    {
        void GuessEnemyDronesActivity(ContextInfo context);
    }

    class BasicDroneActivityObserver : IDroneActivityObserver
    {
        class ZoneDistance
        {
            public Zone Zone { get; set; }
            public double Distance { get; set; }
        }

        public void GuessEnemyDronesActivity(ContextInfo context)
        {
            var zones = context.Context.Zones;
            var zoneCenters = zones.Select(z => z.Center).ToArray();
            var zoneDistances = zones.Select(z => new ZoneDistance { Zone = z }).ToArray();

            foreach(var drone in context.EnemyDrones)
            {
                // based on the drone last movement, try to identify the zone it's trying to reach
                var position = drone.Drone.Position;
                var previousPosition = drone.Drone.PreviousPosition;
                var dx = position.X - previousPosition.X;
                var dy = position.Y - previousPosition.Y;
                if(dx == 0 && dy == 0) // this drone didn't move. Is it already inside a zone?
                {            
                    drone.AssociatedZone = zones.FirstOrDefault(z => z.Contains(position));
                }
                else
                {
                    var distances = HelperExtensions.RespectiveDistancesToLine(zoneCenters, previousPosition, position);
                    for(int i = 0; i < zoneCenters.Length; i++)
                        zoneDistances[i].Distance = distances[i];

                    var possibleTargets = zoneDistances
                        .Where(zd => zd.Distance < 1.5 * Zone.Radius)
                        .Where(zd => MathHelper.DotProduct(previousPosition, position, previousPosition, zd.Zone.Center) > 0) // ignore zones behind the drone
                        .Select(zd => zd.Zone)
                        .ToArray();

                    if(possibleTargets.Any())
                        drone.AssociatedZone = possibleTargets.MinBy(z => z.Center.DistanceTo(position));
                    else
                        drone.AssociatedZone = null;
                }

                if(drone.AssociatedZone == null)
                    drone.TaskType = TaskType.Unknown;
                else
                    drone.TaskType = (drone.AssociatedZone.OwnerId == drone.Drone.TeamId ? TaskType.Defend : TaskType.Attack);
            }
        }
    }

    public interface ITaskOrganizer
    {
        IList<Task> DefineTasks(ContextInfo context);
    }

    class OneTaskPerZoneOrganizer : ITaskOrganizer
    {
        private List<Task> _tasks;

        private Level IncreaseLevel(Level lvl)
        {
            return lvl == Level.Low ? Level.Medium : Level.High;
        }

        private Level DecreaseLevel(Level lvl)
        {
            return lvl == Level.High ? Level.Medium : Level.Low;
        }
        
        public IList<Task> DefineTasks(ContextInfo context)
        {
            if(_tasks == null)
                _tasks = context.Zones.Select(z => new Task {AssociatedZone = z.Zone, IsStillValid = true}).ToList();

            // calculate importance of attack vs defense based on nbr of owned zones
            var ownedZonesRatio = context.Context.Zones.Count(z => z.OwnerId == context.MyTeamId) / (double)context.Zones.Length;
            var necessaryZoneRatio = 1.0 / context.Context.Teams.Count;
            var maxDistanceForEasyTask = 12 * GameContext.MaxMoveDistance;
            var surroundingDistance = Zone.Radius * 6;

            Level defenseImportance, attackImportance;
            if(ownedZonesRatio > 0.8 * necessaryZoneRatio)
            {
                defenseImportance = Level.High;
                attackImportance = Level.Low;
            }
            else if(ownedZonesRatio < 0.4 * necessaryZoneRatio)
            {
                defenseImportance = Level.Low;
                attackImportance = Level.High;
            }
            else
            {
                defenseImportance = Level.Medium;
                attackImportance = Level.Medium;
            }

            foreach(var task in _tasks)
            {
                var zoneInfo = context.Zones[task.AssociatedZone.Id];
                var zone = zoneInfo.Zone;

                task.Type = (task.AssociatedZone.OwnerId == context.MyTeamId ? TaskType.Defend : TaskType.Attack);
                task.Importance = (task.Type == TaskType.Attack ? attackImportance : defenseImportance);

                var enemyDronesWithDistance = context.EnemyDrones
                    .Select(d => new {Drone = d, Distance = d.Drone.Position.DistanceTo(zone.Center)})
                    .ToArray();

                // define difficulty & nbr required drones
                if(task.Type == TaskType.Attack)
                {
                    double avgDefendersDistance = GameContext.FieldWidth * 3;
                    double avgOtherAttackersDistance = avgDefendersDistance;

                    var defendingDrones = enemyDronesWithDistance
                        .Where(d => d.Distance <= surroundingDistance || (d.Drone.TaskType == TaskType.Defend && d.Drone.AssociatedZone == zone))
                        .ToArray();
                    var nbrDefendingDrones = defendingDrones.Length;
                    int nbrOtherAttackingDrones = 0;

                    if(defendingDrones.Any())
                        avgDefendersDistance = defendingDrones.Average(d => d.Distance);

                    var otherAttackingDroneSquads = enemyDronesWithDistance
                        .Where(d => d.Distance <= surroundingDistance || (d.Drone.TaskType == TaskType.Attack && d.Drone.AssociatedZone == zone))
                        .ToLookup(d => d.Drone.Drone.TeamId);

                    if(otherAttackingDroneSquads.Any())
                    {
                        var biggestSquad = otherAttackingDroneSquads.MaxBy(s => s.Count());
                        nbrOtherAttackingDrones = biggestSquad.Count();
                        avgOtherAttackersDistance = biggestSquad.Average(d => d.Distance);
                    }

                    task.NbrRequiredDrones = 1 + Math.Max(nbrDefendingDrones, nbrOtherAttackingDrones);

                    var avgDistanceOfMyDrones = context.MyDrones
                        .Select(d => d.Drone.Position.DistanceTo(zone.Center))
                        .OrderBy(dist => dist)
                        .Take(task.NbrRequiredDrones)
                        .Average();

                    if(avgDistanceOfMyDrones < avgDefendersDistance && avgDistanceOfMyDrones < avgOtherAttackersDistance)
                        task.Difficulty = Level.Low;
                    else if(avgDistanceOfMyDrones > avgDefendersDistance && avgDistanceOfMyDrones > avgOtherAttackersDistance)
                        task.Difficulty = Level.High;
                    else
                        task.Difficulty = Level.Medium;

                    if(avgDistanceOfMyDrones > maxDistanceForEasyTask)
                        task.Difficulty = this.IncreaseLevel(task.Difficulty);
                }
                else
                {
                    // find attacking/threatening drones
                    int biggestAttackingSquadSize = 0;
                    double avgAttackingSquadDistance = GameContext.FieldWidth * 3;
                    int nbrAttackingSquads = 0;

                    var attackingDroneSquads = enemyDronesWithDistance
                        .Where(d => d.Distance <= surroundingDistance || (d.Drone.TaskType == TaskType.Attack && d.Drone.AssociatedZone == zone))
                        .ToLookup(d => d.Drone.Drone.TeamId);

                    if(attackingDroneSquads.Any())
                    {
                        nbrAttackingSquads = attackingDroneSquads.Count();
                        var biggestSquad = attackingDroneSquads.MaxBy(s => s.Count());
                        biggestAttackingSquadSize = biggestSquad.Count();
                        avgAttackingSquadDistance = biggestSquad.Average(d => d.Distance);
                    }

                    task.NbrRequiredDrones = Math.Max(biggestAttackingSquadSize, 0);

                    var myDroneDistances = context.MyDrones
                        .Select(d => d.Drone.Position.DistanceTo(zone.Center))
                        .OrderBy(dist => dist)
                        .Take(task.NbrRequiredDrones)
                        .ToArray();

                    var avgDistanceOfMyDrones = myDroneDistances.Any() ? myDroneDistances.Average() : GameContext.FieldWidth;

                    if(avgDistanceOfMyDrones < avgAttackingSquadDistance)
                        task.Difficulty = Level.Low;
                    else
                        task.Difficulty = Level.Medium;

                    if(avgDistanceOfMyDrones > maxDistanceForEasyTask)
                        task.Difficulty = this.IncreaseLevel(task.Difficulty);
                }

                this.UpdateTaskPriority(task, zoneInfo);
            }

            return _tasks;
        }

        private void UpdateTaskPriority(Task task, ZoneInfo zone)
        {
            Func<Level, int> levelToInt = lvl => (lvl == Level.Low ? 0 : (lvl == Level.Medium ? 1 : 2));

            task.Priority = 0
                + 10.0 * levelToInt(task.Importance)
                - 1.0 * levelToInt(task.Difficulty)
                + 0.5 * levelToInt(zone.StrategicValue)
                - 0.5 * task.NbrRequiredDrones;
        }
    }

    public interface IDroneAllocator
    {
        void AllocateDronesToTasks(ContextInfo context, IList<Task> tasks);
    }

    class PriorityBasedBasicDroneAllocator : IDroneAllocator
    {
        public void AllocateDronesToTasks(ContextInfo context, IList<Task> tasks)
        {
            var availableDrones = context.MyDrones.ToList();
            foreach(var drone in availableDrones)
                drone.CurrentTask = null;

            var sortedTasks = tasks.OrderByDescending(t => t.Priority).ToArray();
            for(int i = 0; i < sortedTasks.Length && availableDrones.Any(); i++)
            {
                var task = sortedTasks[i];
                if(availableDrones.Count < task.NbrRequiredDrones)
                {
                    // there are not enough available drones to handle this task
                    // maybe we should skip it and concentrate on the next task
                    if(i < sortedTasks.Length - 1)
                    {
                        var nextTask = sortedTasks[i + 1];
                        if(availableDrones.Count >= nextTask.NbrRequiredDrones)
                            continue;
                    }
                }

                int nbrAllocatedDrones = 0;
                while(nbrAllocatedDrones < task.NbrRequiredDrones)
                {
                    if(!availableDrones.Any())
                        break;

                    // Pick the drone which is closest to the task target)
                    var candidate = availableDrones.MinBy(d => d.Drone.Position.DistanceTo(task.AssociatedZone.Center));
                    availableDrones.Remove(candidate);
                    candidate.CurrentTask = task;
                    nbrAllocatedDrones++;
                }
            }

            if(availableDrones.Any()) // some drones were not assigned => do a second pass
            {
                for(int i = 0; i < sortedTasks.Length && availableDrones.Any(); i++)
                {
                    var task = sortedTasks[i];
                    
                    // Pick the drone which is closest to the task target)
                    var candidate = availableDrones.MinBy(d => d.Drone.Position.DistanceTo(task.AssociatedZone.Center));
                    availableDrones.Remove(candidate);
                    candidate.CurrentTask = task;
                }
            }
        }
    }

    public interface IDroneCommander
    {
        void SetDroneDestinations(ContextInfo context);
    }

    class BasicDroneCommander : IDroneCommander
    {
        public void SetDroneDestinations(ContextInfo context)
        {
            foreach(var droneInfo in context.MyDrones)
                droneInfo.Destination = this.GetDroneDestination(context, droneInfo);
        }

        private Point GetDroneDestination(ContextInfo context, MyDroneInfo droneInfo)
        {
            var task = droneInfo.CurrentTask;
            if(task != null)
            {
                if(task.Type == TaskType.Defend && task.AssociatedZone.Contains(droneInfo.Drone.Position))
                    return droneInfo.Drone.Position;

                return droneInfo.Drone.Position.GetZoneBorderPoint(task.AssociatedZone);
            }

            return droneInfo.Drone.Position;
        }
    }

    public class TaskBasedPlayer : IPlayer
    {
        private ContextInfo _contextInfo;

        private readonly IDroneActivityObserver _droneActivityObserver = new BasicDroneActivityObserver();
        private readonly IZoneEvaluator _zoneEvaluator = new BasicZoneEvaluator();
        private readonly ITaskOrganizer _taskOrganizer = new OneTaskPerZoneOrganizer();
        private readonly IDroneAllocator _droneAllocator = new PriorityBasedBasicDroneAllocator();
        private readonly IDroneCommander _droneCommander = new BasicDroneCommander();

        public int TeamId { get; set; }

        public void Initialize(GameContext context)
        {
            _contextInfo = new ContextInfo(this.TeamId, context);
            _contextInfo.Zones = context.Zones.Select(z => new ZoneInfo(z)).ToArray();
            _contextInfo.MyDrones = context.GetDronesOfTeam(this.TeamId).Select(d => new MyDroneInfo(d)).ToArray();
            _contextInfo.EnemyDrones = context.GetDronesOfOtherTeams(this.TeamId).Select(d => new EnemyDroneInfo(d)).ToArray();

            _zoneEvaluator.GiveInitialZoneEvaluations(_contextInfo.Zones);
        }

        public IEnumerable<Point> Play(GameContext context)
        {
            _droneActivityObserver.GuessEnemyDronesActivity(_contextInfo);

            _zoneEvaluator.UpdateZoneEvaluations(_contextInfo);

            var tasks = _taskOrganizer.DefineTasks(_contextInfo);

            _droneAllocator.AllocateDronesToTasks(_contextInfo, tasks);

            _droneCommander.SetDroneDestinations(_contextInfo);

            return _contextInfo.MyDrones.Select(d => d.Destination);
        }
    }
}