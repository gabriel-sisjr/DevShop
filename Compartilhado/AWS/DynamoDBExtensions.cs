using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Compartilhado.Models;
using System.Threading.Tasks;

namespace Compartilhado.AWS
{
    /// <summary>
    /// Todas as instancias são criadas dentro dos metodos, devido a volatilidade dos Lambdas.
    /// </summary>
    public static class DynamoDBExtensions
    {
        private readonly static RegionEndpoint _regionEndpoint = RegionEndpoint.USEast1;
        /// <summary>
        /// Extensão do metodo de pedidos.
        /// </summary>
        /// <param name="pedido"></param>
        /// <returns></returns>
        public static async Task SalvarAsync(this Pedido pedido)
        {
            var client = new AmazonDynamoDBClient(_regionEndpoint);
            var context = new DynamoDBContext(client);
            await context.SaveAsync(pedido);
        }
    }
}
