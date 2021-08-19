using Compartilhado.Models;
using System;
using System.Threading.Tasks;

namespace Compartilhado.Externo
{
    public class GatewayPagamento
    {
        private readonly Random _rand = new Random();

        public async Task ProcessaPagamento(Cliente cliente, Pagamento pagamento)
        {
            var percent = _rand.Next(0, 100);
            try
            {
                if (percent <= 75)
                {
                    await Task.Delay(1000);
                    await Task.CompletedTask;
                }
            }
            catch when (percent > 75)
            {
                throw;
            }
        }
    }
}
