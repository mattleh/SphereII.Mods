using GamePath;
using Platform;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;

namespace UAI
{
    public static class SCoreUtils
    {
        public static void DisplayDebugInformation(Context _context, string prefix = "", string postfix = "")
        {
            if (!GameManager.IsDedicatedServer)
            {
                if (!GamePrefs.GetBool(EnumGamePrefs.DebugMenuShowTasks) || _context.Self.IsDead())
                {
                    if (EntityUtilities.GetLeaderOrOwner(_context.Self.entityId) == null)
                        _context.Self.DebugNameInfo = String.Empty;
                    else
                    {
                        if (string.IsNullOrEmpty(_context.Self.DebugNameInfo))
                            _context.Self.DebugNameInfo = _context.Self.EntityName;
                    }
                    return;
                }
            }
            var message = $" ( {_context.Self.entityId} ) {prefix}\n";
            message += $" Active Action: {_context.ActionData.Action?.Name}\n";
            var taskIndex = _context.ActionData.TaskIndex;
            var tasks = _context.ActionData.Action?.GetTasks();
            if (tasks == null)
            {
                message += $" Active Task: None";
                _context.Self.DebugNameInfo = message;
                return;
            }

            message += $" Active Task: {tasks[taskIndex]}\n";
            message += $" Active Target: {_context.ActionData.Target}\n";
            message += $" {postfix}";

            _context.Self.DebugNameInfo = message;
        }

        public static void SimulateActionInstantExecution(Context _context, int _actionIdx, ItemStack _itemStack)
        {
            if (!Equals(_itemStack, ItemStack.Empty))
            {
                _context.Self.MinEventContext.ItemValue = _itemStack.itemValue;

                // starting action fire events.
                _context.Self.MinEventContext.ItemValue.FireEvent(_actionIdx == 0 ? MinEventTypes.onSelfPrimaryActionStart : MinEventTypes.onSelfSecondaryActionStart, _context.Self.MinEventContext);
                _context.Self.FireEvent(_actionIdx == 0 ? MinEventTypes.onSelfPrimaryActionStart : MinEventTypes.onSelfSecondaryActionStart, false);

                _itemStack.itemValue.ItemClass.Actions[_actionIdx].ExecuteInstantAction(_context.Self, _itemStack, false, null);

                // Ending action fire events
                _context.Self.MinEventContext.ItemValue.FireEvent(_actionIdx == 0 ? MinEventTypes.onSelfPrimaryActionEnd : MinEventTypes.onSelfSecondaryActionEnd, _context.Self.MinEventContext);
                _context.Self.FireEvent(_actionIdx == 0 ? MinEventTypes.onSelfPrimaryActionEnd : MinEventTypes.onSelfSecondaryActionEnd, false);
            }

            _context.ActionData.CurrentTask.Stop(_context);
        }

        public static void MoveBack(Context _context, Vector3 position)
        {
            _context.Self.moveHelper.SetMoveTo(position, true);
        }

        public static void TurnToFaceEntity(Context _context, EntityAlive priorityEntity = null)
        {
            if (_context.Self.IsSleeping)
                return;

            var entitiesInBounds = GameManager.Instance.World.GetEntitiesInBounds(_context.Self, new Bounds(_context.Self.position, Vector3.one * 5f));
            if (entitiesInBounds.Count > 0)
            {
                Entity lookEntity = null;

                foreach (var entity in entitiesInBounds)
                {
                    // Prioritize your leader over non-leader players
                    if (priorityEntity != null && entity.entityId == priorityEntity.entityId)
                    {
                        lookEntity = entity;
                        break;
                    }

                    if (entity is EntityPlayerLocal || entity is EntityPlayer)
                    {
                        if (EntityTargetingUtilities.IsEnemy(_context.Self, entity))
                            continue;

                        if (_context.Self.GetActivationCommands(new Vector3i(_context.Self.position), lookEntity as EntityAlive).Length > 0)
                            lookEntity = entity;
                    }
                }

                if (lookEntity != null)
                {
                    SCoreUtils.SetLookPosition(_context, lookEntity);
                }

            }
        }
        public static void HideWeapon(Context _context)
        {
            if (_context.Self.inventory.holdingItemIdx != _context.Self.inventory.DUMMY_SLOT_IDX)
            {
                _context.Self.inventory.SetHoldingItemIdx(_context.Self.inventory.DUMMY_SLOT_IDX);
                _context.Self.inventory.OnUpdate();
            }
        }

        public static void SetWeapon(Context _context)
        {
            if (_context.Self.inventory.holdingItemIdx != 0)
            {
                _context.Self.inventory.SetHoldingItemIdx(0);
                _context.Self.inventory.OnUpdate();
            }
        }

        public static void CheckJump(Context _context)
        {
            if (!_context.Self.onGround || _context.Self.Jumping)
                return;

            // Find out feet, and just a smidge ahead
            var a = _context.Self.position + _context.Self.GetForwardVector() * 0.2f;
            a.y += 0.4f;

            RaycastHit ray;
            // Check if we can hit anything downwards
            if (Physics.Raycast(a - Origin.position, Vector3.down, out ray, 3.4f, 1082195968) && !(ray.distance > 2.2f)) return;

            // if we WILL hit something, don't jump.
            var vector3i = new Vector3i(Utils.Fastfloor(_context.Self.position.x), Utils.Fastfloor(_context.Self.position.y + 2.35f), Utils.Fastfloor(_context.Self.position.z));
            var block = _context.Self.world.GetBlock(vector3i);
            if (block.Block.IsMovementBlocked(_context.Self.world, vector3i, block, BlockFace.None)) return;


            // Stop the forward movement, so we don't slide off the edge.
            EntityUtilities.Stop(_context.Self.entityId);

            // If our target is below us, just drop down without jumping a lot.
            var entityAlive = UAIUtils.ConvertToEntityAlive(_context.ActionData.Target);
            if (entityAlive != null && entityAlive.IsAlive())
            {
                var drop = Mathf.Abs(entityAlive.position.y - _context.Self.position.y);
                if (drop > 2)
                {
                    _context.Self.moveHelper.StartJump(true, 1f, 0f);
                    return;
                }
            }
            // if we are going to land on air, let's not jump so far out.
            var landingSpot = _context.Self.position + _context.Self.GetForwardVector() + _context.Self.GetForwardVector() + Vector3.down;
            var block2 = _context.Self.world.GetBlock(new Vector3i(landingSpot));
            if (block2.isair)
                _context.Self.moveHelper.StartJump(true, 1f, 0f);
            else
                _context.Self.moveHelper.StartJump(true, 2f, 1f);
        }

        public static bool IsBlocked(Context _context)
        {
            // Still calculating the path, let's let it finish.
            if (PathFinderThread.Instance.IsCalculatingPath(_context.Self.entityId))
                return false;

            if (_context.Self.bodyDamage.CurrentStun != EnumEntityStunType.None)
                return true;

            // Check if we need to jump.
            CheckJump(_context);

            var result = _context.Self.moveHelper.BlockedTime <= 0.3f; //&& !_context.Self.navigator.noPathAndNotPlanningOne();
            if (result)
                return false;

            CheckForClosedDoor(_context);

          
            return _context.Self.moveHelper.IsBlocked;
        }


        public static void TeleportToPosition(Context _context, Vector3 position)
        {
            _context.Self.SetPosition(position);
        }

        public static void TeleportToLeader(Context _context, bool blocked = true)
        {
            var leader = EntityUtilities.GetLeaderOrOwner(_context.Self.entityId) as EntityAlive;
            if (leader == null) return;

            if (blocked)
            {
                GameManager.Instance.World.GetRandomSpawnPositionMinMaxToPosition(leader.position, 1, 2, 3, false, out var position);
                if (position == Vector3.zero)
                    position = leader.position + Vector3.back;
                _context.Self.SetPosition(position);
                return;
            }
            // If we are on a mission, don't execute this teleport; let the one on entityaliveSDX handle it.
            var entityAlive = _context.Self as EntityAliveSDX;
            if (entityAlive != null)
            {
                if ( !entityAlive.IsOnMission())
                    entityAlive.TeleportToPlayer(leader, false);
            }
        }

        public static bool HasBuff(Context _context, string buff)
        {
            return !string.IsNullOrEmpty(buff) && _context.Self.Buffs.HasBuff(buff);
        }

        public static Vector3 HasHomePosition(Context _context)
        {
            if (!_context.Self.hasHome())
                return Vector3.zero;

            var homePosition = _context.Self.getHomePosition();
            var position = RandomPositionGenerator.CalcTowards(_context.Self, 5, 100, 10, homePosition.position.ToVector3());
            return position == Vector3.zero ? Vector3.zero : position;
        }

        public static bool IsAnyoneNearby(Context _context, float distance = 20f)
        {
            var nearbyEntities = new List<Entity>();

            // Search in the bounds are to try to find the most appealing entity to follow.
            var bb = new Bounds(_context.Self.position, new Vector3(distance, distance, distance));

            _context.Self.world.GetEntitiesInBounds(typeof(EntityAlive), bb, nearbyEntities);
            for (var i = nearbyEntities.Count - 1; i >= 0; i--)
            {
                var x = nearbyEntities[i] as EntityAlive;
                if (x == null) continue;
                if (x == _context.Self) continue;
                if (x.IsDead()) continue;

                // Otherwise they are an enemy.
                return true;
            }

            return false;
        }

        public static bool CanSee(EntityAlive sourceEntity, EntityAlive targetEntity, float maxDistance = -1)
        {
            // If they are dead, you can't see them anymore...
            if (targetEntity.IsDead())
                return false;

            // If the entity isn't very close to us, make sure they are in our viewcone.
            var distance = sourceEntity.GetDistanceSq(targetEntity);
            if ( distance > 100)
            {
                if (!sourceEntity.IsInViewCone(targetEntity.position))
                    return false;
            }

            // Check to see if its in our "See" cache
            if (sourceEntity.CanSee(targetEntity))
                return true;

            // Are we already targetting each other?
            var target = EntityUtilities.GetAttackOrRevengeTarget(targetEntity.entityId);
            if (target?.entityId == sourceEntity.entityId) return true;
            target = EntityUtilities.GetAttackOrRevengeTarget(sourceEntity.entityId);
            if (target?.entityId == targetEntity.entityId) return true;

            var headPosition = sourceEntity.getHeadPosition();
            var headPosition2 = targetEntity.getHeadPosition();
            var direction = headPosition2 - headPosition;
            var seeDistance = maxDistance;
            if ( maxDistance > -1)
                seeDistance = maxDistance;
            else
                seeDistance = sourceEntity.GetSeeDistance();

            if (direction.magnitude > seeDistance)
                return false;

            var ray = new Ray(headPosition, direction);
            ray.origin += direction.normalized * 0.2f;

            int hitMask = GetHitMaskByWeaponBuff(sourceEntity);
            if (Voxel.Raycast(sourceEntity.world, ray, seeDistance, hitMask, 0.0f))//|| Voxel.Raycast(sourceEntity.world, ray, seeDistance, false, false))
            //if (Voxel.Raycast(sourceEntity.world, ray, seeDistance, true, true)) // Original code
            {
                var hitRootTransform = GameUtils.GetHitRootTransform(Voxel.voxelRayHitInfo.tag, Voxel.voxelRayHitInfo.transform);
                if (hitRootTransform == null)
                    return false;

                var component = hitRootTransform.GetComponent<EntityAlive>();
                if (component != null && component.IsAlive() && targetEntity == component)
                {
                    // Don't wake up the sleeping zombies if the leader is crouching.
                    var leader = EntityUtilities.GetLeaderOrOwner(sourceEntity.entityId) as EntityAlive;
                    if (leader != null && leader.IsCrouching && component.IsSleeping)
                        return false;

                    if (sourceEntity.GetDistanceSq(targetEntity) > 10)
                    {
                        if (!sourceEntity.IsInViewCone(targetEntity.position)) return false;
                    }
                    /// Add the entity to our CanSee Cache, which expires.
                    sourceEntity.SetCanSee(targetEntity);
                    return true;
                }
            }

            return false;
        }

        public static bool IsEnemyNearby(Context _context, float distance = 20f)
        {
            // Do we have a revenge target at any distance? If so, stay paranoid.
            var revengeTarget = _context.Self.GetRevengeTarget();
            if (revengeTarget && !EntityTargetingUtilities.ShouldForgiveDamage(_context.Self, revengeTarget))
                return true;

            var nearbyEntities = new List<Entity>();

            // Search in the bounds are to try to find the most appealing entity to follow.
            var bb = new Bounds(_context.Self.position, new Vector3(distance, distance, distance));

            _context.Self.world.GetEntitiesInBounds(typeof(EntityAlive), bb, nearbyEntities);
            for (var i = nearbyEntities.Count - 1; i >= 0; i--)
            {
                var x = nearbyEntities[i] as EntityAlive;
                if (x == null) continue;
                if (x == _context.Self) continue;
                if (x.IsDead()) continue;

                // Check to see if they are our enemy first, before deciding if we should see them.
                if (!EntityTargetingUtilities.IsEnemy(_context.Self, x)) continue;

                // Can we see them?
                if (!SCoreUtils.CanSee(_context.Self, x, distance)) continue;

                // Otherwise they are an enemy.
                return true;
            }

            return false;
        }

        public static void SetCrouching(Context _context, bool crouch = false)
        {
            _context.Self.Crouching = crouch;
        }

        public static List<Vector3> ScanForTileEntities(Context _context, string _targetTypes = "")
        {
            var paths = new List<Vector3>();
            var blockPosition = _context.Self.GetBlockPosition();
            var chunkX = World.toChunkXZ(blockPosition.x);
            var chunkZ = World.toChunkXZ(blockPosition.z);

            if (string.IsNullOrEmpty(_targetTypes) || _targetTypes.ToLower().Contains("basic"))
                _targetTypes = "LandClaim, Loot, VendingMachine, Forge, Campfire, Workstation, PowerSource";
            for (var i = -1; i < 2; i++)
            {
                for (var j = -1; j < 2; j++)
                {
                    var chunk = (Chunk)_context.Self.world.GetChunkSync(chunkX + j, chunkZ + i);
                    if (chunk == null) continue;

                    var tileEntities = chunk.GetTileEntities();
                    foreach (var tileEntity in tileEntities.list)
                    {
                        foreach (var filterTypeFull in _targetTypes.Split(','))
                        {
                            // Check if the filter type includes a :, which may indicate we want a precise block.
                            var filterType = filterTypeFull;
                            var blockNames = "";
                            if (filterTypeFull.Contains(":"))
                            {
                                filterType = filterTypeFull.Split(':')[0];
                                blockNames = filterTypeFull.Split(':')[1];
                            }
                            // Parse the filter type and verify if the tile entity is in the filter.
                            var targetType = EnumUtils.Parse<TileEntityType>(filterType, true);
                            if (tileEntity.GetTileEntityType() != targetType) continue;

                            switch (tileEntity.GetTileEntityType())
                            {
                                case TileEntityType.None:
                                    continue;
                                // If the loot containers were already touched, don't path to them.
                                case TileEntityType.Loot:
                                    if (((TileEntityLootContainer)tileEntity).bTouched)
                                        continue;
                                    break;
                                case TileEntityType.SecureLoot:
                                    if (((TileEntitySecureLootContainer)tileEntity).bTouched)
                                        continue;
                                    break;
                            }

                            // Search for the tile entity's block name to see if its filtered.
                            if ( !string.IsNullOrEmpty(blockNames))
                            {
                                if (!blockNames.Contains(tileEntity.blockValue.Block.GetBlockName()))
                                    continue;
                            }
                            var position = tileEntity.ToWorldPos().ToVector3();
                            paths.Add(position);
                        }
                    }
                }
            }


            // sort the paths to keep the closes one.
            paths.Sort(new SCoreUtils.NearestPathSorter(_context.Self));
            return paths;
        }

        public static float SetSpeed(Context _context, bool panic = false)
        {
            var speed = _context.Self.GetMoveSpeed();
            if (panic)
                speed = _context.Self.GetMoveSpeedPanic();

            _context.Self.navigator.setMoveSpeed(speed);
            return speed;
        }

        public static void FindPath(Context _context, Vector3 _position, bool panic = false)
        {
            // If we have a leader, and following, match the player speed.
            var leader = EntityUtilities.GetLeaderOrOwner(_context.Self.entityId) as EntityAlive;
            if (leader != null && _context.ActionData.CurrentTask is UAITaskFollowSDX)
            {
                var tag = FastTags.Parse("running");
                panic = leader.CurrentMovementTag.Test_AllSet(tag);
            }

            var speed = SetSpeed(_context, panic);
            _position = SCoreUtils.GetMoveToLocation(_context, _position);

            //if (!_context.Self.navigator.noPathAndNotPlanningOne())
            //{
            //    // If there's not a lot of distance to go, don't re-path.
            //    var distance = Vector3.Distance(_context.Self.position, _position);
            //    if (distance < 2f)
            //        return;
            //}

            // Path finding has to be set for Breaking Blocks so it can path through doors
//            var path = PathFinderThread.Instance.GetPath(_context.Self.entityId);
  //          if (path.path == null && !PathFinderThread.Instance.IsCalculatingPath(_context.Self.entityId))
                _context.Self.FindPath(_position, speed, true, null);
        }


        public static Vector3 AlignToEdge(Vector3 vector)
        {
            return new Vector3i(vector).ToVector3();
        }

        // allows the NPC to climb ladders
        public static Vector3 GetMoveToLocation(Context _context, Vector3 position, float maxDist = 10f)
        {
            var vector = _context.Self.world.FindSupportingBlockPos(position);
            if (!(maxDist > 0f)) return vector;

            var Targetblock = _context.Self.world.GetBlock(new Vector3i(vector));
            if (Targetblock.Block is BlockLadder)
                vector = SCoreUtils.AlignToEdge(vector);
            var vector2 = new Vector3(_context.Self.position.x, vector.y, _context.Self.position.z);

            var vector3 = vector - vector2;
            var magnitude = vector3.magnitude;

            if (magnitude > 5f)
                return vector;
            if (magnitude <= maxDist)
            {
                // When climbing ladders, align the vector to its edges to allow better ladder migration.
                var yDist = vector.y - _context.Self.position.y;
                if (yDist > 1.5f || yDist < -1.5f)
                {
                    return SCoreUtils.AlignToEdge(vector);
                }

                return vector2;
                //return vector.y - _context.Self.position.y > 1.5f ? vector : vector2;
            }
            else
            {
                vector3 *= maxDist / magnitude;
                var vector4 = vector - vector3;
                vector4.y += 0.51f;
                var pos = World.worldToBlockPos(vector4);
                var block = _context.Self.world.GetBlock(pos);
                var block2 = block.Block;

                if (block2.PathType <= 0)
                {
                    RaycastHit raycastHit;
                    if (Physics.Raycast(vector4 - Origin.position, Vector3.down, out raycastHit, 1.02f, 1082195968))
                    {
                        vector4.y = raycastHit.point.y + Origin.position.y;
                        return vector4;
                    }

                    if (block2.IsElevator((int)block.rotation))
                    {
                        vector4.y = vector.y;
                        return vector4;
                    }
                }
            }

            return SCoreUtils.AlignToEdge(vector);
        }

        public static bool CheckForClosedDoor(Context _context)
        {
            // If you are not blocked, don't bother processing.
            //  if (!(_context.Self.moveHelper.BlockedTime >= 0.1f)) return false;
            if (!_context.Self.moveHelper.IsBlocked) return false;

            // If they are not human, and are not hired, don't let them open doors.
            // This allows pets to open doors
            if (!EntityUtilities.IsHuman(_context.Self.entityId) || !EntityUtilities.IsHired(_context.Self.entityId)) return false;

            var blockPos = _context.Self.moveHelper.HitInfo.hit.blockPos;
            var block = GameManager.Instance.World.GetBlock(blockPos);

            if (!Block.list[block.type].HasTag(BlockTags.Door) || BlockDoor.IsDoorOpen(block.meta)) return false;

            var canOpenDoor = true;
            if (GameManager.Instance.World.GetTileEntity(0, blockPos) is TileEntitySecureDoor tileEntitySecureDoor)
            {
                if (tileEntitySecureDoor.IsLocked())
                {
                    canOpenDoor = false;
                    if (tileEntitySecureDoor.GetOwner() == PlatformManager.InternalLocalUserIdentifier)

                        canOpenDoor = false;
                }
                //if (tileEntitySecureDoor.IsLocked() && tileEntitySecureDoor.GetOwner() == "") // its locked, and you are not the owner.
                //                    canOpenDoor = false;
            }

            if (!canOpenDoor) return false;


            SphereCache.AddDoor(_context.Self.entityId, blockPos);
            EntityUtilities.OpenDoor(_context.Self.entityId, blockPos);
            Task task = Task.Delay(2000)
                .ContinueWith(t => CloseDoor(_context, blockPos));

            //  We were blocked, so let's clear it.
            _context.Self.moveHelper.ClearBlocked();
            return true;
        }

        public static void SetLookPosition(Context _context, object target)
        {
            var enemytarget = EntityUtilities.GetAttackOrRevengeTarget(_context.Self.entityId);
            if (enemytarget != null && enemytarget.IsDead())
            {
                _context.Self.SetAttackTarget(null, 30);
                _context.Self.SetRevengeTarget(null);
                return;
            }
            var entityAlive = UAIUtils.ConvertToEntityAlive(_context.ActionData.Target);
            if (entityAlive != null)
            {
                var headPosition = entityAlive.getHeadPosition();
                var forwardVector = _context.Self.GetForwardVector();
                _context.Self.RotateTo(entityAlive, 45f, 45);
                _context.Self.SetLookPosition(headPosition + forwardVector);
            }

            if (target is Vector3 vector)
            {
                _context.Self.RotateTo(vector.x, vector.y, vector.z, 45f, 45f);
                _context.Self.SetLookPosition(vector);
            }
        }
        public static void CloseDoor(Context _context, Vector3i doorPos)
        {
            EntityUtilities.CloseDoor(_context.Self.entityId, doorPos);
            SphereCache.RemoveDoor(_context.Self.entityId, doorPos);
        }

        public class NearestPathSorter : IComparer<Vector3>
        {
            public NearestPathSorter(Entity _self)
            {
                this.self = _self;
            }

            public int Compare(Vector3 _obj1, Vector3 _obj2)
            {
                var distanceSq = this.self.GetDistanceSq(_obj1);
                var distanceSq2 = this.self.GetDistanceSq(_obj2);
                if (distanceSq < distanceSq2)
                    return -1;

                if (distanceSq > distanceSq2)
                    return 1;

                return 0;
            }

            private Entity self;
        }

        public static void GetItemFromContainer(Context _context, TileEntityLootContainer tileLootContainer)
        {
            var blockPos = tileLootContainer.ToWorldPos();
            if (string.IsNullOrEmpty(tileLootContainer.lootListName))
                return;
            if (tileLootContainer.bTouched)
                return;

            tileLootContainer.bTouched = true;
            tileLootContainer.bWasTouched = true;

            // Nothing to loot.
            if (tileLootContainer.items == null) return;

            SCoreUtils.SetLookPosition(_context, blockPos);
            _context.Self.MinEventContext.TileEntity = tileLootContainer;
            _context.Self.FireEvent(MinEventTypes.onSelfOpenLootContainer);

            var lootContainer = LootContainer.GetLootContainer(tileLootContainer.lootListName);
            if (lootContainer == null)
                return;
            var gameStage = EffectManager.GetValue(PassiveEffects.LootGamestage, null, 10, _context.Self);
            var array = lootContainer.Spawn(_context.Self.rand, tileLootContainer.items.Length, gameStage, 0f, null, new FastTags());

            for (var i = 0; i < array.Count; i++)
                _context.Self.lootContainer.AddItem(array[i].Clone());

            _context.Self.FireEvent(MinEventTypes.onSelfLootContainer);
        }

        public static bool CheckContainer(Context _context, Vector3 _vector)
        {
            if (!_context.Self.onGround)
                return false;

            SCoreUtils.SetLookPosition(_context, _vector);

            var lookRay = new Ray(_context.Self.position, _context.Self.GetLookVector());
            if (!Voxel.Raycast(_context.Self.world, lookRay, 3f, false, false))
                return false; // Not seeing the target.

            if (!Voxel.voxelRayHitInfo.bHitValid)
                return false; // Missed the target. Overlooking?

            // Still too far away.
            var sqrMagnitude2 = (_vector - _context.Self.position).sqrMagnitude;
            if (sqrMagnitude2 > 1f)
                return false;

            var tileEntity = _context.Self.world.GetTileEntity(Voxel.voxelRayHitInfo.hit.clrIdx, new Vector3i(_vector));
            switch (tileEntity)
            {
                // if the TileEntity is a loot container, then loot it.
                case TileEntityLootContainer tileEntityLootContainer:

                    GetItemFromContainer(_context, tileEntityLootContainer);
                    break;
            }

            // Stop the move helper, so the entity does not slide around.
            EntityUtilities.Stop(_context.Self.entityId);
            return true;
        }

        /// <summary>
        /// <para>
        /// Store the attributes for a package.
        /// </para>
        /// 
        /// <para>
        /// The package name cannot be empty. If it is, the attributes will not be stored.
        /// </para>
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="element">The XML element with the attributes to store.</param>
        public static void StoreAttributes(
            UAIPackage package,
            XmlElement element)
        {
            var key = GetKey(package, null);
            if (key != null)
                _storedElements[key] = element;
        }

        /// <summary>
        /// <para>
        /// Store the attributes for an action associated with a package.
        /// </para>
        /// 
        /// <para>
        /// Neither the package name nor the action name can be empty. If either are empty, the
        /// attributes will not be stored.
        /// </para>
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="action">The action.</param>
        /// <param name="element">The XML element with the attributes to store.</param>
        public static void StoreAttributes(
            UAIPackage package,
            UAIAction action,
            XmlElement element)
        {
            var key = GetKey(package, action);
            if (key != null)
                _storedElements[key] = element;
        }

        public static IUAITargetFilter<Entity> GetEntityFilter(
            UAIPackage package,
            UAIAction action,
            EntityAlive self)
        {
            return GetTargetFilter<Entity>(package, action, self, EntityFilterAttribute);
        }

        public static IUAITargetFilter<Vector3> GetWaypointFilter(
            UAIPackage package,
            UAIAction action,
            EntityAlive self)
        {
            return GetTargetFilter<Vector3>(package, action, self, WaypointFilterAttrubute);
        }

        private static string GetKey(UAIPackage package, UAIAction action)
        {
            if (string.IsNullOrEmpty(package?.Name))
                return null;

            if (action == null)
            {
                return package.Name;
            }

            if (string.IsNullOrEmpty(action?.Name))
                return null;

            return $"{package?.Name}-{action?.Name}";
        }

        private static IUAITargetFilter<T> GetTargetFilter<T>(
            UAIPackage package,
            UAIAction action,
            EntityAlive self,
            string attribute)
        {
            var key = GetKey(package, action);
            if (key == null)
                return null;

            if (!_storedElements.TryGetValue(key, out var element))
                return null;

            if (!element.HasAttribute(attribute))
                return null;

            // This only works for the filter classes in SCore - do we need to worry about other
            // modders adding their own?
            var filterName = "UAI.UAIFilter" + element.GetAttribute(attribute);

            var type = Type.GetType(filterName);
            if (type == null)
                return null;

            return Activator.CreateInstance(type, new object[] { self }) as IUAITargetFilter<T>;
        }

        private static readonly string EntityFilterAttribute = "entity_filter";

        private static readonly string WaypointFilterAttrubute = "waypoint_filter";

        private static readonly Dictionary<string, XmlElement> _storedElements = new Dictionary<string, XmlElement>();

        public static int GetHitMaskByWeaponBuff(EntityAlive entity)
        {
            // Raycasts should always collide with these types of blocks.
            // This is 0x42, which is the "base" value used if you call the
            // Voxel.Raycast(World _worldData, Ray ray, float distance, bool bHitTransparentBlocks, bool bHitNotCollidableBlocks)
            // overload; the transparent and non-collidable values are "ORed" to that, as needed.
            int baseMask =
                (int)HitMasks.CollideMovement |
                (int)HitMasks.Liquid;

            // Check for specialized ranged weapons
            if (entity.Buffs.HasBuff("LBowUser") ||
                entity.Buffs.HasBuff("XBowUser"))
            {
                return baseMask | (int)HitMasks.CollideArrows;
            }

            if (entity.Buffs.HasBuff("RocketLUser"))
            {
                return baseMask | (int)HitMasks.CollideRockets;
            }

            // Otherwise, if it has the "ranged" tag, assume bullets
            if (entity.HasAnyTags(FastTags.Parse("ranged")))
            {
                return baseMask | (int)HitMasks.CollideBullets;
            }

            // Otherwise, assume melee
            return baseMask | (int)HitMasks.CollideMelee;
        }


        /// <summary>
        /// These hit mask values are taken verbatim from Voxel.raycastNew.
        /// The names represent whether a raycast should <em>collide with</em> the block.
        /// </summary>
        private enum HitMasks : int
        {
            Transparent = 1,
            Liquid = 2,
            NotCollidable = 4,
            CollideBullets = 8,
            CollideRockets = 0x10,
            CollideArrows = 0x20,
            CollideMovement = 0x40,
            CollideMelee = 0x80,
        }
    }
}