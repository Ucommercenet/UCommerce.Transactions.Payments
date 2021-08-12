using System;
using System.Collections.Generic;
using System.Linq;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure.Logging;
using Ucommerce.Pipelines;
using Ucommerce.Pipelines.Initialization;
using Ucommerce.Security;

namespace Ucommerce.Transactions.Payments.Quickpay.Pipelines.Initialize
{
    public class QuickpayExtendedPaymentDefinitionTask : IPipelineTask<InitializeArgs>
    {
        private readonly IRepository<Definition> _definitionRepository;
        private readonly IRepository<DefinitionType> _definitionTypeRepository;
        private readonly IRepository<DataType> _dataTypeRepository;
        private readonly ILoggingService _logger;
        private readonly string _definitionName = "Quickpay";
        private readonly int _definitionTypeId = 4;
        private readonly int _shortTextDataType = 1;
        private readonly int _booleanDataType = 6;

        public QuickpayExtendedPaymentDefinitionTask(IRepository<Definition> definitionRepository, IRepository<DefinitionType> definitionTypeRepository, IRepository<DataType> dataTypeRepository, ICurrentUserNameService currentUserNameService, ILoggingService logger)
        {
            _definitionRepository = definitionRepository;
            _definitionTypeRepository = definitionTypeRepository;
            _dataTypeRepository = dataTypeRepository;
            _logger = logger;
        }

        public PipelineExecutionResult Execute(InitializeArgs subject)
        {
            Definition definition = _definitionRepository.Select().FirstOrDefault(d => d.Name.Equals(_definitionName));
            if (definition != null)
            {
                definition.Deleted = false;
            }
            else
            {
                definition = new Definition()
                {
                    Name = _definitionName,
                    Description = "Configuration for Quickpay",
                    DefinitionType = _definitionTypeRepository.Get(_definitionTypeId)
                };
                this._logger.Log<QuickpayExtendedPaymentDefinitionTask>("Quickpay payment definition created.");
            }

            var shortTextDataType = _dataTypeRepository.Get(_shortTextDataType);
            var booleanDataType = _dataTypeRepository.Get(_booleanDataType);

            this.CreateOrUpdateDefinitionField(definition, "ApiKey", shortTextDataType);
            this.CreateOrUpdateDefinitionField(definition, "PrivateAccountKey", shortTextDataType);
            this.CreateOrUpdateDefinitionField(definition, "Merchant", shortTextDataType, "12345678");
            this.CreateOrUpdateDefinitionField(definition, "AgreementId", shortTextDataType, "12345678");
            this.CreateOrUpdateDefinitionField(definition, "CallbackUrl", shortTextDataType, "(auto)");
            this.CreateOrUpdateDefinitionField(definition, "Language", shortTextDataType, "(auto)");
            this.CreateOrUpdateDefinitionField(definition, "AcceptUrl", shortTextDataType);
            this.CreateOrUpdateDefinitionField(definition, "CancelUrl", shortTextDataType);
            this.CreateOrUpdateDefinitionField(definition, "AutoCapture", booleanDataType);
            this.CreateOrUpdateDefinitionField(definition, "CancelTestCardOrders", booleanDataType, "True");

            RemoveField(definition, "TestMode");
            RemoveField(definition, "Md5secret");
            
            definition.Save();

            return PipelineExecutionResult.Success;
        }

        private void RemoveField(Definition definition, string fieldName)
        {
            var field = definition.DefinitionFields.FirstOrDefault(x => x.Name.Equals(fieldName));
            if (field != null)
                definition.DefinitionFields.Remove(field);
        }

        private void CreateOrUpdateDefinitionField(
            Definition definition,
            string name,
            DataType dataType,
            string defaultValue = "")
        {
            ICollection<DefinitionField> definitionFields = definition != null ? definition.DefinitionFields : throw new ArgumentNullException(nameof(definition));
            var field = definitionFields.FirstOrDefault(
                x => x.Name == name && x.Definition.DefinitionId == definition.DefinitionId);
            if (field == null)
                this.CreateDefinitionField(definition, name, false, true, true, defaultValue, dataType);
            else
                this.UpdateDefinitionField(field, false, true, true, defaultValue, dataType, false);
        }

        private void CreateDefinitionField(
            Definition definition,
            string name,
            bool multilingual,
            bool displayOnSite,
            bool renderInEditor,
            string defaultValue,
            DataType dataType)
        {
            DefinitionField definitionField = new DefinitionField
            {
                Name = name,
                Multilingual = multilingual,
                DisplayOnSite = displayOnSite,
                RenderInEditor = renderInEditor,
                DefaultValue = defaultValue,
                DataType = dataType,
            };
            definition.AddDefinitionField(definitionField);
        }

        public void UpdateDefinitionField(
            DefinitionField definitionField,
            bool multilingual,
            bool displayOnSite,
            bool renderInEditor,
            string defaultValue,
            DataType dataType,
            bool deleted)
        {
            definitionField.Multilingual = multilingual;
            definitionField.Deleted = deleted;
            definitionField.DisplayOnSite = displayOnSite;
            definitionField.RenderInEditor = renderInEditor;
            definitionField.DefaultValue = defaultValue;
            definitionField.DataType = dataType;
        }
    }
}
