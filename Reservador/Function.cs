using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Compartilhado.AWS;
using Compartilhado.Models;
using Compartilhado.Models.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Reservador
{
    public class Function
    {
        private readonly static RegionEndpoint _regionEndpoint = RegionEndpoint.USEast1;

        private readonly AmazonDynamoDBClient _dynamoDBClient;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            _dynamoDBClient = new AmazonDynamoDBClient(_regionEndpoint);
        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            if (evnt.Records.Count > 1) throw new InvalidOperationException("Somente uma mensagem por Lambda.");

            var message = evnt.Records.FirstOrDefault();
            if (message == null) return;

            foreach (var msg in evnt.Records)
            {
                await ProcessMessageAsync(msg, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message {message.Body}");

            var pedido = JsonConvert.DeserializeObject<Pedido>(message.Body);
            pedido.Status = StatusPedido.RESERVADO;
            foreach (var prod in pedido.Produtos)
            {
                try
                {
                    await BaixarEstoque(prod.Id, prod.Quantidade);
                    prod.Reservado = true;
                    context.Logger.LogLine($"Produto {prod.Id} - {prod.Nome} deu baixa em {prod.Quantidade} no estoque.");
                }
                catch (ConditionalCheckFailedException)
                {
                    pedido.JustificativaDeCancelamento = $"{prod.Quantidade} unidades de {prod.Id} - {prod.Nome} indisponiveis no estoque.";
                    pedido.Cancelado = true;
                    context.Logger.LogLine($"ERRO: {pedido.JustificativaDeCancelamento}");

                    break;
                }
            }


            if (pedido.Cancelado)
            {
                foreach (var prod in pedido.Produtos)
                {
                    if (prod.Reservado)
                    {
                        prod.Reservado = false;
                        await DevolverAoEstoque(prod.Id, prod.Quantidade);
                        context.Logger.LogLine($"Devolvidos {prod.Quantidade} do produto {prod.Id} - {prod.Nome} ao estoque.");
                    }
                }

                await AmazonUtils.EnviarParaFilaSNS(FilaSNS.FALHA, pedido);
                await pedido.SalvarAsync();
            }
            else
            {
                await AmazonUtils.EnviarParaFilaSQS(FilaSQS.RESERVADO, pedido);
                await pedido.SalvarAsync();
            }
        }

        /// <summary>
        /// Soma a quantidade de itens passado por parametro.
        /// </summary>
        /// <param name="id">Id do item a ser somado.</param>
        /// <param name="quantidade">Quantidade à ser incrementada.</param>
        /// <returns></returns>
        private async Task DevolverAoEstoque(string id, int quantidade)
        {
            var request = new UpdateItemRequest
            {
                TableName = "estoque",
                ReturnValues = "NONE",
                Key = new Dictionary<string, AttributeValue> { { "Id", new AttributeValue { S = id } } },
                UpdateExpression = "SET Quantidade = (Quantidade + :quantidadeDoPedido)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":quantidadeDoPedido", new AttributeValue { N = quantidade.ToString() } } }
            };

            await _dynamoDBClient.UpdateItemAsync(request);
        }

        /// <summary>
        /// Retira uma quantidade de determinado item do estoque.
        /// </summary>
        /// <param name="id">Id do item a ser somado.</param>
        /// <param name="quantidade">Quantidade à ser incrementada.</param>
        /// <returns></returns>
        private async Task BaixarEstoque(string id, int quantidade)
        {
            /*
             * Request não retornará nada e buscará com a chave ID.
             * Fará um update na quantidade de itens no estoque, caso a condição seja atendida.
             * Caso não consiga fazer o update, disparará uma exceção do tipo "ConditionalCheckFailedException" recebida no catch acima.
             */
            var request = new UpdateItemRequest
            {
                TableName = "estoque",
                ReturnValues = "NONE",
                Key = new Dictionary<string, AttributeValue> { { "Id", new AttributeValue { S = id } } },
                UpdateExpression = "SET Quantidade = (Quantidade - :quantidadeDoPedido)",
                ConditionExpression = "Quantidade >= :quantidadeDoPedido",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":quantidadeDoPedido", new AttributeValue { N = quantidade.ToString() } } }
            };

            await _dynamoDBClient.UpdateItemAsync(request);
        }
    }
}
