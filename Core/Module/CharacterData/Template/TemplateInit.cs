﻿using System;
using System.Collections.Generic;
using Core.Module.CharacterData.Template.Class;
using L2Logger;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Module.CharacterData.Template
{
    public class TemplateInit
    {
        private readonly IDictionary<byte, ITemplateHandler> _handlers;

        public TemplateInit(IServiceProvider serviceProvider)
        {
            var basicStatBonusInit = serviceProvider.GetRequiredService<PcParameterInit>();
            _handlers = new Dictionary<byte, ITemplateHandler>();
            RegisterTemplateHandler(new Fighter(basicStatBonusInit));
            RegisterTemplateHandler(new Mage(basicStatBonusInit));
            RegisterTemplateHandler(new ElvenFighter(basicStatBonusInit));
            RegisterTemplateHandler(new ElvenMage(basicStatBonusInit));
            RegisterTemplateHandler(new DarkFighter(basicStatBonusInit));
            RegisterTemplateHandler(new DarkMage(basicStatBonusInit));
            RegisterTemplateHandler(new OrcFighter(basicStatBonusInit));
            RegisterTemplateHandler(new OrcMage(basicStatBonusInit));
            RegisterTemplateHandler(new DwarvenFighter(basicStatBonusInit));
        }

        private void RegisterTemplateHandler(ITemplateHandler templateHandler)
        {
            byte classId = templateHandler.GetClassId();
            _handlers.Add(classId, templateHandler);
        }

        public ITemplateHandler GetTemplateByClassId(byte classId)
        {
            try
            {
                return _handlers[classId];
            }
            catch (Exception ex)
            {
                LoggerManager.Error("TemplateInit:" + ex.Message);
                throw;
            }
        }
    }
}