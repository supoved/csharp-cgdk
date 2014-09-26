using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{

    #region Strategy

    public sealed class MyStrategy : IStrategy
    {
        private static Dictionary<long, HockeyistRole> _team = new Dictionary<long, HockeyistRole>();

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            var system = new Environment(self, world, game, move);

            if (!_team.ContainsKey(self.Id))
                _team.Add(self.Id, PickRole(_team));

            if (world.GetMyPlayer().IsJustScoredGoal || world.GetMyPlayer().IsJustMissedGoal)
                return;

            if (system.Self.SwingTicks > 0)
            {
                var closestEnemy = system.GetClosestEnemy();
                if (system.World.Puck.OwnerHockeyistId == system.Self.Id)
                {
                    DoAction.Strike(system);
                }
                else if (closestEnemy != null && system.Self.GetDistanceTo(closestEnemy) < system.Game.StickLength && system.Self.GetAngleTo(closestEnemy) < system.Game.StickSector / 2)
                {
                    var nextEnemyPosition = new Point(closestEnemy.X + closestEnemy.SpeedX, closestEnemy.Y + closestEnemy.SpeedY);
                    var enemyWillBeInZoneNextTurn = system.Self.GetDistanceTo(nextEnemyPosition.X, nextEnemyPosition.Y) < system.Game.StickLength * 2 / 3
                        && Math.Abs(system.Self.GetAngleTo(nextEnemyPosition.X, nextEnemyPosition.Y)) < system.Game.StickSector / 2;

                    if (enemyWillBeInZoneNextTurn && system.Self.SwingTicks < system.Game.MaxEffectiveSwingTicks)
                    {
                        system.Move.Action = ActionType.Swing;
                    }
                    else
                    {
                        system.Move.Action = ActionType.Strike;
                    }
                }
                else
                {
                    system.Move.Action = ActionType.CancelStrike;
                }
            }
            else if (self.Id == world.Puck.OwnerHockeyistId)
            {
                FollowTactics.Atack(system);
            }
            else
            {
                var role = _team[self.Id];
                if (_team.ContainsKey(system.World.Puck.OwnerHockeyistId))
                {
                    if (_team[system.World.Puck.OwnerHockeyistId] == HockeyistRole.Defender &&
                        role == HockeyistRole.Atacker)
                    {
                        _team[system.World.Puck.OwnerHockeyistId] = HockeyistRole.Atacker;
                        _team[self.Id] = HockeyistRole.Defender;
                    }
                }

                if (role == HockeyistRole.Atacker)
                    FollowTactics.PursuePuck(system);
                else
                {
                    FollowTactics.Defend(system);
                }
            }
        }

        private HockeyistRole PickRole(Dictionary<long, HockeyistRole> t)
        {
            if (!t.Any())
            {
                return HockeyistRole.Atacker;
            }
            else if (t.Count % 2 == 1)
            {
                return HockeyistRole.Defender;
            }
            else
            {
                return HockeyistRole.Support;
            }
        }
    }

    #endregion

    #region Helpers

    public class Environment
    {
        public Hockeyist Self { get; set; }
        public World World { get; set; }
        public Game Game { get; set; }
        public Move Move { get; set; }

        public HockeyistRole SelfRole { get; set; }

        public Environment(Hockeyist self, World world, Game game, Move move)
        {
            Self = self;
            World = world;
            Game = game;
            Move = move;
        }
    }

    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point(double x, double y)
            : this()
        {
            this.X = x;
            this.Y = y;
        }


    }

    public enum HockeyistRole
    {
        Atacker,
        Defender,
        Support
    }

    public static class PointExtensions
    {
        public static Point ToPoint(this Puck puck)
        {
            return new Point { X = puck.X, Y = puck.Y };
        }
    }

    #endregion

    #region Actions

    public static class DoAction
    {
        public static void MoveTo(Environment environment, Point target, bool rush = true)
        {
            //todo: calculate fastast way with analizing current position and speed
            var distanceToTarget = environment.Self.GetDistanceTo(target.X, target.Y);
            var angleToTarget = environment.Self.GetAngleTo(target.X, target.Y);
            var selfSpeed = environment.SpeedValue(environment.Self.SpeedX, environment.Self.SpeedY);

            //var timeToTarget = distanceToTarget / selfSpeed;
            var timeToTarget = environment.CalculateTicketsToGetToPosition(target.X, target.Y);
            var k = distanceToTarget > environment.Game.StickLength ? distanceToTarget / environment.Game.StickLength * 2 : distanceToTarget / environment.Game.StickLength;

            if (Math.Abs(angleToTarget) > (Math.PI * 2) / 3 && distanceToTarget < environment.Game.StickLength * 4) //движение назад, так быстрее
            {
                var timeToStop = selfSpeed;

                if (timeToTarget < timeToStop)
                {
                    environment.Move.SpeedUp = rush ? 1 : 1 - k;
                }
                else
                {
                    environment.Move.SpeedUp = rush ? -1 : -k;
                }

                environment.Move.Turn = environment.Self.GetAngleTo(target.X, target.Y) - Math.PI;
            }
            else if (Math.Abs(angleToTarget) < (Math.PI * 2) / 3 && Math.Abs(angleToTarget) > Math.PI/3 && distanceToTarget < environment.Game.StickLength * 4) // маневр цель с боку
            {
                environment.Move.Turn = environment.Self.GetAngleTo(target.X, target.Y);
            }
            else //движение вперед - цель передомной
            {
                var timeToStop = selfSpeed;

                if (timeToTarget < timeToStop)
                {
                    environment.Move.SpeedUp = (rush ? -1 : k - 1);
                }
                else
                {
                    environment.Move.SpeedUp = rush ? 1 : k;
                }

                environment.Move.Turn = environment.Self.GetAngleTo(target.X, target.Y);
            }
            
        }

        public static void FaceTo(Environment environment, Point target)
        {
            environment.Move.Turn = environment.Self.GetAngleTo(target.X, target.Y);
        }

        public static void Stop(Environment environment)
        {
            //Todo: increase speed to speedup stop
            environment.Move.SpeedUp = 0;
        }

        public static void Strike(Environment environment)
        {
            //todo: predict gatekeeper block chance, pick angle acordingly
            Player opponentPlayer = environment.World.GetOpponentPlayer();

            double netX = 0.5D * (opponentPlayer.NetBack + opponentPlayer.NetFront);
            double netY = 0.5D * (opponentPlayer.NetBottom + opponentPlayer.NetTop);
            netY += (environment.Self.Y < netY ? 0.5D : -0.5D) * environment.Game.GoalNetHeight;

            double angleToNet = environment.Self.GetAngleTo(netX, netY);
            environment.Move.Turn = angleToNet;

            if (Math.Abs(angleToNet) < 1.0D * Math.PI / 180.0D || environment.Self.SwingTicks > 0)
            {
                var closestEnemy = environment.GetClosestEnemy();

                var nextEnemyPosition = new Point(closestEnemy.X + closestEnemy.SpeedX, closestEnemy.Y + closestEnemy.SpeedY);
                var enemyDoNotThreat = environment.World.Puck.GetDistanceTo(nextEnemyPosition.X, nextEnemyPosition.Y) > environment.Game.StickLength * 3 / 2;

                if (enemyDoNotThreat && environment.Self.SwingTicks < environment.Game.MaxEffectiveSwingTicks)
                {
                    environment.Move.Action = ActionType.Swing;
                }
                else
                {
                    environment.Move.Action = ActionType.Strike;
                }
            }
        }
    }

    #endregion


    public static class FollowTactics
    {
        public static void Defend(Environment environment)
        {
            var player = environment.World.GetMyPlayer();
            var net = GetNetPoint(environment, true);

            //var puckDistanceKoef = environment.World.Puck.Radius*2 + environment.Self.Radius*1.5;
            var puckDistanceKoef = environment.Self.Radius * 3 + environment.World.Puck.Radius;
            //var puckDistanceKoef = 200;
            double defX = (player.NetBack > player.NetFront ? -1 : 1) * puckDistanceKoef + player.NetFront;
            double defY = net.Y;
            //double defY = net.Y + (Math.Abs(environment.World.Puck.Y - net.Y) > 100 ? 1 : 0) * (environment.World.Puck.Y > net.Y ? -1 : 1) * (player.NetBottom - player.NetTop) / 3;

            var distanceToPuck = environment.Self.GetDistanceTo(environment.World.Puck.X, environment.World.Puck.Y);
            var distanceToNet = environment.Self.GetDistanceTo(defX, defY);
            var minDistanceToPuck = GetMinDustanceToPuck(environment);
            var isPuckInStrikeZone = IsPuckInStrikeZone(environment);

            if (distanceToPuck < environment.Game.StickLength && environment.World.Puck.OwnerPlayerId != environment.World.GetMyPlayer().Id) //puck is near and not under control
            {
                PursuePuck(environment);
            }
            if (isPuckInStrikeZone && distanceToPuck < environment.Game.StickLength * 1.2 && environment.World.Puck.OwnerPlayerId == environment.World.GetOpponentPlayer().Id) //enemy with puck in strike zone
            {
                PursuePuck(environment);
            }
            else if (environment.World.Puck.GetDistanceTo(net.X, net.Y) < environment.Self.GetDistanceTo(net.X, net.Y) && environment.World.Puck.OwnerPlayerId != environment.World.GetMyPlayer().Id) //puck is closer to net than self
            {
                DoAction.MoveTo(environment, new Point { X = defX, Y = defY });
            }
            else if (distanceToPuck < environment.Game.StickLength * 1.5 && environment.World.Puck.OwnerPlayerId != environment.World.GetMyPlayer().Id) // puck is not far and not under control
            {
                PursuePuck(environment);
            }
            else if (distanceToPuck < minDistanceToPuck && environment.World.Puck.OwnerPlayerId != environment.World.GetMyPlayer().Id) //puck is closer to me than other players and not unde control
            {
                PursuePuck(environment);
            }
            else if (distanceToNet > environment.Game.StickLength * 2) // I am too far away
            {
                DoAction.MoveTo(environment, new Point { X = defX, Y = defY });
            }
            else if (distanceToNet > environment.World.Puck.Radius*2) // I am near def point
            {
                DoAction.MoveTo(environment, new Point { X = defX, Y = defY }, false);
            }
            else //nothing to do
            {
                DoAction.Stop(environment);
                DoAction.FaceTo(environment, environment.World.Puck.ToPoint());
            }
        }

        private static double GetMinDustanceToPuck(Environment environment)
        {
            return environment.World.Hockeyists
                .Where(_ => _.Type != HockeyistType.Goalie)
                .Select(_ => new { distance = environment.World.Puck.GetDistanceTo(_), isTeammate = _.IsTeammate })
                .Select(_ => _.isTeammate ? _.distance / 2 : _.distance)
                .Min();
        }

        private static bool IsPuckInStrikeZone(Environment environment)
        {
            var net = GetNetPoint(environment, true);
            return environment.World.Puck.GetDistanceTo(net.X, net.Y) < 500;
        }

        private static Point GetNetPoint(Environment environment, bool teamNet)
        {
            var player = teamNet ? environment.World.GetMyPlayer() : environment.World.GetOpponentPlayer();

            var netX = (player.NetBack + player.NetFront) / 2;
            var netY = (player.NetBottom + player.NetTop) / 2;

            return new Point { X = netX, Y = netY };
        }

        public static double SpeedValue(this Environment environment, double speedX, double speedY)
        {
            return Math.Sqrt(Math.Abs(speedX) * (Math.Abs(speedX)
                + Math.Abs(speedY) * Math.Abs(speedY)));
        }

        public static double SpeedValue(this Unit unit)
        {
            return Math.Sqrt(Math.Abs(unit.SpeedX) * (Math.Abs(unit.SpeedX)
                + Math.Abs(unit.SpeedY) * Math.Abs(unit.SpeedY)));
        }

        public static void PursuePuck(Environment environment)
        {
            var player = environment.World.GetMyPlayer();

            var distanceToPuck = environment.Self.GetDistanceTo(environment.World.Puck.X, environment.World.Puck.Y);
            var closestEnemy = GetClosestEnemy(environment);

            if (distanceToPuck < environment.Game.StickLength)
            {
                if (environment.World.Puck.OwnerPlayerId == environment.World.GetOpponentPlayer().Id) //enemy with puck at strikerange
                {
                    if (environment.Self.RemainingCooldownTicks > 0)
                    {
                        var target = CalculateNextPoint(environment, environment.World.Puck.X, environment.World.Puck.Y, environment.World.Puck.SpeedX, environment.World.Puck.SpeedY);
                        DoAction.MoveTo(environment, target, false);
                    }
                    else if (Math.Abs(environment.Self.GetAngleTo(environment.World.Puck)) > environment.Game.StickSector / 2)
                    {
                        DoAction.FaceTo(environment, environment.World.Puck.ToPoint());
                    }
                    else
                    {
                        var puckOwner = environment.World.Hockeyists.FirstOrDefault(_ => _.Id == environment.World.Puck.OwnerHockeyistId);
                        if (puckOwner != null && environment.SpeedValue(puckOwner.SpeedX, puckOwner.SpeedY) > environment.SpeedValue(environment.Self.SpeedX, environment.Self.SpeedY))
                            environment.Move.Action = ActionType.Strike;
                        else
                            environment.Move.Action = ActionType.TakePuck;
                    }
                }
                else if (environment.World.Puck.OwnerHockeyistId == -1)
                {
                    if (environment.Self.RemainingCooldownTicks > 0)
                    {
                        var target = CalculateNextPoint(environment, environment.World.Puck.X, environment.World.Puck.Y, environment.World.Puck.SpeedX, environment.World.Puck.SpeedY);
                        DoAction.MoveTo(environment, target, false);
                    }
                    else
                    {
                        if (Math.Abs(environment.Self.GetAngleTo(environment.World.Puck)) > environment.Game.StickSector / 2)
                        {
                            DoAction.FaceTo(environment, environment.World.Puck.ToPoint());
                        }
                        else
                        {//todo: add probability
                            if (environment.SpeedValue(environment.Self.SpeedX, environment.Self.SpeedY) > 10 && PuckMovingIntoOurNet(environment.World.Puck.X, environment.World.Puck.SpeedX, player.NetFront) && Math.Abs(environment.Self.GetAngleTo(player.NetFront, (player.NetTop + player.NetBottom) / 2)) > Math.PI / 2)
                                environment.Move.Action = ActionType.Strike;
                            else
                                environment.Move.Action = ActionType.TakePuck;
                        }
                    }
                }
            }
            else if (closestEnemy != null
                && closestEnemy.GetDistanceTo(environment.Self) < environment.Game.StickLength * 3 / 4
                && environment.Self.RemainingCooldownTicks == 0
                && Math.Abs(Math.Abs(environment.Self.GetAngleTo(closestEnemy) - environment.Game.StickSector / 2)) < 0.15
                && closestEnemy.State != HockeyistState.KnockedDown
                )
            {
                var enemyAngle = Math.Abs(environment.Self.GetAngleTo(closestEnemy));
                if (enemyAngle > environment.Game.StickSector / 2)
                {
                    DoAction.FaceTo(environment, new Point { X = closestEnemy.X, Y = closestEnemy.Y });
                }
                else
                {
                    var nextEnemyPosition = new Point(closestEnemy.X + closestEnemy.SpeedX, closestEnemy.Y + closestEnemy.SpeedY);
                    var enemyWillBeInZoneNextTurn = environment.Self.GetDistanceTo(nextEnemyPosition.X, nextEnemyPosition.Y) < environment.Game.StickLength * 1 / 2
                        && Math.Abs(environment.Self.GetAngleTo(nextEnemyPosition.X, nextEnemyPosition.Y)) < environment.Game.StickSector * 3/4;

                    if (enemyWillBeInZoneNextTurn && environment.Self.SwingTicks < environment.Game.MaxEffectiveSwingTicks)
                    {
                        environment.Move.Action = ActionType.Swing;
                    }
                    else
                    {
                        environment.Move.Action = ActionType.Strike;
                    }
                }
            }
            else
            {
                var target = CalculateNextPoint(environment, environment.World.Puck.X, environment.World.Puck.Y, environment.World.Puck.SpeedX, environment.World.Puck.SpeedY);
                DoAction.MoveTo(environment, target);
            }
        }

        public static Hockeyist GetClosestEnemy(this Environment environment)
        {
            return environment.World.Hockeyists.Where(_ => !_.IsTeammate && _.Type != HockeyistType.Goalie).OrderBy(_ => _.GetDistanceTo(environment.Self)).FirstOrDefault();
        }
        public static double GetDistanceBetween(this Environment environment, double x1, double y1, double x2, double y2)
        {
            double xRange = x1 - x2;
            double yRange = y1 - y2;
            return Math.Sqrt(xRange * xRange + yRange * yRange);
        }

        private static bool PuckMovingIntoOurNet(double puckX, double speed, double netX)
        {
            return (puckX < netX && speed > 0) || (puckX > netX && speed < 0);
        }

        public static double CalculateTicketsToGetToPosition(this Environment environment, double targetX, double targetY)
        {
            var speed = environment.SpeedValue(environment.Self.SpeedX, environment.Self.SpeedY);
            var t = -speed + Math.Sqrt(speed * speed + 2 + environment.Self.GetDistanceTo(targetX, targetY));

            return t;
        }

        private static Point CalculateNextPoint(Environment environment, double targetX, double targetY, double targetSpeedX, double targetSpeedY)
        {
            var timeToGetThere = environment.CalculateTicketsToGetToPosition(targetX, targetY);
            var bestTime = 1000000.0;
            var result = new Point
            {
                X = targetX,
                Y = targetY
            };
            var j = 0;

            while (timeToGetThere <= bestTime && j <= timeToGetThere)
            {
                bestTime = timeToGetThere;

                result.X = targetX;
                result.Y = targetY;

                targetX = targetX + targetSpeedX;
                targetY = targetY + targetSpeedY;

                if (targetY >= environment.World.Height || targetY < 0)
                {
                    targetSpeedY = -targetSpeedY;
                }
                if (targetX >= environment.World.Width - environment.Game.GoalNetWidth || targetX < environment.Game.GoalNetWidth)
                {
                    targetSpeedX = -targetSpeedX;
                }

                timeToGetThere = environment.CalculateTicketsToGetToPosition(targetX, targetY);
                j++;
            }

            return result;
        }


        public static void Atack(Environment environment)
        {
            var player = environment.World.GetOpponentPlayer();
            var closestEnemy = environment.GetClosestEnemy();

            var netX = (player.NetBack + player.NetFront) / 2.01;
            var netY = (player.NetBottom + player.NetTop) / 2.2;
            var kx = environment.Game.GoalNetHeight * 1.6;

            var strikePosition = new Point
            {
                X = environment.Self.X > netX ? netX + kx : netX - kx,
                Y = environment.Self.Y > netY ? player.NetBottom + environment.World.Puck.Radius : player.NetTop - environment.World.Puck.Radius
            };

            var manuverPosition = new Point
            {
                X = (environment.World.Width - environment.Game.GoalNetWidth)/2,
                Y = environment.Self.Y < netY ? 4 * environment.Self.Radius + 2 * environment.World.Puck.Radius : environment.World.Height - 2 * environment.World.Puck.Radius - 4 * environment.Self.Radius
            };
                
            if (environment.Self.GetDistanceTo(netX, netY) < (environment.World.Width - environment.Game.GoalNetWidth)*2/3)
            {
                if (environment.Self.GetDistanceTo(strikePosition.X, strikePosition.Y) < environment.Game.StickLength/2)
                {
                    DoAction.Strike(environment);
                }
                else if (environment.Self.GetDistanceTo(strikePosition.X, strikePosition.Y) < environment.Game.StickLength &&
                         closestEnemy.GetDistanceTo(environment.Self) < environment.Game.StickLength)
                {
                    DoAction.Strike(environment);
                }
                else
                {
                    DoAction.MoveTo(environment, strikePosition, false);
                }
            }
            else
            {
                DoAction.MoveTo(environment, manuverPosition);
            }
        }
    }
}