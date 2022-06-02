using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Pipelines;
using Ucommerce.Pipelines.Initialization;

namespace Ucommerce.Transactions.Payments.Adyen.Pipelines.Initialize
{
    class SetupAdyenPaymentMethodDefinitionTask : IPipelineTask<InitializeArgs>
    {
        private readonly IRepository<Definition> _definitionRepository;
        private readonly IRepository<DefinitionField> _definitionFieldRepository;
        private readonly IRepository<DefinitionType> _definitionTypeRepository;
        private readonly IRepository<DataType> _dataTypeRepository;

        public SetupAdyenPaymentMethodDefinitionTask(IRepository<Definition> definitionRepository,
            IRepository<DefinitionField> definitionFieldRepository,
            IRepository<DefinitionType> definitionTypeRepository,
            IRepository<DataType> dataTypeRepository)
        {
            _definitionRepository = definitionRepository;
            _definitionFieldRepository = definitionFieldRepository;
            _definitionTypeRepository = definitionTypeRepository;
            _dataTypeRepository = dataTypeRepository;
        }
        public PipelineExecutionResult Execute(InitializeArgs subject)
        {
            Definition definition = _definitionRepository.Select(x => x.Name == "Adyen").FirstOrDefault();

            bool definitionExists = definition != null;

            if (!definitionExists)
            {
                definition = new Definition();
                definition.BuiltIn = false;
                definition.CreatedBy = "Adyen";
                definition.CreatedOn = DateTime.Now;
                definition.ModifiedBy = "Adyen";
                definition.ModifiedOn = DateTime.Now;
                definition.Description = "Adyen payment gateway configuration parameter";
                definition.DefinitionType = _definitionTypeRepository.SingleOrDefault(x => x.Name == "PaymentMethod Definitions");
                definition.Guid = Guid.NewGuid();
                definition.Name = "Adyen";
            }
            else
            {
                RemoveLegacyFields(definition);
            }

            CreateNewFields(definition);

            _definitionRepository.Save(definition);

            return PipelineExecutionResult.Success;
        }

        private void CreateNewFields(Definition definition)
        {
            var stringDataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "ShortText");
            var boolDataType = _dataTypeRepository.SingleOrDefault(x => x.TypeName == "Boolean");

            var fields = new List<KeyValuePair<string, DataType>>
            {
                new KeyValuePair<string, DataType>("ClientKey", stringDataType),
                new KeyValuePair<string, DataType>("Live", boolDataType),
                new KeyValuePair<string, DataType>("PaymentFormTemplate", stringDataType),
                new KeyValuePair<string, DataType>("ApiKey", stringDataType),
                new KeyValuePair<string, DataType>("MerchantAccount", stringDataType),
                new KeyValuePair<string, DataType>("CallbackUrl", stringDataType),
                new KeyValuePair<string, DataType>("AcceptUrl", stringDataType),
                new KeyValuePair<string, DataType>("DeclineUrl", stringDataType)
            };

            foreach (var field in fields)
            {
                var defField = new DefinitionField
                    {
                        BuiltIn = true, 
                        DataType = field.Value,
                        Definition = definition,
                        DefinitionFieldDescriptions = new List<DefinitionFieldDescription>(),
                        Multilingual = false,
                        Name = field.Key,
                        Guid = Guid.NewGuid(),
                        DisplayOnSite = true,
                        RenderInEditor = true
                    };
                
                definition.AddDefinitionField(defField);
                // _definitionFieldRepository.Save(defField);
            }
        }

        private void RemoveLegacyFields(Definition definition)
        {
            var legacyFieldNames = new string[] { "NotificationUsername", "NotificationPassword", "FlowSelection", "SkinCode", "AllowedMethods", "BlockedMethods", "HmacSharedSecret", 
                "ShipBeforeDatePlusDays", "ShipBeforeDatePlusHours", "ShipBeforeDatePlusMinutes", "SessionValidityPlusMinutes", "Offset", "OfferEmail", "BrandCode", "WebServiceUsername",
                "WebServicePassword", "UseRecurringContract", "SigningAlgorithm", "ClientKey", "Live", "PaymentFormTemplate", "ApiKey", "MerchantAccount", "CallbackUrl", "ResultUrl", "AcceptUrl",
                "DeclineUrl"
            };

            foreach (var definitionField in definition.DefinitionFields.Where(x => legacyFieldNames.Any(legacyFieldName => legacyFieldName.Equals(x.Name))))
            {
                definitionField.Deleted = true;
                // _definitionFieldRepository.Save(definitionField);
            }
        }
    }
}
