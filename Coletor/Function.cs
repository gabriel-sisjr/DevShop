using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Compartilhado.AWS;
using Compartilhado.Models;
using Compartilhado.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Coletor
{
    public class Function
    {
        private readonly static RegionEndpoint _regionEndpoint = RegionEndpoint.USEast1;
        public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
        {
            // Caso precise de logs
            //context.Logger.LogLine($"Beginning to process {dynamoEvent.Records.Count} records...");

            foreach (var record in dynamoEvent.Records)
            {
                if (record.EventName == "INSERT")
                {
                    var pedido = record.Dynamodb.NewImage.ToObject<Pedido>();
                    pedido.Status = StatusPedido.COLETADO;
                    try
                    {
                        ProcessarValorTotalPedido(pedido);
                        await AmazonUtils.EnviarParaFilaSQS(FilaSQS.PEDIDO, pedido); // Disparando na Fila.
                        context.Logger.LogLine($"Sucesso ao adicionar o pedido: {pedido.Id}");
                    }
                    catch (Exception e)
                    {
                        context.Logger.Log($"Erro: {e.Message}");
                        pedido.JustificativaDeCancelamento = e.Message;
                        pedido.Cancelado = true;

                        // TODO: Adicionar à fila de falha.
                        await AmazonUtils.EnviarParaFilaSNS(FilaSNS.FALHA, pedido);
                    }

                    // TODO: Salvar o pedido.
                    await pedido.SalvarAsync();
                }
            }

            context.Logger.LogLine("Stream processing complete.");
        }

        private void ProcessarValorTotalPedido(Pedido pedido)
        {
            pedido.Produtos.ForEach(async prod =>
            {
                var produtoDoEstoque = await ObterProdutoDoDynamoDBAsync(prod.Id);
                if (produtoDoEstoque == null) throw new InvalidOperationException($"Produto não encontrado no estoque. Id: {prod.Id}");
                prod.Valor = produtoDoEstoque.Valor;
                prod.Nome = produtoDoEstoque.Nome;
            });

            var valorTotal = pedido.Produtos.Sum(x => x.Valor * x.Quantidade);
            if (pedido.ValorTotal != 0 && pedido.ValorTotal != valorTotal)
                throw new InvalidOperationException($"Valores totais divergentes. Valor esperado do pedido: R${pedido.ValorTotal} || Valor Calculado: R${valorTotal}");

            pedido.ValorTotal = valorTotal;
        }

        private async Task<Produto> ObterProdutoDoDynamoDBAsync(string id)
        {
            var client = new AmazonDynamoDBClient(_regionEndpoint);

            // Montando o Request pra enviar.
            var request = new QueryRequest
            {
                TableName = "estoque",
                KeyConditionExpression = "Id = :v_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":v_id", new AttributeValue { S = id } } } // Evitar SQL Injection.
            };

            var response = await client.QueryAsync(request);
            var item = response.Items.FirstOrDefault(); // Pegando o primeiro (devido a busca por ID)
            if (item == null) return null;

            return item.ToObject<Produto>();
        }
    }
}