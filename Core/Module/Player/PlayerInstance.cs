﻿using System;
using System.Threading.Tasks;
using Core.Controller;
using Core.Module.CharacterData;
using Core.Module.CharacterData.Template;
using Core.Module.NpcData;
using Core.Module.WorldData;
using Core.NetworkPacket.ServerPacket;
using Core.NetworkPacket.ServerPacket.CharacterPacket;
using DataBase.Interfaces;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Network;

namespace Core.Module.Player
{
    public sealed class PlayerInstance : Character
    {
        private readonly PlayerAppearance _playerAppearance;
        private readonly PlayerCharacterInfo _playerCharacterInfo;
        private readonly PlayerModel _playerModel;
        private readonly ITemplateHandler _templateHandler;
        private static PlayerLoader _playerLoader;
        private readonly PlayerMoveToLocation _toLocation;
        private readonly PlayerDesire _playerDesire;
        private readonly PlayerStatus _playerStatus;
        private readonly PlayerCombat _playerCombat;
        private readonly PlayerInventory _playerInventory;
        private readonly PlayerUseItem _playerUseItem;
        private readonly PlayerSkill _playerSkill;
        private readonly PlayerEffect _playerEffect;
        private readonly PlayerSkillMagic _playerSkillMagic;
        private readonly PlayerMessage _playerMessage;
        private readonly PlayerZone _playerZone;
        private readonly PlayerTargetAction _playerTargetAction;
        private readonly PlayerKnownList _playerKnownList;
        private readonly PlayerAction _playerAction;
            
        public Location Location { get; set; }
        public IServiceProvider ServiceProvider { get; }
        public NpcInstance LastTalkedNpc { get; set; }
        public GameServiceController Controller { get; set; }
        private readonly IUnitOfWork _unitOfWork;
        private readonly WorldInit _worldInit;
        public PlayerInstance(ITemplateHandler template, PlayerAppearance playerAppearance, IServiceProvider provider, IUnitOfWork unitOfWork)
        {
            ServiceProvider = provider;
            _templateHandler = template;
            _playerAppearance = playerAppearance;
            _unitOfWork = unitOfWork;
            _playerModel = new PlayerModel(this);
            _playerAction = new PlayerAction(this);
            _playerCharacterInfo = new PlayerCharacterInfo(this);
            _toLocation = new PlayerMoveToLocation(this);
            _playerDesire = new PlayerDesire(this);
            _playerStatus = new PlayerStatus(this);
            _playerCombat = new PlayerCombat(this);
            _playerInventory = new PlayerInventory(this);
            _playerUseItem = new PlayerUseItem(this);
            _playerSkill = new PlayerSkill(this);
            _playerEffect = new PlayerEffect(this);
            _playerSkillMagic = new PlayerSkillMagic(this);
            _playerMessage = new PlayerMessage(this);
            _playerZone = new PlayerZone(this);
            _playerTargetAction = new PlayerTargetAction(this);
            _playerKnownList = new PlayerKnownList(this);

            _worldInit = provider.GetRequiredService<WorldInit>();
        }

        public IUnitOfWork GetUnitOfWork() => _unitOfWork;

        public ITemplateHandler TemplateHandler() => _templateHandler;
        public PlayerAppearance PlayerAppearance() => _playerAppearance;
        public PlayerModel PlayerModel() => _playerModel;
        public PlayerCharacterInfo PlayerCharacterInfo() => _playerCharacterInfo;
        public PlayerDesire PlayerDesire() => _playerDesire;
        public PlayerStatus PlayerStatus() => _playerStatus;
        public PlayerCombat PlayerCombat() => _playerCombat;
        public PlayerInventory PlayerInventory() => _playerInventory;
        public PlayerUseItem PlayerUseItem() => _playerUseItem;
        public PlayerSkill PlayerSkill() => _playerSkill;
        //public PlayerEffect PlayerEffect() => _playerEffect;
        public PlayerSkillMagic PlayerSkillMagic() => _playerSkillMagic;
        public PlayerMessage PlayerMessage() => _playerMessage;
        public PlayerZone PlayerZone() => _playerZone;
        internal PlayerTargetAction PlayerTargetAction() => _playerTargetAction;
        public PlayerAction PlayerAction() => _playerAction;
        public override ICharacterCombat CharacterCombat() => _playerCombat;
        public override ICharacterKnownList CharacterKnownList() => _playerKnownList;

        private static PlayerLoader PlayerLoader(IServiceProvider serviceProvider)
        {
            return _playerLoader ??= new PlayerLoader(serviceProvider);
        }

        public static Task<PlayerInstance> Load(int objectId, IServiceProvider serviceProvider)
        {
            return PlayerLoader(serviceProvider).Load(objectId);
        }
        
        public Task SendPacketAsync(ServerPacket serverPacket)
        {
            if (Controller is null)
                return Task.CompletedTask;
            return Controller.SendPacketAsync(serverPacket);
        }
        
        public async Task SendActionFailedPacketAsync()
        {
            await Controller.SendPacketAsync(new ActionFailed());
        }
        
        public async Task SendUserInfoAsync()
        {
            if (Controller is null) return;
            await Controller.SendPacketAsync(new UserInfo(this));
            //Broadcast.toKnownPlayers(this, new CharInfo(this)); TODO
        }
        
        public PlayerMoveToLocation PlayerLocation()
        {
            return _toLocation;
        }
        
        public async Task FindCloseNpc()
        {
            foreach (NpcInstance npcInstance in _worldInit.GetVisibleNpc(this))
            {
                if (!CalculateRange.CheckIfInRange(2000, npcInstance.GetX(), npcInstance.GetY(),
                        npcInstance.GetZ(), 20,
                        GetX(), GetY(), GetZ(), 20, true))
                {
                    continue;
                }
                if (CharacterKnownList().HasObjectInKnownList(npcInstance.ObjectId))
                {
                    continue;
                }
                var npcServerRequest = new NpcServerRequest
                {
                    EventName = EventName.Created,
                    NpcName = npcInstance.GetTemplate().GetStat().Name,
                    NpcType = npcInstance.GetTemplate().GetStat().Type,
                    NpcObjectId = npcInstance.ObjectId,
                    IsActiveNpc = true
                };
                await SendObjectToNpcServerAsync(npcServerRequest);
                CharacterKnownList().AddToKnownList(npcInstance.ObjectId, npcInstance);
                npcInstance.CharacterKnownList().AddToKnownList(ObjectId, this);
                await SendPacketAsync(new NpcInfo(npcInstance));
            }
        }

        public async Task SendObjectToNpcServerAsync(NpcServerRequest npcServerRequest)
        {
            await ServiceProvider.GetRequiredService<NpcServiceController>()
                    .SendMessageToNpcService(npcServerRequest);
        }


        public override async Task SendToKnownPlayers(ServerPacket packet)
        {
            foreach (var (objectId, worldObject) in CharacterKnownList().GetKnownObjects())
            {
                if (worldObject is PlayerInstance targetInstance)
                {
                    await targetInstance.SendPacketAsync(packet);
                    await SendPacketAsync(new CharInfo(targetInstance));
                }
            }
        }

        public async Task DeleteMeAsync()
        {
            if (GetWorldRegion() != null)
            {
                GetWorldRegion().RemoveFromZones(this);
            }
            await PlayerTargetAction().RemoveTargetAsync();
            //_worldInit.RemoveObject(this);
            _worldInit.RemoveFromAllPlayers(this);
            _worldInit.RemoveVisibleObject(this, WorldObjectPosition().GetWorldRegion());
            WorldObjectPosition().SetWorldRegion(null);
            
            CharacterKnownList().RemoveMeFromKnownObjects();
            CharacterKnownList().RemoveAllKnownObjects();
        }

        public override int GetMaxHp()
        {
            return _playerStatus.GetMaxHp();
        }

        public override int GetMagicalAttack()
        {
            return _playerCombat.GetMagicalAttack();
        }

        public override int GetMagicalDefence()
        {
            return _playerCombat.GetMagicalDefence();
        }

        public override int GetPhysicalDefence()
        {
            return _playerCombat.GetPhysicalDefence();
        }

        private Task<bool> IsTargetSelected(PlayerInstance playerInstance)
        {
            return Task.FromResult(this == playerInstance.PlayerTargetAction().GetTarget());
        }

        public override async Task RequestActionAsync(PlayerInstance playerInstance)
        {
            if (!await IsTargetSelected(playerInstance))
            {
                await base.RequestActionAsync(playerInstance);
                // Set the target of the PlayerInstance player
                await playerInstance.SendPacketAsync(new MyTargetSelected(this.ObjectId, 0));
                return;
            }
            playerInstance.PlayerDesire().AddDesire(Desire.InteractDesire, this);
        }

        public override void SpawnMe(int x, int y, int z)
        {
            base.SpawnMe(x, y, z);
            CharacterMovement().SetRunning();
            StorePlayerObject();
        }
        private void StorePlayerObject()
        {
            Initializer.WorldInit().StorePlayerObject(this);
        }
    }
}