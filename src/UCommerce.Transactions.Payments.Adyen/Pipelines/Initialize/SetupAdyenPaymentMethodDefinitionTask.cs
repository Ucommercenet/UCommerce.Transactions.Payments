using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Event;
using NHibernate.Impl;
using NHibernate.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Infrastructure.Threading;
using Ucommerce.Pipelines;
using Ucommerce.Pipelines.Initialization;
using Ucommerce.Search.Extensions;

namespace Ucommerce.Transactions.Payments.Adyen.Pipelines.Initialize
{
    public class SetupAdyenPaymentMethodDefinitionTask : IPipelineTask<InitializeArgs>
    {
        private static readonly IReadOnlyDictionary<string, string> _keys = new List<KeyValuePair<string, string>>
        {
            new("ClientKey", "ShortText"),
            new("Live", "Boolean"),
            new("PaymentFormTemplate", "ShortText"),
            new("ApiKey", "ShortText"),
            new("MerchantAccount", "ShortText"),
            new("CallbackUrl", "ShortText"),
            new("AcceptUrl", "ShortText"),
            new("DeclineUrl", "ShortText")
        }.ToDictionary();

        private readonly IRepository<DataType> _dataTypeRepository;
        private readonly IRepository<DefinitionField> _definitionFieldRepository;
        private readonly IRepository<Definition> _definitionRepository;
        private readonly IRepository<DefinitionType> _definitionTypeRepository;
        private readonly ILoggingService _loggingService;

        /// <summary>The wait period between tries.</summary>
        protected int GracePeriodBetweenTriesInMilliseconds { get; }

        /// <summary>The max number of times to run the task.</summary>
        protected int MaxNumberOfTries { get; }

        public SetupAdyenPaymentMethodDefinitionTask(IRepository<Definition> definitionRepository,
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


            MaxNumberOfTries = 1;
            GracePeriodBetweenTriesInMilliseconds = 100;
        }

        public PipelineExecutionResult Execute(InitializeArgs subject)
        {
            CreateAdyenDefinitions();
            return PipelineExecutionResult.Success;
        }

        protected virtual void CreateAdyenDefinitions()
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
                        Description = "Adyen payment gateway configuration parameter",
                        DefinitionType = definitionType,
                        Guid = Guid.NewGuid(),
                        Name = "Adyen"
                    };

                    _definitionRepository.Save(definition);
                    definition = GetAdyenDefinition();
                    if (definition is null)
                    {
                        throw new Exception("Unable to create new definition for Adyen");
                    }
                }
                else
                {
                    RemoveLegacyFields(definition.DefinitionFields.ToList());
                }

                CreateNewFields(definition);

                _definitionRepository.Save(definition);
            }
            finally
            {
                session.Listeners.PreInsertEventListeners = oldListeners;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual Definition? GetAdyenDefinition()
        {
            var definitions = _definitionRepository.Select(def => def.Name == "Adyen")
                                                   .FetchMany(def => def.DefinitionFields)
                                                   .ToList();
            return definitions.FirstOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual IReadOnlyDictionary<string, DataType> GetMissingFields()
        {
            var ids = _keys.Keys;
            var existing = _definitionFieldRepository.Select(field => ids.Contains(field.Name))
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
            var boolDataType = _dataTypeRepository.Select(x => x.TypeName == "Boolean")
                                                  .FirstOrDefault();

            if (stringDataType == null || boolDataType == null)
            {
                _loggingService.Information<SetupAdyenPaymentMethodDefinitionTask>("DataTypes could not be found");
            }

            return missing.ToDictionary(field => field.Key,
                                        field => field.Key == "ShortText" ? stringDataType : boolDataType)!;
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual DefinitionType? GetPaymentDefinitionType()
        {
            return _definitionTypeRepository.Select(def => def.Name == "PaymentMethod Definitions")
                                            .FirstOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void RemoveLegacyFields(IReadOnlyList<DefinitionField> fields)
        {
            var legacyFieldNames = new[]
            {
                "NotificationUsername", "NotificationPassword", "FlowSelection", "SkinCode", "AllowedMethods",
                "BlockedMethods", "HmacSharedSecret",
                "ShipBeforeDatePlusDays", "ShipBeforeDatePlusHours", "ShipBeforeDatePlusMinutes",
                "SessionValidityPlusMinutes", "Offset", "OfferEmail",
                "BrandCode", "WebServiceUsername",
                "WebServicePassword", "UseRecurringContract", "SigningAlgorithm",
                "ResultUrl"
            };

            var setToDeleted = fields
                               .Where(field => legacyFieldNames.Any(legacyFieldName =>
                                                                        legacyFieldName.Equals(field.Name)))
                               .ToArray();
            setToDeleted.ForEach(field => field.Deleted = true);
            _definitionFieldRepository.Save(setToDeleted);
        }

        private void CreateNewFields(Definition definition)
        {
            var dataTypes = GetMissingFields();
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
                                  })
                                  .ToArray();

            _definitionFieldRepository.Save(fields);
        }
    }
}
