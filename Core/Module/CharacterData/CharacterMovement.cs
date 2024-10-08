﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Controller;
using Core.GeoEngine;
using Core.GeoEngine.Pathfinding;
using Core.Module.AreaData;
using Core.Module.NpcData;
using Core.Module.Player;
using Core.Module.WorldData;
using Core.NetworkPacket.ServerPacket;
using Core.NetworkPacket.ServerPacket.CharacterPacket;
using Core.TaskManager;
using Helpers;
using L2Logger;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Module.CharacterData;

public class CharacterMovement
{
    private readonly GameTimeController _timeController;
    private MoveData _move;
    private readonly Character _character;
    private bool _cursorKeyMovement = false;
    private bool _isFlying = false;
    private bool _isInVehicle = false;
    public bool IsMoving => _move != null;
    private readonly WorldInit _worldInit;
    private readonly GeoEngineInit _geoEngineInit;
    private readonly CharacterMovementStatus _characterMovementStatus;
    public CharacterMovementStatus CharacterMovementStatus() => _characterMovementStatus;
    public Character Character() => _character;

    public CharacterMovement(Character character)
    {
        _character = character;
        _timeController = character.ServiceProvider.GetRequiredService<GameTimeController>();
        _worldInit = _character.ServiceProvider.GetRequiredService<WorldInit>();
        _characterMovementStatus = new CharacterMovementStatus(this);
        _geoEngineInit = _character.ServiceProvider.GetRequiredService<GeoEngineInit>();
    }
        
    public async Task MoveToLocation(int x, int y, int z, int offset)
    {
        // Get the Move Speed of the Creature
        var speed = _character.CharacterCombat().GetCharacterSpeed();
        if ((speed <= 0))
        {
            await _character.SendActionFailedPacketAsync();
            return;
        }
            
        // Get current position of the Creature
        var curX = _character.GetX();
        var curY = _character.GetY();
        var curZ = _character.GetZ();

        //LoggerManager.Info($"curSpeed: {speed} curX: {curX} curY: {curY} curZ: {curZ} distX: {x} distY: {y} distZ: {z}");
            
        double dx = (x - curX);
        double dy = (y - curY);
        double dz = (z - curZ);
        var distance = Utility.Hypot(dx,dy);
            
        double cos;
        double sin;

        bool verticalMovementOnly = _isFlying && (distance == 0) && (dz != 0);
            
        var isInWater = _character.CharacterZone().IsInsideZone(AreaId.Water);
        if (isInWater && (distance > 700))
        {
            var divider = 700 / distance;
            x = curX + (int) (divider * dx);
            y = curY + (int) (divider * dy);
            z = curZ + (int) (divider * dz);
            dx = (x - curX);
            dy = (y - curY);
            dz = (z - curZ);
            distance = Utility.Hypot(dx, dy);
        }
            
        // Check if a movement offset is defined or no distance to go through
        if ((offset > 0) || (distance < 1))
        {
            // approximation for moving closer when z coordinates are different
            // TODO: handle Z axis movement better
            offset -= (int)Math.Abs(dz);
            if (offset < 5)
            {
                offset = 5;
            }
			
            // If no distance to go through, the movement is canceled
            if ((distance < 1) || ((distance - offset) <= 0))
            {
                // Notify the AI that the Creature is arrived at destination
                _character.CharacterNotifyEvent().NotifyEvent(CtrlEvent.EvtArrived);
                return;
            }
			
            // Calculate movement angles needed
            sin = dy / distance;
            cos = dx / distance;
            distance -= (offset - 5); // due to rounding error, we have to move a bit closer to be in range
			
            // Calculate the new destination with offset included
            x = curX + (int) (distance * cos);
            y = curY + (int) (distance * sin);
        }
        else
        {
            // Calculate movement angles needed
            sin = dy / distance;
            cos = dx / distance;
        }
            
        // Create and Init a MoveData object
        // GEODATA MOVEMENT CHECKS AND PATHFINDING
        // Initialize not on geodata path
        var m = new MoveData {OnGeodataPathIndex = -1, DisregardingGeodata = false};
            
        var originalDistance = distance;
        var originalX = x;
        var originalY = y;
        var originalZ = z;
            
        var gtx = (originalX - _worldInit.MapMinX) >> 4;
        var gty = (originalY - _worldInit.MapMinY) >> 4;
        if (IsOnGeoDataPath())
        {
            try
            {
                if ((gtx == _move.GeoPathGtx) && (gty == _move.GeoPathGty))
                {
                    return;
                }
						
                _move.OnGeodataPathIndex = -1; // Set not on geodata path.
            }
            catch (Exception ex)
            {
                LoggerManager.Error( "IsOnGeoDataPath" + ex.Message);
            }
        }
        
        var directMove = _character.IsPlayer() && _character.CharacterDesire().GetDesire() == Desire.AttackDesire;

        if (directMove //
            || (!_isInVehicle // Not in vehicle.
                && !(_character.IsPlayer() && (distance > 3000)) // Should be able to click far away and move.
                && !(_character.IsAttackable() && (Math.Abs(dz) > 100)) // Monsters can move on ledges.
                && !(((curZ - z) > 300) &&
                     (distance < 300)))) // Prohibit correcting destination if character wants to fall.
        {
            // location different if destination wasn't reached (or just z coord is different)
            var destiny = _geoEngineInit.GetValidLocation(curX, curY, curZ, x, y, z, _character.ObjectId);
            x = destiny.GetX();
            y = destiny.GetY();
            if (!_character.IsPlayer())
            {
                z = destiny.GetZ();
            }
            dx = x - curX;
            dy = y - curY;
            dz = z - curZ;
        
            distance = verticalMovementOnly ? Math.Pow(dz, 2) : Utility.Hypot(dx, dy);
        }

        // Pathfinding checks.
        if (((originalDistance - distance) > 30))
        {
            m.GeoPath =  _geoEngineInit.CellPathFinding().FindPath(curX, curY, curZ, originalX, originalY, originalZ, _character.ObjectId, true);
            var found = (m.GeoPath != null) && (m.GeoPath.Count > 1);

            // If path not found and this is an Attackable, attempt to find closest path to destination.
            if (!found && _character.IsAttackable())
            {
                var xMin = Math.Min(curX, originalX);
                var xMax = Math.Max(curX, originalX);
                var yMin = Math.Min(curY, originalY);
                var yMax = Math.Max(curY, originalY);
                var maxDiff = Math.Min(Math.Max(xMax - xMin, yMax - yMin), 500);
                xMin -= maxDiff;
                xMax += maxDiff;
                yMin -= maxDiff;
                yMax += maxDiff;
                var destinationX = 0;
                var destinationY = 0;
                var shortDistance = double.MaxValue;
                double tempDistance;
                LinkedList<AbstractNodeLoc> tempPath;
                for (int sX = xMin; sX < xMax; sX += 500)
                {
                    for (int sY = yMin; sY < yMax; sY += 500)
                    {
                        tempDistance = Utility.Hypot(sX - originalX, sY - originalY);
                        if (tempDistance < shortDistance)
                        {
                            tempPath = _geoEngineInit.CellPathFinding().FindPath(curX, curY, curZ, sX, sY, originalZ, _character.ObjectId, false);
                            found = tempPath is { Count: > 1 };
                            if (found)
                            {
                                shortDistance = tempDistance;
                                m.GeoPath = tempPath;
                                destinationX = sX;
                                destinationY = sY;
                            }
                        }
                    }
                }
                found = m.GeoPath is { Count: > 1 };
                if (found)
                {
                    originalX = destinationX;
                    originalY = destinationY;
                }
            }

            if (found)
            {
                m.OnGeodataPathIndex = 0; // on first segment
                m.GeoPathGtx = gtx;
                m.GeoPathGty = gty;
                m.GeoPathAccurateTx = originalX;
                m.GeoPathAccurateTy = originalY;
                x = m.GeoPath.ElementAt(m.OnGeodataPathIndex).GetX();
                y = m.GeoPath.ElementAt(m.OnGeodataPathIndex).GetY();
                z = m.GeoPath.ElementAt(m.OnGeodataPathIndex).GetZ();
                dx = x - curX;
                dy = y - curY;
                dz = z - curZ;
                distance = verticalMovementOnly ? Math.Pow(dz, 2) : Utility.Hypot(dx, dy);
                sin = dy / distance;
                cos = dx / distance;
            }
            else // No path found.
            {
                if (_character.IsPlayer())
                {
                    await _character.SendActionFailedPacketAsync();
                    return;
                }

                m.DisregardingGeodata = true;

                x = originalX;
                y = originalY;
                z = originalZ;
                distance = originalDistance;
            }
        }
            
        // If no distance to go through, the movement is canceled
        if ((distance < 1) && _character is NpcInstance npc)
        {
            npc.NpcDesire().AddDesire(Desire.IdleDesire, npc);
            await npc.SendActionFailedPacketAsync();
            return;
        }

        var ticksToMove = 1 + (int) ((_timeController.TicksPerSecond * distance) / speed);
        m.XDestination = x;
        m.YDestination = y;
        m.ZDestination = z; // this is what was requested from client
        // Calculate and set the heading of the Creature
        m.Heading = 0; // initial value for coordinate sync
            
        // Does not break heading on vertical movements
        _character.Heading = CalculateRange.CalculateHeadingFrom(cos, sin);
            
        m.MoveStartTime = _timeController.GetGameTicks();
            
        // Set the Creature _move object to MoveData object
        _move = m;
            
        _timeController.RegisterMovingObject(_character);
            
        // Create a task to notify the AI that Creature arrives at a check point of the movement
        if ((ticksToMove * _timeController.MillisInTick) > 3000)
        {
            TaskManagerScheduler.Schedule(() => 
            {
                _character.CharacterNotifyEvent().NotifyEvent(CtrlEvent.EvtArrivedRevalidate);
            }, 2000);
        }
    }
        
    public async Task<bool> UpdatePosition()
    {
        var move = _move;
        if (move == null)
        {
            return await Task.FromResult(true);
        }
            
        // Check if the position has already be calculated
        if (move.MoveTimestamp == 0)
        {
            move.MoveTimestamp = move.MoveStartTime;
            move.XAccurate = _character.GetX();
            move.YAccurate = _character.GetY();
        }
        // Check if the position has already be calculated
        var gameTicks = _timeController.GetGameTicks();
        if (move.MoveTimestamp == gameTicks)
        {
            return await Task.FromResult(false);
        }
            
        var xPrev = _character.GetX();
        var yPrev = _character.GetY();
        var zPrev = _character.GetZ(); // the z coordinate may be modified by coordinate synchronizations
        double dx;
        double dy;
        double dz;
            
        dx = move.XDestination - move.XAccurate;
        dy = move.YDestination - move.YAccurate;
        dz = move.ZDestination - zPrev;

        var speed = _character.CharacterCombat().GetCharacterSpeed();

        if (_character is PlayerInstance playerInstance)
        {
            // Stop movement when player has clicked far away and intersected with an obstacle.
            var distance = Utility.Hypot(dx, dy);
            var angle = Utility.ConvertHeadingToDegree(playerInstance.Heading);
            if (distance > 3000)
            {
                var radian = Utility.ToRadians(angle);
                var course = Utility.ToRadians(180);
                var frontDistance = 10 * (speed / 100);
                var x1 = (int) (Math.Cos(Math.PI + radian + course) * frontDistance);
                var y1 = (int) (Math.Sin(Math.PI + radian + course) * frontDistance);
                var x = xPrev + x1;
                var y = yPrev + y1;
                if (!_geoEngineInit.CanMoveToTarget(xPrev, yPrev, zPrev, x, y, zPrev, _character.ObjectId))
                {
                    _move.OnGeodataPathIndex = -1;
                    if ( _character.CharacterDesire().IsFollowing())
                    {
                        _character.CharacterDesire().StopFollow();
                    }
                    _character.CharacterDesire().AddDesire(Desire.IdleDesire, _character);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                    
            }
                
        }
            
        // Distance from destination.
        var delta = (dx * dx) + (dy * dy);
        // Distance from destination.
        var isFloating = false;
        if (!isFloating && (delta < 10000) && ((dz * dz) > 2500)) // Close enough, allows error between client and server geodata if it cannot be avoided.
        {
            delta = Math.Sqrt(delta);
        }
        else
        {
            delta = Math.Sqrt(delta + (dz * dz));
        }
        var collision = _character.CharacterCombat().GetCollisionRadius();

        delta = Math.Max(0.00001, delta - collision);

        var distFraction = CalculateDistanceFraction(delta, speed, gameTicks - move.MoveTimestamp);

        if (distFraction > 1.79)
        {
            // Set the position of the Creature to the destination.
            _character.SetXYZ(move.XDestination, move.YDestination, move.ZDestination);
        }
        else
        {
            move.XAccurate += dx * distFraction;
            move.YAccurate += dy * distFraction;
			
            // Set the position of the Creature to estimated after parcial move.
            _character.SetXYZ((int) move.XAccurate, (int) move.YAccurate, zPrev + (int) ((dz * distFraction) + 0.895));
        }
            
        //LoggerManager.Info($"curX: {_character.GetX()} curY: {_character.GetY()} curZ: {_character.GetZ()} gameTicks: {gameTicks} speed: {speed}");
            
        // Set the timer of last position update to now
        move.MoveTimestamp = gameTicks;
        _character.CharacterZone().RevalidateZone();
        if (((gameTicks - move.LastBroadcastTime) >= 3) && IsOnGeoDataPath(move))
        {
            move.LastBroadcastTime = gameTicks;
            await _character.SendToKnownPlayers(new CharMoveToLocation(_character));
        }
        return await Task.FromResult(distFraction > 1.79);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<bool> MoveToNextRoutePoint()
    {
        var move = _move;
        if (move == null)
        {
            return await Task.FromResult(false);
        }

        if (!IsOnGeoDataPath(move))
        {
            // Cancel the move action
            _move = null;
            return await Task.FromResult(false);
        }

        // Get the Move Speed of the Creature
        var speed = _character.CharacterCombat().GetCharacterSpeed();
        if ((speed <= 0))
        {
            // Cancel the move action
            _move = null;
            return await Task.FromResult(false);
        }

        var newMove = CreateNewMoveData(move);

        var distance = Utility.Hypot(newMove.XDestination - _character.GetX(), newMove.YDestination - _character.GetY());
        // Calculate and set the heading of the Creature
        if (distance != 0)
        {
            _character.Heading = CalculateRange.CalculateHeadingFrom(_character.GetX(), _character.GetY(), newMove.XDestination, newMove.YDestination);
        }

        // Calculate the number of ticks between the current position and the destination
        // One tick added for rounding reasons
        var ticksToMove = (int) ((_timeController.TicksPerSecond * distance) / speed);
        newMove.Heading = 0; // initial value for coordinate sync
        newMove.MoveStartTime = _timeController.GetGameTicks();
        // Set the Creature _move object to MoveData object
        _move = newMove;

        _timeController.RegisterMovingObject(_character);
        // Create a task to notify the AI that Creature arrives at a check point of the movement
        if ((ticksToMove * _timeController.MillisInTick) > 3000)
        {
            TaskManagerScheduler.Schedule(() =>
            {
                _character.CharacterNotifyEvent().NotifyEvent(CtrlEvent.EvtArrivedRevalidate);
            }, 2000);
        }

        // the CtrlEvent.EVT_ARRIVED will be sent when the character will actually arrive to destination by GameTimeController

        // Send a Server->Client packet CharMoveToLocation to the actor and all PlayerInstance in its _knownPlayers
        var packet = new CharMoveToLocation(_character);
        await _character.SendPacketAsync(packet);
        await _character.SendToKnownPlayers(packet);
        return await Task.FromResult(true);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="move"></param>
    /// <returns></returns>
    private MoveData CreateNewMoveData(MoveData move)
    {
        // Create and Init a MoveData object
        var newMove = new MoveData
        {
            // Update MoveData object
            OnGeodataPathIndex = move.OnGeodataPathIndex + 1, // next segment
            GeoPath = move.GeoPath,
            GeoPathGtx = move.GeoPathGtx,
            GeoPathGty = move.GeoPathGty,
            GeoPathAccurateTx = move.GeoPathAccurateTx,
            GeoPathAccurateTy = move.GeoPathAccurateTy
        };

        if (move.OnGeodataPathIndex == (move.GeoPath.Count - 2))
        {
            newMove.XDestination = move.GeoPathAccurateTx;
            newMove.YDestination = move.GeoPathAccurateTy;
            newMove.ZDestination = move.GeoPath.ElementAt(newMove.OnGeodataPathIndex).GetZ();
        }
        else
        {
            newMove.XDestination = move.GeoPath.ElementAt(newMove.OnGeodataPathIndex).GetX();
            newMove.YDestination = move.GeoPath.ElementAt(newMove.OnGeodataPathIndex).GetY();
            newMove.ZDestination = move.GeoPath.ElementAt(newMove.OnGeodataPathIndex).GetZ();
        }

        return newMove;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="delta"></param>
    /// <param name="speed"></param>
    /// <param name="timeElapsed"></param>
    /// <returns></returns>
    private double CalculateDistanceFraction(double delta, double speed, int timeElapsed)
    {
        var distFraction = double.MaxValue;
        if (delta > 1)
        {
            var distPassed = (speed * timeElapsed) / _timeController.TicksPerSecond;
            distFraction = distPassed / delta;
        }
        return distFraction;
    }

    /// <summary>
    /// GetXDestination
    /// </summary>
    /// <returns></returns>
    public int GetXDestination()
    {
        return _move?.XDestination ?? _character.GetX();
    }

    /// <summary>
    /// GetYDestination
    /// </summary>
    /// <returns></returns>
    public int GetYDestination()
    {
        return _move?.YDestination ?? _character.GetY();
    }

    /// <summary>
    /// GetZDestination
    /// </summary>
    /// <returns></returns>
    public int GetZDestination()
    {
        return _move?.ZDestination ?? _character.GetZ();
    }

    /// <summary>
    /// StopMoveAsync
    /// </summary>
    /// <param name="pos"></param>
    public async Task StopMoveAsync(Location pos)
    {
        // Delete movement data of the Creature
        _move = null;
        //_characterMovementStatus.SetStand();
        _character.WorldObjectPosition().SetXYZ(pos.GetX(), pos.GetY(), pos.GetZ());
        _character.Heading = pos.GetHeading();

        if (_character.IsPlayer())
        {
            _character.CharacterZone().RevalidateZone();
        }
        var stopMovePacket = new StopMove(_character);
        await _character.SendToKnownPlayers(stopMovePacket);
    }

    /// <summary>
    /// IsOnGeoDataPath
    /// </summary>
    /// <returns></returns>
    private bool IsOnGeoDataPath()
    {
        return _move is { OnGeodataPathIndex: >= 0 } && _move.OnGeodataPathIndex < _move.GeoPath.Count - 1;
    }

    /// <summary>
    /// IsOnGeoDataPath
    /// </summary>
    /// <param name="move"></param>
    /// <returns></returns>
    private bool IsOnGeoDataPath(MoveData move)
    {
        return move.OnGeodataPathIndex >= 0 && move.OnGeodataPathIndex < move.GeoPath.Count - 1;
    }
}