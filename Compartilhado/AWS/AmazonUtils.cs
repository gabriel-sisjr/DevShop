using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Compartilhado.Models;
using Compartilhado.Models.Enums;
using System.Text.Json;
using System.Threading.Tasks;

namespace Compartilhado.AWS
{
    public static class AmazonUtils
    {
        private readonly static RegionEndpoint _regionEndpoint = RegionEndpoint.USEast1;
        private readonly static string _UrlQueuePedido = "https://sqs.us-east-1.amazonaws.com/205886172740";

        public static async Task EnviarParaFilaSQS<T>(FilaSQS filaSQS, T objeto)
        {
            var jsonObjeto = JsonSerializer.Serialize(objeto);
            var client = new AmazonSQSClient(_regionEndpoint);

            var request = new SendMessageRequest
            {
                // Essa configuração de URL foi feita pra deixar generica a conexão na fila que for passada pelo ENUM.
                QueueUrl = $"{_UrlQueuePedido}/{filaSQS.ToString().ToLower()}",
                MessageBody = jsonObjeto
            };

            await client.SendMessageAsync(request);
        }

        public static async Task EnviarParaFilaSNS<T>(FilaSNS filaSNS, T objeto)
        {
            // TODO: Implementar a fila SNS
            await Task.CompletedTask;
        }
    }
}
