using System;
using System.Collections.Generic;
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
            var system = new System(self, world, game, move);

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

    public class System
    {
        public Hockeyist Self { get; set; }
        public World World { get; set; }
        public Game Game { get; set; }
        public Move Move { get; set; }

        public HockeyistRole SelfRole { get; set; }

        public System(Hockeyist self, World world, Game game, Move move)
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
        public static void MoveTo(System system, Point target, bool rush = true)
        {
            //todo: calculate fastast way with analizing current position and speed
            var distanceToTarget = system.Self.GetDistanceTo(target.X, target.Y);
            var angleToTarget = system.Self.GetAngleTo(target.X, target.Y);
            var selfSpeed = system.SpeedValue(system.Self.SpeedX, system.Self.SpeedY);

            var timeToTarget = distanceToTarget / selfSpeed;
            var k = distanceToTarget / system.Game.StickLength > 1 ? 1 : (distanceToTarget % system.Game.StickLength) / 10;

            if (Math.Abs(angleToTarget) > Math.PI / 2 && distanceToTarget < system.Game.StickLength * 4) //движение назад, так быстрее
            {
                var timeToStop = selfSpeed / system.Game.HockeyistSpeedUpFactor;

                if (timeToTarget < timeToStop)
                {
                    system.Move.SpeedUp = rush ? -0.5 : 1 - k;
                }
                else
                {
                    system.Move.SpeedUp = rush ? -1 : -k;
                }

                system.Move.Turn = system.Self.GetAngleTo(target.X, target.Y) - Math.PI;
            }
            else //движение вперед - цель передомной
            {
                var timeToStop = selfSpeed / system.Game.HockeyistSpeedDownFactor;

                if (timeToTarget < timeToStop)
                {
                    system.Move.SpeedUp = rush ? 0.5 : k - 1;
                }
                else
                {
                    system.Move.SpeedUp = rush ? 1 : k;
                }

                system.Move.SpeedUp = rush ? 1D : k;
                system.Move.Turn = system.Self.GetAngleTo(target.X, target.Y);
            }
        }

        public static void FaceTo(System system, Point target)
        {
            system.Move.Turn = system.Self.GetAngleTo(target.X, target.Y);
        }

        public static void Stop(System system)
        {
            //Todo: increase speed to speedup stop
            system.Move.SpeedUp = 0;
        }

        public static void Strike(System system)
        {
            //todo: predict gatekeeper block chance, pick angle acordingly
            Player opponentPlayer = system.World.GetOpponentPlayer();

            double netX = 0.5D * (opponentPlayer.NetBack + opponentPlayer.NetFront);
            double netY = 0.5D * (opponentPlayer.NetBottom + opponentPlayer.NetTop);
            netY += (system.Self.Y < netY ? 0.5D : -0.5D) * system.Game.GoalNetHeight;

            double angleToNet = system.Self.GetAngleTo(netX, netY);
            system.Move.Turn = angleToNet;

            if (Math.Abs(angleToNet) < 1.0D * Math.PI / 180.0D || system.Self.SwingTicks > 0)
            {
                var closestEnemy = system.GetClosestEnemy();

                var nextEnemyPosition = new Point(closestEnemy.X + closestEnemy.SpeedX, closestEnemy.Y + closestEnemy.SpeedY);
                var enemyDoNotThreat = system.Self.GetDistanceTo(nextEnemyPosition.X, nextEnemyPosition.Y) > system.Game.StickLength * 3 / 2;

                if (enemyDoNotThreat && system.Self.SwingTicks < system.Game.MaxEffectiveSwingTicks)
                {
                    system.Move.Action = ActionType.Swing;
                }
                else
                {
                    system.Move.Action = ActionType.Strike;
                }
            }
        }
    }

    #endregion


    public static class FollowTactics
    {
        public static void Defend(System system)
        {
            var player = system.World.GetMyPlayer();
            var net = GetNetPoint(system, true);

            var puckDistanceKoef = (100 * Math.Abs(system.World.Puck.X - net.X) / system.World.Width) + 100;
            //var puckDistanceKoef = 200;
            double defX = (player.NetBack > player.NetFront ? -1 : 1) * puckDistanceKoef + net.X;

            double defY = net.Y + (Math.Abs(system.World.Puck.Y - net.Y) > 100 ? 1 : 0) * (system.World.Puck.Y > net.Y ? -1 : 1) * (player.NetBottom - player.NetTop) / 3;

            var distanceToPuck = system.Self.GetDistanceTo(system.World.Puck.X, system.World.Puck.Y);
            var distanceToNet = system.Self.GetDistanceTo(defX, defY);
            var minDistanceToPuck = GetMinDustanceToPuck(system);
            var isPuckInStrikeZone = IsPuckInStrikeZone(system);

            if (distanceToPuck < system.Game.StickLength && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id) //puck is near and not under control
            {
                PursuePuck(system);
            }
            ////if (isPuckInStrikeZone && distanceToPuck < system.Game.StickLength * 4 && system.World.Puck.OwnerPlayerId == system.World.GetOpponentPlayer().Id) //enemy with puck in strike zone
            ////{
            ////    PursuePuck(system);
            ////}
            else if (system.World.Puck.GetDistanceTo(net.X, net.Y) < system.Self.GetDistanceTo(net.X, net.Y) && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id) //puck is closer to net than self
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY });
            }
            else if (distanceToPuck < system.Game.StickLength * 2.5 && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id) // puck is not far and not under control
            {
                PursuePuck(system);
            }
            else if (distanceToPuck < minDistanceToPuck && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id) //puck is closer to me than other players and not unde control
            {
                PursuePuck(system);
            }
            else if (distanceToNet > system.Game.StickLength * 2) // I am too far away
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY });
            }
            else if (distanceToNet > system.Game.StickLength / 2) // I am near def point
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY }, false);
            }
            else //nothing to do
            {
                DoAction.Stop(system);
                DoAction.FaceTo(system, system.World.Puck.ToPoint());
            }
        }

        private static double GetMinDustanceToPuck(System system)
        {
            return system.World.Hockeyists
                .Where(_ => _.Type != HockeyistType.Goalie)
                .Select(_ => new { distance = system.World.Puck.GetDistanceTo(_), isTeammate = _.IsTeammate })
                .Select(_ => _.isTeammate ? _.distance / 2 : _.distance)
                .Min();
        }

        private static bool IsPuckInStrikeZone(System system)
        {
            var net = GetNetPoint(system, true);
            return system.World.Puck.GetDistanceTo(net.X, net.Y) < 500;
        }

        private static Point GetNetPoint(System system, bool teamNet)
        {
            var player = teamNet ? system.World.GetMyPlayer() : system.World.GetOpponentPlayer();

            var netX = (player.NetBack + player.NetFront) / 2;
            var netY = (player.NetBottom + player.NetTop) / 2;

            return new Point { X = netX, Y = netY };
        }

        public static double SpeedValue(this System system, double speedX, double speedY)
        {
            return Math.Sqrt(Math.Abs(speedX) * (Math.Abs(speedX)
                + Math.Abs(speedY) * Math.Abs(speedY)));
        }

        public static void PursuePuck(System system)
        {
            var player = system.World.GetMyPlayer();

            var distanceToPuck = system.Self.GetDistanceTo(system.World.Puck.X, system.World.Puck.Y);
            var closestEnemy = GetClosestEnemy(system);

            if (distanceToPuck < system.Game.StickLength)
            {
                if (system.World.Puck.OwnerPlayerId == system.World.GetOpponentPlayer().Id) //enemy with puck at strikerange
                {
                    if (system.Self.RemainingCooldownTicks > 0)
                    {
                        var target = CalculateNextPoint(system, system.World.Puck.X, system.World.Puck.Y, system.World.Puck.SpeedX, system.World.Puck.SpeedY);
                        DoAction.MoveTo(system, target, false);
                    }
                    else if (Math.Abs(system.Self.GetAngleTo(system.World.Puck)) > system.Game.StickSector / 2)
                    {
                        DoAction.FaceTo(system, system.World.Puck.ToPoint());
                    }
                    else
                    {
                        var puckOwner = system.World.Hockeyists.FirstOrDefault(_ => _.Id == system.World.Puck.OwnerHockeyistId);
                        if (puckOwner != null && system.SpeedValue(puckOwner.SpeedX, puckOwner.SpeedY) > system.SpeedValue(system.Self.SpeedX, system.Self.SpeedY))
                            system.Move.Action = ActionType.Strike;
                        else
                            system.Move.Action = ActionType.TakePuck;
                    }
                }
                else if (system.World.Puck.OwnerHockeyistId == -1)
                {
                    if (system.Self.RemainingCooldownTicks > 0)
                    {
                        var target = CalculateNextPoint(system, system.World.Puck.X, system.World.Puck.Y, system.World.Puck.SpeedX, system.World.Puck.SpeedY);
                        DoAction.MoveTo(system, target, false);
                    }
                    else
                    {
                        if (Math.Abs(system.Self.GetAngleTo(system.World.Puck)) > system.Game.StickSector / 2)
                        {
                            DoAction.FaceTo(system, system.World.Puck.ToPoint());
                        }
                        else
                        {//todo: add probability
                            if ((Math.Abs(system.World.Puck.SpeedX) + Math.Abs(system.World.Puck.SpeedX)) / 2 > 18 && PuckMovingIntoOurNet(system.World.Puck.X, system.World.Puck.SpeedX, player.NetFront) && Math.Abs(system.Self.GetAngleTo(player.NetFront, (player.NetTop + player.NetBottom) / 2)) > Math.PI / 2)
                                system.Move.Action = ActionType.Strike;
                            else
                                system.Move.Action = ActionType.TakePuck;
                        }
                    }
                }
            }
            else if (closestEnemy != null
                && closestEnemy.GetDistanceTo(system.Self) < system.Game.StickLength * 2 / 3
                && system.Self.RemainingCooldownTicks == 0
                && Math.Abs(Math.Abs(system.Self.GetAngleTo(closestEnemy) - system.Game.StickSector / 2)) < 0.15
                && closestEnemy.State != HockeyistState.KnockedDown
                )
            {
                var enemyAngle = Math.Abs(system.Self.GetAngleTo(closestEnemy));
                if (enemyAngle > system.Game.StickSector / 2)
                {
                    DoAction.FaceTo(system, new Point { X = closestEnemy.X, Y = closestEnemy.Y });
                }
                else
                {
                    var nextEnemyPosition = new Point(closestEnemy.X + closestEnemy.SpeedX, closestEnemy.Y + closestEnemy.SpeedY);
                    var enemyWillBeInZoneNextTurn = system.Self.GetDistanceTo(nextEnemyPosition.X, nextEnemyPosition.Y) < system.Game.StickLength * 1 / 2
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
            }
            else
            {
                var target = CalculateNextPoint(system, system.World.Puck.X, system.World.Puck.Y, system.World.Puck.SpeedX, system.World.Puck.SpeedY);
                DoAction.MoveTo(system, target);
            }
        }

        public static Hockeyist GetClosestEnemy(this System system)
        {
            return system.World.Hockeyists.Where(_ => !_.IsTeammate && _.Type != HockeyistType.Goalie).OrderBy(_ => _.GetDistanceTo(system.Self)).FirstOrDefault();
        }
        public static double GetDistanceBetween(this System system, double x1, double y1, double x2, double y2)
        {
            double xRange = x1 - x2;
            double yRange = y1 - y2;
            return Math.Sqrt(xRange * xRange + yRange * yRange);
        }

        private static bool PuckMovingIntoOurNet(double puckX, double speed, double netX)
        {
            return (puckX < netX && speed > 0) || (puckX > netX && speed < 0);
        }

        private static double CalculateTicketsToGetToPosition(this System system, double targetX, double targetY)
        {
            var speed = system.SpeedValue(system.Self.SpeedX, system.Self.SpeedY);
            var t = -speed + Math.Sqrt(speed * speed + 2 + system.Self.GetDistanceTo(targetX, targetY));

            return t;
        }

        private static Point CalculateNextPoint(System system, double targetX, double targetY, double targetSpeedX, double targetSpeedY)
        {
            var timeToGetThere = system.CalculateTicketsToGetToPosition(targetX, targetY);
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

                if (targetY >= system.World.Height || targetY < 0)
                {
                    targetSpeedY = -targetSpeedY;
                }
                if (targetX >= system.World.Width - system.Game.GoalNetWidth || targetX < system.Game.GoalNetWidth)
                {
                    targetSpeedX = -targetSpeedX;
                }

                timeToGetThere = system.CalculateTicketsToGetToPosition(targetX, targetY);
                j++;
            }

            return result;
        }


        public static void Atack(System system)
        {
            var player = system.World.GetOpponentPlayer();
            var closestEnemy = system.GetClosestEnemy();

            var netX = (player.NetBack + player.NetFront) / 2;
            var netY = (player.NetBottom + player.NetTop) / 2.2;
            var kx = system.Game.GoalNetHeight * 1.6;

            var strikePosition = new Point
            {
                X = system.Self.X > netX ? netX + kx : netX - kx,
                Y = system.Self.Y > netY ? player.NetBottom + system.World.Puck.Radius : player.NetTop - system.World.Puck.Radius
            };

            var manuverPosition = new Point
            {
                X = system.Self.X > netX ? netX + kx : netX - kx,
                Y = system.Self.Y > netY ? player.NetBottom + 2*system.World.Puck.Radius : player.NetTop - 2*system.World.Puck.Radius
            };
                
            if (system.Self.GetDistanceTo(netX, netY) < system.Game.GoalNetHeight * 2)
            {
                if (system.Self.GetDistanceTo(strikePosition.X, strikePosition.Y) < system.Game.StickLength/2)
                {
                    DoAction.Strike(system);
                }
                else if (system.Self.GetDistanceTo(strikePosition.X, strikePosition.Y) < system.Game.StickLength &&
                         closestEnemy.GetDistanceTo(system.Self) < system.Game.StickLength)
                {
                    DoAction.Strike(system);
                }
                else
                {
                    DoAction.MoveTo(system, strikePosition, false);
                }
            }
            else
            {
                DoAction.MoveTo(system, manuverPosition);
            }
        }
    }
}