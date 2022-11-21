using System.Collections.Generic;
using System;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Infrastructure;
using Ucommerce.Pipelines;
using Ucommerce.Pipelines.Initialization;
using System.Linq;
using NHibernate.Impl;
using NHibernate.Event;
using NHibernate.Linq;
using Ucommerce.Search.Extensions;
using NHibernate.Util;

namespace Ucommerce.Transactions.Payments.QuickpayLink.Pipelines.Initialize
{
    internal class SetupQuickpayLinkPaymentMethodDefinitionTask : IPipelineTask<InitializeArgs>
    {
        private static readonly IReadOnlyDictionary<string, string> _keys = new Dictionary<string, string>
        {
            {"ApiKey", "ShortText"},
            {"PaymentMethods", "ShortText"},
            {"CallbackUrl", "ShortText"},
            {"AcceptUrl", "ShortText"},
            {"CancelUrl", "ShortText"},
        };

        private readonly IRepository<DataType> _dataTypeRepository;
        private readonly IRepository<DefinitionField> _definitionFieldRepository;
        private readonly IRepository<Definition> _definitionRepository;
        private readonly IRepository<DefinitionType> _definitionTypeRepository;
        private readonly ILoggingService _loggingService;

        public SetupQuickpayLinkPaymentMethodDefinitionTask(IRepository<Definition> definitionRepository,
                                                     IRepository<DefinitionField> definitionFieldRepository,
                                                     IRepository<DefinitionType> definitionTypeRepository,
                                                     IRepository<DataType> dataTypeRepository,
                                                     ILoggingService loggingService)
        {
            _definitionRepository =
                definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
            _definitionFieldRepository = definitionFieldRepository
                                         ?? throw new ArgumentNullException(nameof(definitionFieldRepository));
            _definitionTypeRepository = definitionTypeRepository
                                        ?? throw new ArgumentNullException(nameof(definitionTypeRepository));
            _dataTypeRepository = dataTypeRepository ?? throw new ArgumentNullException(nameof(dataTypeRepository));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public PipelineExecutionResult Execute(InitializeArgs subject)
        {
            CreateQuickpayLinkDefinitions();
            return PipelineExecutionResult.Success;
        }

        protected virtual void CreateQuickpayLinkDefinitions()
        {
            var sessionProvider = ObjectFactory.Instance.Resolve<ISessionProvider>();

            if (sessionProvider.GetSession() is not SessionImpl session)
            {
                throw new Exception("Session not found");
            }

            var oldListeners = session.Listeners.PreInsertEventListeners;
            session.Listeners.PreInsertEventListeners = new List<IPreInsertEventListener>().ToArray();

            try
            {
                var definition = GetAdyenDefinition();
                var definitionType = GetPaymentDefinitionType();
                if (definitionType is null)
                {
                    throw new Exception("Payment definition type does not exist");
                }

                if (definition is null)
                {
                    definition = new Definition
                    {
                        BuiltIn = true,
                        CreatedBy = "System",
                        CreatedOn = DateTime.Now,
                        ModifiedBy = "System",
                        ModifiedOn = DateTime.Now,
                        Description = "Configuration for QuickpayLink",
                        DefinitionType = definitionType,
                        Guid = Guid.NewGuid(),
                        Name = "QuickpayLink"
                    };

                    _definitionRepository.Save(definition);
                    definition = GetAdyenDefinition();
                    if (definition is null)
                    {
                        throw new Exception("Unable to create new definition for QuickpayLink");
                    }
                }

                CreateNewFields(definition);

                _definitionRepository.Save(definition);
            }
            finally
            {
                session.Listeners.PreInsertEventListeners = oldListeners;
            }
        }

        protected virtual Definition? GetAdyenDefinition()
        {
            var definitions = _definitionRepository.Select(def => def.Name == "QuickpayLink")
                                                   .FetchMany(def => def.DefinitionFields)
                                                   .ToList();
            return definitions.FirstOrDefault();
        }

        protected virtual IReadOnlyDictionary<string, DataType> GetMissingFields(Definition definition)
        {
            var ids = _keys.Keys;
            var existing = _definitionFieldRepository.Select(field => ids.Contains(field.Name) && field.Definition.DefinitionId == definition.Id)
                                                     .Select(field => field.Name)
                                                     .ToList();

            var missing = _keys.Where(field => !existing.Contains(field.Key))
                               .ToDictionary();

            if (!missing.Any())
            {
                return new Dictionary<string, DataType>();
            }

            var stringDataType = _dataTypeRepository.Select(x => x.TypeName == "ShortText")
                                                    .FirstOrDefault();

            if (stringDataType == null)
            {
                _loggingService.Information<SetupQuickpayLinkPaymentMethodDefinitionTask>("DataTypes could not be found");
            }

            return missing.ToDictionary(field => field.Key, field => stringDataType);
        }

        protected virtual DefinitionType GetPaymentDefinitionType()
        {
            return _definitionTypeRepository.Select(def => def.Name == "PaymentMethod Definitions")
                                            .FirstOrDefault();
        }

        private void CreateNewFields(Definition definition)
        {
            var dataTypes = GetMissingFields(definition);
            var fields = dataTypes.Select(field => new DefinitionField
            {
                BuiltIn = true,
                DataType = field.Value,
                Definition = definition,
                DefinitionFieldDescriptions = new List<DefinitionFieldDescription>(),
                Multilingual = false,
                Name = field.Key,
                DisplayOnSite = true,
                RenderInEditor = true
            }).ToArray();

            _definitionFieldRepository.Save(fields);
        }
    }
}
