using Compartilhado.AWS;
using Compartilhado.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Cadastrador.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidoController : ControllerBase
    {
        [HttpPost]
        public async Task PostAsync([FromBody] Pedido pedido)
        {
            pedido.Id = Guid.NewGuid().ToString();
            pedido.DataDeCriacao = DateTime.Now;

            await pedido.SalvarAsync();

            Console.WriteLine($"Pedido salvo com ID: {pedido.Id}");
        }
    }
}
