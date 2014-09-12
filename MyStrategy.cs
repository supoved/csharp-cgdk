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

            if (self.Id == world.Puck.OwnerHockeyistId)
            {
                FollowTactics.Atack(system);
            }
            else
            {
                var role = _team[self.Id];
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
            var defAngle = system.Self.GetAngleTo(target.X, target.Y);
            if (Math.Abs(defAngle) > Math.PI / 2)
            {
                system.Move.SpeedUp = rush ? -1D : -0.5D;
                system.Move.Turn = system.Self.GetAngleTo(target.X, target.Y) - Math.PI;
            }
            else
            {
                system.Move.SpeedUp = rush ? 1D : 0.6D;
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

            if (Math.Abs(angleToNet) < 1.0D * Math.PI / 180.0D)
            {
                system.Move.Action = ActionType.Strike;
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

            if (distanceToPuck < system.Game.StickLength && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id)
            {
                PursuePuck(system);
            }
            if (isPuckInStrikeZone && distanceToPuck < system.Game.StickLength * 4 && system.World.Puck.OwnerPlayerId == system.World.GetOpponentPlayer().Id)
            {
                PursuePuck(system);
            }
            else if (system.World.Puck.X > system.Self.X && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id)
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY });
            }
            else if (distanceToPuck < system.Game.StickLength * 2.5 && system.World.Puck.OwnerPlayerId != system.World.GetMyPlayer().Id)
            {
                PursuePuck(system);
            }
            else if (distanceToPuck < minDistanceToPuck)
            {
                PursuePuck(system);
            }
            else if (distanceToNet > system.Game.StickLength * 2)
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY });
            }
            else if (distanceToNet > system.Game.StickLength / 2)
            {
                DoAction.MoveTo(system, new Point { X = defX, Y = defY }, false);
            }
            else
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

        public static void PursuePuck(System system)
        {
            var player = system.World.GetMyPlayer();

            var distanceToPuck = system.Self.GetDistanceTo(system.World.Puck.X, system.World.Puck.Y);
            var closestEnemy = system.World.Hockeyists.Where(_ => !_.IsTeammate && _.Type != HockeyistType.Goalie).OrderBy(_ => _.GetDistanceTo(system.Self)).FirstOrDefault();

            if (distanceToPuck < system.Game.StickLength)
            {
                if (system.World.Puck.OwnerPlayerId == system.World.GetOpponentPlayer().Id)
                {
                    system.Move.Action = ActionType.Strike;
                }
                else if (system.World.Puck.OwnerHockeyistId == -1)
                {
                    if (system.Self.RemainingCooldownTicks > 0)
                    {
                        DoAction.MoveTo(system, system.World.Puck.ToPoint(), false);
                    }
                    else
                    {
                        if (Math.Abs(system.Self.GetAngleTo(system.World.Puck)) > system.Game.StickSector/2)
                        {
                            DoAction.FaceTo(system, system.World.Puck.ToPoint());
                        }
                        else
                        {//todo: add probability
                            if ((Math.Abs(system.World.Puck.SpeedX) + Math.Abs(system.World.Puck.SpeedX)) / 2 > 22 && PuckMovingIntoOurNet(system.World.Puck.X, system.World.Puck.SpeedX, player.NetFront) && Math.Abs(system.Self.GetAngleTo(player.NetFront, (player.NetTop + player.NetBottom) / 2)) > Math.PI / 2)
                                system.Move.Action = ActionType.Strike;
                            else
                                system.Move.Action = ActionType.TakePuck;
                        }
                    }
                }
            }
            else if (closestEnemy != null && closestEnemy.GetDistanceTo(system.Self) < system.Game.StickLength && system.Self.RemainingCooldownTicks == 0)
            {
                var enemyAngle = Math.Abs(system.Self.GetAngleTo(closestEnemy));
                if (enemyAngle > system.Game.StickSector/2 && Math.Abs(enemyAngle - system.Game.StickSector/2) < 0.15 )
                {
                    DoAction.FaceTo(system, new Point { X = closestEnemy.X, Y = closestEnemy.Y });
                }
                else
                {
                    system.Move.Action = ActionType.Strike;
                }
            }
            else
            {
                DoAction.MoveTo(system, system.World.Puck.ToPoint());
            }
        }

        private static bool PuckMovingIntoOurNet(double puckX, double speed, double netX)
        {
            return (puckX < netX && speed > 0) || (puckX > netX && speed < 0);
        }


        public static void Atack(System system)
        {
            var player = system.World.GetOpponentPlayer();

            var netX = (player.NetBack + player.NetFront) / 2;
            var netY = (player.NetBottom + player.NetTop) / 2;

            if (system.Self.GetDistanceTo(netX, netY) < 450)
            {
                DoAction.Strike(system);
            }
            else
            {
                var strikePosition = new Point
                {
                    X = system.Self.X > netX ? netX + 400 : netX - 400,
                    Y = system.Self.Y > netY ? player.NetBottom + (system.World.Height - player.NetBottom) / 4 : player.NetTop - (player.NetTop / 4)
                };

                DoAction.MoveTo(system, strikePosition);
            }
        }
    }
}