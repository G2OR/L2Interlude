﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Controller;
using Core.Module.Player.CharacterData.Response;
using Microsoft.Extensions.DependencyInjection;
using Network;

namespace Core.Module.Player.CharacterData.Request
{
    public class NewCharacter : PacketBase
    {
        private readonly GameServiceController _controller;
        public NewCharacter(IServiceProvider serviceProvider, Packet packet, GameServiceController controller) : base(serviceProvider)
        {
            _controller = controller;
        }

        public override async Task Execute()
        {
            await _controller.SendPacketAsync(new CharTemplates());
        }
    }
}