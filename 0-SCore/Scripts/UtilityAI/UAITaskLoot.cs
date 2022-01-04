﻿using GamePath;
using UnityEngine;

namespace UAI
{
    public class UAITaskLoot : UAITaskMoveToTarget
    {
        private static readonly string AdvFeatureClass = "AdvancedTroubleshootingFeatures";
        private static readonly string Feature = "UtilityAILoggingMin";

        private string _targetTypes;
        private Vector3 _vector;
        private string _buff;

        private bool hadBuff = false;
        private EntityAlive _leader;

        protected override void initializeParameters()
        {
            base.initializeParameters();
            if (Parameters.ContainsKey("TargetType")) _targetTypes = Parameters["TargetType"];
            if (Parameters.ContainsKey("buff")) _buff = Parameters["buff"];

        }

        public override void Stop(Context _context)
        {
            SphereCache.RemovePath(_context.Self.entityId, _vector);
            BlockUtilitiesSDX.removeParticles(new Vector3i(_vector));

            base.Stop(_context);
        }
        public override void Update(Context _context)
        {
            if (SCoreUtils.IsBlocked(_context))
                this.Stop(_context);

            if ( _leader)
                SCoreUtils.SetCrouching(_context, _leader.IsCrouching);

            var enemy = EntityUtilities.GetAttackOrRevengeTarget(_context.Self.entityId);
            if (enemy != null && enemy.IsAlive())
            {
                _context.Self.Buffs.RemoveBuff(_buff);
                Stop(_context);
                return;
            }

            if (SCoreUtils.CheckContainer(_context, _vector))
            {
                // If the NPC does not have the buff anymore, check to see if they ever had it for this task
                if (!_context.Self.Buffs.HasBuff(_buff))
                {
                    // We don't have the buff anymore, but we did before. This pathing is complete.
                    if ( hadBuff)
                    {
                        Stop(_context);
                        return;
                    }
                    _context.Self.Buffs.AddBuff(_buff);
                    hadBuff = true;
                }
                return;
            }

           base.Update(_context);
        }


        public override void Start(Context _context)
        {
            var paths = SphereCache.GetPaths(_context.Self.entityId);
            if (paths == null || paths.Count == 0)
            {
                paths = SCoreUtils.ScanForTileEntities(_context, _targetTypes);
                if (paths.Count == 0)
                {
                    Stop(_context);
                    return;
                }
                SphereCache.AddPaths(_context.Self.entityId, paths);
            }
            if (distance == 0)
                distance = 4f;

            // sort
            paths.Sort(new SCoreUtils.NearestPathSorter(_context.Self));
            _vector = paths[0];

            if (!GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled) )
                BlockUtilitiesSDX.addParticles("", new Vector3i(_vector));

            _leader = EntityUtilities.GetLeaderOrOwner(_context.Self.entityId) as EntityAlive;

            SCoreUtils.FindPath(_context, _vector, false);
            _context.ActionData.Started = true;
            _context.ActionData.Executing = true;
        }
    }
}