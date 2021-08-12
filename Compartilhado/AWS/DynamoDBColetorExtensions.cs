using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using System.Collections.Generic;

namespace Compartilhado.AWS
{
    public static class DynamoDBColetorExtensions
    {
        private readonly static RegionEndpoint _regionEndpoint = RegionEndpoint.USEast1;

        /// <summary>
        /// Recebe um dicionario e retorna um objeto do tipo passado.
        /// </summary>
        /// <typeparam name="T">Tipo a ser retornado</typeparam>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static T ToObject<T>(this Dictionary<string, AttributeValue> dictionary)
        {
            // Convertendo em um documento do tipo DynamoDB.
            var document = Document.FromAttributeMap(dictionary);

            var client = new AmazonDynamoDBClient(_regionEndpoint);
            var context = new DynamoDBContext(client);

            return context.FromDocument<T>(document);
        }
    }
}
