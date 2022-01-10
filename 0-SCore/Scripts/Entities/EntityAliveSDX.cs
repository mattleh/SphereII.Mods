/*
 * Class: EntityAliveSDX
 * Author:  sphereii 
 * Category: Entity
 * Description:
 *      This mod is an extension of the base entityAlive. This is meant to be a base class, where other classes can extend
 *      from, giving them the ability to accept quests and buffs.
 * 
 * Usage:
 *      Add the following class to entities that are meant to use these features. 
 *
 *      <property name="Class" value="EntityAliveSDX, SCore" />
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UAI;
using UnityEngine;
using UnityEngine.Serialization;

// ReSharper disable once CheckNamespace
public class EntityAliveSDX : EntityTrader
{
    public List<string> lstQuests = new List<string>();
    public bool isAlwaysAwake;

    [FormerlySerializedAs("PatrolCoordinates")]
    public List<Vector3> patrolCoordinates = new List<Vector3>();

    [FormerlySerializedAs("GuardPosition")]
    public Vector3 guardPosition = Vector3.zero;

    [FormerlySerializedAs("GuardLookPosition")]
    public Vector3 guardLookPosition = Vector3.zero;

    public float flEyeHeight = -1f;
    public bool bWentThroughDoor;

    public EntityAlive Owner;
    public bool isTeleporting = false;

    // if the NPC isn't available, don't return a loot. This disables the "Press <E> to search..."
    public override string GetLootList()
    {
        if (IsAvailable() == false)
            return "";

        return base.GetLootList();
    }
    // Check to see if the NPC is available
    public bool IsAvailable()
    {
        if (this.Buffs.HasCustomVar("onMission") && this.Buffs.GetCustomVar("onMission") == 1f)
            return false;
        return true;
    }

    public bool canJump = true;

    private readonly bool _blDisplayLog = false;

    // We use a tempList to store the patrol coordinates of each vector, but centered over the block. This allows us to check to make sure each
    // vector we are storing is on a new block, and not just  10.2 and 10.4. This helps smooth out the entity's walk. However, we do want accurate patrol points,
    // so we store the accurate patrol positions for the entity.
    private readonly List<Vector3> _tempList = new List<Vector3>();

    private int _defaultTraderID;
    private List<string> _startedThisFrame;

    // Default name
    private string _strMyName = "Bob";
    private string _strTitle;

    private TileEntityTrader _tileEntityTrader;
    private TraderArea _traderArea;

    public ItemValue meleeWeapon = ItemClass.GetItem("meleeClubIron");
    public QuestJournal questJournal = new QuestJournal();

    // This sets the entity's default scale, so when we re-scale it to make it disappear, everything
    // will still run and work, and we can re-set it.
    private Vector3 scale;

    public override string EntityName
    {
        get
        {
            // No configured name? return the default.
            if (_strMyName == "Bob")
                return entityName;

            // Don't return the name when on a mission.
            if (IsOnMission()) return "";

            if (string.IsNullOrEmpty(_strTitle))
                return Localization.Get(_strMyName);
            return Localization.Get(_strMyName) + " the " + Localization.Get(_strTitle);
        }
        set
        {
            if (value.Equals(entityName)) return;

            entityName = value;
            bPlayerStatsChanged |= !isEntityRemote;
        }
    }

    public void DisplayLog(string strMessage)
    {
        if (_blDisplayLog && !IsDead())
            Debug.Log(entityName + ": " + strMessage);
    }

    public override string ToString()
    {
        return entityName;
        //return EntityUtilities.DisplayEntityStats(entityId);
    }

    public override bool CanEntityJump()
    {
        return canJump;
    }


    public bool IsOnMission()
    {
        return this.Buffs.HasCustomVar("onMission") && this.Buffs.GetCustomVar("onMission") == 1f;
    }

    // SendOnMission will make the NPC disappear and be unavailable
    public void SendOnMission(bool send)
    {
        if (send)
        {
            var enemy = GetRevengeTarget();
            if (enemy != null)
            {
                SetAttackTarget(null, 0);
                enemy.SetAttackTarget(null, 0);
                enemy.SetRevengeTarget(null);
                enemy.DoRagdoll(new DamageResponse());
                SetRevengeTarget(null);
            }
            // Don't let anything target you
            isIgnoredByAI = true;

            // rescale to make it invisible.
            transform.localScale = new Vector3(0, 0, 0);
            emodel.SetVisible(false, false);
            Buffs.AddCustomVar("onMission", 1f);

            // Turn off the compass
            if (this.NavObject != null)
                this.NavObject.IsActive = false;
            // Clear the debug information, usually set from UAI
            this.DebugNameInfo = "";

            // Turn off the display component
            SetupDebugNameHUD(false);
        }
        else
        {
            transform.localScale = scale;

            emodel.SetVisible(true, true);
            Buffs.RemoveCustomVar("onMission");
            if (this.NavObject != null)
                this.NavObject.IsActive = true;
            isIgnoredByAI = false;
            SetupDebugNameHUD(true);
        }
    }

    public override float GetEyeHeight()
    {
        return flEyeHeight == -1f ? base.GetEyeHeight() : flEyeHeight;
    }

    public override void SetModelLayer(int _layerId, bool _force = false)
    {
        //Utils.SetLayerRecursively(this.emodel.GetModelTransform().gameObject, _layerId);
    }

    // Over-ride for CopyProperties to allow it to read in StartingQuests.
    public override void CopyPropertiesFromEntityClass()
    {
        base.CopyPropertiesFromEntityClass();
        var _entityClass = EntityClass.list[entityClass];

        flEyeHeight = EntityUtilities.GetFloatValue(entityId, "EyeHeight");

        // Read in a list of names then pick one at random.
        if (_entityClass.Properties.Values.ContainsKey("Names"))
        {
            var text = _entityClass.Properties.Values["Names"];
            var names = text.Split(',');

            var index = UnityEngine.Random.Range(0, names.Length);
            _strMyName = names[index];
        }

        if (_entityClass.Properties.Values.ContainsKey("SleeperInstantAwake"))
            isAlwaysAwake = true;

        if (_entityClass.Properties.Values.ContainsKey("Titles"))
        {
            var text = _entityClass.Properties.Values["Titles"];
            var names = text.Split(',');
            var index = UnityEngine.Random.Range(0, names.Length);
            _strTitle = names[index];
        }

        var component = gameObject.GetComponent<BoxCollider>();
        if (component)
        {
            DisplayLog(" Box Collider: " + component.size.ToCultureInvariantString());
            DisplayLog(" Current Boundary Box: " + boundingBox.ToCultureInvariantString());
        }

        if (_entityClass.Properties.Classes.ContainsKey("Boundary"))
        {
            DisplayLog(" Found Boundary Settings");
            var strBoundaryBox = "0,0,0";
            var strCenter = "0,0,0";
            var dynamicProperties3 = _entityClass.Properties.Classes["Boundary"];
            foreach (var keyValuePair in dynamicProperties3.Values.Dict.Dict)
            {
                DisplayLog("Key: " + keyValuePair.Key);
                switch (keyValuePair.Key)
                {
                    case "BoundaryBox":
                        DisplayLog(" Found a Boundary Box");
                        strBoundaryBox = dynamicProperties3.Values[keyValuePair.Key];
                        continue;
                    case "Center":
                        DisplayLog(" Found a Center");
                        strCenter = dynamicProperties3.Values[keyValuePair.Key];
                        break;
                }
            }

            var box = StringParsers.ParseVector3(strBoundaryBox);
            var center = StringParsers.ParseVector3(strCenter);
            ConfigureBoundaryBox(box, center);
        }
    }

    protected override float getNextStepSoundDistance()
    {
        return !IsRunning ? 0.5f : 0.25f;
    }

    /// <summary>
    ///     Overrides EntityAlive.OnAddedToWorld().
    ///     When entities are spawned into sleeper volumes, which happens in SleeperVolume.Spawn(),
    ///     several of their properties are set so they are spawned in a sleeping state.
    ///     If the NPC should always be awake, those properties can be reset here.
    /// </summary>
    public override void OnAddedToWorld()
    {
        if (isAlwaysAwake)
        {
            // Set the current order, defaults to "Wander"
            // EntityUtilities.SetCurrentOrder(entityId, EntityUtilities.GetCurrentOrder(entityId));

            // Set in EntityAlive.TriggerSleeperPose() - resetting here
            IsSleeping = false;
        }

        base.OnAddedToWorld();
    }

    public void ConfigureBoundaryBox(Vector3 newSize, Vector3 center)
    {
        var component = gameObject.GetComponent<BoxCollider>();
        if (!component) return;

        DisplayLog(" Box Collider: " + component.size.ToCultureInvariantString());
        DisplayLog(" Current Boundary Box: " + boundingBox.ToCultureInvariantString());
        // Re-adjusting the box collider     
        component.size = newSize;

        scaledExtent = new Vector3(component.size.x / 2f * transform.localScale.x, component.size.y / 2f * transform.localScale.y, component.size.z / 2f * transform.localScale.z);
        var vector = new Vector3(component.center.x * transform.localScale.x, component.center.y * transform.localScale.y, component.center.z * transform.localScale.z);
        boundingBox = BoundsUtils.BoundsForMinMax(-scaledExtent.x, -scaledExtent.y, -scaledExtent.z, scaledExtent.x, scaledExtent.y, scaledExtent.z);

        boundingBox.center = boundingBox.center + vector;

        if (center != Vector3.zero)
            boundingBox.center = center;

        DisplayLog(" After BoundaryBox: " + boundingBox.ToCultureInvariantString());
    }

    public void RestoreSpeed()
    {
        // Reset the movement speed when an attack target is set
        moveSpeed = EntityUtilities.GetFloatValue(entityId, "MoveSpeed");

        Vector2 vector;
        vector.x = moveSpeed;
        vector.y = moveSpeed;
        var _entityClass = EntityClass.list[entityClass];
        _entityClass.Properties.ParseVec(EntityClass.PropMoveSpeedAggro, ref vector);
        moveSpeedAggro = vector.x;
        moveSpeedAggroMax = vector.y;
    }

    public override EntityActivationCommand[] GetActivationCommands(Vector3i _tePos, EntityAlive _entityFocusing)
    {
        // Don't allow you to interact with it when its dead.
        if (IsDead() || NPCInfo == null) return new EntityActivationCommand[0];

        // Do they even like us enough to talk?
        if (EntityTargetingUtilities.IsEnemy(this, _entityFocusing)) return new EntityActivationCommand[0];

        // If not a human, don't talk to them
        if (!EntityUtilities.IsHuman(entityId)) return new EntityActivationCommand[0];

        // do we have an attack or revenge target? don't have time to talk, bro
        var target = EntityUtilities.GetAttackOrRevengeTarget(entityId);
        if (target != null && EntityTargetingUtilities.CanDamage(this, target)) return new EntityActivationCommand[0];

        return new[]
        {
            new EntityActivationCommand("Greet " + EntityName, "talk", true)
        };
    }


    public override bool OnEntityActivated(int _indexInBlockActivationCommands, Vector3i _tePos, EntityAlive _entityFocusing)
    {
        // Don't allow interaction with a Hated entity
        if (EntityTargetingUtilities.IsEnemy(this, _entityFocusing)) return false;

        if (EntityUtilities.GetAttackOrRevengeTarget(entityId) != null) return false;

        // Look at the entity that is talking to you.
        SetLookPosition(_entityFocusing.getHeadPosition());

        // This is used by various dialog boxes to know which EntityID the player is talking too.
        _entityFocusing.Buffs.SetCustomVar("CurrentNPC", entityId);
        Buffs.SetCustomVar("CurrentPlayer", _entityFocusing.entityId);

        // Copied from EntityTrader
        LocalPlayerUI uiforPlayer = LocalPlayerUI.GetUIForPlayer(_entityFocusing as EntityPlayerLocal);

        // We don't want the quest system to consider this NPC as interacted with
        //QuestEventManager.Current.NPCInteracted(this);

        Quest nextCompletedQuest = (_entityFocusing as EntityPlayerLocal).QuestJournal.GetNextCompletedQuest(null, this.entityId);
        // If the quest giver is not defined, don't let them close out the quest. We only want them to close out their own.

        if (nextCompletedQuest != null && nextCompletedQuest.QuestGiverID != entityId)
            nextCompletedQuest = null;

        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            this.activeQuests = QuestEventManager.Current.GetQuestList(GameManager.Instance.World, this.entityId, _entityFocusing.entityId);
            if (this.activeQuests == null)
            {
                this.activeQuests = this.PopulateActiveQuests(_entityFocusing as EntityPlayer, -1);
                QuestEventManager.Current.SetupQuestList(this.entityId, _entityFocusing.entityId, this.activeQuests);
            }
        }
        if (_indexInBlockActivationCommands != 0)
        {
            if (_indexInBlockActivationCommands == 1)
            {
                uiforPlayer.xui.Trader.TraderEntity = this;
                if (nextCompletedQuest == null)
                {
                    GameManager.Instance.TELockServer(0, _tePos, this.TileEntityTrader.entityId, _entityFocusing.entityId, null);
                }
                else
                {
                    if (nextCompletedQuest.QuestGiverID != -1)
                    {
                        QuestEventManager.Current.NPCInteracted(this);
                        uiforPlayer.xui.Dialog.QuestTurnIn = nextCompletedQuest;
                        uiforPlayer.windowManager.CloseAllOpenWindows(null, false);
                        uiforPlayer.xui.Trader.TraderEntity.PlayVoiceSetEntry("questcomplete", true, true);
                        uiforPlayer.windowManager.Open("questTurnIn", true, false, true);
                    }
                }
            }
        }
        else
        {
            uiforPlayer.xui.Dialog.Respondent = this;
            if (nextCompletedQuest == null)
            {
                uiforPlayer.windowManager.CloseAllOpenWindows(null, false);
                uiforPlayer.windowManager.Open("dialog", true, false, true);
                return false;
            }
            if (nextCompletedQuest != null && nextCompletedQuest.QuestGiverID != -1)
            {
                QuestEventManager.Current.NPCInteracted(this);
                uiforPlayer.xui.Dialog.QuestTurnIn = nextCompletedQuest;
                uiforPlayer.windowManager.CloseAllOpenWindows(null, false);
                uiforPlayer.xui.Dialog.Respondent.PlayVoiceSetEntry("questcomplete", true, true);
                uiforPlayer.windowManager.Open("questTurnIn", true, false, true);
            }
        }

        SetSpawnerSource(EnumSpawnerSource.StaticSpawner);

        return true;
    }


    public override bool CanBePushed()
    {
        return true;
    }


    public override void PostInit()
    {
        base.PostInit();

        SetupDebugNameHUD(true);

        // disable god mode, since that's enabled by default in the NPC
        IsGodMode.Value = false;

        if (NPCInfo != null)
            _defaultTraderID = NPCInfo.TraderID;

        // Check if there's a loot container or not already attached to store its stuff.
        DisplayLog(" Checking Entity's Loot Container");
        if (lootContainer == null && !string.IsNullOrEmpty(GetLootList()))
        {
            DisplayLog(" Entity does not have a loot container. Creating one.");
            lootContainer = new TileEntityLootContainer(null) { entityId = entityId };

            lootContainer.SetContainerSize(new Vector2i(8, 6));

            // If the loot list is available, set the container to that size.
            lootContainer.SetContainerSize(LootContainer.GetLootContainer(GetLootList()).size);
        }

        Buffs.SetCustomVar("$waterStaminaRegenAmount", 0, false);

        SetupStartingItems();
        inventory.SetHoldingItemIdx(0);

        // Does a quick local scan to see what pathing blocks, if any, are nearby. If one is found nearby, then it'll use that code for pathing.
        SetupAutoPathingBlocks();

        scale = transform.localScale;
    }

    public virtual void UpdatePatrolPoints(Vector3 position)
    {
        // Center the x and z values of the passed in blocks for a unique check.
        var temp = position;
        temp.x = 0.5f + Utils.Fastfloor(position.x);
        temp.z = 0.5f + Utils.Fastfloor(position.z);
        temp.y = Utils.Fastfloor(position.y);

        if (!_tempList.Contains(temp))
        {
            _tempList.Add(temp);
            if (!patrolCoordinates.Contains(position))
                patrolCoordinates.Add(position);
        }
    }

    // Reads the buff and quest information
    public override void Read(byte _version, BinaryReader _br)
    {
        base.Read(_version, _br);
        _strMyName = _br.ReadString();
        questJournal = new QuestJournal();
        questJournal.Read(_br);
        patrolCoordinates.Clear();
        var strPatrol = _br.ReadString();
        foreach (var strPatrolPoint in strPatrol.Split(';'))
        {
            var temp = ModGeneralUtilities.StringToVector3(strPatrolPoint);
            if (temp != Vector3.zero)
                patrolCoordinates.Add(temp);
        }

        var strGuardPosition = _br.ReadString();
        guardPosition = ModGeneralUtilities.StringToVector3(strGuardPosition);
        factionId = _br.ReadByte();
        guardLookPosition = ModGeneralUtilities.StringToVector3(_br.ReadString());
        try
        {
            Buffs.Read(_br);
        }
        catch (Exception)
        {
            // fail safe to protect game saves
        }

        Progression.Read(_br, this);
    }


    public void SetupAutoPathingBlocks()
    {
        if (Buffs.HasCustomVar("PathingCode") && (Buffs.GetCustomVar("PathingCode") < 0 || Buffs.GetCustomVar("PathingCode") > 0))
            return;

        // Check if pathing blocks are defined.
        var blocks = EntityUtilities.ConfigureEntityClass(entityId, "PathingBlocks");
        if (blocks.Count == 0)
            return;

        //Scan for the blocks in the area
        var pathingVectors = ModGeneralUtilities.ScanForTileEntityInChunksListHelper(position, blocks, entityId);
        if (pathingVectors == null || pathingVectors.Count == 0)
            return;


        // Find the nearest block, and if its a sign, read its code.
        var target = ModGeneralUtilities.FindNearestBlock(position, pathingVectors);
        var tileEntitySign = GameManager.Instance.World.GetTileEntity(0, new Vector3i(target)) as TileEntitySign;
        if (tileEntitySign == null) return;

        // Since signs can have multiple codes, splite with a ,, parse each one.
        var text = tileEntitySign.GetText();
        foreach (var temp in text.Split(','))
        {
            if (!StringParsers.TryParseFloat(temp, out var code)) continue;

            Buffs.AddCustomVar("PathingCode", code);
            return;
        }
    }

    // Saves the buff and quest information
    public override void Write(BinaryWriter _bw)
    {
        base.Write(_bw);
        _bw.Write(_strMyName);
        questJournal.Write(_bw);
        var strPatrolCoordinates = "";
        foreach (var temp in patrolCoordinates) strPatrolCoordinates += ";" + temp;

        _bw.Write(strPatrolCoordinates);
        _bw.Write(guardPosition.ToString());
        _bw.Write(factionId);
        _bw.Write(guardLookPosition.ToString());
        try
        {
            Buffs.Write(_bw);
        }
        catch (Exception)
        {
            // fail safe to protect game saves
        }

        Progression.Write(_bw);
    }

    public void GiveQuest(string strQuest)
    {
        // Don't give duplicate quests.
        foreach (var quest in questJournal.quests)
            if (quest.ID == strQuest.ToLower())
                return;

        // Make sure the quest is valid
        var newQuest = QuestClass.CreateQuest(strQuest);
        if (newQuest == null)
            return;

        // If there's no shared owner, it tries to read the PlayerLocal's entity ID. This entity doesn't have that.
        newQuest.SharedOwnerID = entityId;
        newQuest.QuestGiverID = -1;
        questJournal.AddQuest(newQuest);
    }

    protected override void UpdateJump()
    {
        if (this.walkType == 4 && !this.isSwimming)
        {
            base.FaceJumpTo();
            this.jumpState = EntityAlive.JumpState.Climb;
            if (!this.emodel.avatarController || !this.emodel.avatarController.IsAnimationJumpRunning())
            {
                this.Jumping = false;
            }
            if (this.jumpTicks == 0 && this.accumulatedRootMotion.y > 0.005f)
            {
                this.jumpTicks = 30;
            }
            return;
        }
        base.UpdateJump();
        if (this.isSwimming)
        {
            return;
        }
        this.accumulatedRootMotion.y = 0f;
    }
    public override void MoveEntityHeaded(Vector3 _direction, bool _isDirAbsolute)
    {
        // Check the state to see if the controller IsBusy or not. If it's not, then let it walk.
        var isBusy = false;
        if (emodel != null && emodel.avatarController != null)
            emodel.avatarController.TryGetBool("IsBusy", out isBusy);
        if (isBusy)
            return;

        if (this.walkType == 4 && this.Jumping)
        {
            this.motion = this.accumulatedRootMotion;
            this.accumulatedRootMotion = Vector3.zero;
            this.IsRotateToGroundFlat = true;
            if (this.moveHelper != null)
            {
                Vector3 vector = this.moveHelper.JumpToPos - this.position;
                if (Utils.FastAbs(vector.y) < 0.2f)
                {
                    this.motion.y = vector.y * 0.2f;
                }
                if (Utils.FastAbs(vector.x) < 0.3f)
                {
                    this.motion.x = vector.x * 0.2f;
                }
                if (Utils.FastAbs(vector.z) < 0.3f)
                {
                    this.motion.z = vector.z * 0.2f;
                }
                if (vector.sqrMagnitude < 0.010000001f)
                {
                    if (this.emodel && this.emodel.avatarController)
                    {
                        this.emodel.avatarController.StartAnimationJump(AnimJumpMode.Land);
                    }
                    this.Jumping = false;
                }
            }
            this.entityCollision(this.motion);
            return;
        }
        base.MoveEntityHeaded(_direction, _isDirAbsolute);
    }

      protected override void HandleNavObject()
    {
        if (EntityClass.list[this.entityClass].NavObject != "")
        {
            if (this.LocalPlayerIsOwner() && this.Owner != null)
            {
                this.NavObject = NavObjectManager.Instance.RegisterNavObject("ally", this.emodel.GetModelTransform(), "");
                this.NavObject.UseOverrideColor = true;
                this.NavObject.OverrideColor = Color.cyan;
                this.NavObject.DisplayName = EntityName;

                return;
            }
            if (this.NavObject != null)
            {
                NavObjectManager.Instance.UnRegisterNavObject(this.NavObject);
                this.NavObject = null;
            }
        }
    }

    public bool LocalPlayerIsOwner()
    {
        var leader = EntityUtilities.GetLeaderOrOwner(entityId);
        if (leader != null)
        {
            if (GameManager.Instance.World.IsLocalPlayer(leader.entityId))
                return true;
        }
        return false;
    }


    public override void OnUpdateLive()
    {
        if (IsDead())
        {
            SetupDebugNameHUD(false);
        }
        // If we don't have a leader, make sure that we don't have a nav object set up.
        var leader = EntityUtilities.GetLeaderOrOwner(entityId);
        if (leader == null)
        {
            Owner = null;
            HandleNavObject();
            SetupDebugNameHUD(false);

        }

        if (leader)
        {
            var distanceToLeader = GetDistance(leader);
            if (distanceToLeader > 60 && distanceToLeader < 5)
            {
                TeleportToPlayer(leader as EntityAlive);
                // If we teleported, we want to clear those paths so they can be re-aquired.
                SphereCache.RemovePaths(entityId);
            }

            // This needs to be set for the entities to be still alive, so the player can teleport them
            IsEntityUpdatedInUnloadedChunk = true;
            bWillRespawn = true; // this needs to be off for entities to despawn after being killed. Handled via SetDead()

            SetupDebugNameHUD(true);
            // If they are ordered to stay. don't disappear and re-appear.
            if (Buffs.HasCustomVar("CurrentOrder") && Buffs.GetCustomVar("CurrentOrder") != 2)
            {
                // if our leader is attached, that means they are attached to a vehicle
                if (leader.AttachedToEntity != null)
                {
                    SendOnMission(true);
                }
                else
                {
                    // No longer attached to the vehicle, but still has the cvar? return the NPC to the player.
                    if (Buffs.HasCustomVar("onMission"))
                        SendOnMission(false);
                }
            }
        }
        // However, if we don't have an owner, but a leader, this means we've been hired.
        if (Owner == null && leader != null)
        {
            Owner = leader as EntityAlive;
            this.Owner.AddOwnedEntity(this);

            if (GameManager.Instance.World.IsLocalPlayer(leader.entityId))
            {
                this.HandleNavObject();
            }
        }

        SetupAutoPathingBlocks();

        if (Buffs.HasCustomVar("PathingCode") && Buffs.GetCustomVar("PathingCode") == -1)
            EntityUtilities.SetCurrentOrder(entityId, EntityUtilities.Orders.Stay);

        // Wake them up if they are sleeping, since the trigger sleeper makes them go idle again.
        if (!sleepingOrWakingUp && isAlwaysAwake)
        {
            IsSleeping = true;
            ConditionalTriggerSleeperWakeUp();
        }

        Buffs.RemoveBuff("buffnewbiecoat", false);
        Stats.Health.MaxModifier = Stats.Health.Max;

        // Set CanFall and IsOnGround
        if (emodel != null && emodel.avatarController != null)
        {
            emodel.avatarController.SetBool("CanFall", !emodel.IsRagdollActive && bodyDamage.CurrentStun == EnumEntityStunType.None && !isSwimming);
            emodel.avatarController.SetBool("IsOnGround", onGround || isSwimming);
        }

        base.OnUpdateLive();

        // Allow EntityAliveSDX to get buffs from blocks
        if (!isEntityRemote && !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            UpdateBlockRadiusEffects();

        // No NPC info, don't continue
        if (NPCInfo == null)
            return;

        // If the Tile Entity Trader isn't set, set it now. Sometimes this fails, and won't allow interaction.
        if (_tileEntityTrader == null)
        {
            _tileEntityTrader = new TileEntityTrader(null);
            _tileEntityTrader.entityId = entityId;
            _tileEntityTrader.TraderData.TraderID = NPCInfo.TraderID;
        }

        if (!this.isEntityRemote)
        {
            if (this.emodel)
            {
                AvatarController avatarController = this.emodel.avatarController;
                if (avatarController)
                {
                    var flag = this.onGround || this.isSwimming || this.bInElevator;
                    if (flag)
                    {
                        this.fallTime = 0f;
                        this.fallThresholdTime = 0f;
                        if (this.bInElevator)
                        {
                            this.fallThresholdTime = 0.6f;
                        }
                    }
                    else
                    {
                        if (this.fallThresholdTime == 0f)
                        {
                            this.fallThresholdTime = 0.1f + this.rand.RandomFloat * 0.3f;
                        }
                        this.fallTime += 0.05f;
                    }
                    var canFall = !this.emodel.IsRagdollActive && this.bodyDamage.CurrentStun == EnumEntityStunType.None && !this.isSwimming && !this.bInElevator && this.jumpState == EntityAlive.JumpState.Off && !this.IsDead();
                    if (this.fallTime <= this.fallThresholdTime)
                    {
                        canFall = false;
                    }
                    avatarController?.SetFallAndGround(canFall, flag);
                }
            }


        }

        //var entitiesInBounds = GameManager.Instance.World.GetEntitiesInBounds(this, new Bounds(position, Vector3.one * 5f));
        //if (entitiesInBounds.Count > 0)
        //{
        //    foreach (var entity in entitiesInBounds)
        //    {
        //        if (entity is EntityPlayerLocal || entity is EntityPlayer)
        //        {
        //            // Check your faction relation. If you hate each other, don't stop and talk.
        //            var myRelationship = FactionManager.Instance.GetRelationshipTier(this, entity as EntityPlayer);
        //            if (myRelationship == FactionManager.Relationship.Hate)
        //                break;

        //            var player = entity as EntityPlayer;
        //            if (player && player.IsSpectator)
        //                    continue;

        //            if (GetDistance(player) < 1.5 && moveHelper != null)
        //            { 
        //                moveHelper.SetMoveTo(player.GetLookVector(), false);
        //                break;
        //            }

        //            // Turn to face the player, and stop the movement.
        //            SetLookPosition(entity.getHeadPosition());
        //            RotateTo(entity, 90f, 90f);
        //            EntityUtilities.Stop(entityId);
        //            break;
        //        }
        //    }
        //}
    }
    private float fallTime;


    private float fallThresholdTime;

    public override Ray GetLookRay()
    {
        return new Ray(position + new Vector3(0f, GetEyeHeight() * eyeHeightHackMod, 0f), GetLookVector());
    }

    public void ToggleTraderID(bool Restore)
    {
        if (NPCInfo == null)
            return;

        // Check if we are restoring the default trader ID.
        if (Restore)
            NPCInfo.TraderID = _defaultTraderID;
        else
            NPCInfo.TraderID = 0;
    }

    public override int DamageEntity(DamageSource _damageSource, int _strength, bool _criticalHit, float _impulseScale)
    {
        if (EntityUtilities.GetBoolValue(entityId, "Invulnerable"))
            return 0;

        if (Buffs.HasBuff("buffInvulnerable"))
            return 0;

        // If we are being attacked, let the state machine know it can fight back
        if (!EntityTargetingUtilities.CanTakeDamage(this, world.GetEntity(_damageSource.getEntityId())))
            return 0;

        emodel.avatarController.SetBool("IsBusy", false);

        // Turn off the trader ID while it deals damage to the entity
        ToggleTraderID(false);
        var damage = base.DamageEntity(_damageSource, _strength, _criticalHit, _impulseScale);
        ToggleTraderID(true);
        return damage;
    }


    public new void SetRevengeTarget(EntityAlive _other)
    {
        if (IsOnMission())
            return;

        if (_other)
        {
            // Forgive friendly fire, even from explosions.
            var myLeader = EntityUtilities.GetLeaderOrOwner(entityId) as EntityAlive;
            if (myLeader)
                if (myLeader.entityId == _other.entityId)
                    return;
            if (EntityUtilities.IsAnAlly(entityId, _other.entityId))
                return;

            if (_other.IsDead())
                return;
        }

        base.SetRevengeTarget(_other);
        //  Debug.Log("Adding Buff for RevengeTarget() ");
        Buffs.AddBuff("buffNotifyTeamAttack");
    }

    public override void SetDead()
    {
        var leader = EntityUtilities.GetLeaderOrOwner(entityId) as EntityPlayerLocal;
        if ( leader )
        {
                GameManager.ShowTooltip(primaryPlayer, $"Oh no! {EntityName} has died. :(");

        }
        bWillRespawn = false;
        if (this.NavObject != null)
        {
            NavObjectManager.Instance.UnRegisterNavObject(this.NavObject);
            this.NavObject = null;
        }
        SetupDebugNameHUD(false);
        base.SetDead();
    }
    public new void SetAttackTarget(EntityAlive _attackTarget, int _attackTargetTime)
    {
        if (_attackTarget != null)
            if (_attackTarget.IsDead())
                return;

        if (IsOnMission())
            return;

        if (_attackTarget == null)
            // Some of the AI tasks resets the attack target when it falls down stunned; this will prevent the NPC from ignoring its stunned opponent.
            if (attackTarget != null && attackTarget.IsAlive())
                return;

        base.SetAttackTarget(_attackTarget, _attackTargetTime);
        // Debug.Log("Adding Buff for Attack Target() ");
        Buffs.AddBuff("buffNotifyTeamAttack");
    }

    public override bool CanDamageEntity(int _sourceEntityId)
    {
        return EntityTargetingUtilities.CanTakeDamage(this, world.GetEntity(_sourceEntityId));
    }


    public void TeleportToPlayer(EntityAlive target)
    {
        if (target == null) return;

        var target2i = new Vector2(target.position.x, target.position.z);
        var mine2i = new Vector2(position.x, position.z);
        var distance = Vector2.Distance(target2i, mine2i);
        //var distance = GetDistance(target);
        if (distance < 20) return;

        if (isTeleporting) return;
        var myPosition = RandomPositionGenerator.CalcAway(target, 10, 20,2, target.position);

        // Find the ground.
        myPosition.y = (int)GameManager.Instance.World.GetHeightAt(myPosition.x, myPosition.z) +1;

        motion = Vector3.zero;
        navigator.clearPath();
        this.SetPosition(myPosition, true);
        StartCoroutine(validateTeleport());
    }
    private float getAltitude(Vector3 pos)
    {
        RaycastHit raycastHit;
        if (Physics.Raycast(pos - Origin.position, Vector3.down, out raycastHit, 1000f, 65536))
        {
            return raycastHit.distance;
        }
        return -1f;
    }
    private IEnumerator validateTeleport()
    {
        yield return new WaitForSeconds(1f);
        if (getAltitude(position) <= 0)
        {
            var myposition = RandomPositionGenerator.CalcAway(Owner, 10, 20, 5, Owner.position);

            // Find the ground.
            myposition.y = (int)GameManager.Instance.World.GetHeightAt(position.x, position.z) + 1;
            motion = Vector3.zero;
            navigator.clearPath();
            this.SetPosition(myposition, true);
        }
        this.isTeleporting = false;
        yield return null;
        yield break;
    }

    public override void ProcessDamageResponseLocal(DamageResponse _dmResponse)
    {
        if (EntityUtilities.GetBoolValue(entityId, "Invulnerable"))
            return;

        if (Buffs.HasBuff("buffInvulnerable"))
            return;

        if (!isEntityRemote)
        {
            // If we are being attacked, let the state machine know it can fight back
            emodel.avatarController.SetBool("IsBusy", false);

            // Turn off the trader ID while it deals damage to the entity
            ToggleTraderID(false);
            base.ProcessDamageResponseLocal(_dmResponse);
            ToggleTraderID(true);
        }
    }


    public override void MarkToUnload()
    {


        // Something asked us to despawn. Check if we are in a trader area. If we are, ignore the request.
        if (_traderArea == null)
            _traderArea = world.GetTraderAreaAt(new Vector3i(position));

        if (_traderArea != null)
        {
            IsDespawned = false;
            return;
        }

        base.MarkToUnload();
    }

    private void UpdateBlockRadiusEffects()
    {
        var blockPosition = GetBlockPosition();
        var num = World.toChunkXZ(blockPosition.x);
        var num2 = World.toChunkXZ(blockPosition.z);
        _startedThisFrame = new List<string>();
        for (var i = -1; i < 2; i++)
            for (var j = -1; j < 2; j++)
            {
                var chunk = (Chunk)world.GetChunkSync(num + j, num2 + i);
                if (chunk == null) continue;

                var tileEntities = chunk.GetTileEntities();
                for (var k = 0; k < tileEntities.list.Count; k++)
                {
                    var tileEntity = tileEntities.list[k];

                    if (!tileEntity.IsActive(world)) continue;

                    var block = world.GetBlock(tileEntity.ToWorldPos());
                    var block2 = Block.list[block.type];
                    if (block2.RadiusEffects == null) continue;


                    var distanceSq = GetDistanceSq(tileEntity.ToWorldPos().ToVector3());
                    for (var l = 0; l < block2.RadiusEffects.Length; l++)
                    {
                        var blockRadiusEffect = block2.RadiusEffects[l];
                        if (distanceSq <= blockRadiusEffect.radius * blockRadiusEffect.radius && !Buffs.HasBuff(blockRadiusEffect.variable))
                            Buffs.AddBuff(blockRadiusEffect.variable);
                    }
                }
            }
    }

    public override float GetMoveSpeed()
    {
        var speed = EffectManager.GetValue(PassiveEffects.WalkSpeed, null, this.moveSpeed, this, null, default(FastTags), true, true, true, true, 1, true);
        if (IsCrouching)
            speed = EffectManager.GetValue(PassiveEffects.CrouchSpeed, null, this.moveSpeed, this, null, default(FastTags), true, true, true, true, 1, true);

        return speed;
    }

    public override float GetMoveSpeedAggro()
    {
        var speed = EffectManager.GetValue(PassiveEffects.RunSpeed, null, this.moveSpeedPanic, this, null, default(FastTags), true, true, true, true, 1, true);
        return speed;
    }
    public new float GetMoveSpeedPanic()
    {
        var speed = EffectManager.GetValue(PassiveEffects.RunSpeed, null, this.moveSpeedPanic, this, null, default(FastTags), true, true, true, true, 1, true);
        return speed;
    }


    //protected override void updateSpeedForwardAndStrafe(Vector3 _dist, float _partialTicks)
    //{
    //    if (isEntityRemote && _partialTicks > 1f) _dist /= _partialTicks;
    //    speedForward *= 0.5f;
    //    speedStrafe *= 0.5f;
    //    speedVertical *= 0.5f;
    //    if (Mathf.Abs(_dist.x) > 0.001f || Mathf.Abs(_dist.z) > 0.001f)
    //    {
    //        var num = Mathf.Sin(-rotation.y * 3.14159274f / 180f);
    //        var num2 = Mathf.Cos(-rotation.y * 3.14159274f / 180f);
    //        speedForward += num2 * _dist.z - num * _dist.x;
    //        speedStrafe += num2 * _dist.x + num * _dist.z;
    //    }

    //    if (Mathf.Abs(_dist.y) > 0.001f) speedVertical += _dist.y;

    //    SetMovementState();
    //}


}