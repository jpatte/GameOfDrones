using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfDrones
{
    public enum TaskType { Attack, Defend, Unknown }
    public enum Level { Medium = 1, Low = 0, High = 2 }

    public class Task
    {
        public TaskType Type { get; set; }
        public int NbrRequiredDrones { get; set; }
        public double AverageGainablePoints { get; set; }
        public ZoneInfo AssociatedZone { get; set; }
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

            var xAvg = xSum / (double)zones.Count;
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
        class ZoneWithDistance
        {
            public Zone Zone { get; set; }
            public double Distance { get; set; }
        }

        public void GuessEnemyDronesActivity(ContextInfo context)
        {
            var zones = context.Context.Zones;
            var zoneCenters = zones.Select(z => z.Center).ToArray();
            var zoneDistances = zones.Select(z => new ZoneWithDistance { Zone = z }).ToArray();

            foreach(var drone in context.EnemyDrones.AsParallel())
            {
                // based on the drone last movement, try to identify the zone it's trying to reach
                var position = drone.Drone.Position;
                var previousPosition = drone.Drone.PreviousPosition;

                if(position.DistanceTo(previousPosition) < 2) // this drone didn't move. Is it already inside a zone?
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
        const int nbrTurnsConsidered = 10;
        const double surroundingDistance = Zone.Radius * 5 / GameContext.MaxMoveDistance;

        class DroneWithDistance
        {
            public Drone Drone { get; set; }
            public int NbrTurns { get; set; }
            public TaskType TaskType { get; set; }
            public Zone AssociatedZone { get; set; }
        }

        private List<Task> _tasks;

        private int CalculateRequiredDronesForAttack(IList<DroneWithDistance> myDrones, IEnumerable<DroneWithDistance> enemyDroneSquad, out int nbrTurnsToSucceed)
        {
            var myTeamId = myDrones[0].Drone.TeamId;
            int nbrAllies = 0, nbrEnemies = 0;
            nbrTurnsToSucceed = nbrTurnsConsidered;

            var sortedDroneWaves = myDrones.Concat(enemyDroneSquad)
                .ToLookup(d => d.NbrTurns)
                .OrderBy(w => w.Key);

            foreach(var wave in sortedDroneWaves)
            {
                foreach(var drone in wave)
                {
                    if(drone.Drone.TeamId == myTeamId)
                        nbrAllies++;
                    else
                        nbrEnemies++;
                }

                if(nbrAllies > nbrEnemies)
                {
                    nbrTurnsToSucceed = wave.Key;
                    return nbrAllies;
                }
            }

            return 0;
        }

        private int CalculateRequiredDronesForDefense(IList<DroneWithDistance> myDrones, IEnumerable<DroneWithDistance> enemyDroneSquad, out int nbrTurnsToFail)
        {
            var myTeamId = myDrones[0].Drone.TeamId;
            int nbrAllies = 0, nbrEnemies = 0;
            nbrTurnsToFail = nbrTurnsConsidered;

            var sortedDroneWaves = myDrones.Concat(enemyDroneSquad)
                .ToLookup(d => d.NbrTurns)
                .OrderBy(w => w.Key);

            foreach(var wave in sortedDroneWaves)
            {
                foreach(var drone in wave)
                {
                    if(drone.Drone.TeamId == myTeamId)
                        nbrAllies++;
                    else
                        nbrEnemies++;
                }

                if(nbrEnemies > nbrAllies)
                {
                    nbrTurnsToFail = wave.Key;
                    return nbrAllies;
                }
            }

            return nbrEnemies;
        }

        public IList<Task> DefineTasks(ContextInfo context)
        {
            if(_tasks == null)
                _tasks = context.Zones.Select(z => new Task { AssociatedZone = z }).ToList();

            foreach(var task in _tasks.AsParallel())
            {
                var zoneInfo = task.AssociatedZone;
                var zone = zoneInfo.Zone;

                task.Type = (zone.OwnerId == context.MyTeamId ? TaskType.Defend : TaskType.Attack);

                Func<Point, int> nbrTurnsToReachZone = p => (int)Math.Ceiling(Math.Max(0, p.DistanceTo(zone.Center) - Zone.Radius) / GameContext.MaxMoveDistance);

                var myDrones = context.MyDrones
                    .Select(d => new DroneWithDistance
                    {
                        Drone = d.Drone,
                        NbrTurns = nbrTurnsToReachZone(d.Drone.Position)
                    })
                    .OrderBy(d => d.NbrTurns)
                    .ToArray();

                var enemyDrones = context.EnemyDrones
                    .Select(d => new DroneWithDistance
                    {
                        Drone = d.Drone,
                        NbrTurns = nbrTurnsToReachZone(d.Drone.Position),
                        TaskType = d.TaskType,
                        AssociatedZone = d.AssociatedZone
                    })
                    .ToArray();

                // calculate nbr of required drones & gainable points
                if(task.Type == TaskType.Attack)
                {
                    var defendingDrones = enemyDrones
                        .Where(d => d.Drone.TeamId == zone.OwnerId)
                        .Where(d => d.NbrTurns <= surroundingDistance || (d.TaskType == TaskType.Defend && d.AssociatedZone == zone))
                        .ToArray();

                    // how much drones do I need to capture this zone?
                    int nbrTurnsToSucceed;
                    int nbrRequiredDrones = this.CalculateRequiredDronesForAttack(myDrones, defendingDrones, out nbrTurnsToSucceed);

                    // if they are other attacking squads, how much drones do I need to make sure I can recapture the zone if necessary? 
                    var otherAttackingDroneSquads = enemyDrones
                        .Where(d => d.Drone.TeamId != zone.OwnerId)
                        .Where(d => d.NbrTurns <= surroundingDistance || (d.TaskType == TaskType.Attack && d.AssociatedZone == zone))
                        .ToLookup(d => d.Drone.TeamId);

                    foreach(var enemySquad in otherAttackingDroneSquads)
                    {
                        int nbrTurnsToSucceed2;
                        int nbrRequiredDrones2 = this.CalculateRequiredDronesForAttack(myDrones, enemySquad, out nbrTurnsToSucceed2);
                        if(nbrTurnsToSucceed2 >= nbrTurnsToSucceed)
                        {
                            nbrTurnsToSucceed = nbrTurnsToSucceed2;
                            nbrRequiredDrones = nbrRequiredDrones2;
                        }
                    }

                    task.NbrRequiredDrones = nbrRequiredDrones;
                    task.AverageGainablePoints = (nbrTurnsConsidered - nbrTurnsToSucceed) / (double)nbrTurnsConsidered;
                }
                else
                {
                    int nbrTurnsToFail = nbrTurnsConsidered;
                    int nbrRequiredDrones = 0;

                    // how much drones do I need to defend the zone against each attacking squad? 
                    var attackingDroneSquads = enemyDrones
                        .Where(d => d.NbrTurns <= surroundingDistance || (d.TaskType == TaskType.Attack && d.AssociatedZone == zone))
                        .ToLookup(d => d.Drone.TeamId);

                    foreach(var enemySquad in attackingDroneSquads)
                    {
                        int nbrTurnsToFail2;
                        int nbrRequiredDrones2 = this.CalculateRequiredDronesForDefense(myDrones, enemySquad, out nbrTurnsToFail2);
                        if(nbrTurnsToFail2 <= nbrTurnsToFail)
                        {
                            nbrTurnsToFail = nbrTurnsToFail2;
                            nbrRequiredDrones = nbrRequiredDrones2;
                        }
                    }

                    task.NbrRequiredDrones = nbrRequiredDrones;
                    task.AverageGainablePoints = nbrTurnsToFail / (double)nbrTurnsConsidered;
                }
            }

            return _tasks;
        }
    }

    public interface IDroneAllocator
    {
        void AllocateDronesToTasks(ContextInfo context, IList<Task> tasks);
    }

    class PriorityBasedDroneAllocator : IDroneAllocator
    {
        public void AllocateDronesToTasks(ContextInfo context, IList<Task> tasks)
        {
            // calculate importance of attack vs defense
            var ownedZonesRatio = context.Context.Zones.Count(z => z.OwnerId == context.MyTeamId) / (double)context.Zones.Length;
            var necessaryZoneRatio = 1.0 / context.Context.Teams.Count;
            Level defenseImportance = Level.Medium, attackImportance = Level.Medium;

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
            var taskImportances = tasks.ToDictionary(task => task, task => task.Type == TaskType.Attack ? attackImportance : defenseImportance);

            // calculate tasks priorities
            var taskPriorities = tasks.ToDictionary(task => task, task => 0
                + 4.0 * task.AverageGainablePoints
                + 2.0 * (int)taskImportances[task]
                + 0.5 * (int)task.AssociatedZone.StrategicValue
                - 1.0 * task.NbrRequiredDrones);

            //foreach(var task in tasks)
            //{
            //    Console.Error.WriteLine("{0}: {1} Priority={2:0.00} Pts={3:0.00} Req={4} Imp={5} Strat={6}",
            //        task.AssociatedZone.Zone.Id, task.Type, taskPriorities[task], task.AverageGainablePoints, task.NbrRequiredDrones, taskImportances[task], task.AssociatedZone.StrategicValue);
            //}

            // alllocate drones to tasks
            var availableDrones = context.MyDrones.ToList();
            foreach(var drone in availableDrones)
                drone.CurrentTask = null;

            var sortedTasks = tasks.OrderByDescending(t => taskPriorities[t]).ToArray();
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
                    var candidate = availableDrones.MinBy(d => d.Drone.Position.DistanceTo(task.AssociatedZone.Zone.Center));
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
                    var candidate = availableDrones.MinBy(d => d.Drone.Position.DistanceTo(task.AssociatedZone.Zone.Center));
                    availableDrones.Remove(candidate);
                    candidate.CurrentTask = task;
                }
            }
        }
    }

    class FocusedDroneAllocator : IDroneAllocator
    {
        public void AllocateDronesToTasks(ContextInfo context, IList<Task> tasks)
        {
            // how many zones do I need?
            var nbrZonesToTarget = (int)Math.Ceiling(1 + context.Zones.Length / (double)context.Context.Teams.Count);
            nbrZonesToTarget++;

            // get zones to consider
            ZoneInfo[] zonesToTarget;

            var myZones = context.Zones.Where(z => z.Zone.OwnerId == context.MyTeamId).ToArray();
            if(myZones.Any())
            {
                var medianPoint = HelperExtensions.GetMedianPoint(myZones.Select(z => z.Zone.Center).ToArray());
                zonesToTarget = context.Zones.OrderBy(z => medianPoint.DistanceTo(z.Zone.Center)).Take(nbrZonesToTarget).ToArray();
            }
            else
            {
                zonesToTarget = context.Zones;
            }

            // alllocate drones to tasks
            var availableDrones = context.MyDrones.ToList();
            foreach(var drone in availableDrones)
                drone.CurrentTask = null;

            var sortedTasks = tasks
                .Where(t => zonesToTarget.Contains(t.AssociatedZone))
                .OrderByDescending(t => t.NbrRequiredDrones == 0 ? int.MaxValue : t.AverageGainablePoints / t.NbrRequiredDrones)
                .ToArray();
            int nbrAssignedTasks = 0;
            for(int i = 0; i < sortedTasks.Length && nbrAssignedTasks < nbrZonesToTarget && availableDrones.Any(); i++)
            {
                var task = sortedTasks[i];
                if(availableDrones.Count() < task.NbrRequiredDrones)
                    continue;

                for(int nbrAllocatedDrones = 0; nbrAllocatedDrones < task.NbrRequiredDrones; nbrAllocatedDrones++)
                {
                    // Pick the drone which is closest to the task target)
                    var candidate = availableDrones.MinBy(d => d.Drone.Position.DistanceTo(task.AssociatedZone.Zone.Center));
                    availableDrones.Remove(candidate);
                    candidate.CurrentTask = task;
                }
                nbrAssignedTasks++;
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
                if(task.Type == TaskType.Defend && task.AssociatedZone.Zone.Contains(droneInfo.Drone.Position))
                    return droneInfo.Drone.Position; // no need to move

                return droneInfo.Drone.Position.GetZoneBorderPoint(task.AssociatedZone.Zone);
            }
            else
            {
                // send the drone on patrol
                var bestZones = context.Zones.Where(z => z.Zone.OwnerId == context.MyTeamId).OrderByDescending(z => (int)z.StrategicValue).Take(2).ToArray();
                if(bestZones.Length == 0)
                {
                    return droneInfo.Drone.Position;
                }
                else if(bestZones.Length == 1)
                {
                    return droneInfo.Drone.Position.GetZoneBorderPoint(bestZones[0].Zone);
                }
                else
                {
                    var c1 = bestZones[0].Zone.Center;
                    var c2 = bestZones[1].Zone.Center;
                    return new Point(c1.X + (c2.X - c1.X) / 2, c1.Y + (c2.Y - c1.Y) / 2);
                }
            }
        }
    }

    public class TaskBasedPlayer : IPlayer
    {
        private ContextInfo _contextInfo;

        private IDroneActivityObserver _droneActivityObserver;
        private IZoneEvaluator _zoneEvaluator;
        private ITaskOrganizer _taskOrganizer;
        private IDroneAllocator _droneAllocator;
        private IDroneCommander _droneCommander;

        public int TeamId { get; set; }

        public void Initialize(GameContext context)
        {
            _droneActivityObserver = new BasicDroneActivityObserver();
            _zoneEvaluator = new BasicZoneEvaluator();
            _taskOrganizer = new OneTaskPerZoneOrganizer();
            _droneAllocator = (context.Teams.Count > 2 ? (IDroneAllocator)new FocusedDroneAllocator() : (IDroneAllocator)new PriorityBasedDroneAllocator());
            _droneCommander = new BasicDroneCommander();

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

        public void Dispose()
        {
        }
    }
}