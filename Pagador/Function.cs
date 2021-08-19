using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Compartilhado.AWS;
using Compartilhado.Externo;
using Compartilhado.Models;
using Compartilhado.Models.Enums;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Pagador
{
    public class Function
    {
        public Function() { }

        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            if (evnt.Records.Count > 1) throw new InvalidOperationException("Somente uma mensagem por Lambda.");

            var message = evnt.Records.FirstOrDefault();
            if (message == null) return;

            // Processa a msg.
            await ProcessMessageAsync(message, context);

            /*
            foreach (var msg in evnt.Records)
            {
                await ProcessMessageAsync(msg, context);
            }
            */
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message {message.Body}");

            var pedido = JsonConvert.DeserializeObject<Pedido>(message.Body);
            var cliente = pedido.Cliente;
            var pagamento = pedido.Pagamento;

            var processador = new GatewayPagamento();
            try
            {
                await processador.ProcessaPagamento(cliente, pagamento);
                pedido.Status = StatusPedido.PAGO;
                context.Logger.LogLine($"O pedido {pedido.Id} de {pedido.Cliente.Nome} no valor de R${pedido.ValorTotal} foi pago com sucesso.");
            }
            catch (Exception)
            {
                pedido.JustificativaDeCancelamento = $"ERRO: O pedido {pedido.Id} de {pedido.Cliente.Nome} no valor de R${pedido.ValorTotal} foi negado.";
                pedido.Cancelado = true;
                context.Logger.LogLine($"ERRO: {pedido.JustificativaDeCancelamento}");
            }


            if (pedido.Cancelado)
                await AmazonUtils.EnviarParaFilaSNS(FilaSNS.FALHA, pedido);
            else
                await AmazonUtils.EnviarParaFilaSQS(FilaSQS.PAGO, pedido);

        }
    }
}
