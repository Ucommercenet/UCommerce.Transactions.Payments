using System;
using Ucommerce.EntitiesV2.Definitions;

namespace Ucommerce.Transactions.Payments.Adyen.Extensions
{
    public static class DynamicEntityExtensions
    {
        /// <summary>Dynamic access to custom properties.</summary>
        /// <remarks>
        /// Will look up both non-language specific properties and
        /// properties specific to the current culture.
        /// </remarks>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static dynamic? DynamicProperty<T>(this IDynamicEntity entity)
        {
            try
            {
                return entity.DynamicProperty<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
