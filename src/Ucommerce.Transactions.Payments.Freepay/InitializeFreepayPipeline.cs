using NHibernate.Event;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Impl;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;
using Ucommerce.Pipelines;
using Ucommerce.Pipelines.Initialization;

namespace Ucommerce.Transactions.Payments.Freepay
{
    public class InitializeFreepayPipeline : IPipelineTask<InitializeArgs>
    {
        private readonly IRepository<Definition> _definitionRepository;
        private readonly IRepository<DefinitionType> _definitionTypeRepository;
        private readonly IRepository<DataType> _dataTypeRepository;

        public InitializeFreepayPipeline(IRepository<Definition> definitionRepository,
            IRepository<DefinitionType> definitionTypeRepository,
            IRepository<DataType> dataTypeRepository)
        {
            _definitionRepository = definitionRepository;
            _definitionTypeRepository = definitionTypeRepository;
            _dataTypeRepository = dataTypeRepository;
        }

        public PipelineExecutionResult Execute(InitializeArgs subject)
        {
            var sessionProvider = ObjectFactory.Instance.Resolve<ISessionProvider>();
            var session = sessionProvider.GetSession() as SessionImpl;
            IPreInsertEventListener[] oldListeners = session.Listeners.PreInsertEventListeners;
            session.Listeners.PreInsertEventListeners = new List<IPreInsertEventListener> { }.ToArray();

            Definition definition = _definitionRepository.Select(x => x.Name == "Freepay").FetchMany(d => d.DefinitionFields).FirstOrDefault();

            bool definitionExists = definition != null;

            if (!definitionExists)
            {
                definition = new Definition();
                definition.BuiltIn = false;
                definition.CreatedBy = "Freepay";
                definition.CreatedOn = DateTime.Now;
                definition.ModifiedBy = "Freepay";
                definition.ModifiedOn = DateTime.Now;
                definition.Description = "Freepay payment gateway configuration parameter";
                definition.DefinitionType = _definitionTypeRepository.SingleOrDefault(x => x.Name == "PaymentMethod Definitions");
                definition.Guid = Guid.NewGuid();
                definition.Name = "Freepay";
                session.Save(definition);
            }

            if(!definitionExists || !definition.DefinitionFields.Any(x => x.Name == "TestMode"))
            {
                var definitionField = new DefinitionField();
                definitionField.DataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "Boolean");
                definitionField.Guid = Guid.NewGuid();
                definitionField.Name = "TestMode";
                definitionField.BuiltIn = true;
                definitionField.DefaultValue = "False";
                definitionField.Definition = definition;
                definitionField.DisplayOnSite = true;
                definitionField.RenderInEditor = true;
                session.Save(definitionField);
            }

            if (!definitionExists || !definition.DefinitionFields.Any(x => x.Name == "ApiKey"))
            {
                var definitionField = new DefinitionField();
                definitionField.DataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "ShortText");
                definitionField.Guid = Guid.NewGuid();
                definitionField.Name = "ApiKey";
                definitionField.BuiltIn = true;
                definitionField.DefaultValue = "";
                definitionField.Definition = definition;
                definitionField.DisplayOnSite = true;
                definitionField.RenderInEditor = true;
                session.Save(definitionField);
            }

            if (!definitionExists || !definition.DefinitionFields.Any(x => x.Name == "AcceptUrl"))
            {
                var definitionField = new DefinitionField();
                definitionField.DataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "ShortText");
                definitionField.Guid = Guid.NewGuid();
                definitionField.Name = "AcceptUrl";
                definitionField.BuiltIn = true;
                definitionField.DefaultValue = "/shop/checkout/Accept.aspx";
                definitionField.Definition = definition;
                definitionField.DisplayOnSite = true;
                definitionField.RenderInEditor = true;
                session.Save(definitionField);
            }

            if (!definitionExists || !definition.DefinitionFields.Any(x => x.Name == "CancelUrl"))
            {
                var definitionField = new DefinitionField();
                definitionField.DataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "ShortText");
                definitionField.Guid = Guid.NewGuid();
                definitionField.Name = "CancelUrl";
                definitionField.BuiltIn = true;
                definitionField.DefaultValue = "/shop/checkout/Cancel.aspx";
                definitionField.Definition = definition;
                definitionField.DisplayOnSite = true;
                definitionField.RenderInEditor = true;
                session.Save(definitionField);
            }

            session.Listeners.PreInsertEventListeners = oldListeners;

            return PipelineExecutionResult.Success;
        }
    }
}
