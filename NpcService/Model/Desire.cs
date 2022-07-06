﻿using Helpers;

namespace NpcService.Model
{
    public class Desire
    {
        private readonly int _npcObjectId;
        private readonly int _playerObjectId;
        private readonly ProcessBusinessLogic _blLogic;
        private readonly NpcService _npcService;
        private readonly PriorityQueue<DesireObject> _priorityDesire;
        private readonly NpcCreature _npcCreature;

        public Desire(NpcCreature npcCreature, int playerObjectId, NpcService npcNpcService)
        {
            _priorityDesire = new PriorityQueue<DesireObject>();
            _npcService = npcNpcService;
            _playerObjectId = playerObjectId;
            _npcObjectId = npcCreature.NpcObjectId;
            _npcCreature = npcCreature;
            _blLogic = new ProcessBusinessLogic();
            _blLogic.ProcessCompleted += bl_ProcessCompleted; // register with an event
        }

        public void AddEffectActionDesire(NpcCreature sm, int actionId, int moveAround, int desire)
        {
            var npcDesire = new NpcDesire
            {
                ActionDesire = ActionDesire.AddEffectActionDesire,
                ObjectId = _npcObjectId,
                ActionId = actionId,
            };
            _priorityDesire.Enqueue(new DesireObject(desire, ActionDesire.AddEffectActionDesire, npcDesire));
            _blLogic.StartProcess();
        }

        public void AddMoveAroundDesire(int moveAround, int desire)
        {
            var npcDesire = new NpcDesire
            {
                ActionDesire = ActionDesire.AddMoveAroundDesire,
                ObjectId = _npcObjectId,
            };
            _priorityDesire.Enqueue(new DesireObject(desire, ActionDesire.AddEffectActionDesire, npcDesire));
            _blLogic.StartProcess();
        }

        public NpcDesire GetDesire()
        {
            return _priorityDesire.Dequeue().NpcDesire;
        }
        
        // event handler
        public async void bl_ProcessCompleted(object sender, ProcessEventArgs e)
        {
            var desire = GetDesire();
            _npcCreature.SetDesire(desire.ActionDesire);
            NpcServerResponse npcServiceResponse;
            switch (desire.ActionDesire)
            {
                case ActionDesire.AddEffectActionDesire:
                    npcServiceResponse = new NpcServerResponse
                    {
                        EventName = EventName.EffectActionDesire,
                        NpcObjectId = desire.ObjectId,
                        SocialId = desire.ActionId,
                        PlayerObjectId = desire.PlayerObjectId,
                    };
                    await _npcService.SendMessageAsync(npcServiceResponse);
                    break;
                case ActionDesire.AddMoveAroundDesire:
                    npcServiceResponse = new NpcServerResponse
                    {
                        EventName = EventName.AddMoveAroundDesire,
                        NpcObjectId = desire.ObjectId,
                    };
                    await _npcService.SendMessageAsync(npcServiceResponse);
                    break;
                case ActionDesire.AddUseSkillDesire:
                    npcServiceResponse = new NpcServerResponse
                    {
                        EventName = EventName.AddUseSkillDesire,
                        PchSkillId = desire.PchSkillId,
                        NpcObjectId = desire.ObjectId,
                        PlayerObjectId = desire.PlayerObjectId,
                    };
                    await _npcService.SendMessageAsync(npcServiceResponse);
                    break;
                case ActionDesire.AddAttackDesire:
                    npcServiceResponse = new NpcServerResponse
                    {
                        EventName = EventName.AddAttackDesire,
                        NpcObjectId = desire.ObjectId,
                        PlayerObjectId = desire.PlayerObjectId,
                    };
                    await _npcService.SendMessageAsync(npcServiceResponse);
                    break;
            }
            //Console.WriteLine("Process " + (e.IsSuccessful? "Completed Successfully": "failed"));
            //Console.WriteLine("Completion Time: " + e.CompletionTime.ToLongDateString());
        }

        public void AddUseSkillDesire(Talker talker, int pchSkillId, int skillClassification, int castingMethod, int desire)
        {
            var npcDesire = new NpcDesire
            {
                ActionDesire = ActionDesire.AddUseSkillDesire,
                PchSkillId = pchSkillId,
                ObjectId = _npcObjectId,
                PlayerObjectId = _playerObjectId
            };
            _priorityDesire.Enqueue(new DesireObject(desire, ActionDesire.AddUseSkillDesire, npcDesire));
            _blLogic.StartProcess();
        }

        public void AddAttackDesire(Talker attacker, int actionId, int desire)
        {
            var npcDesire = new NpcDesire
            {
                ActionDesire = ActionDesire.AddAttackDesire,
                ObjectId = _npcObjectId,
                ActionId = actionId,
                PlayerObjectId = _playerObjectId
            };
            _priorityDesire.Enqueue(new DesireObject(desire, ActionDesire.AddAttackDesire, npcDesire));
            _blLogic.StartProcess();
        }
    }
}